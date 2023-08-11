using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Application.Setup;
using TGF.CA.Infrastructure.Communication;
using TGF.CA.Infrastructure.Security.Identity.Authorization;
using TGF.CA.Infrastructure.Security.Identity.Authorization.Role;

namespace TGF.CA.Infrastructure.Security.Identity
{
    public static class Identity_DI
    {
        public static async Task<IServiceCollection> AddCustomIdentityAsync(this IServiceCollection aServiceCollection)
        {
            await aServiceCollection.AddDiscordOAuthPlusJWTAuthentication();
            aServiceCollection.AddAuthorization(options =>
            {
                PolicyBuilder.AddRoleHierarchyPolicy(options, "Admin");
                PolicyBuilder.AddRoleHierarchyPolicy(options, "Espada");
                PolicyBuilder.AddRoleHierarchyPolicy(options, "Daga");
                PolicyBuilder.AddRoleHierarchyPolicy(options, "Cadete");
                PolicyBuilder.AddRoleHierarchyPolicy(options, "Afiliado");
            });
            return aServiceCollection;
        }


    }
}
