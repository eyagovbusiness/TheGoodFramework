using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TGF.CA.Infrastructure.Secrets.Vault
{
    /// <summary>
    /// Settings for Vault secrets manager connection for the free version.
    /// </summary>
    public record Settings
    {
        public string? VaultUrl { get; private set; }
        public string? TokenApi { get; init; }

        public void UpdateUrl(string aUrl)
        {
            VaultUrl = aUrl;
        }
    }
}
