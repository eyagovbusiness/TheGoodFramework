using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TGF.CA.Infrastructure.Discovery;
using TGF.CA.Infrastructure.Secrets.Common;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines;
using VaultSharp.V1.SystemBackend;

namespace TGF.CA.Infrastructure.Secrets.Vault
{
    public interface ISecretsManager
    {
        Task<T> Get<T>(string aPath) where T : new();
        Task<UsernamePasswordCredentials> GetRabbitMQCredentials(string aRoleName);
        void UpdateUrl(string aVaultServiceUrl);
        public Task<VaultSharp.V1.SystemBackend.HealthStatus> GetHealthStatusAsync();
    }

    public class SecretsManager : ISecretsManager
    {
        private readonly Settings _vaultSettings;

        public SecretsManager(IOptions<Settings> aVaultSettings, IServiceDiscovery? aServiceDiscovery = null)
        {
            _vaultSettings = aVaultSettings.Value with { TokenApi = GetTokenFromEnvironmentVariable() };
            if (aServiceDiscovery != null)
                this.ConfigureDiscoveredSecretsManager(aServiceDiscovery); //TO-DO:TEMPORARY SOLUTION, blocks thread(on startup is not so big problem, but not good)
        }

        public async Task<T> Get<T>(string aPath)
            where T : new()
        {
            VaultClient client = new VaultClient(new VaultClientSettings(_vaultSettings.VaultUrl,
                new TokenAuthMethodInfo(_vaultSettings.TokenApi)));

            Secret<SecretData> lKv2Secret = await client.V1.Secrets.KeyValue.V2
                .ReadSecretAsync(path: aPath, mountPoint: "secret");

            return lKv2Secret.Data.Data.ToObject<T>();
        }

        public async Task<UsernamePasswordCredentials> GetRabbitMQCredentials(string aRoleName)
        {
            VaultClient client = new VaultClient(new VaultClientSettings(_vaultSettings.VaultUrl,
                new TokenAuthMethodInfo(_vaultSettings.TokenApi)));

            Secret<UsernamePasswordCredentials> lSecret = await client.V1.Secrets.RabbitMQ
                .GetCredentialsAsync(aRoleName, "rabbitmq");
            return lSecret.Data;
        }

        private string GetTokenFromEnvironmentVariable()
            => Environment.GetEnvironmentVariable("VAULT_TOKEN")
                ?? throw new NotImplementedException("Error: not specified VAULT_TOKEN env_var");

        public void UpdateUrl(string aVaultServiceUrl)
            => _vaultSettings.UpdateUrl(aVaultServiceUrl);

        public async Task<VaultSharp.V1.SystemBackend.HealthStatus> GetHealthStatusAsync()
        {
                VaultClient lClient = new(new VaultClientSettings(_vaultSettings.VaultUrl,
                                                                    new TokenAuthMethodInfo(_vaultSettings.TokenApi)));
                return await lClient.V1.System.GetHealthStatusAsync();
        }

    }

}


