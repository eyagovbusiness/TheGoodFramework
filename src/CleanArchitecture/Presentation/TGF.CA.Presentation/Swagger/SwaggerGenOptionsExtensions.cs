using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using TGF.CA.Infrastructure.Middleware.Security;

namespace TGF.CA.Presentation.Swagger
{

    /// <summary>
    /// Swagger options to configure swagger in order to:
    /// </summary>
    public static class SwaggerGenOptionsExtensions
    {
        /// <summary>
        /// <see cref="SwaggerGenOptions"/> extension method to perform several custom configurations for swagger:
        /// <list type="bullet">
        /// <item>
        /// <description>Integrate authentication UI to test endpoints that require Authorization and/or Authentication.</description>
        /// </item>
        /// <item>
        /// <description>Add endpoint summaries.</description>
        /// </item>
        /// <item>
        /// <description>Configure swagger paths to work behind a reverse proxy.</description>
        /// </item>
        /// <item>
        /// <description>Exclude private endpoints.</description>
        /// </item>
        /// </list>
        /// </summary>
        /// <param name="aXmlCommentFileList">List of <see cref="string"/> with the full paths of the documentation files containing the endpoint summaries.</param>
        /// <param name="aBaseSwaggerPath">
        /// When it is not null or empty this <see cref="string"/> defines the base path used to modify the original swagger paths.
        /// When it is set with the right value it makes possible that swagger works behind a reverse proxy. 
        /// </param>
        public static void ConfigureSwagger(this SwaggerGenOptions aOptions, IEnumerable<string>? aXmlCommentFileList, string? aBaseSwaggerPath)
        => aOptions.ConfigureJWTBearerAuth()
            .ConfigureEndpointDescriptions(aXmlCommentFileList)
            .ConfigureBehindProxy(aBaseSwaggerPath)
            .ConfigureExcludingPrivateEndpoints();

        #region Private

        /// <summary>
        /// Configure swagger options to integrate authentication UI to test endpoints that require Authorization and/or Authentication.
        /// </summary>
        private static SwaggerGenOptions ConfigureJWTBearerAuth(this SwaggerGenOptions aOptions)
        {
            aOptions.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Description = "Bearer Token",
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                BearerFormat = "JWT",
                Scheme = "Bearer"
            });

            aOptions.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer",
                        }
                    },
                    Array.Empty<string>()
                }
            });
            return aOptions;
        }

        /// <summary>
        /// Configure swagger options to Add endpoint summaries.
        /// </summary>
        /// <param name="aXmlCommentFileList">List of <see cref="string"/> with the full paths of the documentation files containing the endpoint summaries.</param>
        private static SwaggerGenOptions ConfigureEndpointDescriptions(this SwaggerGenOptions aOptions, IEnumerable<string>? aXmlCommentFileList)
        {
            if (aXmlCommentFileList != null)
                foreach (var lXmlDocFileFullPath in aXmlCommentFileList)
                    aOptions.IncludeXmlComments(lXmlDocFileFullPath);
            return aOptions;
        }

        /// <summary>
        /// Configure swagger paths to work behind a reverse proxy.
        /// </summary>
        /// <param name="aBaseSwaggerPath">
        /// When it is not null or empty this <see cref="string"/> defines the base path used to modify the original swagger paths.
        /// When it is set with the right value it makes possible that swagger works behind a reverse proxy. 
        /// </param>
        private static SwaggerGenOptions ConfigureBehindProxy(this SwaggerGenOptions aOptions, string? aBaseSwaggerPath)
        {
            if (!string.IsNullOrEmpty(aBaseSwaggerPath))
                aOptions.DocumentFilter<BasePathDocumentFilter>(aBaseSwaggerPath);
            return aOptions;
        }

        /// <summary>
        /// Configure swagger options to exclude private endpoints.
        /// </summary>
        private static void ConfigureExcludingPrivateEndpoints(this SwaggerGenOptions aOptions)
            => aOptions.DocInclusionPredicate((docName, apiDescription) => !apiDescription.RelativePath!.Contains(PrivateEndpointPaths.Default.TrimStart('/')));

        #endregion


    }
}
