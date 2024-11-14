using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Application;
using TGF.CA.Domain.ExternalContracts;
using TGF.CA.Infrastructure.Discovery;

namespace TGF.CA.Infrastructure.DB.PostgreSQL {
    internal static class PostgreSQLHelpers
    {

        /// <summary>
        /// Gets the PostgreSQL connection string from the configured PostgreSQL database for this application.
        /// </summary>
        /// <param name="aServiceProvider">Service provider.</param>
        /// <param name="aDatabaseName">Database name.</param>
        /// <returns>The PostgreSQL connection string.</returns>
        internal static async Task<string> GetConnectionString(IServiceProvider aServiceProvider, string aDatabaseName)
        {
            var lPostgreSQLSecrets = await GetPostgreSQLSecrets(aServiceProvider);
            var lPostgreSQLDiscoveryData = await GetPostgreSQLDiscoveryData(aServiceProvider);
            return $"Host={lPostgreSQLDiscoveryData.Server};Port={lPostgreSQLDiscoveryData.Port};Username={lPostgreSQLSecrets.Username};Password={lPostgreSQLSecrets.Password};Database={aDatabaseName};";

        }

        #region private

        private static async Task<DiscoveryData> GetPostgreSQLDiscoveryData(IServiceProvider aServiceProvider)
            => await aServiceProvider
                .GetRequiredService<IServiceDiscovery>()!
                .GetDiscoveryData(InfraServicesRegistry.PostgreSQL)
                 ?? throw new Exception("Error loading retrieving the PostgreSQL discovery data!!");

        private static async Task<PostgreSQLSecrets> GetPostgreSQLSecrets(IServiceProvider aServiceProvider)
            => await aServiceProvider
               .GetRequiredService<ISecretsManager>()!
               .Get<PostgreSQLSecrets>("postgres")
                ?? throw new Exception("Error loading retrieving the PostgreSQL secrets!!");

        private record PostgreSQLSecrets : IBasicCredentials
        {
            public string Username { get; set; } = default!;
            public string Password { get; set; } = default!;
        }

        #endregion

    }
}