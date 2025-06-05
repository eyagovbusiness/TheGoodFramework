using Microsoft.Extensions.Hosting;
using TGF.CA.Infrastructure.InvariantConstants;
using TGF.CA.Infrastructure.Logging.Loggers;

namespace TGF.CA.Infrastructure.Logging {
    /// <summary>
    /// Class to add an extension method to IHostBuilder adding and configuring logging for this host from its configuration.
    /// </summary>
    public static class LoggerConfigurator {

        /// <summary>
        /// Adds into this host's logging pipeline the appropriate logger, configured according to this host configuration.
        /// </summary>
        /// <param name="aHostBuilder">Host where logging has to be added.</param>
        /// <returns>This IHostBuilder</returns>
        public static IHostBuilder ConfigureLogging(this IHostBuilder aHostBuilder)
        => ConfigureConsoleLogger(aHostBuilder);

        /// <summary>
        /// Configures the console logger for the host based on the configuration provided.
        /// </summary>
        /// <param name="aHostBuilder">The host builder to configure.</param>
        /// <returns>The configured host builder.</returns>
        private static IHostBuilder ConfigureConsoleLogger(IHostBuilder aHostBuilder)
        => aHostBuilder.ConfigureServices((context, services) => {
            var provider = context.Configuration[ConfigurationKeys.Logging.Console.Provider];
            if (string.IsNullOrEmpty(provider))
                throw new InvalidOperationException($"Logging provider configuration key '{ConfigurationKeys.Logging.Console.Provider}' cannot be null or empty.");

            switch (provider) {
                case ConfigurationValues.Logging.Console.Provider.OpenTelemetry:
                    aHostBuilder.ConfigureConsoleOpenTelemetryLogging();
                    break;
                case ConfigurationValues.Logging.Console.Provider.Serilog:
                    aHostBuilder.ConfigureConsoleSerilogLogging();
                    break;
                default:
                    throw new InvalidOperationException($"Unknown logging provider: {provider}");
            }
        });
    }
}
