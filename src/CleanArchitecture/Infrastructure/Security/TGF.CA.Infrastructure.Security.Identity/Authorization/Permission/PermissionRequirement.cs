using Microsoft.AspNetCore.Authorization;

namespace TGF.CA.Infrastructure.Security.Identity.Authorization
{
    /// <summary>
    /// Implementation of <see cref="IAuthorizationRequirement"/> that defines permission requirements.
    /// </summary>
    /// <typeparam name="TPermissionsEnum">The enum type that represents the permissions, the expected enum type should be defined with the attribute Flags.</typeparam>
    public class PermissionRequirement<TPermissionsEnum> : IAuthorizationRequirement
        where TPermissionsEnum : struct
    {
        public TPermissionsEnum RequiredPermissions { get; }

        public PermissionRequirement(TPermissionsEnum aRequiredPermissions)
            => RequiredPermissions = aRequiredPermissions;
        public PermissionRequirement(string aRequiredPermissionsAsString)
        {
            if (Enum.TryParse(typeof(TPermissionsEnum), aRequiredPermissionsAsString, out object? lParsedValue) && lParsedValue is TPermissionsEnum lPermissions)
                RequiredPermissions = lPermissions;
            else
                throw new ArgumentException("An invalid permissions string was provided when creating a new instance of PermissionRequirement<TPermissionsEnum>!!");
        }
    }
}
