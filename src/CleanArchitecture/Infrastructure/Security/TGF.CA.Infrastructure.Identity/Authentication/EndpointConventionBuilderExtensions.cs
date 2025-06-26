using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using TGF.CA.Application;

namespace TGF.CA.Infrastructure.Identity.Authentication {
    public static class EndpointConventionBuilderExtensions {

        /// <summary>
        /// Configures this endpoint to require the DiscordAuthSchemeName defined in the application layer.
        /// </summary>
        public static TBuilder RequireDiscord<TBuilder>(this TBuilder aBuilder)
            where TBuilder : IEndpointConventionBuilder
        => aBuilder.RequireAuthorization(new AuthorizeAttribute {
            AuthenticationSchemes = AuthenticationSchemes.DiscordAuthSchemeName
        });

        /// <summary>
        /// Sets the authorization scheme to require JWT Bearer authentication, any endpoint with this specification will require a valid JWT token to access it.
        /// </summary>
        public static TBuilder RequireJWTBearer<TBuilder>(this TBuilder aBuilder)
            where TBuilder : IEndpointConventionBuilder
        => aBuilder.RequireAuthorization(new AuthorizeAttribute {
            AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme
        });

        /// <summary>
        /// Sets the authorization scheme to require OIDC authentication, any endpoint with this specification will require either OpenID Connect authentication to access it OR the Cookie authentication scheme generated after a sucessful sign-in with OIDC.
        /// </summary>
        public static TBuilder RequireOIDC<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
        => builder.RequireAuthorization(new AuthorizeAttribute {
            AuthenticationSchemes = string.Join(",",
            [
                AuthenticationSchemes.OIDC_CookieAuthSchemeName,
                AuthenticationSchemes.OIDCAuthSchemeName
            ])
        });
    }
}
