using System.Text;
using System.Text.Json;

namespace Nocturne.Connectors.Twiist.Tests.Services;

internal static class TwiistJwtTestTokens
{
    public static string CreateUnsigned(params string[] groups)
    {
        var header = Base64Url("""{"alg":"none"}""");
        var payload = Base64Url(JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["cognito:groups"] = groups
        }));

        return $"{header}.{payload}.";
    }

    private static string Base64Url(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
