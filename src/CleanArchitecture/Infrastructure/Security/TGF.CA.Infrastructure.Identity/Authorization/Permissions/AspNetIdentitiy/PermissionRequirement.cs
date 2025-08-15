using Microsoft.AspNetCore.Authorization;

namespace SF.Common.Infrastructure.Identity.Authorization.Permissions.AspNetIdentitiy {
    /// <summary>
    /// An <see cref="IAuthorizationRequirement"/> that enforces one or more required permission claim values.
    /// </summary>
    public class PermissionRequirement : IAuthorizationRequirement {
        /// <summary>
        /// Gets the type of claim required for authorization. 
        /// </summary>
        public string ClaimType { get; }
        /// <summary>
        /// Gets the set of required permission claim values.
        /// </summary>
        public IReadOnlyCollection<string> RequiredValues { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PermissionRequirement"/> class.
        /// </summary>
        /// <param name="requiredValues">
        /// One or more permission claim values that the user must have to satisfy the requirement.
        /// </param>
        /// <exception cref="ArgumentException">Thrown if no permissions are provided.</exception>
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
