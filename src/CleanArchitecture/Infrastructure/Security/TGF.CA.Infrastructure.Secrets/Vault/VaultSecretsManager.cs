using Microsoft.Extensions.Logging;
using TGF.CA.Application;
using TGF.CA.Domain.External;
using TGF.CA.Infrastructure.Discovery;
using TGF.CA.Infrastructure.Security.Secrets.Common;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines;

namespace TGF.CA.Infrastructure.Security.Secrets.Vault
{
    /// <summary>
    /// Represents username and password credentials specifically for internal usage.DO NOT DELETE, seems not used but it is to allow matching between the interface and the base class
    /// </summary>
    internal class InternalUsernamePasswordCredentials : UsernamePasswordCredentials, IBasicCredentials { }

    /// <summary>
    /// Provides methods to manage secrets, using Vault.
    /// </summary>
    public class VaultSecretsManager : ISecretsManager
    {
        private readonly Lazy<Task<VaultClient>> _vaultClient;
        private readonly ILogger<VaultSecretsManager> _logger;
        public VaultSecretsManager(IServiceDiscovery aServiceDiscovery, ILogger<VaultSecretsManager> aLogger)
        {
            if (aServiceDiscovery == null)
                throw new ArgumentNullException(nameof(aServiceDiscovery), "Service discovery was null.");
            _logger = aLogger;
            _vaultClient = new Lazy<Task<VaultClient>>(GetVaultClient(aServiceDiscovery));
        }

        #region ISecretsManager

        public async Task<T> Get<T>(string aPath)
            where T : new()
        {
            var lVaultClient = await _vaultClient.Value;
            var lKv1Secret = await lVaultClient.V1.Secrets.KeyValue.V1
                .ReadSecretAsync(path: aPath, mountPoint: "secret");

            return lKv1Secret.Data.ToObject<T>();
        }

        public async Task<object> GetValueObject(string aPath, string aKey)
        {
            var lVaultClient = await _vaultClient.Value;

            // Use Vault v1 to read the secret
            var lKv1Secret = await lVaultClient.V1.Secrets.KeyValue.V1
                .ReadSecretAsync(path: aPath);

            // Retrieve the key's value from the secret data
            if (!lKv1Secret.Data.TryGetValue(aKey, out var value))
            {
                throw new Exception($"Key '{aKey}' not found in secret at path '{aPath}'.");
            }

            return value;
        }

        public async Task<IBasicCredentials> GetRabbitMQCredentials(string aRoleName)
        {
            var lVaultClient = await _vaultClient.Value;
            Secret<UsernamePasswordCredentials> lSecret = await lVaultClient.V1.Secrets.RabbitMQ
                .GetCredentialsAsync(aRoleName, "rabbitmq");
            return (IBasicCredentials)lSecret.Data;
        }

        public async Task<string> GetTokenSecret(string aTokenName)
        {
            var lAPISecret = await GetValueObject("data/tokensecrets", aTokenName)
                             ?? throw new Exception("Error loading retrieving the APISecret!!");
            return lAPISecret.ToString()!;
        }

        public async Task<string> GetServiceKey(string aServiceName)
        {
            var lAPISecret = await GetValueObject("data/apisecrets", aServiceName)
                 ?? throw new Exception($"Error loading retrieving the ApiKey for service {aServiceName}.");
            return lAPISecret.ToString()!;
        }

        public async Task<bool> GetIsHealthy()
        {
            var lVaultClient = await _vaultClient.Value;
            var lHealthStatus = await lVaultClient.V1.System.GetHealthStatusAsync();
            return lHealthStatus.Initialized && !lHealthStatus.Sealed;
        }

        #endregion

        #region Private

        /// <summary>
        /// Used to retrieve the vault api token.
        /// </summary>
        private static string GetTokenFromEnvironmentVariable()
             => Environment.GetEnvironmentVariable("VAULT_TOKEN")
                ?? throw new InvalidOperationException("Error: not specified VAULT_TOKEN env_var");

        /// <summary>
        /// Used for Lazy initialization of the VaultClient.
        /// </summary>
        private async Task<VaultClient> GetVaultClient(IServiceDiscovery aServiceDiscovery)
        {
            try
            {
                string lVaultAddress = await aServiceDiscovery.GetFullAddress(InfraServicesRegistry.VaultSecretsManager)
                    ?? throw new Exception("Vault address not found in service registry.");
                string lVaultToken = GetTokenFromEnvironmentVariable();

                return new VaultClient(new VaultClientSettings(lVaultAddress, new TokenAuthMethodInfo(lVaultToken)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing Vault client.");
                throw;
            }
        }

        #endregion

    }

}


