using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace TGF.CA.Infrastructure.Logging;

/// <summary>
/// Extension methods for configuring logging with log level overrides from configuration.
/// </summary>
public static class LoggingExtensions {
    /// <summary>
    /// Applies log level overrides from the configuration to the logging builder. 
    /// </summary>
    public static void ApplyLogLevelOverrides(ILoggingBuilder loggingBuilder, IConfiguration configuration) {
        var logLevelSection = configuration.GetSection("Logging:LogLevel");
        foreach (var kvp in logLevelSection.GetChildren()) {
            if (Enum.TryParse<LogLevel>(kvp.Value, out var level)) {
                loggingBuilder.AddFilter(kvp.Key, level);
            }
        }
    }
    /// <summary>
    /// Applies Serilog minimum level overrides from the Logging:LogLevel section of configuration.
    /// </summary>
    public static LoggerConfiguration ApplySerilogLevelOverridesFromConfiguration(
        this LoggerConfiguration loggerConfiguration,
        IConfiguration configuration) {
        var logLevelSection = configuration.GetSection("Logging:LogLevel");
        foreach (var kvp in logLevelSection.GetChildren()) {
            if (string.Equals(kvp.Key, "Default", StringComparison.OrdinalIgnoreCase))
                continue; // Default is handled elsewhere

            if (Enum.TryParse<LogEventLevel>(kvp.Value, true, out var level)) {
                loggerConfiguration.MinimumLevel.Override(kvp.Key, level);
            }
        }
        return loggerConfiguration;
    }
}