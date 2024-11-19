using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TGF.Common.Extensions;

namespace TGF.CA.Infrastructure
{
    public static class InfrastructureSetupAbstractions
    {
        /// <summary>
        /// Applies all pending migrations to the specified <see cref="DbContext"/> type.
        /// </summary>
        /// <typeparam name="TDbContext">The type of the <see cref="DbContext"/> for which migrations should be applied.</typeparam>
        /// <param name="lWebApplication">The <see cref="WebApplication"/> instance.</param>
        /// <returns>The same <see cref="WebApplication"/> instance after applying the migrations.</returns>
        public static async Task<WebApplication> UseMigrations<TDbContext>(this WebApplication lWebApplication)
            where TDbContext : DbContext
        => await lWebApplication.UseMigrations(typeof(TDbContext));

        /// <summary>
        /// Applies all pending migrations to the specified DbContext types.
        /// </summary>
        /// <param name="lWebApplication">The <see cref="WebApplication"/> instance.</param>
        /// <param name="lDbContextTypes">An array of <see cref="DbContext"/> types for which migrations should be applied.</param>
        /// <returns>The same <see cref="WebApplication"/> instance after applying the migrations.</returns>
        public static async Task<WebApplication> UseMigrations(this WebApplication lWebApplication, params Type[] lDbContextTypes)
        {
            using (var lScope = lWebApplication.Services.CreateScope())
            {
                foreach (var lDbContextType in lDbContextTypes)
                    await WebApplicationMigration(lScope.ServiceProvider, lDbContextType);
            }
            return lWebApplication;
        }

        /// <summary>
        /// Applies all pending migrations for the given <see cref="DbContext"/> type.
        /// </summary>
        /// <param name="lServiceProvider">The <see cref="IServiceProvider"/> instance.</param>
        /// <param name="lDbContextType">The type of the <see cref="DbContext"/> for which migrations should be applied.</param>
        private static async Task WebApplicationMigration(IServiceProvider lServiceProvider, Type lDbContextType) {
            if (!typeof(DbContext).IsAssignableFrom(lDbContextType))
                throw new ArgumentException($"Type '{lDbContextType.FullName}' is not a DbContext type.");

            var lDbContext = lServiceProvider.GetService(lDbContextType) as DbContext
                             ?? throw new InvalidOperationException($"No service for type '{lDbContextType.FullName}' has been registered.");

            await RetryUtility.ExecuteWithRetryAsync(
                async () => {
                    await lDbContext.Database.MigrateAsync();
                    return true; // Return true if no exception occurs.
                },
                _ => false, // Always return false so it never retries based on result.
                aMaxRetries: 10, // Customize max retries as needed.
                aDelayMilliseconds: 2000, // Customize delay between retries.
                CancellationToken.None // Pass a CancellationToken if applicable.
            );
        }
    }
}