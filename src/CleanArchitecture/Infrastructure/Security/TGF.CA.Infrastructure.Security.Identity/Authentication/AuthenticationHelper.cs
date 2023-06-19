using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TGF.CA.Infrastructure.Security.Identity.Authentication;
using TGF.CA.Infrastructure.Security.Secrets.Vault;

namespace TGF.CA.Application.Setup
{
    /// <summary>
    /// 
    /// </summary>
    public static class Authentication_DI
    {
        public static void AddAuthentication(this IServiceCollection aServiceCollection)
        {
            ServiceProvider lServiceProvider = aServiceCollection.BuildServiceProvider();
            aServiceCollection.AddAuthentication(authOptions =>
            {
                authOptions.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                authOptions.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                authOptions.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(jwtBearerOptions =>
            {
                jwtBearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetRabbitMqSecretCredentials(lServiceProvider).Result.SecretKey!)),
                    ValidateIssuerSigningKey = true,
                    //ValidIssuer = string.Empty,
                    ValidateIssuer = false,
                    //ValidAudience = string.Empty,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                };
            });
        }

        private static async Task<APISecrets> GetRabbitMqSecretCredentials(IServiceProvider serviceProvider)
        {
            var secretManager = serviceProvider.GetService<ISecretsManager>();
            return await secretManager!.Get<APISecrets>("apisecrets");
        }
    }
}
