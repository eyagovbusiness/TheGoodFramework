using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Application;
using TGF.CA.Domain.External;
using TGF.CA.Infrastructure.Discovery;

namespace TGF.CA.Infrastructure.DB.MySQL
{
    internal static class MySQLHelpers
    {

        /// <summary>
        /// Adds a healthcheck in the target <see cref="IHealthChecksBuilder"/> for the MySql database resolved from the IConfiguration and the provided database name.
        /// </summary>
        /// <param name="aHealthChecksBuilder">Target <see cref="IHealthChecksBuilder"/>.</param>
        /// <param name="aDatabaseName">strign with MySql database name.</param>
        /// <returns>Updated <see cref="IHealthChecksBuilder"/>.</returns>
        //internal static async Task<IHealthChecksBuilder> AddMySqlHealthCheck(this IHealthChecksBuilder aHealthChecksBuilder, string aDatabaseName)
        //{
        //    ServiceProvider lServiceProvider = aHealthChecksBuilder.Services.BuildServiceProvider();
        //    string lMySqlConnectionString = await GetConnectionString(lServiceProvider, aDatabaseName);
        //    aHealthChecksBuilder.AddMySql(lMySqlConnectionString/*, "Database"*/);
        //    return aHealthChecksBuilder;
        //}

        /// <summary>
        /// Gets the MySql connection string from the configured MySql database for this application.
        /// </summary>
        /// <param name="aServiceProvider">Service provider.</param>
        /// <param name="aDatabaseName">Database name.</param>
        /// <returns>The MySql connection string.</returns>
        internal static async Task<string> GetConnectionString(IServiceProvider aServiceProvider, string aDatabaseName)
        {
            var lMySqlSecrets = await GetMySQLSecrets(aServiceProvider);
            var lMySqlDiscoveryData = await GetMySQLDiscoveryData(aServiceProvider);
            return
                $"Server={lMySqlDiscoveryData.Server};Port={lMySqlDiscoveryData.Port};Database={aDatabaseName};Uid={lMySqlSecrets.Username};Pwd={lMySqlSecrets.Password};";
        }

        #region private

        private static async Task<DiscoveryData> GetMySQLDiscoveryData(IServiceProvider aServiceProvider)
            => await aServiceProvider
                .GetRequiredService<IServiceDiscovery>()!
                .GetDiscoveryData(InfraServicesRegistry.MySQL)
                 ?? throw new Exception("Error loading retrieving the MySQL discovery data!!");

        private static async Task<MySQLSecrets> GetMySQLSecrets(IServiceProvider aServiceProvider)
            => await aServiceProvider
               .GetRequiredService<ISecretsManager>()!
               .Get<MySQLSecrets>("mysql")
                ?? throw new Exception("Error loading retrieving the MySQL secrets!!");

        private record MySQLSecrets : IBasicCredentials
        {
            public string? Username { get; set; }
            public string? Password { get; set; }
        }

        #endregion

    }
}