using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nocturne.Connectors.Twiist.Configurations;
using Nocturne.Connectors.Twiist.Services;
using Xunit;

namespace Nocturne.Connectors.Twiist.Tests.Services;

public class TwiistCognitoTokenProviderTests
{
    [Fact]
    public async Task LoginAndRefresh_SendExpectedCognitoRequestShapes()
    {
        var followerToken = TwiistJwtTestTokens.CreateUnsigned("Follower");
        var handler = new RecordingHttpMessageHandler()
            .EnqueueJson(AuthResponse("access-1", followerToken, "refresh-1", 3600))
            .EnqueueJson(AuthResponse("access-2", followerToken, null, 3600));

        var provider = CreateProvider(handler);

        var first = await provider.GetAccessTokenAsync();
        var second = await provider.ForceRefreshAsync();

        first.Should().Be("access-1");
        second.Should().Be("access-2");
        handler.Requests.Should().HaveCount(2);

        var login = JsonDocument.Parse(handler.Bodies[0]).RootElement;
        login.GetProperty("AuthFlow").GetString().Should().Be("USER_PASSWORD_AUTH");
        login.GetProperty("ClientId").GetString().Should().Be(TwiistConstants.DefaultCognitoClientId);
        login.GetProperty("AuthParameters").GetProperty("USERNAME").GetString().Should().Be("follower@example.com");
        login.GetProperty("AuthParameters").GetProperty("PASSWORD").GetString().Should().Be("secret");

        var refresh = JsonDocument.Parse(handler.Bodies[1]).RootElement;
        refresh.GetProperty("AuthFlow").GetString().Should().Be("REFRESH_TOKEN_AUTH");
        refresh.GetProperty("AuthParameters").GetProperty("REFRESH_TOKEN").GetString().Should().Be("refresh-1");

        handler.Requests[0].RequestUri!.ToString().Should()
            .Be($"{TwiistConstants.DefaultCognitoBaseUrl}/{TwiistConstants.DefaultCognitoPoolId}");
        handler.Requests[0].Headers.GetValues("X-Amz-Target").Single()
            .Should().Be("AWSCognitoIdentityProviderService.InitiateAuth");
        handler.Requests[0].Content!.Headers.ContentType!.MediaType
            .Should().Be("application/x-amz-json-1.1");
    }

    [Fact]
    public async Task Login_WhenIdTokenMissingFollowerGroup_ThrowsClearError()
    {
        var nonFollowerToken = TwiistJwtTestTokens.CreateUnsigned("Patient");
        var handler = new RecordingHttpMessageHandler()
            .EnqueueJson(AuthResponse("access-1", nonFollowerToken, "refresh-1", 3600));

        var provider = CreateProvider(handler);

        var act = () => provider.GetAccessTokenAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Follower*Cognito group*");
    }

    private static TwiistCognitoTokenProvider CreateProvider(RecordingHttpMessageHandler handler)
    {
        var config = Options.Create(new TwiistConnectorConfiguration
        {
            Username = "follower@example.com",
            Password = "secret"
        });

        return new TwiistCognitoTokenProvider(
            new HttpClient(handler),
            config,
            NullLogger<TwiistCognitoTokenProvider>.Instance);
    }

    private static string AuthResponse(
        string accessToken,
        string idToken,
        string? refreshToken,
        int expiresIn)
    {
        var refreshPart = refreshToken == null ? "" : $@",""RefreshToken"":""{refreshToken}""";
        return $$"""
        {
          "AuthenticationResult": {
            "AccessToken": "{{accessToken}}",
            "IdToken": "{{idToken}}",
            "ExpiresIn": {{expiresIn}},
            "TokenType": "Bearer"{{refreshPart}}
          }
        }
        """;
    }
}
