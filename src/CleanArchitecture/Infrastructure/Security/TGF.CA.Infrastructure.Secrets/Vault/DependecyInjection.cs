using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Infrastructure.Discovery;
using TGF.CA.Infrastructure.Security.Secrets.Vault;

namespace TGF.CA.Infrastructure.Security.Secrets
{
    public static class DependecyInjection
    {
        /// <summary>
        /// Adds Vault service to the service collection for secrets managemnet.
        /// The aDiscoveredUrl is optional as it will be calculated on the setup project, but only if that project is in use.
        /// </summary>
        public static void AddVaultSecretsManager(this IServiceCollection aServiceCollection, IConfiguration aConfiguration, string? aDiscoveredUrl = null)
        {
            aServiceCollection.Configure<Settings>(aConfiguration.GetSection("VaultSecrets"));
            aServiceCollection.PostConfigure<Settings>(settings =>
            {
                if (!string.IsNullOrWhiteSpace(aDiscoveredUrl))
                    settings.UpdateUrl(aDiscoveredUrl);
            });
            aServiceCollection.AddSingleton<ISecretsManager, SecretsManager>();

        }

        public static void ConfigureDiscoveredSecretsManager(this ISecretsManager aScretsManager, IServiceDiscovery aServiceDiscovery)
        {
            string lDiscoveredUrl = GetVaultUrl(aServiceDiscovery).Result;//TO-DO:TEMPORARY SOLUTION, blocks thread(on startup is not so big problem, but not good)
            aScretsManager.UpdateUrl(lDiscoveredUrl);
        }

        private static async Task<string> GetVaultUrl(IServiceDiscovery aServiceDiscovery)
            => await aServiceDiscovery?.GetFullAddress(InfraServicesRegistry.VaultSecretsManager)!;

    }
}
