using Microsoft.Extensions.Configuration;

namespace TGF.CA.Presentation
{
    /// <summary>
    /// Class to help building HealthChecksUI configuration.
    /// </summary>
    public static class HealthCheckHelper
    {
        /// <summary>
        /// Builds a new <see cref="IConfiguration"/> from in-memory stored HealthChecksUI configuration.
        /// </summary>
        /// <param name="aAditionalHealtCheckConfig">Aditional JSON configuration to add in Dictionary format.</param>
        /// <returns><see cref="IConfiguration"/> with added HealthChecksUI configuration from memory.</returns>
        public static IConfiguration BuildBasicHealthCheck(IConfiguration? aConfiguration = default, Dictionary<string, string?>? aAditionalHealtCheckConfig = null)
        {
            var lNewConfigurationBuilder =
                new ConfigurationBuilder()
                .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    {"HealthChecksUI:HealthChecks:0:Name", $"{aConfiguration?.GetValue<string>("AppName") ?? "self"}"},
                    {"HealthChecksUI:HealthChecks:0:Uri", $"http://localhost/health"},
                    {"HealthChecksUI:EvaluationTimeInSeconds", $"{aConfiguration?.GetValue<string>("HealthCheckTickInSeconds") ?? "60"}" },
                    {"HealthChecksUI:MinimumSecondsBetweenFailureNotifications", "90" }
                });

            return aAditionalHealtCheckConfig == null
                ? lNewConfigurationBuilder.Build()
                : lNewConfigurationBuilder.AddInMemoryCollection(aAditionalHealtCheckConfig).Build();

        }
    }
}
