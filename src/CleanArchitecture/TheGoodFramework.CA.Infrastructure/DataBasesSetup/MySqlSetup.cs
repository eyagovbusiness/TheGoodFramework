using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Infrastructure.DataBasesDI;

namespace TGF.CA.Infrastructure.DataBasesSetup
{
    /// <summary>
    /// Static class to help setting up a new MySql database.
    /// </summary>
    public static class MySqlSetup
    {
        /// <summary>
        /// Adds a new MySql database to this <see cref="IServiceCollection"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serviceCollection"></param>
        /// <param name="databaseName"></param>
        /// <returns>Updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddMySql<T>(this IServiceCollection aServiceCollection, string aDatabaseName)
            where T : DbContext
        {
            return aServiceCollection
                .AddMySqlDbContext<T>(serviceProvider => GetConnectionString(serviceProvider, aDatabaseName))
                .AddMySqlHealthCheck(serviceProvider => GetConnectionString(serviceProvider, aDatabaseName));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <param name="databaseName"></param>
        /// <returns></returns>
        private static async Task<string> GetConnectionString(IServiceProvider aServiceProvider, string aDatabaseName)
        {
            return null;
            //ISecretManager secretManager = serviceProvider.GetRequiredService<ISecretManager>();
            //IServiceDiscovery serviceDiscovery = serviceProvider.GetRequiredService<IServiceDiscovery>();

            //DiscoveryData mysqlData = await serviceDiscovery.GetDiscoveryData(DiscoveryServices.MySql);
            //MySqlCredentials credentials = await secretManager.Get<MySqlCredentials>("mysql");

            //return
            //    $"Server={mysqlData.Server};Port={mysqlData.Port};Database={aDatabaseName};Uid={credentials.Username};password={credentials.Password};";
        }


        private record MySqlCredentials
        {
            public string Username { get; init; } = null!;
            public string Password { get; init; } = null!;
        }
    }
}