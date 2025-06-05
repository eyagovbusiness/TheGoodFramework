using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TGF.CA.Infrastructure.InvariantConstants;

namespace TGF.CA.Infrastructure.Logging {
    public static class Logger_DI {
        /// <summary>
        /// Configures logging for the specified <see cref="WebApplicationBuilder"/>.
        /// </summary>
        /// <param name="webApplicationBuilder">The <see cref="WebApplicationBuilder"/> instance.</param>
        /// <param name="logger">Optional out parameter to get the configured <see cref="ILogger"/> instance.</param>
        public static void ConfigureLogging<Tlogger>(this WebApplicationBuilder webApplicationBuilder, out ILogger<Tlogger>? logger) {
            ConfigureLogging(webApplicationBuilder);
            logger = webApplicationBuilder.Services.BuildServiceProvider().GetRequiredService<ILogger<Tlogger>>();
            logger!.LogInformation(
                "Starting {AppName} - {ServiceName} ...",
                webApplicationBuilder.Configuration[ConfigurationKeys.AppMetadata.AppName],
                webApplicationBuilder.Configuration[ConfigurationKeys.AppMetadata.ServiceName]
            );
        }

        /// <summary>
        /// Configures logging for the specified <see cref="WebApplicationBuilder"/>.
        /// </summary>
        /// <param name="webApplicationBuilder"></param>
        public static void ConfigureLogging(this WebApplicationBuilder webApplicationBuilder)
        => webApplicationBuilder.Host.ConfigureLogging();
    }
}
