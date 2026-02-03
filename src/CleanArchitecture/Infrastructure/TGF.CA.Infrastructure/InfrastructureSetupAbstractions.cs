using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TGF.Common.Extensions;

namespace TGF.CA.Infrastructure {
    public static class InfrastructureSetupAbstractions {

        /// <summary>
        /// Applies all pending migrations to the specified <see cref="DbContext"/> type.
        /// </summary>
        /// <typeparam name="TDbContext">The type of the <see cref="DbContext"/> for which migrations should be applied.</typeparam>
        /// <param name="webApplication">The <see cref="WebApplication"/> instance.</param>
        /// <param name="runSeederFunction">An optional function to run after applying migrations, typically used for seeding the database.</param></param>
        /// <returns>The same <see cref="WebApplication"/> instance after applying the migrations.</returns>
        public static async Task<WebApplication> UseMigrations<TDbContext>(this WebApplication webApplication, Func<IServiceProvider, Task>? runSeederFunction = default)
            where TDbContext : DbContext
        => await webApplication.UseMigrations(runSeederFunction, typeof(TDbContext));

        /// <summary>
        /// Applies all pending migrations to the specified DbContext types.
        /// </summary>
        /// <param name="webApplication">The <see cref="WebApplication"/> instance.</param>
        /// <param name="dbContextTypes">An array of <see cref="DbContext"/> types for which migrations should be applied.</param>
        /// <param name="runSeederFunction">An optional function to run after applying migrations, typically used for seeding the database.</param></param>
        /// <returns>The same <see cref="WebApplication"/> instance after applying the migrations.</returns>
        public static async Task<WebApplication> UseMigrations(this WebApplication webApplication, Func<IServiceProvider, Task>? runSeederFunction = default, params Type[] dbContextTypes) {
            using (var scope = webApplication.Services.CreateScope()) {
                foreach (var dbContextType in dbContextTypes)
                    await WebApplicationMigration(scope.ServiceProvider, dbContextType, runSeederFunction);
            }
            return webApplication;
        }

        /// <summary>
        /// Applies all pending migrations for the given <see cref="DbContext"/> type.
        /// </summary>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/> instance.</param>
        /// <param name="dbContextType">The type of the <see cref="DbContext"/> for which migrations should be applied.</param>
        /// <param name="runSeederFunction">An optional function to run after applying migrations, typically used for seeding the database.</param></param>
        private static async Task WebApplicationMigration(IServiceProvider serviceProvider, Type dbContextType, Func<IServiceProvider, Task>? runSeederFunction = default) {
            if (!typeof(DbContext).IsAssignableFrom(dbContextType))
                throw new ArgumentException($"Type '{dbContextType.FullName}' is not a DbContext type.");

            var dbContext = serviceProvider.GetService(dbContextType) as DbContext
                             ?? throw new InvalidOperationException($"No service for type '{dbContextType.FullName}' has been registered.");

            await serviceProvider.GetRequiredService<IRetryUtility>().ExecuteWithRetryAsync(
                async () => {
                    await EnsureInterceptorConnectionString(dbContext);
                    await dbContext.Database.MigrateAsync();//does not trigger interceptors(even tho it should)
                    if (runSeederFunction is not null)
                        await runSeederFunction(serviceProvider);
                    return true; // Return true if no exception occurs.
                },
                _ => false, // Always return false so it never retries based on result.
                aMaxRetries: 10, // Customize max retries as needed.
                aDelayMilliseconds: 500, // Customize delay between retries.
                CancellationToken.None // Pass a CancellationToken if applicable.
            );
        }

        /// <summary>
        /// Ensures the interceptor connection string is set for the specified <see cref="DbContext"/>.
        /// </summary>
        /// <param name="dbContext"></param>
        /// <returns></returns>
        private static async Task EnsureInterceptorConnectionString(DbContext dbContext) {
            await dbContext.Database.OpenConnectionAsync();
            await dbContext.Database.CloseConnectionAsync();
        }
    }
}