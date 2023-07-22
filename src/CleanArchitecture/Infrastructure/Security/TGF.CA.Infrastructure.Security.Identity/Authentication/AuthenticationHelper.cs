using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TGF.CA.Infrastructure.Security.Secrets.Vault;

namespace TGF.CA.Infrastructure.Security.Identity.Authentication
{
    public class DiscordUserAuth
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
    }
    public static class AuthenticationHelper
    {

        public static async Task<string> GetAPISecret(this ISecretsManager aSecretsManager)
        {
            var lAPISecret = await aSecretsManager.GetValueObject("apisecrets", "SecretKey")
                             ?? throw new Exception("Error loading retrieving the APISecret!!");

            return lAPISecret.ToString()!;
        }
        public static async Task<DiscordUserAuth> GetDiscordUserAuth(this ISecretsManager aSecretsManager)
        {
            var lDiscordUserAuth = await aSecretsManager.Get<DiscordUserAuth>("discordauth")
                             ?? throw new Exception("Error loading retrieving the client auth secrets!!");

            return lDiscordUserAuth;
        }
    }
}
