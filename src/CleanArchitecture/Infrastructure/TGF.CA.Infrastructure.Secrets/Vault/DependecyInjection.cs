﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TGF.CA.Infrastructure.Discovery;
using TGF.CA.Infrastructure.Secrets.Vault;

namespace TGF.CA.Infrastructure.Secrets
{
    public static class DependecyInjection
    {
        /// <summary>
        /// Adds Vault service to the service collection for secrets managemnet.
        /// The aDiscoveredUrl is optional as it will be calculated on the setup project, but only if that project is in use.
        /// </summary>
        public static void AddVaultSecretsManager(this IServiceCollection aServiceCollection, IConfiguration aConfiguration, string? aDiscoveredUrl = null)
        {
            aServiceCollection.Configure<Settings>(aConfiguration.GetSection("VaultSecrets"));
            aServiceCollection.PostConfigure<Settings>(settings =>
            {
                if (!string.IsNullOrWhiteSpace(aDiscoveredUrl))
                    settings.UpdateUrl(aDiscoveredUrl);
            });
            aServiceCollection.AddSingleton<ISecretManager, SecretsManager>();

        }

        public static void ConfigureDiscoveredSecretsManager(this ISecretManager aScretManager, IServiceDiscovery aServiceDiscovery)
        {
            string lDiscoveredUrl = GetVaultUrl(aServiceDiscovery).Result;//TO-DO:TEMPORARY SOLUTION, blocks thread(on startup is not so big problem, but not good)
            aScretManager.UpdateUrl(lDiscoveredUrl);
        }

        private static async Task<string> GetVaultUrl(IServiceDiscovery aServiceDiscovery)
            => await aServiceDiscovery?.GetFullAddress(InfraServicesRegistry.VaultSecretsManager)!;

    }
}