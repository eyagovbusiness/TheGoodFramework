using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace TGF.CA.Infrastructure.DB.MySQL
{
    /// <summary>
    /// Provides a factory for creating instances of DbContext during design time.
    /// This is used primarily for Entity Framework migrations and other design-time operations.
    /// </summary>
    /// <typeparam name="TDbContext">Type of DbContext to instantiate.</typeparam>
    /// <remarks>
    /// WARNING: This factory should ONLY be used in a development environment for design-time operations.
    /// Production or Staging connection strings will never be used for design-time operations. 
    /// </remarks>
    public abstract class MySQLDesignTimeDbContextFactory<TDbContext> : IDesignTimeDbContextFactory<TDbContext>
        where TDbContext : DbContext
    {

        /// <summary>
        /// Abstract method that derived classes must implement to provide the MySQL version
        /// </summary>
        /// <returns>The <see cref="MySqlServerVersion"/> used for the development database.</returns>
        protected abstract MySqlServerVersion GetServerVersion();

        /// <summary>
        /// Creates a new instance of <see cref="TDbContext"/>.
        /// </summary>
        /// <param name="args">Arguments provided at design time.</param>
        /// <returns>A new instance of <typeparamref name="TDbContext"/>.</returns>
        public TDbContext CreateDbContext(string[] args)
        {
            IConfigurationRoot lConfiguration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.Development.json")
                .Build();

            var lOptionsBuilder = new DbContextOptionsBuilder<TDbContext>();
            var lConnectionString = lConfiguration.GetConnectionString("MySQLDevelopmentOnlyConnection");
            var lServerVersion = GetServerVersion();

            lOptionsBuilder.UseMySql(lConnectionString, lServerVersion);

            return (Activator.CreateInstance(typeof(TDbContext), new object[] { lOptionsBuilder.Options }) as TDbContext)!;
        }
    }

}
