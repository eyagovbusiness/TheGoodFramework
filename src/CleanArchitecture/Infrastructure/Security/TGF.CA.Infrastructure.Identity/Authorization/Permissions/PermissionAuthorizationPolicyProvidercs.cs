using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace TGF.CA.Infrastructure.Security.Identity.Authorization.Permissions
{
    /// <summary>
    /// Subclass of <see cref="DefaultAuthorizationPolicyProvider"/> that can be added to any WebApplicationBuilder's collection of services as singelton service to extend the default ahuthorization policy provider in order to register dinamically custom policies required by endpoints at runtime by using the EndpointConventionBuilderExtension <see cref="PermissionAuthorizationEndpointConventionBuilderExtensions.RequirePermissions{TBuilder, TPermissionsEnum}(TBuilder, TPermissionsEnum[])"/>.
    /// The <see cref="TPermissionsEnum"/> type must be the same for both.
    /// </summary>
    /// <typeparam name="TPermissionsEnum">The enum type that represents the permissions, the expected enum type should be defined with the attribute Flags.</typeparam>
    public class PermissionAuthorizationPolicyProvider<TPermissionsEnum> : DefaultAuthorizationPolicyProvider
        where TPermissionsEnum : struct
    {

        /// <summary>
        /// Private cache that reuses the on demand created AuthorizationPolicy instances for any other endpoint that will authorize under the same permissions constraint policy.
        /// </summary>
        private readonly ConcurrentDictionary<string, AuthorizationPolicy> _policyCache = new();

        public PermissionAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options) : base(options)
        {
        }

        /// <summary>
        /// Override of <see cref="DefaultAuthorizationPolicyProvider.GetPolicyAsync(string)"/> that intercepts any policy request when the authorization process is triggered in order to provide the permission-replated policies that were not staticly defined during the application setup.
        /// </summary>
        /// <param name="aPolicyName">Name of the required policy.</param>
        /// <returns>The required <see cref="AuthorizationPolicy"/> instance.</returns>
        public override async Task<AuthorizationPolicy?> GetPolicyAsync(string aPolicyName)
        {
            if (_policyCache.TryGetValue(aPolicyName, out AuthorizationPolicy? lCachedPolicy))
                return lCachedPolicy;

            AuthorizationPolicy? lExistingPolicy = await base.GetPolicyAsync(aPolicyName);
            if (lExistingPolicy == null)
            {
                var lNewPolicy = new AuthorizationPolicyBuilder()
                    .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermissionRequirement<TPermissionsEnum>(aPolicyName))
                    .Build();

                _policyCache.TryAdd(aPolicyName, lNewPolicy);

                return lNewPolicy;
            }

            return lExistingPolicy;
        }
    }

}
