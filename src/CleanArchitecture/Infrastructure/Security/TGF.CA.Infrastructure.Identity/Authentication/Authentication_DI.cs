using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using TGF.CA.Infrastructure.Security.Identity.Authentication;

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
                options.Cookie.SameSite = SameSiteMode.Lax;
            })
            .AddCustomJwtBearer(lAPISecret)
            .AddOAuth(AuthenticationSchemes.DiscordAuthSchemeName,
                options =>
                {
                    options.AuthorizationEndpoint = "https://discord.com/oauth2/authorize";
                    options.Scope.Add("identify");
                    options.CallbackPath = new PathString("/auth/OAuthCallback");

                    options.ClientId = lDiscordUserAuth.ClientId!;
                    options.ClientSecret = lDiscordUserAuth.ClientSecret!;

                    options.TokenEndpoint = "https://discord.com/api/oauth2/token";
                    options.UserInformationEndpoint = "https://discord.com/api/users/@me";

                    options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                    options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
                    options.ClaimActions.MapJsonKey(ClaimTypes.GivenName, "global_name");

                    options.AccessDeniedPath = new PathString("/auth/OAuthFailed");

                    options.Events = GetMyDiscordOAuthEvents();
                }
            );
        }


        #region Private

        private static async Task<string> GetAPISecret(IServiceCollection aServiceCollection)
            => await aServiceCollection.BuildServiceProvider()
               .GetRequiredService<ISecretsManager>()!
               .GetTokenSecret(DefaultTokenNames.AccessToken);

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
                var lRequest = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                lRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                lRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

                var lResponse = await context.Backchannel.SendAsync(lRequest, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);
                lResponse.EnsureSuccessStatusCode();

                var lUser = JsonDocument.Parse(await lResponse.Content.ReadAsStringAsync()).RootElement;

                context.RunClaimActions(lUser);
            }
        };

        #endregion

    }
}
