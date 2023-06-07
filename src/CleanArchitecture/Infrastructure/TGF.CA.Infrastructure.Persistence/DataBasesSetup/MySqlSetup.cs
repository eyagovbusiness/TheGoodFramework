using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Infrastructure.Discovery;
using TGF.CA.Infrastructure.Secrets.Vault;

namespace TGF.CA.Infrastructure.Persistence.DataBasesSetup
{
    internal static class MySqlSetup
    {
        internal static async Task<string> GetConnectionString(IServiceProvider serviceProvider, string databaseName)
        {
            ISecretsManager secretManager = serviceProvider.GetRequiredService<ISecretsManager>();
            IServiceDiscovery serviceDiscovery = serviceProvider.GetRequiredService<IServiceDiscovery>();

            DiscoveryData mysqlData = await serviceDiscovery.GetDiscoveryData(InfraServicesRegistry.MySql);
            MySqlCredentials credentials = await secretManager.Get<MySqlCredentials>("mysql");

            return
                $"Server={mysqlData.Server};Port={mysqlData.Port};Database={databaseName};Uid={credentials.username};Pwd={credentials.password};";
        }

        internal record MySqlCredentials
        {
            public string username { get; init; } = null!;
            public string password { get; init; } = null!;
        }
    }
}