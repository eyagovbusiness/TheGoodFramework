using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace TGF.Model.Model
{
    public class MySqlDbContext : DbContext
    {
        protected readonly IConfiguration Configuration;

        public MySqlDbContext(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        /// <summary>
        /// Configures the connection to Db using MySql Database from appsettings connection string.
        /// </summary>
        /// <param name="aOptionsBuilder"></param>
        protected override void OnConfiguring(DbContextOptionsBuilder aOptionsBuilder)
        {
            //Connect to mysql with connection string from app settings
            var lConnectionString = Configuration.GetConnectionString("WebApiDatabase");
            aOptionsBuilder.UseMySQL(lConnectionString);
        }

    }
}