using Microsoft.Extensions.Logging;
using TGF.CA.Application;
using TGF.CA.Domain.ExternalContracts;
using TGF.CA.Infrastructure.Discovery;
using TGF.CA.Infrastructure.Secrets.Common;
using TGF.Common.Extensions;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines;

namespace TGF.CA.Infrastructure.Secrets.Vault {
    /// <summary>
    /// Represents username and password credentials specifically for internal usage.DO NOT DELETE, seems not used but it is to allow matching between the interface and the base class
    /// </summary>
    internal class InternalUsernamePasswordCredentials : UsernamePasswordCredentials, IBasicCredentials { }

    /// <summary>
    /// Provides methods to manage secrets, using Vault.
    /// </summary>
    public class VaultSecretsManager : ISecretsManager {
        private readonly Lazy<Task<VaultClient>> _vaultClient;
        private readonly ILogger<VaultSecretsManager> _logger;
        public VaultSecretsManager(IServiceDiscovery aServiceDiscovery, ILogger<VaultSecretsManager> aLogger) {
            if (aServiceDiscovery == null)
                throw new ArgumentNullException(nameof(aServiceDiscovery), "Service discovery was null.");
            _logger = aLogger;
            _vaultClient = new Lazy<Task<VaultClient>>(GetVaultClient(aServiceDiscovery));
        }

        #region ISecretsManager

        public async Task<T> Get<T>(string aPath)
            where T : new() {
            var lVaultClient = await _vaultClient.Value;
            Secret<SecretData> lKv2Secret = await lVaultClient.V1.Secrets.KeyValue.V2
                .ReadSecretAsync(path: aPath, mountPoint: "secret");

            return lKv2Secret.Data.Data.ToObject<T>();
        }

        public async Task<object> GetValueObject(string aPath, string aKey) {
            var lVaultClient = await _vaultClient.Value;
            Secret<SecretData> lKv2Secret = await lVaultClient.V1.Secrets.KeyValue.V2
                .ReadSecretAsync(path: aPath, mountPoint: "secret");
            return lKv2Secret.Data.Data[aKey];
        }

        public async Task<IBasicCredentials> GetRabbitMQCredentials(string aRoleName) {
            var lVaultClient = await _vaultClient.Value;
            Secret<UsernamePasswordCredentials> lSecret = await lVaultClient.V1.Secrets.RabbitMQ
                .GetCredentialsAsync(aRoleName, "rabbitmq");
            return (IBasicCredentials)lSecret.Data;
        }

        public async Task<string> GetTokenSecret(string aTokenName) {
            var lAPISecret = await GetValueObject("tokensecrets", aTokenName)
                             ?? throw new Exception("Error loading retrieving the APISecret!!");
            return lAPISecret.ToString()!;
        }

        public async Task<string> GetServiceKey(string aServiceName) {
            var lAPISecret = await GetValueObject("apisecrets", aServiceName)
                 ?? throw new Exception($"Error loading retrieving the ApiKey for service {aServiceName}.");
            return lAPISecret.ToString()!;
        }

        public async Task<bool> GetIsHealthy() {
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
        private async Task<VaultClient> GetVaultClient(IServiceDiscovery aServiceDiscovery) {
            return await RetryUtility.ExecuteWithRetryAsync(
                async () => {
                    try {
                        string lVaultAddress = await aServiceDiscovery.GetFullAddress(InfraServicesRegistry.VaultSecretsManager)
                            ?? throw new Exception("Vault address not found in service registry.");
                        string lVaultToken = GetTokenFromEnvironmentVariable();

                        return new VaultClient(new VaultClientSettings(lVaultAddress, new TokenAuthMethodInfo(lVaultToken)));
                    }
                    catch (Exception ex) {
                        _logger.LogWarning(ex, "Error initializing Vault client.");
                        throw; // Rethrow to ensure RetryUtility handles it.
                    }
                },
                _ => false, // Retry only on exceptions.
                aMaxRetries: 10, // Customize max retries as needed.
                aDelayMilliseconds: 2000, // Customize delay between retries.
                CancellationToken.None // Pass a CancellationToken if applicable.
            );
        }


        #endregion

    }

}


