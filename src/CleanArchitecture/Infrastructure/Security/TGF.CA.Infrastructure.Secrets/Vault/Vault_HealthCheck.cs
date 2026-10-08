using Microsoft.Extensions.Diagnostics.HealthChecks;
using TGF.CA.Application;

namespace TGF.CA.Infrastructure.Secrets.Vault {
    public class Vault_HealthCheck(ISecretsManager aSecretsManager) : IHealthCheck {
        private readonly ISecretsManager _secretsManager = aSecretsManager;

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext aContext, CancellationToken aCancellationToken = default) {
            try {
                var lIsHealty = await _secretsManager.GetIsHealthy();
                return lIsHealty
                    ? HealthCheckResult.Healthy("Vault is initialized and unsealed.")
                    : HealthCheckResult.Unhealthy("Vault is not initialized or sealed.");
            }
            catch (Exception) {
                // Provider/exception text is untrusted (addresses, tokens) and is published anonymously: fixed description only.
                return HealthCheckResult.Unhealthy("Failed to check Vault health.");
            }
        }
    }
}
