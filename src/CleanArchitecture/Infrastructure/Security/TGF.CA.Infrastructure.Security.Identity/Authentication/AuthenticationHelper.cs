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
        /// <summary>
        /// Replaces the value of the URL parameter "redirect_uri" with the provided string. 
        /// </summary>
        /// <param name="aOriginalUri">Original full Uri.</param>
        /// <param name="aNewRedirectUri">Provided string that will replace the redirect_uri.</param>
        /// <returns></returns>
        public static string ReplaceRedirectUri(this string aOriginalUri, string aNewRedirectUri)
        {
            // Parse the original URL
            UriBuilder lUriBuilder = new UriBuilder(aOriginalUri);

            string lRedirectUriParamName = "redirect_uri=";
            int lRedirectUriStart = aOriginalUri.IndexOf(lRedirectUriParamName);

            int lRedirectUriEnd = aOriginalUri.IndexOf('&', lRedirectUriStart);
            if (lRedirectUriEnd == -1) lRedirectUriEnd = aOriginalUri.Length;

            string lRedirectUriParamNameAndValue = aOriginalUri.Substring(lRedirectUriStart, lRedirectUriEnd - lRedirectUriStart);

            // Update the "redirect_uri" parameter
            lUriBuilder.Query = lUriBuilder.Query.Replace(
                lRedirectUriParamNameAndValue,
                lRedirectUriParamName + Uri.EscapeDataString(aNewRedirectUri)
            );

            // Get the updated URL
            return lUriBuilder.Uri.ToString();
        }
    }
}
