using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using TGF.CA.Infrastructure.InvariantConstants;

namespace TGF.CA.Infrastructure.Logging.Loggers {
    /// <summary>
    /// Provides configuration methods for setting up logging using OpenTelemetry.
    /// </summary>
    public static class OpenTelemetryLoggerConfiguration {
        /// <summary>
        /// Configures OpenTelemetry logging to log messages to the console and optionally to an OTLP exporter.
        /// The log level is retrieved from the application's configuration.
        /// </summary>
        /// <param name="hostBuilder">The <see cref="IHostBuilder"/> used to configure the application.</param>
        /// <returns>The configured <see cref="IHostBuilder"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown if the log level value is invalid.</exception>
        public static IHostBuilder ConfigureConsoleOpenTelemetryLogging(this IHostBuilder hostBuilder)
        => hostBuilder.ConfigureLogging((HostBuilderContext context, ILoggingBuilder loggingBuilder) => {
            loggingBuilder.ClearProviders();

            var logLevel = context.Configuration[ConfigurationKeys.Logging.LogLevel.Default];
            if (string.IsNullOrEmpty(logLevel))
                throw new NullReferenceException($"{ConfigurationKeys.Logging.LogLevel.Default} from appsettings key cannot be null or empty.");

            if (!Enum.TryParse<LogLevel>(logLevel, true, out var parsedLogLevel))
                throw new ArgumentException($"Invalid log level: {logLevel}");

            loggingBuilder.SetMinimumLevel(parsedLogLevel);

            // Apply per-category log level overrides from configuration
            LoggingExtensions.ApplyLogLevelOverrides(loggingBuilder, context.Configuration);

            loggingBuilder.AddOpenTelemetry(options => {
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
                options.ParseStateValues = true;

                options.SetResourceBuilder(ResourceBuilder.CreateDefault()
                    .AddService(context.Configuration["OpenTelemetry:ServiceName"] ?? "DefaultApp"));

                options.AddConsoleExporter();

                // Optional: OTLP exporter
                // options.AddOtlpExporter(otlpOptions =>
                // {
                //     otlpOptions.Endpoint = new Uri("http://your-otel-collector:4317");
                // });
            });
        });
    }
}
