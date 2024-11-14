
namespace TGF.CA.Application.DTOs
{
    public enum AuthenticationForwardingType
    {
        JWT,
        Cookie
    }

    /// <summary>
    /// DTO for wrapping authentication forwarding across microservices when service A calls service B's endpoint and needs to forward authentication (Cookie, JWT, etc.).
    /// </summary>
    /// <param name="AuthenticationContent">The authentication content, in case it is a Cookie the name of the cookie is expected to be as part of the content like "MyCookieName=auisgfduyiFVI"</param>
    /// <remarks> in case it is a Cookie the name of the cookie is expected to be as part of the content like "MyCookieName=auisgfduyiFVI" </remarks>
    public record AuthenticationForwardingDTO(AuthenticationForwardingType AuthenticationType, string AuthenticationContent);
}

