using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TGF.CA.Infrastructure.Discovery;
using TGF.CA.Presentation.Middleware;
using TGF.Common.Logging;

namespace TGF.CA.Application.Setup.MinimalAPIs
{
    /// <summary>
    /// Class to support logic abstraction on the WebApplication creation for Minimal APIs.
    /// </summary>
    public static class MinimalWebApplicationAbstraction
    {
        private static Action<WebApplicationBuilder> DefaultBuildActions(params Type[] aScanMarkerList) =>
            (lBuilder) =>
            {
                lBuilder.Services.AddCustomAuthentication();
                lBuilder.Services.AddAuthorization();
                lBuilder.Services.AddEndpointsApiExplorer();
                lBuilder.Services.AddSwaggerGen();
                lBuilder.Services.AddEndpointDefinitions(aScanMarkerList);
                lBuilder.Configuration.AddConfiguration(HealthCheckHelper.BuildBasicHealthCheck(lBuilder.Configuration));
                lBuilder.Services.AddHealthChecks();
                lBuilder.Services.AddHealthChecksUI().AddInMemoryStorage();
                lBuilder.Host.ConfigureSerilog();
                lBuilder.Services.AddDiscoveryService(lBuilder.Configuration);
            };


        /// <summary>
        /// Creates a new instance of <see cref="WebApplication"/> with custom serilog configuration from <see cref="LoggerConfigurator"/>. Also additional <see cref="WebApplicationBuilder"/> actions can included in the build.
        /// </summary>
        /// <param name="aWebHostBuilderAction">Custom logic to add on the WebApplicationBuilder that will be used to build the resulting <see cref="WebApplication"/>.</param>
        /// <returns>A new customized instance of <see cref="WebApplication"/>.</returns>
        public static WebApplication CreateCustomWebApplication(Action<WebApplicationBuilder>? aWebApplicationBuilder = null, params Type[] aScanMarkerList)
        {
            WebApplicationBuilder lBuilder = WebApplication.CreateBuilder();
            var buildActions = DefaultBuildActions(aScanMarkerList);
            buildActions(lBuilder);
            aWebApplicationBuilder?.Invoke(lBuilder);

            return lBuilder.Build();

        }

        /// <summary>
        /// Async version of <see cref="CreateCustomWebApplication"/>, the usage of this version is very specific and it is not recommended for general purpose.
        /// </summary>
        /// <param name="aWebHostBuilderFunction">Custom logic to add on the WebApplicationBuilder that will be used to build the resulting <see cref="WebApplication"/>.</param>
        /// <returns>A new customized instance of <see cref="WebApplication"/>.</returns>
        /// <remarks>Any service added from aWebHostBuilderAction function will not be possible to be injected any dependency from the host DI container if the dependency is injected via async creation method instead of the synchronous class constructor of the service.</remarks>
        public static async Task<WebApplication> CreateCustomWebApplicationAsync(Func<WebApplicationBuilder, Task>? aWebHostBuilderFunction = null, params Type[] aScanMarkerList)
        {
            WebApplicationBuilder lBuilder = WebApplication.CreateBuilder();
            var buildActions = DefaultBuildActions(aScanMarkerList);
            buildActions(lBuilder);

            if (aWebHostBuilderFunction != null)
                await aWebHostBuilderFunction.Invoke(lBuilder);

            return lBuilder.Build();

        }

        /// <summary>
        /// Custom application run configuration.
        /// </summary>
        /// <param name="aWebApplication"></param>
        public static void CustomMinimalRun(this WebApplication aWebApplication)
        {


            if (aWebApplication.Environment.IsDevelopment() || aWebApplication.Environment.IsStaging())
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

            aWebApplication.UseMiddleware<ExceptionHandlerMiddleware>();
            aWebApplication.UseCors();
            aWebApplication.UseAuthentication();
            aWebApplication.UseAuthorization();
            aWebApplication.UseRouting().UseEndpoints(config => config.MapHealthChecksUI());
            aWebApplication.UseSwagger();
            aWebApplication.UseSwaggerUI();
            aWebApplication.UseEndpointDefinitions();

            aWebApplication.Run();
        }

    }
}