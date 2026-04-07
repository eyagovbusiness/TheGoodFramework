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
            webApplicationBuilder.Services.Configure<SlasconeOptions>(webApplicationBuilder.Configuration.GetSection(ConfigurationKeys.Licensing.Slascone.Key));
            webApplicationBuilder.Services.AddTransient<ISlasconeClientFactory, SlasconeClientFactory>();
            webApplicationBuilder.Services.AddSingleton<ISlasconeLicensePrinter, SlasconeLicensePrinter>();
            webApplicationBuilder.Services.AddSingleton<ILicensingService, LicensingService>();
            webApplicationBuilder.Services.AddSingleton<ISlasconeSessionReleaseService, SlasconeSessionReleaseService>();
            webApplicationBuilder.Services.AddSingleton<IDeviceInfoService, DeviceInfoService>();
            webApplicationBuilder.Services.AddHostedService<LicensingStartupHostedService>();
            webApplicationBuilder.ConfigureSlasconeHealthCheck();
        }

        /// <summary>
        /// Adds a global gate that blocks all requests unless the license session is open.
        /// </summary>
        /// <param name="allowedPaths">
        /// Paths that must remain accessible (e.g., /healthz/ready, /healthz/live, /internal/licensing/close).
        /// </param>
        public static IApplicationBuilder UseLicensingGate(this WebApplication webApplication, params string[] allowedPaths)
            => webApplication.UseMiddleware<LicensingGateMiddleware>(allowedPaths ?? []);

        #region Private Methods
        /// <summary>
        /// Configures health checks for Slascone licensing.
        /// </summary>
        /// <param name="webApplicationBuilder"></param>
        private static void ConfigureSlasconeHealthCheck(this WebApplicationBuilder webApplicationBuilder) {
            webApplicationBuilder.Services.AddHealthChecks().AddCheck<SlasconeLicensingHealthCheck>(InfrastrcutureConstants.HealthCheckNames.LicenseCompliance);
            webApplicationBuilder.Services.AddSingleton<SlasconeLicensingHealthCheckCacheService>();
        }
        #endregion

    }
}
