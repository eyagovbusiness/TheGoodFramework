using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using TGF.CA.Infrastructure.InvariantConstants;
namespace TGF.CA.Infrastructure.Logging.Loggers {
    /// <summary>  
    /// Provides extension methods for configuring Serilog logger instances.  
    /// </summary>  
    public static class SerilogLoggerConfiguration {

        /// <summary>  
        /// Configures Serilog to log messages to the console with a specified log level.  
        /// Clears existing logging providers before configuring Serilog.  
        /// </summary>  
        /// <param name="hostBuilder">The <see cref="IHostBuilder"/> used to configure the application.</param>  
        /// <exception cref="ArgumentNullException">Thrown if the log level configuration key is null or empty.</exception>  
        /// <exception cref="ArgumentException">Thrown if the log level value is invalid.</exception>  
        public static void ConfigureConsoleSerilogLogging(this IHostBuilder hostBuilder)
        => hostBuilder.ConfigureLogging((context, loggingBuilder) => {
            loggingBuilder.ClearProviders();
            LoggingExtensions.ApplyLogLevelOverrides(loggingBuilder, context.Configuration);
        })
        .UseSerilog((context, loggerConfiguration) => {
            var logLevel = context.Configuration[ConfigurationKeys.Logging.LogLevel.Default];

            if (string.IsNullOrEmpty(logLevel))
                throw new NullReferenceException($"{ConfigurationKeys.Logging.LogLevel.Default} from appsettings key cannot be null or empty.");

            if (!Enum.TryParse<Serilog.Events.LogEventLevel>(logLevel, true, out var parsedLogLevel))
                throw new ArgumentException($"Invalid log level: {logLevel}");

            var computedOutputTemplate = ((int)parsedLogLevel) > ((int)Serilog.Events.LogEventLevel.Information)
            ? "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
            : "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

            loggerConfiguration
                .Enrich.FromLogContext()
                .MinimumLevel.Is(parsedLogLevel)
                .ApplySerilogLevelOverridesFromConfiguration(context.Configuration)
                .WriteTo.Console(outputTemplate: computedOutputTemplate);
        });
    }
}
