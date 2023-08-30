using Microsoft.Extensions.Options;
using TGF.CA.Application;
using TGF.CA.Infrastructure.Discovery;
using TGF.CA.Infrastructure.Security.Secrets.Common;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines;

namespace TGF.CA.Infrastructure.Security.Secrets.Vault
{

    internal class InternalUsernamePasswordCredentials : UsernamePasswordCredentials, IBasicCredentials { }

    public class SecretsManager : ISecretsManager
    {
        private readonly VaultSettings _vaultSettings;
        private readonly IServiceDiscovery _serviceDiscovery;

        public SecretsManager(IOptions<VaultSettings> aVaultSettings, IServiceDiscovery? aServiceDiscovery = null)
        {
            _serviceDiscovery = aServiceDiscovery 
                ?? throw new Exception("Unable to setup Vault SecretsManager because the service discovery was null.");
            _vaultSettings = aVaultSettings.Value with { TokenApi = GetTokenFromEnvironmentVariable() };
        }

        #region ISecretsManager

        public async Task<T> Get<T>(string aPath)
            where T : new()
        {
            if (_vaultSettings.VaultUrl == null)
                await ConfigureDiscoveredSecretsManager();

            VaultClient client = new VaultClient(new VaultClientSettings(_vaultSettings.VaultUrl,
                new TokenAuthMethodInfo(_vaultSettings.TokenApi)));

            Secret<SecretData> lKv2Secret = await client.V1.Secrets.KeyValue.V2
                .ReadSecretAsync(path: aPath, mountPoint: "secret");

            return lKv2Secret.Data.Data.ToObject<T>();
        }

        /// <summary>
        /// Gets an object represeting the value of the key:value pair from a given secrets path.
        /// </summary>
        /// <param name="aPath"></param>
        /// <param name="aKey"></param>
        /// <returns></returns>
        public async Task<object> GetValueObject(string aPath, string aKey)
        {
            if (_vaultSettings.VaultUrl == null)
                await ConfigureDiscoveredSecretsManager();

            VaultClient client = new VaultClient(new VaultClientSettings(_vaultSettings.VaultUrl,
                new TokenAuthMethodInfo(_vaultSettings.TokenApi)));

            Secret<SecretData> lKv2Secret = await client.V1.Secrets.KeyValue.V2
                .ReadSecretAsync(path: aPath, mountPoint: "secret");

            return lKv2Secret.Data.Data[aKey];
        }

        public async Task<IBasicCredentials> GetRabbitMQCredentials(string aRoleName)
        {
            VaultClient client = new(new VaultClientSettings(_vaultSettings.VaultUrl,
                new TokenAuthMethodInfo(_vaultSettings.TokenApi)));

            Secret<UsernamePasswordCredentials> lSecret = await client.V1.Secrets.RabbitMQ
                .GetCredentialsAsync(aRoleName, "rabbitmq");
            return (IBasicCredentials)lSecret.Data;
        }

        public async Task<string> GetAPISecret()
        {
            var lAPISecret = await GetValueObject("apisecrets", "SecretKey")
                             ?? throw new Exception("Error loading retrieving the APISecret!!");

            return lAPISecret.ToString()!;
        }

        public async Task<bool> GetIsHealthy()
        {
            if (_vaultSettings.VaultUrl == null)
                await ConfigureDiscoveredSecretsManager();

            VaultClient lClient = new(new VaultClientSettings(_vaultSettings.VaultUrl,
                                                                new TokenAuthMethodInfo(_vaultSettings.TokenApi)));
            var lHealthStatus = await lClient.V1.System.GetHealthStatusAsync();
            return lHealthStatus.Initialized && !lHealthStatus.Sealed;
        }

        #endregion

        #region Private

        private static string GetTokenFromEnvironmentVariable()
             => Environment.GetEnvironmentVariable("VAULT_TOKEN")
                ?? throw new NotImplementedException("Error: not specified VAULT_TOKEN env_var");

        private async Task ConfigureDiscoveredSecretsManager()
        {
            string lDiscoveredUrl = await _serviceDiscovery!.GetFullAddress(InfraServicesRegistry.VaultSecretsManager)
                ?? throw new Exception("Error fetching the fault address from the service registry.");
            _vaultSettings.UpdateUrl(lDiscoveredUrl);
        }

        #endregion

    }

}


