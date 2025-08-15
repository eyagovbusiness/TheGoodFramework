using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;

namespace SF.Common.Infrastructure.Identity.Authorization.Permissions.AspNetIdentitiy {
    public static class PermissionAuthorizationEndpointConventionBuilderExtensions {
        public static TBuilder RequiresClaims<TBuilder>(
            this TBuilder builder,
            string claimType,
            params string[] requiredValues)
            where TBuilder : IEndpointConventionBuilder {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (string.IsNullOrWhiteSpace(claimType))
                throw new ArgumentNullException(nameof(claimType));

            if (requiredValues == null || requiredValues.Length == 0)
                throw new ArgumentException("At least one claim value must be provided.", nameof(requiredValues));

            // Policy name format: ClaimType:Value1,Value2
            var policyName = $"{claimType}:{string.Join(",", requiredValues)}";

            return builder.RequireAuthorization(new AuthorizeAttribute(policyName));
        }
    }
}
