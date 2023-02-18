namespace TheGoodFramework.Model
{
    public enum ConnectionProviderEnum
    {
        SqlServer,
        Oracle,
        MySql,
        PostgreSql,
        OracleDevart
    }

    /// <summary>
    /// Defines connection to database
    /// </summary>
    public class ConnectionConfig
    {
        /// <summary>
        /// IP address or host name
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// Communication port number
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Name of database or service (Oracle SID)
        /// </summary>
        public string Catalog { get; set; }

        /// <summary>
        /// Login name of user 
        /// </summary>
        public string User { get; set; }

        /// <summary>
        /// Password for given user
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Type of database provider
        /// </summary>
        public ConnectionProviderEnum Provider { get; set; }

        /// <summary>
        /// Redundant connection - failover server
        /// </summary>
        public ConnectionConfig Redundant { get; set; }

        /// <summary>
        /// Maximum timeout for each command. If not set, default is used.
        /// </summary>
        public int? CommandTimeout { get; set; }

        public override string ToString()
        {
            return String.Format("Host:{0}; Port:{1}; Catalog:{2}; User:{3}; Password:{4}; Provider:{5}", this.Host, this.Port, this.Catalog, this.User, this.Password, this.Provider);
        }
    }

}
