﻿using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using TGF.CA.Application;

namespace TGF.CA.Infrastructure.Security.Identity.Authentication
{
    public static class EndpointConventionBuilderExtensions
    {

        /// <summary>
        /// Configures this endpoint to require the DiscordAuthSchemeName defined in the application layer.
        /// </summary>
        public static TBuilder RequireDiscord<TBuilder>(this TBuilder aBuilder)
            where TBuilder : IEndpointConventionBuilder
        => aBuilder.RequireAuthorization(new AuthorizeAttribute
        {
            AuthenticationSchemes = AuthenticationSchemes.DiscordAuthSchemeName
        });

        public static TBuilder RequireJWTBearer<TBuilder>(this TBuilder aBuilder)
            where TBuilder : IEndpointConventionBuilder
        => aBuilder.RequireAuthorization(new AuthorizeAttribute
        {
            AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme
        });
    }
}
