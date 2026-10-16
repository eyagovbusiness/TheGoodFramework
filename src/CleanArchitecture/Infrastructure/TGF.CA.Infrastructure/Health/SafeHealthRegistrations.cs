using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net;

namespace TGF.CA.Infrastructure.Health;

/// <summary>
/// Wraps registered health checks so no raw exception, description or data can reach ASP.NET's default health infrastructure
/// (which logs them before any response writer runs). The aggregate endpoint/service is not replaced and registration metadata is untouched.
/// </summary>
public static class SafeHealthRegistrations {
    /// <summary>Internal HealthCheckResult data key through which a check may hand over a trusted integer HTTP status (100-599).</summary>
    public const string HttpStatusCodeDataKey = "TGF.SafeHealth.HttpStatusCode";

    /// <summary>The only registration allowed to surface the numeric status diagnostic.</summary>
    public const string ImageRegistryCheckName = "ImageRegistry";

    /// <summary>Wraps every registration once. Safe to call repeatedly and after all health registrations.</summary>
    public static IServiceCollection AddSafeHealthCheckRegistrations(this IServiceCollection services)
        => services.PostConfigure<HealthCheckServiceOptions>(options => {
            foreach (var registration in options.Registrations) {
                Wrap(registration);
            }
        });

    /// <summary>Wraps only the registrations with the given name (used by adapters that own a third-party check).</summary>
    public static IServiceCollection AddSafeHealthCheckRegistration(this IServiceCollection services, string registrationName)
        => services.PostConfigure<HealthCheckServiceOptions>(options => {
            foreach (var registration in options.Registrations.Where(candidate => candidate.Name == registrationName)) {
                Wrap(registration);
            }
        });

    /// <summary>Replaces the registration factory by a lazy safe one with the same scope; idempotent.</summary>
    public static void Wrap(HealthCheckRegistration registration) {
        ArgumentNullException.ThrowIfNull(registration);
        if (registration.Factory.Target is SafeFactory) {
            return;
        }

        registration.Factory = new SafeFactory(registration.Factory, registration.Name).Create;
    }

    private sealed class SafeFactory(Func<IServiceProvider, IHealthCheck> original, string name) {
        public IHealthCheck Create(IServiceProvider serviceProvider) {
            IHealthCheck inner;
            try {
                inner = original(serviceProvider);
            } catch (Exception) {
                // Fixed boundary exception without inner/Data: the framework does not catch construction failures, so this stays an unexpected 500.
                throw new InvalidOperationException($"Health check '{name}' could not be constructed.");
            }

            return new SafeCheck(inner, name);
        }
    }

    private sealed class SafeCheck(IHealthCheck inner, string name) : IHealthCheck {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
            HealthCheckResult result;
            try {
                result = await inner.CheckHealthAsync(context, cancellationToken);
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                // Caller cancellation or the registration timeout (both cancel this token): keep cancellation semantics, drop message/inner/Data.
                throw new OperationCanceledException("The health check was cancelled.", cancellationToken);
            } catch (Exception) {
                return new HealthCheckResult(context.Registration.FailureStatus, $"{name} check failed unexpectedly.");
            }

            return new HealthCheckResult(result.Status, Describe(result));
        }

        private string Describe(HealthCheckResult result) {
            if (name == ImageRegistryCheckName
                && result.Data.TryGetValue(HttpStatusCodeDataKey, out var value)
                && value is int statusCode
                && statusCode is >= 100 and <= 599) {
                return $"{name} is unavailable: {DescribeStatus(statusCode)}";
            }

            return $"{name} is {result.Status}.";
        }
    }

    /// <summary>Known enum name plus number, or just the number for non-standard codes (never provider text).</summary>
    public static string DescribeStatus(int statusCode)
        => Enum.IsDefined(typeof(HttpStatusCode), statusCode) ? $"{(HttpStatusCode)statusCode} ({statusCode})" : statusCode.ToString();
}