using Microsoft.AspNetCore.Authorization;

namespace SF.Common.Infrastructure.Identity.Authorization.Permissions.AspNetIdentitiy {
    public class PermissionRequirement : IAuthorizationRequirement {
        public string ClaimType { get; }
        public IReadOnlyCollection<string> RequiredValues { get; }

        public PermissionRequirement(string claimType, params string[] requiredValues) {
            if (string.IsNullOrWhiteSpace(claimType))
                throw new ArgumentNullException(nameof(claimType));
            if (requiredValues == null || requiredValues.Length == 0)
                throw new ArgumentException("At least one required claim value must be specified.", nameof(requiredValues));

            ClaimType = claimType;
            RequiredValues = requiredValues;
        }
    }
}
