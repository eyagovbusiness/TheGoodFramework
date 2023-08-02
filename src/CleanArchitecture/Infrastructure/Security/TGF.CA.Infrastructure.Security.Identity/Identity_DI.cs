using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Application.Setup;
using TGF.CA.Infrastructure.Communication;

namespace TGF.CA.Infrastructure.Security.Identity
{
    public static class Identity_DI
    {
        public static async Task<IServiceCollection> AddCustomIdentityAsync(this IServiceCollection aServiceCollection)
        {
            await aServiceCollection.AddDiscordOAuthPlusJWTAuthentication();
            aServiceCollection.AddAuthorization();
            //aServiceCollection.AddAuthorization(options =>
            //{
            //    options.AddPolicy("IsAdmin", policy =>
            //        policy.RequireRole("Admin").RequireAuthenticatedUser());
            //});
            return aServiceCollection;
        }
    }
}
