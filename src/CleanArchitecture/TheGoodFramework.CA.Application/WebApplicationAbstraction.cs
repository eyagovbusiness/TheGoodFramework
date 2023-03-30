using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TGF.CA.Presentation.Middleware;
using TGF.Common.Logging;

namespace TGF.CA.Application
{
    /// <summary>
    /// Class to support logic abstraction on the WebApplication creation.
    /// </summary>
    public static class WebApplicationAbstraction
    {
        private static Action<WebApplicationBuilder> _defaultBuildActions =>
            (lBuilder) =>
            {
                lBuilder.Configuration.AddConfiguration(HealthCheckHelper.BuildBasicHealthCheck());
                lBuilder.Services.AddHealthChecks();
                lBuilder.Services.AddHealthChecksUI().AddInMemoryStorage();
                lBuilder.Host.ConfigureSerilog();
                lBuilder.Services.AddControllers();
                lBuilder.Services.AddEndpointsApiExplorer();
                lBuilder.Services.AddSwaggerGen();
            };


        /// <summary>
        /// Creates a new instance of <see cref="WebApplication"/> with custom serilog configuration from <see cref="LoggerConfigurator"/>. Also additional <see cref="WebApplicationBuilder"/> actions can included in the build.
        /// </summary>
        /// <param name="aWebHostBuilderAction">Custom logic to add on the WebApplicationBuilder that will be used to build the resulting <see cref="WebApplication"/>.</param>
        /// <returns>A new customized instance of <see cref="WebApplication"/>.</returns>
        public static WebApplication CreateCustomWebApplication(Action<WebApplicationBuilder>? aWebApplicationBuilder = null)
        {
            WebApplicationBuilder lBuilder = WebApplication.CreateBuilder();

            _defaultBuildActions.Invoke(lBuilder);

            if (aWebApplicationBuilder != null)
                aWebApplicationBuilder.Invoke(lBuilder);

            return lBuilder.Build();

        }

        /// <summary>
        /// Async version of <see cref="CreateCustomWebApplication"/>, the usage of this version is very specific and it is not recommended for general purpose.
        /// </summary>
        /// <param name="aWebHostBuilderFunction">Custom logic to add on the WebApplicationBuilder that will be used to build the resulting <see cref="WebApplication"/>.</param>
        /// <returns>A new customized instance of <see cref="WebApplication"/>.</returns>
        /// <remarks>Any service added from aWebHostBuilderAction function will not be possible to be injected any dependency from the host DI container if the dependency is injected via async creation method instead of the synchronous class constructor of the service.</remarks>
        public static async Task<WebApplication> CreateCustomWebApplicationAsync(Func<WebApplicationBuilder, Task>? aWebHostBuilderFunction = null)
        {
            WebApplicationBuilder lBuilder = WebApplication.CreateBuilder();

            _defaultBuildActions.Invoke(lBuilder);

            if (aWebHostBuilderFunction != null)
                await aWebHostBuilderFunction.Invoke(lBuilder);

            return lBuilder.Build();

        }

        /// <summary>
        /// Custom application run configuration.
        /// </summary>
        /// <param name="aWebApplication"></param>
        public static void CustomRun(this WebApplication aWebApplication)
        {

            if (aWebApplication.Environment.IsDevelopment())
            {
                aWebApplication.UseSwagger();
                aWebApplication.UseSwaggerUI();
            }
            else
                aWebApplication.UseHttpsRedirection();

            aWebApplication.MapHealthChecks("/health", new HealthCheckOptions()
            {
                Predicate = _ => true,
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });
            aWebApplication.UseHealthChecksUI(aConfig =>
            {
                aConfig.UIPath = "/health-ui";
            });

            aWebApplication.UseMiddleware<LoggingMiddleware>();
            aWebApplication.UseRouting().UseEndpoints(config => config.MapHealthChecksUI());
            aWebApplication.UseAuthorization();
            aWebApplication.MapControllers();
            aWebApplication.Run();
        }

    }
}