using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
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
        /// Configures a <see cref="WebApplicationBuilder"/> with default presentation. This includes custom serilog configuration, addition of a buch of built-in services, swagger, custom minimal api <see cref="IEndpointDefinition"/>, ui for healthChecks and more.
        /// </summary>
        /// <param name="aWebApplicationBuilder">The builder for the web application.</param>
        /// <param name="aXmlCommentFileList">The list of XML comment files to include in Swagger documentation. Used to add endpoint descriptions in swagger.</param>
        /// <param name="aBaseSwaggerPath">The base path for Swagger, should to be set when swagger runs behind a reverse proxy.</param>
        /// <param name="aScanMarkerList">The types to be scanned looking for any <see cref="IEndpointDefinition"/> implementation in the assemply, registering all the required enpoints. For every assembly which has endpoint deffinitions which should be included in this builder, at least one type of each assembly must be added to this list.</param>
        /// <returns>The configured <see cref=" WebApplicationBuilder"/>.</returns>
        public static WebApplicationBuilder ConfigureDefaultPresentation(this WebApplicationBuilder aWebApplicationBuilder, IEnumerable<string>? aXmlCommentFileList = default, string? aBaseSwaggerPath = default, params Type[] aScanMarkerList)
        {

            aWebApplicationBuilder.Services.Configure<JsonOptions>(options =>
            {
                options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            });
            aWebApplicationBuilder.Services.AddSerializer();
            aWebApplicationBuilder.Services.AddEndpointsApiExplorer();

            aWebApplicationBuilder.Services.AddSwaggerGen(opt => opt.ConfigureSwagger(aXmlCommentFileList, aBaseSwaggerPath));
            aWebApplicationBuilder.Services.AddEndpointDefinitions(aScanMarkerList);

            #region Infrastructure
            aWebApplicationBuilder.Configuration.AddConfiguration(HealthCheckHelper.BuildBasicHealthCheck(aWebApplicationBuilder.Configuration));
            aWebApplicationBuilder.Services.AddHealthChecks();
            aWebApplicationBuilder.Services.AddHealthChecksUI().AddInMemoryStorage(); 
            aWebApplicationBuilder.Host.ConfigureSerilog();
            #endregion

            aWebApplicationBuilder.Services.AddProblemDetails();

            return aWebApplicationBuilder;
        }

        /// <summary>
        /// Configures a CORS policy to allow cross origin requests from the frontend domain.
        /// </summary>
        /// <param name="aConfiguration">Configuration with the expected "FrontendURL" section with a string representing the frontend url to be allowed by CORS policy.</param>
        /// <exception cref="Exception">Thros an exception if FrontendURL is not configured in appsettings.</exception>
        public static WebApplicationBuilder ConfigureFrontendCORS(this WebApplicationBuilder aWebApplicationBuilder, IConfiguration aConfiguration)
        {
            var lFrontUrl = aConfiguration.GetValue<string>("FrontendURL")
                ?? throw new Exception("Error while configuring the default presentation, FrontendURL was not found in appsettings. Please add this configuration.");
            aWebApplicationBuilder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontCorsPolicy", builder => builder.WithOrigins(lFrontUrl)
                                                                    .AllowAnyHeader().AllowAnyMethod().AllowCredentials());
            });
            return aWebApplicationBuilder;
        }

        /// <summary>
        /// Use Authentication and Authorization.
        /// </summary>
        public static void UseIdentity(this WebApplication aWebApplication)
        {
            aWebApplication.UseAuthentication();
            aWebApplication.UseAuthorization();//UseAuthorization must be called after UseRouting() and before UseEndpoints()
        }

        /// <summary>
        /// Use the custom CORS policy to allow requests from the configured Frontend domain.
        /// </summary>
        public static void UseFrontendCORS(this WebApplication aWebApplication)
            => aWebApplication.UseCors("AllowFrontCorsPolicy");//CORS should be placed before routing.

    }
}
