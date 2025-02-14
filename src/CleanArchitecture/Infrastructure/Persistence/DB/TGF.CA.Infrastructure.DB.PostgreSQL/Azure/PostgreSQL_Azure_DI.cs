using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TGF.CA.Infrastructure.DB.PostgreSQL.Azure {

    /// <summary>
    /// Static class to support PostgreSQL within the DI framework.
    /// </summary>
    public static class PostgreSQL_Azure_DI {

        /// <summary>
        /// Adds PostgreSQL service with a connection to the specified database, using the given DbContext type and authentication method.
        /// Includes its own health check.
        /// </summary>
        /// <typeparam name="TDbContext">The DbContext type to be registered.</typeparam>
        /// <param name="serviceCollection">The target <see cref="IServiceCollection"/>.</param>
        /// <returns>Updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddAzurePostgreSQL<TDbContext>(this IServiceCollection serviceCollection, IConfiguration configuration)
            where TDbContext : Microsoft.EntityFrameworkCore.DbContext {

            var serviceProvider = serviceCollection.BuildServiceProvider();
            return serviceCollection
                .AddSingleton<ManagedIdentitiyPostgreSQLInterceptor>()
                .AddDbContext<TDbContext>((provider, options) => {
                    var interceptor = provider.GetRequiredService<ManagedIdentitiyPostgreSQLInterceptor>();
                    options.UseNpgsql()
                    .AddInterceptors(interceptor);
                })
                .AddPostgresHealthCheckFromConnectionString<TDbContext>(configuration);

        }

        /// <summary>
        /// Adds a healthcheck in the target <see cref="IServiceCollection"/> for the PostgreSQL database.
        /// </summary>
        /// <returns>Updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddPostgresHealthCheckFromConnectionString<TDbContext>(this IServiceCollection serviceCollection, IConfiguration configuration)
             where TDbContext : Microsoft.EntityFrameworkCore.DbContext
        => serviceCollection
            .AddHealthChecks()
            .AddCheck<PostgreSQLAzureHealthCheck>(PostgreSQLHelpers.GetDatabaseCQRSName<TDbContext>(configuration) + "Database")
            .Services;

    }
}
