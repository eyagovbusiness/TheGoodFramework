using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TGF.CA.Application;
using TGF.Common.Extensions;

namespace TGF.CA.Infrastructure.Secrets.Vault {
    public static class VaultSecretsManager_DI {
        /// <summary>
        /// Adds Vault service to the service collection for secrets management and default health check.
        /// </summary>
        public static IServiceCollection AddVaultSecretsManager(this IServiceCollection aServiceCollection, IConfiguration configuration, ILogger logger) {
            _ = logger ??
                throw new ArgumentNullException(nameof(logger), "AddVaultSecretsManager requires a builder logger!");
            var discoveryAddress = configuration["Discovery:Address"];
            if (discoveryAddress.IsNullOrWhiteSpace()) {
                logger.LogInformation("Discovery cofiguration is missing in appsettings, skipping vault secrets manager registration since it required the discovery service...");
                return aServiceCollection;
            }
            return aServiceCollection.AddSingleton<ISecretsManager, VaultSecretsManager>()
            .AddConsulHealthChecks("SecretsManager");
        }


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
