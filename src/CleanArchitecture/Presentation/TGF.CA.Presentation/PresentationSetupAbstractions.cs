using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        public static WebApplicationBuilder ConfigureDefaultPresentation(
            this WebApplicationBuilder aWebApplicationBuilder,
            IEnumerable<string>? aXmlCommentFileList = default,
            string? aBaseSwaggerPath = default,
            bool aUseStringEnums = true,
            params Type[] aScanMarkerList)
        {
            if (aUseStringEnums)
            {
                // Configure JSON options for Minimal API
                aWebApplicationBuilder.Services.ConfigureHttpJsonOptions(options =>
                {
                    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
                });

                // Additional configuration for Swagger to reflect enums as strings
                aWebApplicationBuilder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                });
            }

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
        /// Configures CORS to allow requests from the specified frontend URL pattern, supporting dynamic subdomains.
        /// </summary>
        /// <param name="aWebApplicationBuilder">The WebApplicationBuilder instance.</param>
        /// <param name="aConfiguration">The configuration instance.</param>
        /// <returns>The modified WebApplicationBuilder instance.</returns>
        /// <exception cref="Exception">Thrown when CORSFrontendURL configuration is not found.</exception>
        public static WebApplicationBuilder ConfigureFrontendCORS(this WebApplicationBuilder aWebApplicationBuilder, IConfiguration aConfiguration)
        {
            var lCORSFrontUrl = aConfiguration.GetValue<string>("FRONTEND_URL")
                ?? aConfiguration.GetValue<string>("FrontendURL")
                ?? throw new Exception("Error while configuring the default presentation, FrontendURL was not found in appsettings or environment variables. Please add this configuration.");

            var lLocalDevelopmentUrl = aConfiguration.GetValue<string>("DevelopmentDomain"); // Replace with your local development URL if different

            aWebApplicationBuilder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontCorsPolicy", builder =>
                    builder.SetIsOriginAllowed(origin => IsOriginAllowed(origin, lCORSFrontUrl, lLocalDevelopmentUrl))
                           .AllowAnyHeader()
                           .AllowAnyMethod()
                           .AllowCredentials());
            });

            return aWebApplicationBuilder;
        }

        /// <summary>
        /// Validates if the origin is allowed for CORS based on the frontend URL and local development URL. Allows origins like https://environment.subdomain.domain.TLD
        /// </summary>
        /// <param name="aOrigin">The origin of the incoming request.</param>
        /// <param name="aFrontendUrl">The configured frontend URL.</param>
        /// <param name="aLocalDevelopmentUrl">The local development URL (optional, for development environments).</param>
        /// <returns>True if the origin is allowed; false otherwise.</returns>
        /// <remarks>
        /// This method applies the following logic:
        /// 1. Extracts the main domain from the FrontendURL.
        /// 2. Allows the request if the origin matches either the FrontendURL or the local development URL.
        /// 3. Ensures that the origin's domain ends with the main domain.
        /// 4. Allows the case where there is no subdomain.
        /// 5. Validates that, if a subdomain exists, it matches the first part of the FrontendURL's domain.
        /// </remarks>
        private static bool IsOriginAllowed(string aOrigin, string aFrontendUrl, string? aLocalDevelopmentUrl)
        {
            var lMainDomain = new Uri(aFrontendUrl).Host;

            if (aOrigin == aFrontendUrl || aOrigin == aLocalDevelopmentUrl)
                return true;

            var lOriginDomain = new Uri(aOrigin).Host;
            var lHostParts = lOriginDomain.Split('.');

            if (!lOriginDomain.EndsWith(lMainDomain, StringComparison.OrdinalIgnoreCase))
                return false;

            if (lHostParts.Length == 2)
                return true;

            return lHostParts[0] == lMainDomain.Split('.')[0];

        }


        /// <summary>
        /// Determines if the specified origin is allowed based on the allowed origin pattern.
        /// </summary>
        /// <param name="origin">The origin URL to check.</param>
        /// <param name="allowedOriginPattern">The allowed origin pattern.</param>
        /// <returns>True if the origin is allowed; otherwise, false.</returns>
        private static bool IsOriginAllowed(string origin, string allowedOriginPattern)
        {
            if (Uri.TryCreate(origin, UriKind.Absolute, out var originUri) &&
                Uri.TryCreate(allowedOriginPattern, UriKind.Absolute, out var allowedUri))
            {
                return originUri.Scheme == allowedUri.Scheme &&
                       originUri.Host.EndsWith(allowedUri.Host, StringComparison.OrdinalIgnoreCase);
            }
            return false;
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
