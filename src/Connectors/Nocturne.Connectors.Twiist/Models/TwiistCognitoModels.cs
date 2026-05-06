using System.Text.Json.Serialization;

namespace Nocturne.Connectors.Twiist.Models;

public sealed class TwiistCognitoAuthResponse
{
    [JsonPropertyName("AuthenticationResult")]
    public TwiistCognitoAuthResult AuthenticationResult { get; set; } = new();
}

public sealed class TwiistCognitoAuthResult
{
    [JsonPropertyName("AccessToken")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("IdToken")]
    public string IdToken { get; set; } = string.Empty;

    [JsonPropertyName("RefreshToken")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("ExpiresIn")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("TokenType")]
    public string TokenType { get; set; } = string.Empty;
}
