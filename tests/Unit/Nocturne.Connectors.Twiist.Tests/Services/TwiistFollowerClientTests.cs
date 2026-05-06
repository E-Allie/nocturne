using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nocturne.Connectors.Twiist.Configurations;
using Nocturne.Connectors.Twiist.Services;
using Xunit;

namespace Nocturne.Connectors.Twiist.Tests.Services;

public class TwiistFollowerClientTests
{
    [Fact]
    public async Task GetOverviews_SendsBearerTokenAndTwiistHeaders()
    {
        var config = Options.Create(new TwiistConnectorConfiguration
        {
            Username = "follower@example.com",
            Password = "secret",
            Server = "https://example.test"
        });

        var cognitoHandler = new RecordingHttpMessageHandler()
            .EnqueueJson(AuthResponse("access-token", TwiistJwtTestTokens.CreateUnsigned("Follower")));
        var tokenProvider = new TwiistCognitoTokenProvider(
            new HttpClient(cognitoHandler),
            config,
            NullLogger<TwiistCognitoTokenProvider>.Instance);

        var followerHandler = new RecordingHttpMessageHandler()
            .EnqueueJson("[]");
        var client = new TwiistFollowerClient(
            new HttpClient(followerHandler),
            config,
            tokenProvider,
            NullLogger<TwiistFollowerClient>.Instance);

        var overviews = await client.GetOverviewsAsync();

        overviews.Should().BeEmpty();
        followerHandler.Requests.Should().ContainSingle();
        var request = followerHandler.Requests[0];
        request.Method.Should().Be(HttpMethod.Get);
        request.RequestUri!.ToString().Should().Be("https://example.test/pwd/overviews");
        request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        request.Headers.Authorization!.Parameter.Should().Be("access-token");
        request.Headers.GetValues("Accept").Single().Should().Be("*/*");
        request.Headers.UserAgent.ToString().Should().Be(TwiistConstants.UserAgent);
        request.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    private static string AuthResponse(string accessToken, string idToken)
    {
        return $$"""
        {
          "AuthenticationResult": {
            "AccessToken": "{{accessToken}}",
            "IdToken": "{{idToken}}",
            "RefreshToken": "refresh",
            "ExpiresIn": 3600,
            "TokenType": "Bearer"
          }
        }
        """;
    }
}
