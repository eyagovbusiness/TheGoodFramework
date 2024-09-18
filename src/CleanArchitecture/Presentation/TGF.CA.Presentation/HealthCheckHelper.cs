﻿using Microsoft.Extensions.Configuration;

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
        public static IConfiguration BuildBasicHealthCheck(IConfiguration? aConfiguration = default, bool aIsHttps = false, Dictionary<string, string?>? aAditionalHealtCheckConfig = null)
        {
            //To-Do: Remove this after all MS are suign HTTPS
            var lProtocol = aIsHttps ? "https" : "http";
            var lPort = aIsHttps ? "5001" : "8080";
            var lNewConfigurationBuilder =
                new ConfigurationBuilder()
                .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    {"HealthChecksUI:HealthChecks:0:Name", $"{aConfiguration?.GetValue<string>("AppName") ?? "self"}"},
                    {"HealthChecksUI:HealthChecks:0:Uri", $"{lProtocol}://localhost:{lPort}/health"},
                    {"HealthChecksUI:EvaluationTimeInSeconds", $"{aConfiguration?.GetValue<string>("HealthCheckTickInSeconds") ?? "60"}" },
                    {"HealthChecksUI:MinimumSecondsBetweenFailureNotifications", "90" }
                });

            return aAditionalHealtCheckConfig == null
                ? lNewConfigurationBuilder.Build()
                : lNewConfigurationBuilder.AddInMemoryCollection(aAditionalHealtCheckConfig).Build();

        }
    }
}
