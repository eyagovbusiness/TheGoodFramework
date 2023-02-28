using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using TGF.Common.Logging.Loggers;

namespace TGF.Common.Logging
{
    /// <summary>
    /// Class to add an extension method to IHostBuilder adding and configuring serilog for this host from its configuration.
    /// </summary>
    public static class LoggerConfigurator
    {
        /// <summary>
        /// Adds into this host's logging pipeline serilog, configured according to this host configuration.
        /// </summary>
        /// <param name="aHostBuilder">host where serilog has to be added.</param>
        /// <returns>this IHostBuilder</returns>
        public static IHostBuilder ConfigureSerilog(this IHostBuilder aHostBuilder)
            => aHostBuilder.UseSerilog((aContext, aLoggerConfiguration)
                => ConfigureSerilogLogger(aLoggerConfiguration, aContext.Configuration));

        /// <summary>
        /// Configures a Serilog.LoggerConfiguration from the Loggers specified in a given host configuration.
        /// </summary>
        /// <param name="aLoggerConfiguration">LoggerConfiguration to configure from the given host IConfiguration</param>
        /// <param name="aConfiguration">Host configuration</param>
        /// <returns>LoggerConfiguration configured from a host configuration</returns>
        private static LoggerConfiguration ConfigureSerilogLogger(LoggerConfiguration aLoggerConfiguration, IConfiguration aConfiguration)
        {
            GraylogLoggerConfiguration aGraylogLoggerConfiguration = new GraylogLoggerConfiguration();
            aConfiguration.GetSection("Logging:Graylog").Bind(aGraylogLoggerConfiguration);
            ConsoleLoggerConfiguration aConsoleLoggerConfiguration = new ConsoleLoggerConfiguration();
            aConfiguration.GetSection("Logging:Console").Bind(aConsoleLoggerConfiguration);

            return aLoggerConfiguration.AddConsoleConfiguration(aConsoleLoggerConfiguration).AddGreylogConfiguration(aGraylogLoggerConfiguration);
        }

    }
}
