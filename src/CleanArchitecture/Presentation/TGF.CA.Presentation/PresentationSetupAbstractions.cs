using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using TGF.CA.Presentation.Middleware;
using TGF.CA.Presentation.MinimalAPI;
using TGF.CA.Presentation.Swagger;
using TGF.Common.Logging;
using TGF.Common.Serialization;

namespace TGF.CA.Presentation
{
    /// <summary>
    /// Provides methods and configurations to set up the presentation layer.
    /// </summary>
    public static class PresentationSetupAbstractions
    {
        /// <summary>
        /// Sets the default configurations for the WebApplicationBuilder.
        /// </summary>
        /// <param name="aXmlCommentFileList">The list of XML comment files to include in Swagger documentation. Used to add endpoint descriptions in swagger.</param>
        /// <param name="aBaseSwaggerPath">The base path for Swagger, should to be set when swagger runs behind a reverse proxy.</param>
        /// <param name="aScanMarkerList">The types to be scanned looking for any <see cref="IEndpointDefinition"/> implementation in the assemply, registering all the required enpoints. For every assembly which has endpoint deffinitions which should be included in this builder, at least one type of each assembly must be added to this list.</param>
        /// <returns>An action to configure the WebApplicationBuilder.</returns>
        private static Action<WebApplicationBuilder> DefaultBuildActions(IEnumerable<string>? aXmlCommentFileList = default, string? aBaseSwaggerPath = default, params Type[] aScanMarkerList) =>
        (lBuilder) =>
        {
            lBuilder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontCorsPolicy", builder => builder.WithOrigins("https://staging.alfilo.org")
                                                                            .AllowAnyHeader().AllowAnyMethod().AllowCredentials());
            });
            lBuilder.Services.Configure<JsonOptions>(options =>
            {
                options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            });
            lBuilder.Services.AddSerializer();
            lBuilder.Services.AddEndpointsApiExplorer();

            lBuilder.Services.AddSwaggerGen(opt => opt.ConfigureSwagger(aXmlCommentFileList, aBaseSwaggerPath));
            lBuilder.Services.AddEndpointDefinitions(aScanMarkerList);

            #region Infrastructure
            lBuilder.Configuration.AddConfiguration(HealthCheckHelper.BuildBasicHealthCheck(lBuilder.Configuration));
            lBuilder.Services.AddHealthChecks();
            lBuilder.Services.AddHealthChecksUI().AddInMemoryStorage();
            lBuilder.Host.ConfigureSerilog();
            #endregion

            lBuilder.Services.AddProblemDetails();

        };

        /// <summary>
        /// Configures a <see cref="WebApplicationBuilder"/> with default presentation. This includes custom serilog configuration, addition of a buch of built-in services, swagger, custom minimal api <see cref="IEndpointDefinition"/>, ui for healthChecks and more.
        /// </summary>
        /// <param name="aWebApplicationBuilder">The builder for the web application.</param>
        /// <param name="aXmlCommentFileList">The list of XML comment files to include in Swagger documentation. Used to add endpoint descriptions in swagger.</param>
        /// <param name="aBaseSwaggerPath">The base path for Swagger, should to be set when swagger runs behind a reverse proxy.</param>
        /// <param name="aScanMarkerList">The types to be scanned looking for any <see cref="IEndpointDefinition"/> implementation in the assemply, registering all the required enpoints. For every assembly which has endpoint deffinitions which should be included in this builder, at least one type of each assembly must be added to this list.</param>
        /// <returns>The configured <see cref=" WebApplicationBuilder"/>.</returns>
        public static WebApplicationBuilder ConfigureDefaultPresentation(this WebApplicationBuilder aWebApplicationBuilder, IEnumerable<string>? aXmlCommentFileList = default, string? aBaseSwaggerPath = default, params Type[] aScanMarkerList)
        {
            var lBuildActions = DefaultBuildActions(aXmlCommentFileList, aBaseSwaggerPath, aScanMarkerList);
            lBuildActions(aWebApplicationBuilder);
            return aWebApplicationBuilder;
        }

        /// <summary>
        /// Applies default configurations to the given WebApplication, including Swagger, CORS, and authentication/authorization setup.
        /// </summary>
        /// <param name="aWebApplication">The web application to be configured.</param>
        /// <param name="aUseIdentity">Indicates whether to use identity-based authentication and authorization.</param>
        /// <param name="aUseCORS">Indicates whether to enable CORS for the application.</param>
        public static void UseDefaultPresentationConfigurations(this WebApplication aWebApplication, bool aUseIdentity = true, bool aUseCORS = true)
        {
            if (aWebApplication.Environment.IsDevelopment() || aWebApplication.Environment.IsStaging())
            {
                aWebApplication.UseSwagger();
                aWebApplication.UseSwaggerUI();
            }

            aWebApplication.MapHealthChecks("/health", new HealthCheckOptions()
            {
                Predicate = _ => true,
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });

            //aWebApplication.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto });
            aWebApplication.UseCustomErrorHandlingMiddleware();
            if (aUseCORS)
                aWebApplication.UseCors("AllowFrontCorsPolicy");//CORS should be placed before routing.

            aWebApplication.UseRouting();//UseRouting() must be called before UseAuthentication() and UseAuthorization()

            if (aUseIdentity)
            {
                aWebApplication.UseAuthentication();
                aWebApplication.UseAuthorization();//UseAuthorization must be called after UseRouting() and before UseEndpoints()
            }

            aWebApplication.MapHealthChecksUI(options => options.UIPath = "/health-ui");
            aWebApplication.UseEndpointDefinitions();
            aWebApplication.Run();
        }
    }
}
