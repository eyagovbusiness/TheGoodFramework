using Microsoft.Extensions.Diagnostics.HealthChecks;
using TGF.CA.Application;

namespace TGF.CA.Infrastructure.Security.Secrets.Vault
{
    public class Vault_HealthCheck : IHealthCheck
    {
        private readonly ISecretsManager _secretsManager;

        public Vault_HealthCheck(ISecretsManager aSecretsManager)
        {
            _secretsManager = aSecretsManager;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext aContext, CancellationToken aCancellationToken = default)
        {
            try
            {
                var lIsHealty = await _secretsManager.GetIsHealthy();
                return lIsHealty
                    ? HealthCheckResult.Healthy("Vault is initialized and unsealed.")
                    : HealthCheckResult.Unhealthy("Vault is not initialized or sealed.");
            }
            catch (Exception lEx)
            {
                return HealthCheckResult.Unhealthy($"Failed to check Vault health: {lEx.Message}");
            }
        }
    }
}
