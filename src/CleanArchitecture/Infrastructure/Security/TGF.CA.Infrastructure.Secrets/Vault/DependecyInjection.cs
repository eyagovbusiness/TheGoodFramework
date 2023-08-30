using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Application;
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
            aServiceCollection.Configure<VaultSettings>(aConfiguration.GetSection("VaultSecrets"));
            aServiceCollection.PostConfigure<VaultSettings>(settings =>
            {
                if (!string.IsNullOrWhiteSpace(aDiscoveredUrl))
                    settings.UpdateUrl(aDiscoveredUrl);
            });
            aServiceCollection.AddSingleton<ISecretsManager, SecretsManager>();

        }
    }
}
