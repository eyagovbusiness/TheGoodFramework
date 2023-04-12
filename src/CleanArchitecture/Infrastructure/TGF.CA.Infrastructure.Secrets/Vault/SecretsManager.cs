using Microsoft.Extensions.Options;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines;
using VaultSharp;
using TGF.CA.Infrastructure.Secrets.Common;

namespace TGF.CA.Infrastructure.Secrets.Vault
{
    public interface ISecretManager
    {
        Task<T> Get<T>(string path) where T : new();
        Task<UsernamePasswordCredentials> GetRabbitMQCredentials(string aRoleName);
        //Task<UsernamePasswordCredentials> GetMySqlredentials(string roleName);
    }

    public class SecretsManager : ISecretManager
    {
        private readonly Settings _vaultSettings;

        public SecretsManager(IOptions<Settings> aVaultSettings)
            => _vaultSettings = aVaultSettings.Value with { TokenApi = GetTokenFromEnvironmentVariable() };

        public async Task<T> Get<T>(string aPath)
            where T : new()
        {
            VaultClient client = new VaultClient(new VaultClientSettings(_vaultSettings.VaultUrl,
                new TokenAuthMethodInfo(_vaultSettings.TokenApi)));

            Secret<SecretData> lKv2Secret = await client.V1.Secrets.KeyValue.V2
                .ReadSecretAsync(path: aPath, mountPoint: "secret");

            return lKv2Secret.Data.Data.ToObject<T>();
        }

        //public Task<UsernamePasswordCredentials> GetMySqlredentials(string roleName)
        //{
        //    throw new NotImplementedException();
        //}

        public async Task<UsernamePasswordCredentials> GetRabbitMQCredentials(string aRoleName)
        {
            VaultClient client = new VaultClient(new VaultClientSettings(_vaultSettings.VaultUrl,
                new TokenAuthMethodInfo(_vaultSettings.TokenApi)));

            Secret<UsernamePasswordCredentials> lSecret = await client.V1.Secrets.RabbitMQ
                .GetCredentialsAsync(aRoleName, "rabbitmq");
            return lSecret.Data;
        }

        private string GetTokenFromEnvironmentVariable()
            => Environment.GetEnvironmentVariable("VAULT-TOKEN")
                ?? throw new NotImplementedException("please specify the VAULT-TOKEN env_var");
    }

}


