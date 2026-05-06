using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nocturne.Connectors.Twiist.Configurations;
using Nocturne.Connectors.Twiist.Models;

namespace Nocturne.Connectors.Twiist.Services;

public sealed class TwiistFollowerClient
{
    private readonly HttpClient _httpClient;
    private readonly TwiistConnectorConfiguration _config;
    private readonly TwiistCognitoTokenProvider _tokenProvider;
    private readonly ILogger<TwiistFollowerClient> _logger;

    public TwiistFollowerClient(
        HttpClient httpClient,
        IOptions<TwiistConnectorConfiguration> config,
        TwiistCognitoTokenProvider tokenProvider,
        ILogger<TwiistFollowerClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<TwiistPackage>> GetOverviewsAsync(
        CancellationToken cancellationToken = default)
    {
        return await GetJsonWithRefreshAsync<List<TwiistPackage>>(
            TwiistConstants.Endpoints.PwdOverviews,
            cancellationToken) ?? [];
    }

    public async Task<TwiistPackage> GetPackageAsync(
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        var package = await GetJsonWithRefreshAsync<TwiistPackage>(
            TwiistConstants.Endpoints.PwdPackage(patientId),
            cancellationToken);

        return package
            ?? throw new InvalidOperationException("Twiist package response was empty.");
    }

    private async Task<T?> GetJsonWithRefreshAsync<T>(
        string path,
        CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
        using var first = await SendGetAsync(path, token, cancellationToken);
        if (first.StatusCode != HttpStatusCode.Unauthorized)
            return await ReadJsonAsync<T>(first, path, cancellationToken);

        _logger.LogInformation("Twiist follower API returned 401 for {Path}; refreshing token.", path);
        var refreshedToken = await _tokenProvider.ForceRefreshAsync(cancellationToken);
        using var second = await SendGetAsync(path, refreshedToken, cancellationToken);
        return await ReadJsonAsync<T>(second, path, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendGetAsync(
        string path,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var url = $"{_config.FollowerServiceUrl}{path}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("Accept", "*/*");
        request.Headers.TryAddWithoutValidation("User-Agent", TwiistConstants.UserAgent);
        request.Content = new StringContent(string.Empty);
        request.Content.Headers.ContentType = new("application/json");

        return await _httpClient.SendAsync(request, cancellationToken);
    }

    private static async Task<T?> ReadJsonAsync<T>(
        HttpResponseMessage response,
        string path,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Twiist follower GET {path} failed with HTTP {(int)response.StatusCode}: {body}",
                null,
                response.StatusCode);
        }

        return JsonSerializer.Deserialize<T>(body, TwiistJson.Options);
    }
}
