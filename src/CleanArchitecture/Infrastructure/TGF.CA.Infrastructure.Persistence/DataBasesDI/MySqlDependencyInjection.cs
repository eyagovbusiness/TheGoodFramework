using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace TGF.CA.Infrastructure.Persistence.DataBasesDI
{
    /// <summary>
    /// Static lass to support MySql within the DI framework.
    /// </summary>
    public static class MySqlDependencyInjection
    {

        /// <summary>
        /// Adds new <see cref="DbContext"/> to the context service provider.
        /// </summary>
        /// <typeparam name="T"><see cref="DbContext"/> to be added to the DI framework.</typeparam>
        /// <param name="aServiceCollection">Target <see cref="IServiceCollection"/>.</param>
        /// <param name="aConnectionStringFunc">Function to resolve the connection strign with MySql database.</param>
        /// <returns>Updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddMySqlDbContext<T>(this IServiceCollection aServiceCollection,
            Func<IServiceProvider, Task<string>> aConnectionStringFunc)
            where T : DbContext
        {
            return aServiceCollection.AddDbContext<T>((serviceProvider, builder) =>
            {
                builder.UseMySQL(aConnectionStringFunc.Invoke(serviceProvider).Result);
            });
        }

        /// <summary>
        /// Adds a healthcheck in the target <see cref="IServiceCollection"/> for the MySql database resolved from the aConnectionStringFunc.
        /// </summary>
        /// <param name="aServiceCollection">Target <see cref="IServiceCollection"/>.</param>
        /// <param name="aConnectionStringFunc">Function to resolve the connection strign with MySql database.</param>
        /// <returns>Updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddMySqlHealthCheck(this IServiceCollection aServiceCollection,
            Func<IServiceProvider, Task<string>> aConnectionStringFunc)
        {
            ServiceProvider lServiceProvider = aServiceCollection.BuildServiceProvider();
            string lMySqlConnectionString = aConnectionStringFunc.Invoke(lServiceProvider).Result;
            aServiceCollection.AddHealthChecks().AddMySql(lMySqlConnectionString);
            return aServiceCollection;
        }

    }
}
