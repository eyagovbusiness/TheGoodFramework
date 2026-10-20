using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Infrastructure.InvariantConstants;
using TGF.CA.Infrastructure.Licensing.Slascone;
using TGF.CA.Infrastructure.Licensing.Slascone.Contracts;
using TGF.CA.Infrastructure.Licensing.Slascone.Helpers;
using TGF.CA.Infrastructure.Licensing.Slascone.Services;

namespace TGF.CA.Infrastructure.Licensing {
    public static class Licensing_DI {

        /// <summary>
        /// Configures Slascone licensing services. Registers the Slascone client, licensing service, and health checks.
        /// </summary>
        public static void ConfigureSlasconeLicensing(this WebApplicationBuilder webApplicationBuilder) {
            webApplicationBuilder.RegisterSlasconeLicensingServices();
            webApplicationBuilder.Services.AddHostedService<LicensingStartupHostedService>();
            webApplicationBuilder.ConfigureSlasconeHealthCheck();
        }

        /// <summary>
        /// Same registration as <see cref="ConfigureSlasconeLicensing(WebApplicationBuilder)"/>, but the licensing startup service performs no work
        /// (no device detection, activation or heartbeat) until <paramref name="activationGate"/> completes. The gate must be asynchronous and
        /// observe the supplied token; cancellation before completion means the host is stopping and nothing is activated. Opt-in: the default
        /// overload keeps the previous immediate activation.
        /// </summary>
        public static void ConfigureSlasconeLicensing(this WebApplicationBuilder webApplicationBuilder, Func<IServiceProvider, CancellationToken, Task> activationGate) {
            ArgumentNullException.ThrowIfNull(activationGate);
            webApplicationBuilder.RegisterSlasconeLicensingServices();
            webApplicationBuilder.Services.AddSingleton<Microsoft.Extensions.Hosting.IHostedService>(serviceProvider => new LicensingStartupHostedService(
                serviceProvider.GetRequiredService<ILicensingService>(),
                serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SlasconeOptions>>(),
                serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LicensingStartupHostedService>>(),
                cancellationToken => activationGate(serviceProvider, cancellationToken)));
            webApplicationBuilder.ConfigureSlasconeHealthCheck();
        }


        /// <summary>
        /// Adds a global gate that blocks all requests unless the license session is open.
        /// </summary>
        /// <param name="allowedPaths">
        /// Paths that must remain accessible (e.g., /healthz/ready, /healthz/live, /internal/licensing/close).
        /// </param>
        public static IApplicationBuilder UseLicensingGate(this WebApplication webApplication, params string[] allowedPaths)
            // Passed as one IEnumerable<string> argument: a bare string[] would be spread by UseMiddleware's params object[] and match no constructor.
            => webApplication.UseMiddleware<LicensingGateMiddleware>((IEnumerable<string>)(allowedPaths ?? []));

        #region Private Methods
        private static void RegisterSlasconeLicensingServices(this WebApplicationBuilder webApplicationBuilder) {
            webApplicationBuilder.Services.Configure<SlasconeOptions>(webApplicationBuilder.Configuration.GetSection(ConfigurationKeys.Licensing.Slascone.Key));
            webApplicationBuilder.Services.AddTransient<ISlasconeClientFactory, SlasconeClientFactory>();
            webApplicationBuilder.Services.AddSingleton<ISlasconeLicensePrinter, SlasconeLicensePrinter>();
            webApplicationBuilder.Services.AddSingleton<ILicensingService, LicensingService>();
            webApplicationBuilder.Services.AddSingleton<IExternalLicenseService, ExternalLicenseService>();
            webApplicationBuilder.Services.AddSingleton<IDeviceInfoService, DeviceInfoService>();
        }

        /// <summary>
        /// Configures health checks for Slascone licensing.
        /// </summary>
        /// <param name="webApplicationBuilder"></param>
        private static void ConfigureSlasconeHealthCheck(this WebApplicationBuilder webApplicationBuilder) {
            webApplicationBuilder.Services.AddHealthChecks().AddCheck<SlasconeLicensingHealthCheck>(
                InfrastrcutureConstants.HealthCheckNames.LicenseCompliance,
                tags: [InfrastrcutureConstants.HealthCheckTags.Ready]);
            webApplicationBuilder.Services.AddSingleton<SlasconeLicensingHealthCheckCacheService>();
        }
        #endregion

    }
}
