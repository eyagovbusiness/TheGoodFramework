using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace SF.Common.Infrastructure.Identity.Authorization.Permissions.AspNetIdentitiy {
    public class PermissionAuthorizationPolicyProvider(
        IOptions<AuthorizationOptions> options
    ) : DefaultAuthorizationPolicyProvider(options) {
        private readonly ConcurrentDictionary<string, AuthorizationPolicy> _policyCache = new();

        public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName) {
            if (_policyCache.TryGetValue(policyName, out var cached))
                return cached;

            var existing = await base.GetPolicyAsync(policyName);
            if (existing == null) {
                // Expected policyName format: "Permission:CanCreateUsers,CanDeleteUsers"
                var parts = policyName.Split(':', 2);
                if (parts.Length == 2) {
                    var claimType = parts[0];
                    var claimValues = parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries);

                    var newPolicy = new AuthorizationPolicyBuilder()
                        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                        .RequireAuthenticatedUser()
                        .AddRequirements(new PermissionRequirement(claimType, claimValues))
                        .Build();

                    _policyCache[policyName] = newPolicy;
                    return newPolicy;
                }
            }

            return existing;
        }
    }
}
