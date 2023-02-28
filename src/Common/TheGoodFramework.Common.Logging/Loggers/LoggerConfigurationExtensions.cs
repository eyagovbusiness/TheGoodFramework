using Serilog;
using Serilog.Sinks.Graylog;

namespace TheGoodFramework.Common.Logging.Loggers
{
    /// <summary>
    /// Class to support needed Serilog.LoggerConfiguration extensions to be configured from this library's custom serilog sink configurations.
    /// </summary>
    public static class LoggerConfigurationExtensions
    {
        /// <summary>
        /// Configure this LoggerConfiguration from a <see cref="ConsoleLoggerConfiguration"/> instance.
        /// </summary>
        /// <param name="aLoggerConfiguration">LoggerConfiguration to configure.</param>
        /// <param name="aConsoleLoggerConfiguration">Configuration object.</param>
        /// <returns>Updated LoggerConfiguration.</returns>
        public static LoggerConfiguration AddConsoleConfiguration(this LoggerConfiguration aLoggerConfiguration, ConsoleLoggerConfiguration aConsoleLoggerConfiguration)
        {
            return aConsoleLoggerConfiguration.Enabled
                    ? aLoggerConfiguration.WriteTo.Console(aConsoleLoggerConfiguration.MinimumLevel)
                    : aLoggerConfiguration;
        }
        /// <summary>
        /// Configure this LoggerConfiguration from a <see cref="GraylogLoggerConfiguration"/> instance.
        /// </summary>
        /// <param name="aLoggerConfiguration">LoggerConfiguration to configure.</param>
        /// <param name="aGraylogLoggerConfiguration">Configuration object.</param>
        /// <returns>Updated LoggerConfiguration.</returns>
        public static LoggerConfiguration AddGreylogConfiguration(this LoggerConfiguration aLoggerConfiguration, GraylogLoggerConfiguration aGraylogLoggerConfiguration)
        {
            return aGraylogLoggerConfiguration.Enabled
                    ? aLoggerConfiguration.WriteTo.Graylog(aGraylogLoggerConfiguration.Host, aGraylogLoggerConfiguration.Port, Serilog.Sinks.Graylog.Core.Transport.TransportType.Udp, minimumLogEventLevel: aGraylogLoggerConfiguration.MinimumLevel)
                    : aLoggerConfiguration;
        }
    }
}
