namespace TGF.CA.Infrastructure.Comm.RabbitMQ.Connection;
/// <summary>
/// Provides the RabbitMQ connection string from the secrets file specified in the configuration.
/// </summary>
internal interface IRabbitMQConnectionStringProvider {
    /// <summary>
    /// Get the connection string for RabbitMQ, each specific implementation will use a different source, for example from the secrets manager or from the secrets file specified in the configuration.
    /// </summary>
    /// <returns>The connection string to the RabbitMQ instance.</returns>
    /// <exception cref="NotSupportedException">Thrown when the secrets source type is missconfigured in appsettings.</exception>
    Task<string> GetConnectionString();
}

