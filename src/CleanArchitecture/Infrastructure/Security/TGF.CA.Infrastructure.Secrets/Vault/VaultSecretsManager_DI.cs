using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Application;
using TGF.CA.Infrastructure.Security.Secrets.Vault;

namespace TGF.CA.Infrastructure.Security.Secrets
{
    public static class VaultSecretsManager_DI
    {
        /// <summary>
        /// Adds Vault service to the service collection for secrets management and default health check.
        /// </summary>
        public static void AddVaultSecretsManager(this IServiceCollection aServiceCollection)
            => aServiceCollection.AddSingleton<ISecretsManager, VaultSecretsManager>()
            .AddConsulHealthChecks("SecretsManager");

        /// <summary>
        /// Adds a basic health check for vault secrets manager.
        /// </summary>
        /// <param name="aHealthCheckName">Display name of the health check.</param>
        public static IServiceCollection AddConsulHealthChecks(this IServiceCollection aServiceCollection, string aHealthCheckName)
            => aServiceCollection
                .AddHealthChecks()
                .AddCheck<Vault_HealthCheck>(aHealthCheckName)
                .Services;

    }
}
