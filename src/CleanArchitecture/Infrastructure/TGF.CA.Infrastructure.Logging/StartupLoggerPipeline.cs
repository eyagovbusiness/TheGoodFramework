using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using Serilog;
using Serilog.Extensions.Logging;
using TGF.CA.Infrastructure.InvariantConstants;

namespace TGF.CA.Infrastructure.Logging;

/// <summary>
/// Builds a PRIVATE, startup-only logging pipeline for the configured provider (Serilog or OpenTelemetry), separate from the host's global logging.
/// </summary>
/// <remarks>
/// It exists so that events of explicitly constructed startup components (EF contexts, Npgsql data source, Identity managers) can be emitted through the
/// real supported sinks WITHOUT the ambient state the global pipeline adds: Serilog gets no <c>LogContext</c> enrichment and OpenTelemetry includes no
/// scopes, and nothing registered in the global factory is shared or reconfigured. It does not change global logging, categories or filters.
/// </remarks>
public static class StartupLoggerPipeline {
    /// <summary>
    /// Creates the private factory. The optional hooks only add sinks/exporters (tests, diagnostics); they cannot re-enable scopes or enrichment.
    /// The caller owns and disposes the returned factory.
    /// </summary>
    public static ILoggerFactory Create(IConfiguration configuration, Action<LoggerConfiguration>? addSerilogSinks = null, Action<OpenTelemetryLoggerOptions>? addOpenTelemetryExporters = null) {
        var provider = configuration[ConfigurationKeys.Logging.Console.Provider];
        var levelText = configuration[ConfigurationKeys.Logging.LogLevel.Default];
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(levelText)) {
            throw new InvalidOperationException("The startup logging provider or default level is not configured.");
        }

        return provider switch {
            ConfigurationValues.Logging.Console.Provider.Serilog => CreateSerilog(levelText, addSerilogSinks),
            ConfigurationValues.Logging.Console.Provider.OpenTelemetry => CreateOpenTelemetry(configuration, levelText, addOpenTelemetryExporters),
            _ => throw new InvalidOperationException("The startup logging provider is not supported.")
        };
    }

    private static ILoggerFactory CreateSerilog(string levelText, Action<LoggerConfiguration>? addSinks) {
        if (!Enum.TryParse<Serilog.Events.LogEventLevel>(levelText, true, out var level)) {
            throw new InvalidOperationException("The startup logging level is invalid.");
        }

        // Deliberately NO Enrich.FromLogContext(): ambient LogContext properties must never reach startup events.
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Is(level)
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}");
        addSinks?.Invoke(configuration);
        return new SerilogLoggerFactory(configuration.CreateLogger(), dispose: true);
    }

    private static ILoggerFactory CreateOpenTelemetry(IConfiguration configuration, string levelText, Action<OpenTelemetryLoggerOptions>? addExporters) {
        if (!Enum.TryParse<LogLevel>(levelText, true, out var level)) {
            throw new InvalidOperationException("The startup logging level is invalid.");
        }

        return LoggerFactory.Create(builder => {
            builder.ClearProviders();
            builder.SetMinimumLevel(level);
            builder.AddOpenTelemetry(options => {
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = false; // ambient scopes must never reattach
                options.ParseStateValues = true;
                options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(configuration["OpenTelemetry:ServiceName"] ?? "DefaultApp"));
                options.AddConsoleExporter();
                addExporters?.Invoke(options);
            });
        });
    }
}
