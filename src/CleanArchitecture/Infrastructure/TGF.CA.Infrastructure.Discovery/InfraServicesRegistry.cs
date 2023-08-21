namespace TGF.CA.Infrastructure.Discovery
{
    /// <summary>
    /// Static class with the different infrastructure services registered in the ServiceRegistry.
    /// The string values must match the Name under which each service was registered. 
    /// </summary>
    public static class InfraServicesRegistry
    {
        public const string RabbitMQMessageBroker = "RabbitMQ";
        public const string VaultSecretsManager = "VaultSecretsManager";
        public const string MySQL = "MySQL";
        public const string PostgreSQL = "PostgreSQL";
    }
}
