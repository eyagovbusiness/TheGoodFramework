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
        /// Sets the authorization scheme to require OIDC authentication, any endpoint with this specification will require either OpenID Connect authentication to access it OR the Cookie authentication scheme generated after a sucessful sign-in.
        /// </summary>
        public static TBuilder RequireOIDC<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
        => builder.RequireAuthorization(new AuthorizeAttribute {
            AuthenticationSchemes = AuthenticationSchemes.OIDCAuthSchemeName
        });

        /// <summary>
        /// Sets the authorization scheme to the Cookie authentication scheme generated after a sucessful sign-in, the cookie contaisn the claims from the signIn method.
        /// </summary>
        public static TBuilder RequireTokenExchangeCookie<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
        => builder.RequireAuthorization(new AuthorizeAttribute {
            AuthenticationSchemes = AuthenticationSchemes.TokenExchangeCookieSchemeName
        });

        /// <summary>
        /// Sets the authorization scheme to require either the TokenExchangeCookieSchemeName or the JWT Bearer authentication scheme, any endpoint with this specification will require a valid JWT token or a valid Token Exchange Cookie to access it.
        /// </summary>
        public static TBuilder RequireTokenExchangeCookieOrJWTBearer<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
        => builder.RequireAuthorization(new AuthorizeAttribute {
            AuthenticationSchemes = string.Join(",",
            [
                AuthenticationSchemes.TokenExchangeCookieSchemeName
                ,JwtBearerDefaults.AuthenticationScheme

            ])
        });
    }
}
