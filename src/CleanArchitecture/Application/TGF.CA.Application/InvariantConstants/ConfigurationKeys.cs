namespace TGF.CA.Application.InvariantConstants {
    /// <summary>
    /// Configuration keys used in appsettings.
    /// </summary>
    public readonly struct ConfigurationKeys {
        public readonly struct FrontendURL {
            public const string Key = nameof(FrontendURL);
        }
        public readonly struct FrontendAuthCallbackURI {
            public const string Key = nameof(FrontendAuthCallbackURI);
        }
        public readonly struct MicrosoftAuthCallbackURI {
            public const string Key = nameof(MicrosoftAuthCallbackURI);
        }
    }
}
