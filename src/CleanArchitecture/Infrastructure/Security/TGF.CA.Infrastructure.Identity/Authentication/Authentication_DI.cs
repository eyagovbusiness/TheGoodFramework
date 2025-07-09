using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using TGF.CA.Application;
using TGF.CA.Application.Contracts.Routing;
using TGF.CA.Application.InvariantConstants;

namespace TGF.CA.Infrastructure.Identity.Authentication {
    /// <summary>
    /// Extension class to add custom authentication logic to our <see cref="IServiceCollection"/> from our WebApplicationBuilder
    /// </summary>
    /// <remarks>REQUIERES <see cref="ISecretsManager"/> service registered.</remarks>
    public static class Authentication_DI {
        public static async Task AddBasicJWTAuthentication(this IServiceCollection aServiceCollection) {
            var lAPISecret = await GetAPISecret(aServiceCollection);
            aServiceCollection.AddAuthentication(authOptions => {
                authOptions.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                authOptions.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                authOptions.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddCustomJwtBearer(lAPISecret);
        }

        public static async Task AddDiscordOAuthPlusJWTAuthentication(this IServiceCollection aServiceCollection, IConfiguration aConfiguration, IWebHostEnvironment aEnvironment) {
            var lAPISecret = await GetAPISecret(aServiceCollection);
            var lDiscordUserAuth = await GetDiscordUserAuth(aServiceCollection);

            aServiceCollection.AddAuthentication(authOptions => {
                authOptions.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                authOptions.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                authOptions.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options => {
                options.Cookie.Name = "PreAuthCookie";
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.Domain = aConfiguration.GetValue<string>("CookieDomain");
            })
            .AddCustomJwtBearer(lAPISecret)
            .AddOAuth(AuthenticationSchemes.DiscordAuthSchemeName,
                options => {
                    options.AuthorizationEndpoint = "https://discord.com/oauth2/authorize";
                    options.Scope.Add("identify");
                    options.CallbackPath = new PathString(TGFEndpointRoutes.auth_OAuthCallback);

                    options.ClientId = lDiscordUserAuth.ClientId!;
                    options.ClientSecret = lDiscordUserAuth.ClientSecret!;

                    options.TokenEndpoint = "https://discord.com/api/oauth2/token";
                    options.UserInformationEndpoint = "https://discord.com/api/users/@me";

                    options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                    options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
                    options.ClaimActions.MapJsonKey(ClaimTypes.GivenName, "global_name");

                    options.AccessDeniedPath = new PathString(TGFEndpointRoutes.auth_OAuthFailed);

                    options.Events = GetMyDiscordOAuthEvents();
                }
            );
        }

        /// <summary>
        /// Configures OIDC authentication for the web application using OpenID Connect and Cookie authentication schemes.
        /// </summary>
        /// <param name="aWebApplicationBuilder"></param>
        /// <param name="onTokenValidatedHandler">Function to handle the OnTokenValidated event.</param>
        /// <remarks>
        /// Cookie scheme used for pre-authentication and OIDC login session
        /// JWT scheme used for API access after token issuance.
        /// IMPORTANT: The order in which the authentication schemes are added matters, when an endpoint is protected by multiple authentication schemes, the first one that matches will be used. Or when a request comes with multiple authentication schemes, the first one that matches will be used.
        /// </remarks>
        public static void ConfigureOIDCPlusJWTAuthentication(this WebApplicationBuilder aWebApplicationBuilder, Func<Microsoft.AspNetCore.Authentication.OpenIdConnect.TokenValidatedContext, Task> onTokenValidatedHandler) {
            var configuration = aWebApplicationBuilder.Configuration;
            var issuer = configuration.GetValue<string>(ConfigurationKeys.FrontendURL.Key) ?? Environment.GetEnvironmentVariable(EnvVariablesNames.FRONTEND_URL)
                ?? throw new Exception("Error while configuring JWT aauthentication, FrontendURL which is used fro token issuer and audience was not found in appsettings. Please add this configuration.");
            aWebApplicationBuilder.Services.AddAuthentication(options => {
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = AuthenticationSchemes.OIDCAuthSchemeName; // Use OIDC to challenge for authentication
                options.DefaultSignInScheme = AuthenticationSchemes.TokenExchangeCookieSchemeName;
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options => {
                options.TokenValidationParameters = new TokenValidationParameters {
                    ValidIssuer = issuer,
                    ValidAudience = issuer,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable(EnvVariablesNames.API_SECRET_KEY))
                    )
                };
            })
            .AddCookie(AuthenticationSchemes.RefreshTokenCookieSchemeName,
                options => ConfigureSecureCookie(options, aWebApplicationBuilder.Configuration, AuthenticationSchemes.RefreshTokenCookieSchemeName)
            )
            .AddCookie(AuthenticationSchemes.TokenExchangeCookieSchemeName,
                options => ConfigureSecureCookie(options, aWebApplicationBuilder.Configuration, AuthenticationSchemes.TokenExchangeCookieSchemeName)
            )
            .AddOpenIdConnect(AuthenticationSchemes.OIDCAuthSchemeName, options => {
                options.Authority = $"https://login.microsoftonline.com/{Environment.GetEnvironmentVariable(EnvVariablesNames.OIDC_AUTH_TENANT_ID)}/v2.0";
                options.ClientId = Environment.GetEnvironmentVariable(EnvVariablesNames.OIDC_AUTH_CLIENT_ID);
                options.ClientSecret = Environment.GetEnvironmentVariable(EnvVariablesNames.OIDC_AUTH_SECRET);

                options.ResponseType = "code";
                options.SaveTokens = true;
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.CallbackPath = configuration[ConfigurationKeys.Auth.MicrosoftAuthCallbackURI];

                options.TokenValidationParameters = new TokenValidationParameters {
                    NameClaimType = "name",
                    RoleClaimType = "roles"
                };

                options.Events = new OpenIdConnectEvents {
                    OnTokenValidated = async context => await onTokenValidatedHandler(context)
                };
            });
        }

        #region Private

        private static void ConfigureSecureCookie(CookieAuthenticationOptions options, IConfiguration configuration, string cookieAuthenticationSchemeName) {
            options.Cookie.Name = cookieAuthenticationSchemeName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Domain = configuration.GetValue<string>($"{cookieAuthenticationSchemeName}Domain");
            // For API endpoints, we override the default cookie authentication behavior to return 401 Unauthorized
            // instead of redirecting to a login page when authentication fails. This is because APIs should not
            // perform redirects (which are intended for browser flows), and clients expect a 401 status code to
            // indicate missing or invalid credentials. Returning 401 also aligns with RESTful standards and avoids
            // leaking endpoint existence via 404s or redirects.
            options.Events.OnRedirectToLogin = context => {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
        }

        private static async Task<string> GetAPISecret(IServiceCollection aServiceCollection)
            => await aServiceCollection.BuildServiceProvider()
               .GetRequiredService<ISecretsManager>()!
               .GetTokenSecret(DefaultTokenNames.AccessToken);

        private static async Task<DiscordUserAuth> GetDiscordUserAuth(IServiceCollection aServiceCollection)
            => await aServiceCollection.BuildServiceProvider()
               .GetRequiredService<ISecretsManager>()!
               .GetDiscordUserAuth();

        private static AuthenticationBuilder AddCustomJwtBearer(this AuthenticationBuilder aAuthenticationBuilder, string aAPISecret)
        => aAuthenticationBuilder.AddJwtBearer(jwtBearerOptions => {
            jwtBearerOptions.TokenValidationParameters = new TokenValidationParameters {
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
        => new() {
            OnCreatingTicket = async context => {
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
