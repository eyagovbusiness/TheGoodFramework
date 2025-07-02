namespace TGF.CA.Application.InvariantConstants {
    /// <summary>
    /// Configuration keys used in appsettings.
    /// </summary>
    public readonly struct ConfigurationKeys {
        public readonly struct FrontendURL {
            public const string Key = nameof(FrontendURL);
        }
        public readonly struct Auth {
            public const string Key = nameof(Auth);
            public const string FrontendAuthCallbackURI = $"{Key}:FrontendAuthCallbackURI";
            public const string MicrosoftAuthCallbackURI = $"{Key}:MicrosoftAuthCallbackURI";
            public const string AccessTokenLifetimeInMinutes = $"{Key}:AccessTokenLifetimeInMinutes";
            public const string RefreshTokenLifetimeInDays = $"{Key}:RefreshTokenLifetimeInDays";
        }
    }
}
