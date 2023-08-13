using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;
using TGF.CA.Application.Setup;
using TGF.CA.Application.Setup.Swagger;
using TGF.CA.Infrastructure.Discovery;
using TGF.CA.Presentation.Middleware;
using TGF.Common.Logging;
using TGF.Common.Serialization;

namespace TGF.CA.Application.MinimalApi.Setup
{
    /// <summary>
    /// Class to support logic abstraction on the WebApplication creation for Minimal APIs.
    /// </summary>
    public static class MinimalWebApplicationAbstraction
    {
        private static Action<WebApplicationBuilder> DefaultBuildActions(params Type[] aScanMarkerList) =>
        (lBuilder) =>
        {
            lBuilder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontCorsPolicy", builder => builder.WithOrigins("http://staging.alfilo.org")
                                                                            .AllowAnyHeader().AllowAnyMethod().AllowCredentials());
            });
            lBuilder.Services.AddSerializer();
            lBuilder.Services.AddDiscoveryService(lBuilder.Configuration);
            lBuilder.Services.AddEndpointsApiExplorer();
            lBuilder.Services.AddSwaggerGen();
            lBuilder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
            lBuilder.Services.AddEndpointDefinitions(aScanMarkerList);
            lBuilder.Configuration.AddConfiguration(HealthCheckHelper.BuildBasicHealthCheck(lBuilder.Configuration));
            lBuilder.Services.AddHealthChecks();
            lBuilder.Services.AddHealthChecksUI().AddInMemoryStorage();
            lBuilder.Host.ConfigureSerilog();
            lBuilder.Services.AddProblemDetails();

        };

        /// <summary>
        /// Creates a new instance of <see cref="WebApplication"/> with custom serilog configuration from <see cref="LoggerConfigurator"/>. Also additional <see cref="WebApplicationBuilder"/> actions can included in the build.
        /// </summary>
        /// <param name="aWebHostBuilderAction">Custom logic to add on the WebApplicationBuilder that will be used to build the resulting <see cref="WebApplication"/>.</param>
        /// <returns>A new customized instance of <see cref="WebApplication"/>.</returns>
        public static WebApplicationBuilder GetNewBuilder(params Type[] aScanMarkerList)
        {
            WebApplicationBuilder lBuilder = WebApplication.CreateBuilder();
            var lBuildActions = DefaultBuildActions(aScanMarkerList);
            lBuildActions(lBuilder);
            return lBuilder;

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


            //aWebApplication.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto });
            aWebApplication.UseCustomErrorHandlingMiddleware();
            aWebApplication.UseRouting();//UseRouting() must be called before UseAuthentication() and UseAuthorization()
            aWebApplication.UseCors("AllowFrontCorsPolicy");
            aWebApplication.UseAuthentication();
            aWebApplication.UseAuthorization();//UseAuthorization must be called after UseRouting() and before UseEndpoints()
            aWebApplication.UseEndpoints(config => config.MapHealthChecksUI());
            aWebApplication.UseSwagger();
            aWebApplication.UseSwaggerUI();
            aWebApplication.UseEndpointDefinitions();

            aWebApplication.Run();
        }

    }
}