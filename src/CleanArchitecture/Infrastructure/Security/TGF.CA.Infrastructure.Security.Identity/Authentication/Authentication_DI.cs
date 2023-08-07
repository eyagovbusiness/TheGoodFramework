﻿using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using TGF.CA.Infrastructure.Security.Identity.Authentication;
using TGF.CA.Infrastructure.Security.Secrets.Vault;

namespace TGF.CA.Application.Setup
{
    /// <summary>
    /// Extension class to add custom authentication logic to our <see cref="IServiceCollection"/> from our WebApplicationBuilder
    /// </summary>
    /// <remarks>REQUIERES <see cref="ISecretsManager"/> service registered.</remarks>
    public static class Authentication_DI
    {
        public static async Task AddBasicJWTAuthentication(this IServiceCollection aServiceCollection)
        {
            var lAPISecret = await GetAPISecret(aServiceCollection);
            aServiceCollection.AddAuthentication(authOptions =>
            {
                authOptions.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                authOptions.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                authOptions.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddCustomJwtBearer(lAPISecret);
        }

        public static async Task AddDiscordOAuthPlusJWTAuthentication(this IServiceCollection aServiceCollection)
        {
            var lAPISecret = await GetAPISecret(aServiceCollection);
            var lDiscordUserAuth = await GetDiscordUserAuth(aServiceCollection);

            aServiceCollection.AddAuthentication(authOptions =>
            {
                authOptions.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                authOptions.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                authOptions.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = "PreAuthCookie";
            })
            .AddCustomJwtBearer(lAPISecret)
            .AddOAuth("Discord", 
                options =>
                {
                    options.AuthorizationEndpoint = "https://discord.com/oauth2/authorize";
                    options.Scope.Add("identify");
                    options.CallbackPath = new PathString("/auth/oauthCallback");

                    options.ClientId = lDiscordUserAuth.ClientId;
                    options.ClientSecret = lDiscordUserAuth.ClientSecret;

                    options.TokenEndpoint = "https://discord.com/api/oauth2/token";
                    options.UserInformationEndpoint = "https://discord.com/api/users/@me";

                    options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                    options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");

                    options.AccessDeniedPath = new PathString("/auth/oauthFailed");

                    options.Events = GetMyDiscordOAuthEvents();
                }
            );
        }

        private static async Task<string> GetAPISecret(IServiceCollection aServiceCollection)
            => await aServiceCollection.BuildServiceProvider()
               .GetRequiredService<ISecretsManager>()!
               .GetAPISecret();

        private static async Task<DiscordUserAuth> GetDiscordUserAuth(IServiceCollection aServiceCollection)
            => await aServiceCollection.BuildServiceProvider()
               .GetRequiredService<ISecretsManager>()!
               .GetDiscordUserAuth();

        private static AuthenticationBuilder AddCustomJwtBearer(this AuthenticationBuilder aAuthenticationBuilder, string aAPISecret)
            => aAuthenticationBuilder.AddJwtBearer(jwtBearerOptions =>
            {
                jwtBearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(aAPISecret)),
                    ValidateIssuerSigningKey = true,
                    //ValidIssuer = string.Empty,
                    ValidateIssuer = false,
                    //ValidAudience = string.Empty,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                };
            });

        private static OAuthEvents GetMyDiscordOAuthEvents()
        => new()
        {
            OnCreatingTicket = async context =>
            {
                var lResquest = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                lResquest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                lResquest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

                var lRespone = await context.Backchannel.SendAsync(lResquest, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);
                lRespone.EnsureSuccessStatusCode();

                var lUser = JsonDocument.Parse(await lRespone.Content.ReadAsStringAsync()).RootElement;

                context.RunClaimActions(lUser);
            }
        };

    }
}
