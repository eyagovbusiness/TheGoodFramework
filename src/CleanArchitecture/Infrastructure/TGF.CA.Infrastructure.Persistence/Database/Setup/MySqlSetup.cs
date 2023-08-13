using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Infrastructure.Discovery;
using TGF.CA.Infrastructure.Security.Secrets.Vault;

namespace TGF.CA.Infrastructure.Persistence.Database.Setup
{
    internal static class MySqlSetup
    {

        /// <summary>
        /// Adds a healthcheck in the target <see cref="IHealthChecksBuilder"/> for the MySql database resolved from the IConfiguration and the provided database name.
        /// </summary>
        /// <param name="aHealthChecksBuilder">Target <see cref="IHealthChecksBuilder"/>.</param>
        /// <param name="aDatabaseName">strign with MySql database name.</param>
        /// <returns>Updated <see cref="IHealthChecksBuilder"/>.</returns>
        internal static async Task<IHealthChecksBuilder> AddMySqlHealthCheck(this IHealthChecksBuilder aHealthChecksBuilder, string aDatabaseName)
        {
            ServiceProvider lServiceProvider = aHealthChecksBuilder.Services.BuildServiceProvider();
            string lMySqlConnectionString = await GetConnectionString(lServiceProvider, aDatabaseName);
            aHealthChecksBuilder.AddMySql(lMySqlConnectionString, "Database");
            return aHealthChecksBuilder;
        }

        /// <summary>
        /// Gets the MySql connection string from the configured MySql database for this application.
        /// </summary>
        /// <param name="aServiceProvider">Service provider.</param>
        /// <param name="aDatabaseName">Database name.</param>
        /// <returns>The MySql connection string.</returns>
        internal static async Task<string> GetConnectionString(IServiceProvider aServiceProvider, string aDatabaseName)
        {
            var lMySqlSecrets = await GetMySqlSecrets(aServiceProvider);
            var lMySqlDiscoveryData = await GetMySqlDiscoveryData(aServiceProvider);
            return
                $"Server={lMySqlDiscoveryData.Server};Port={lMySqlDiscoveryData.Port};Database={aDatabaseName};Uid={lMySqlSecrets.username};Pwd={lMySqlSecrets.password};";
        }

        #region private

        private static async Task<DiscoveryData> GetMySqlDiscoveryData(IServiceProvider aServiceProvider)
            => await aServiceProvider
                .GetRequiredService<IServiceDiscovery>()!
                .GetDiscoveryData(InfraServicesRegistry.MySql)
                 ?? throw new Exception("Error loading retrieving the MySql discovery data!!");

        private static async Task<MySqlSecrets> GetMySqlSecrets(IServiceProvider aServiceProvider)
            => await aServiceProvider
               .GetRequiredService<ISecretsManager>()!
               .Get<MySqlSecrets>("mysql")
                ?? throw new Exception("Error loading retrieving the MySql secrets!!");

        private class MySqlSecrets
        {
            public string username { get; set; }
            public string password { get; set; }
        }

        #endregion

    }
}