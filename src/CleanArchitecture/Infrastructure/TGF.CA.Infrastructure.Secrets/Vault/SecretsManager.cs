using Microsoft.Extensions.Options;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines;
using VaultSharp;
using TGF.CA.Infrastructure.Secrets.Common;
using TGF.CA.Infrastructure.Discovery;

namespace TGF.CA.Infrastructure.Secrets.Vault
{
    public interface ISecretManager
    {
        Task<T> Get<T>(string path) where T : new();
        Task<UsernamePasswordCredentials> GetRabbitMQCredentials(string aRoleName);
        void UpdateUrl(string aVaultServiceUrl);
    }

    public class SecretsManager : ISecretManager
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

    }

}


