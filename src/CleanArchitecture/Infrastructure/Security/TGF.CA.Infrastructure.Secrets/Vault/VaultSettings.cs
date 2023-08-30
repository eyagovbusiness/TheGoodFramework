namespace TGF.CA.Infrastructure.Security.Secrets.Vault
{
    /// <summary>
    /// Settings for Vault secrets manager connection for the free version.
    /// </summary>
    public record VaultSettings
    {
        public string? VaultUrl { get; private set; }
        public string? TokenApi { get; init; }

        public void UpdateUrl(string aUrl)
        {
            VaultUrl = aUrl;
        }
    }
}
