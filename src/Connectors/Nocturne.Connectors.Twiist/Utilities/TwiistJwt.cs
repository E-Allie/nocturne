using System.Text.Json;

namespace Nocturne.Connectors.Twiist.Utilities;

public static class TwiistJwt
{
    public static JsonDocument DecodePayload(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2)
            throw new InvalidOperationException("JWT missing payload segment.");

        var payload = parts[1];
        var padded = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');
        var jsonBytes = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
        return JsonDocument.Parse(jsonBytes);
    }

    public static void VerifyFollowerGroup(string idToken)
    {
        using var payload = DecodePayload(idToken);
        if (!payload.RootElement.TryGetProperty("cognito:groups", out var groups)
            || groups.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("ID token missing cognito:groups claim.");
        }

        foreach (var group in groups.EnumerateArray())
        {
            if (group.GetString() == "Follower")
                return;
        }

        throw new InvalidOperationException(
            "Account is not a member of the \"Follower\" Cognito group; cannot use Twiist Insight follower API.");
    }

}
