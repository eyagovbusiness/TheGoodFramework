using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TGF.CA.Infrastructure.DB.PostgreSQL {

    /// <summary>
    /// Static class to support PostgreSQL within the DI framework.
    /// </summary>
    public static class PostgreSQL_DI {

        /// <summary>
        /// Adds PostgreSQL service with a connection to the specified database and using the given DbContext type. Also includes its own health check.
        /// </summary>
        /// <typeparam name="TDbContext"></typeparam>
        /// <param name="aServiceCollection">Target <see cref="IServiceCollection"/>.</param>
        /// <returns>Updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddPostgreSQL<TDbContext>(
            this IServiceCollection serviceCollection,
            IConfiguration configuration,
            string healthCheckNameOverride = null!)
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
        => serviceCollection
        .AddSingleton<PostgreSQLInterceptor>()
        .AddDbContext<TDbContext>((provider, options) => {
            var interceptor = provider.GetRequiredService<PostgreSQLInterceptor>();
            options.UseNpgsql()
            .AddInterceptors(interceptor);
        })
        .AddPostgresHealthCheck<TDbContext>(configuration, healthCheckNameOverride);

        /// <summary>
        /// Adds a healthcheck in the target <see cref="IServiceCollection"/> for the PostgreSQL database.
        /// </summary>
        /// <returns>Updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddPostgresHealthCheck<TDbContext>(this IServiceCollection serviceCollection, IConfiguration configuration, string healthCheckNameOverride = null!)
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
        => serviceCollection
        .AddHealthChecks()
        .AddCheck<PostgreSQLHealthCheck<TDbContext>>(healthCheckNameOverride ?? PostgreSQLHelpers.GetDatabaseCQRSName<TDbContext>(configuration) + "Database")
        .Services;
    }
}