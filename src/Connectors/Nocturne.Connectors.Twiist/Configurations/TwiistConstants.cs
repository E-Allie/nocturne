namespace Nocturne.Connectors.Twiist.Configurations;

public static class TwiistConstants
{
    public const string DefaultFollowerServiceUrl = "https://follower-service.mytwiistportal.com";
    public const string DefaultCognitoPoolId = "us-east-1_fnkWvSdfv";
    public const string DefaultCognitoClientId = "65ev2vbkr2mle7uu4cqkn7ohgl";
    public const string DefaultCognitoBaseUrl = "https://cognito-idp.us-east-1.amazonaws.com";
    public const string UserAgent = "twiist insiight/1.0.2 CFNetwork/1568.100.1.2.3 Darwin/24.0.0";
    public const string AppName = "TwiistSync";

    public static class Endpoints
    {
        public const string PwdOverviews = "/pwd/overviews";
        public static string PwdPackage(Guid pwdId) => $"/pwd/{pwdId:D}/package";
    }
}
