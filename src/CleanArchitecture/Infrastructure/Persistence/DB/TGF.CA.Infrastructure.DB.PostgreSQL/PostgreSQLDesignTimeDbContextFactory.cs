using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace TGF.CA.Infrastructure.DB.PostgreSQL
{
    /// <summary>
    /// Provides an abstract factory for creating instances of DbContext during design time for PostgreSQL.
    /// This is used primarily for Entity Framework migrations and other design-time operations.
    /// </summary>
    /// <typeparam name="TDbContext">Type of DbContext to instantiate.</typeparam>
    /// <remarks>
    /// WARNING: This factory should ONLY be used in a development environment for design-time operations.
    /// Production or Staging connection strings will never be used for design-time operations. 
    /// </remarks>
    public abstract class PostgreSQLDesignTimeDbContextFactory<TDbContext> : IDesignTimeDbContextFactory<TDbContext>
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        /// <summary>
        /// Provides the schema name for the PostgreSQL database.
        /// Derived classes can override this to specify a different schema name.
        /// </summary>
        /// <returns>The name of the schema. Defaults to "public".</returns>
        protected virtual string GetSchemaName() => "public";

        /// <summary>
        /// Creates a new instance of the DbContext.
        /// </summary>
        /// <param name="args">Arguments provided at design time.</param>
        /// <returns>A new instance of <typeparamref name="TDbContext"/>.</returns>
        public TDbContext CreateDbContext(string[] args)
        {
            IConfigurationRoot lConfiguration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.Design.json")
                .Build();

            var lOptionsBuilder = new DbContextOptionsBuilder<TDbContext>();
            var lConnectionString = lConfiguration.GetConnectionString("PostgreSQLDevelopmentOnlyConnection");

            lOptionsBuilder.UseNpgsql(lConnectionString, options => options.MigrationsHistoryTable("__EFMigrationsHistory", GetSchemaName()));

            return (Activator.CreateInstance(typeof(TDbContext), new object[] { lOptionsBuilder.Options }) as TDbContext)!;
        }


    }

}
