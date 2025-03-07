using Azure.Core;
using Azure.Identity;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data.Common;
using TGF.CA.Infrastructure.InvariantConstants;
using TGF.CA.Infrastructure.Secrets.SecretsFiles;

namespace TGF.CA.Infrastructure.DB.PostgreSQL.Azure {
    /// <summary>
    /// Interceptor for PostgreSQL connections that uses Azure's Managed Identity for authentication.
    /// </summary>
    /// <remarks>
    /// Intended to be registered as a singelton service in the DI container. 
    /// The interceptor will replace the connection string with a new one that includes the access token.
    /// The interceptor will cache the access token and only refresh it when it is about to expire.
    /// </remarks>
    internal class ManagedIdentitiyPostgreSQLInterceptor(IConfiguration configuration)
    : DbConnectionInterceptor {

        private readonly string _managedIdentityClientId = Environment.GetEnvironmentVariable(EnvironmentVariableNames.AZURE_CLIENT_ID)
                ?? throw new NullReferenceException($"[ERROR] The {EnvironmentVariableNames.AZURE_CLIENT_ID} environment variable is not set!");
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        private AccessToken _cachedAccessToken;

        public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (connection is NpgsqlConnection npgsqlConnection)
                npgsqlConnection.ConnectionString = await GetValidNpgsqlConnectionStringAsync(cancellationToken);
            await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
        }

        private async Task<string> GetValidNpgsqlConnectionStringAsync(CancellationToken cancellationToken = default) {
            var accessToken = await GetValidAccessTokenAsync(cancellationToken);
            var postgresSecrets = await SecretsFiles.GetSecretFromConfigAsync<PostgreSQLConnectionSecret>(configuration, ConfigurationKeys.SecretsFiles.SecretsFileNames.PostgresSecrets);
            return postgresSecrets.ToConnectionString(accessToken) + "Pooling=true;MinPoolSize=0;MaxPoolSize=50;";//for now we will keep the pooling configuration here, if flexibility is neede din the future it wll be added to IConfiguration
        }

        private async Task<string> GetValidAccessTokenAsync(CancellationToken cancellationToken) {
            var now = DateTimeOffset.UtcNow.AddMinutes(2);
            bool evaluateExpirationFunc() => !string.IsNullOrEmpty(_cachedAccessToken.Token) && now < _cachedAccessToken.ExpiresOn;

            if (evaluateExpirationFunc())
                return _cachedAccessToken.Token;

            // Lock to prevent concurrent refresh
            await _semaphore.WaitAsync(cancellationToken);
            try {
                // Check again inside lock to avoid duplicate refresh
                if (evaluateExpirationFunc())
                    return _cachedAccessToken.Token;

                // Request a new token
                var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions {
                    ManagedIdentityClientId = _managedIdentityClientId
                });

                var tokenRequestContext = new TokenRequestContext(["https://ossrdbms-aad.database.windows.net/.default"]);
                var tokenResult = await credential.GetTokenAsync(tokenRequestContext, cancellationToken);

                _cachedAccessToken = tokenResult;

                return _cachedAccessToken.Token;
            }
            finally {
                _semaphore.Release();
            }
        }
    }
}
