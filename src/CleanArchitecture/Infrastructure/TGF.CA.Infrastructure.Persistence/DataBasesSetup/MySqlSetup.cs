﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Infrastructure.Persistence.DataBasesDI;

namespace TGF.CA.Infrastructure.Persistence.DataBasesSetup
{
    public static class MySqlSetup
    {
        public static IServiceCollection AddMySql<T>(this IServiceCollection serviceCollection, string databaseName)
            where T : DbContext
        {
            return serviceCollection
                .AddMySqlDbContext<T>(serviceProvider => GetConnectionString(serviceProvider, databaseName))
                .AddMySqlHealthCheck(serviceProvider => GetConnectionString(serviceProvider, databaseName));
        }

        private static async Task<string> GetConnectionString(IServiceProvider serviceProvider, string databaseName)
        {
            return "";
            //WIP
            //ISecretManager secretManager = serviceProvider.GetRequiredService<ISecretManager>();
            //IServiceDiscovery serviceDiscovery = serviceProvider.GetRequiredService<IServiceDiscovery>();

            //DiscoveryData mysqlData = await serviceDiscovery.GetDiscoveryData(DiscoveryServices.MySql);
            //MySqlCredentials credentials = await secretManager.Get<MySqlCredentials>("mysql");

            //return
            //    $"Server={mysqlData.Server};Port={mysqlData.Port};Database={databaseName};Uid={credentials.username};password={credentials.password};";
        }


        private record MySqlCredentials
        {
            public string Username { get; init; } = null!;
            public string Password { get; init; } = null!;
        }
    }
}