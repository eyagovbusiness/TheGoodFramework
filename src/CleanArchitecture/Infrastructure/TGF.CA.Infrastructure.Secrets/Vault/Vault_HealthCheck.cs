using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TGF.CA.Infrastructure.Secrets.Vault
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
                var lHealthResponse = await _secretsManager.GetHealthStatusAsync();

                if (lHealthResponse?.Initialized == true && lHealthResponse?.Sealed == false)
                {
                    return HealthCheckResult.Healthy("Vault is initialized and unsealed.");
                }
                else
                {
                    return HealthCheckResult.Unhealthy("Vault is not initialized or sealed.");
                }
            }
            catch (Exception lEx)
            {
                return HealthCheckResult.Unhealthy($"Failed to check Vault health: {lEx.Message}");
            }
        }
    }
}
