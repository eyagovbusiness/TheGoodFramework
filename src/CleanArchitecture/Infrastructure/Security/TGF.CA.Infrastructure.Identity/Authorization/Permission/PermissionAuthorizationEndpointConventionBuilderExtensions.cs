﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;

namespace TGF.CA.Infrastructure.Security.Identity.Authorization.Permission
{
    /// <summary>
    /// Static class to register the additional logic needed to support endpoints authorization by permissions enums.
    /// </summary>
    public static class PermissionAuthorizationEndpointConventionBuilderExtensions
    {
        /// <summary>
        /// Extension method of <see cref="IEndpointConventionBuilder"/> inspired on <see cref="AuthorizationEndpointConventionBuilderExtensions.RequireAuthorization{TBuilder}(TBuilder, string[])"/>.
        /// This extension method emulates the same behaviour as the RequireAuthorization() method but instead of specifying policy names, enum values representing permissions are provided.
        /// The enum value expected is from an enum type defined with the attribute Flags.
        /// When this method is called on an ednpoint, it creates internally a new <see cref="AuthorizationPolicy"/> that will be used to authorize this endpoint (the created policy will be reused for authorizing any other endpoints that requires the same set of permissions).
        /// </summary>
        /// <typeparam name="TBuilder">Builder derived from the endpoint that will require the permissions authorization.</typeparam>
        /// <typeparam name="TPermissionsEnum">The enum type that represents the permissions, the expected enum type should be defined with the attribute Flags.</typeparam>
        /// <param name="aBuilder"><see cref="IEndpointConventionBuilder"/> related to the endpoint that will require the permissions authorization.</param>
        /// <param name="aPermissions"></param>
        /// <returns>The same instance of <see cref="IEndpointConventionBuilder"/> that can be used to chain more methods.</returns>
        /// <exception cref="ArgumentNullException">Can be thrown when aBuilder or aPermissions arguments are null.</exception>
        public static TBuilder RequirePermissions<TBuilder, TPermissionsEnum>(this TBuilder aBuilder, params TPermissionsEnum[] aPermissions)
            where TBuilder : IEndpointConventionBuilder
            where TPermissionsEnum : struct
        {
            if (aBuilder == null)
                throw new ArgumentNullException(nameof(aBuilder));

            if (aPermissions == null)
                throw new ArgumentNullException(nameof(aPermissions));

            return aBuilder.RequireAuthorization(aPermissions.Select(permission => new AuthorizeAttribute(((int)(object)permission).ToString())).ToArray());
        }
    }
}
