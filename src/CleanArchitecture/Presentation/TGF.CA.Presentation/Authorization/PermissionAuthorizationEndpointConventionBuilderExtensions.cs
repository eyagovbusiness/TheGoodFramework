using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;

namespace TGF.CA.Presentation.Authorization {
    /// <summary>
    /// Extension methods for configuring permission-based authorization requirements on minimal API endpoints.
    /// </summary>
    public static class PermissionAuthorizationEndpointConventionBuilderExtensions {
        /// <summary>
        /// Adds a dynamic permission-based authorization policy requirement to an endpoint.
        /// </summary>
        /// <typeparam name="TBuilder">
        /// The type of endpoint convention builder (e.g., <see cref="RouteHandlerBuilder"/>).
        /// </typeparam>
        /// <param name="builder">
        /// The endpoint convention builder to which the authorization requirement will be applied.
        /// </param>
        /// <param name="claimType">
        /// The type of claim to check (e.g., "Permission", "Role").
        /// </param>
        /// <param name="requiredValues">
        /// One or more claim values that the authenticated user must possess 
        /// to access the endpoint (e.g., "CanCreateUsers", "CanDeleteUsers").
        /// </param>
        /// <returns>
        /// The same <typeparamref name="TBuilder"/> instance for method chaining.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method does not require you to register static policies in <c>Program.cs</c>.
        /// Instead, a <c>PermissionAuthorizationPolicyProvider</c> (if configured) will dynamically 
        /// create and cache policies at runtime based on the specified claim type and values.
        /// </para>
        /// <para>
        /// This is especially useful in systems with many fine-grained permissions, 
        /// as you avoid manually registering each policy.
        /// </para>
        /// </remarks>
        /// <example>
        /// Minimal API usage:
        /// <code>
        /// app.MapPost("/users", CreateUserHandler)
        ///    .RequiresClaims(OmicsFlowClaims.Permission.ClaimType, 
        ///                    OmicsFlowClaims.Permission.Values.CanCreateUsers);
        /// </code>
        /// </example>
        public static TBuilder RequiresClaims<TBuilder>(
            this TBuilder builder,
            string claimType,
            params string[] requiredValues)
            where TBuilder : IEndpointConventionBuilder {
            
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentException.ThrowIfNullOrWhiteSpace(claimType);
            
            if (requiredValues == null || requiredValues.Length == 0)
                throw new ArgumentException("At least one claim value must be provided.", nameof(requiredValues));

            // Policy name format: ClaimType:Value1,Value2
            var policyName = $"{claimType}:{string.Join(",", requiredValues)}";

            return builder.RequireAuthorization(new AuthorizeAttribute(policyName));
        }
    }
}
