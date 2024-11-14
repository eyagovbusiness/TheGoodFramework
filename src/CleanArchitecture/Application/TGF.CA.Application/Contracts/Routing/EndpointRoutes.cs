namespace TGF.CA.Application.Contracts.Routing
{
    public readonly struct TGFEndpointRoutes
    {
        public const string health = "/health";
        public const string healthUi = "/health-ui";
        public const string error = "/error";
        public const string auth_OAuthCallback = "/auth/OAuthCallback";
        public const string auth_OAuthFailed = "/auth/OAuthFailed";
    }
}
