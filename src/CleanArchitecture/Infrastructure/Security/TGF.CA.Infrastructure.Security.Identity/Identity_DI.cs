using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Application.Setup;

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
            //    options.AddPolicy("CanManageUsers", p => p.RequireRole("Espada"));
            //});
            return aServiceCollection;
        }
    }
}
