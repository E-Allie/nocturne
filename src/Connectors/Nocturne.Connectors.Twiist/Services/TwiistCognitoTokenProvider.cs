using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nocturne.Connectors.Twiist.Configurations;
using Nocturne.Connectors.Twiist.Models;
using Nocturne.Connectors.Twiist.Utilities;

namespace Nocturne.Connectors.Twiist.Services;

public sealed class TwiistCognitoTokenProvider
{
    private const string ContentType = "application/x-amz-json-1.1";
    private const string XAmzTarget = "AWSCognitoIdentityProviderService.InitiateAuth";
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(5);

    private readonly HttpClient _httpClient;
    private readonly TwiistConnectorConfiguration _config;
    private readonly ILogger<TwiistCognitoTokenProvider> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private TwiistCognitoAuthResult? _tokens;
    private DateTimeOffset _expiresAt;

    public TwiistCognitoTokenProvider(
        HttpClient httpClient,
        IOptions<TwiistConnectorConfiguration> config,
        ILogger<TwiistCognitoTokenProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsTokenExpired => _tokens == null || DateTimeOffset.UtcNow >= _expiresAt - RefreshSkew;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_tokens != null && DateTimeOffset.UtcNow < _expiresAt - RefreshSkew)
                return _tokens.AccessToken;

            if (!string.IsNullOrWhiteSpace(_tokens?.RefreshToken))
            {
                try
                {
                    await RefreshAsync(_tokens.RefreshToken, cancellationToken);
                    return _tokens!.AccessToken;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Twiist Cognito refresh failed; falling back to password auth.");
                }
            }

            await LoginAsync(cancellationToken);
            return _tokens!.AccessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string> ForceRefreshAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(_tokens?.RefreshToken))
            {
                await RefreshAsync(_tokens.RefreshToken, cancellationToken);
            }
            else
            {
                await LoginAsync(cancellationToken);
            }

            return _tokens!.AccessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private Task LoginAsync(CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, object>
        {
            ["AuthFlow"] = "USER_PASSWORD_AUTH",
            ["AuthParameters"] = new Dictionary<string, string>
            {
                ["USERNAME"] = _config.Username,
                ["PASSWORD"] = _config.Password
            },
            ["ClientId"] = TwiistConstants.DefaultCognitoClientId
        };

        return PostAuthAsync(body, preserveRefreshToken: false, cancellationToken);
    }

    private Task RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, object>
        {
            ["AuthFlow"] = "REFRESH_TOKEN_AUTH",
            ["AuthParameters"] = new Dictionary<string, string>
            {
                ["REFRESH_TOKEN"] = refreshToken
            },
            ["ClientId"] = TwiistConstants.DefaultCognitoClientId
        };

        return PostAuthAsync(body, preserveRefreshToken: true, cancellationToken);
    }

    private async Task PostAuthAsync(
        object body,
        bool preserveRefreshToken,
        CancellationToken cancellationToken)
    {
        var url = $"{TwiistConstants.DefaultCognitoBaseUrl}/{TwiistConstants.DefaultCognitoPoolId}";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.TryAddWithoutValidation("X-Amz-Target", XAmzTarget);
        request.Content.Headers.ContentType = new(ContentType);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Twiist Cognito auth failed with HTTP {(int)response.StatusCode}: {responseText}",
                null,
                response.StatusCode);
        }

        var parsed = JsonSerializer.Deserialize<TwiistCognitoAuthResponse>(
            responseText,
            TwiistJson.Options);

        var result = parsed?.AuthenticationResult
            ?? throw new InvalidOperationException("Twiist Cognito response did not include AuthenticationResult.");

        TwiistJwt.VerifyFollowerGroup(result.IdToken);

        if (preserveRefreshToken && string.IsNullOrWhiteSpace(result.RefreshToken))
            result.RefreshToken = _tokens?.RefreshToken;

        if (string.IsNullOrWhiteSpace(result.AccessToken))
            throw new InvalidOperationException("Twiist Cognito response did not include AccessToken.");

        _tokens = result;
        _expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, result.ExpiresIn));
    }
}
