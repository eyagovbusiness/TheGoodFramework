using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Application.Setup;

namespace TGF.CA.Infrastructure.Security.Identity
{
    /// <summary>
    /// Static class to provide <see cref="IServiceCollection"/> extensions to add Identity related custom logic to the web application.
    /// </summary>
    public static class Identity_DI
    {
        /// <summary>
        /// Adds custom identity logic to the web application. Using Discord OAuth2, session cookies based on Discord Auth and JWT Bearer tokens to authorize api endpoints.
        /// </summary>
        public static async Task<IServiceCollection> AddCustomIdentityAsync(this IServiceCollection aServiceCollection, IConfiguration aConfiguration, IWebHostEnvironment aEnvironment)
        {
            await aServiceCollection.AddDiscordOAuthPlusJWTAuthentication(aConfiguration, aEnvironment);
            aServiceCollection.AddAuthorization();
            //aServiceCollection.AddAuthorization(options =>
            //{
            //    RolePolicyBuilder.AddRoleHierarchyPolicy(options, "Admin");
            //    RolePolicyBuilder.AddRoleHierarchyPolicy(options, "Espada");
            //    RolePolicyBuilder.AddRoleHierarchyPolicy(options, "Daga");
            //    RolePolicyBuilder.AddRoleHierarchyPolicy(options, "Cadete");
            //    RolePolicyBuilder.AddRoleHierarchyPolicy(options, "Afiliado");
            //});
            return aServiceCollection;
        }

    }
}
