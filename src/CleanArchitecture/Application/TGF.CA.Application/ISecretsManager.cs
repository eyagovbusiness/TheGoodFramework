using TGF.CA.Domain.ExternalContracts;

namespace TGF.CA.Application {
    public interface ISecretsManager
    {
        /// <summary>
        /// Retrieves a secret from a specified path in the secrets manager.
        /// </summary>
        /// <typeparam name="T">Type of the object the secret should be deserialized into.</typeparam>
        /// <param name="aPath">Path where the secret is located.</param>
        /// <returns>Deserialized object of type <typeparamref name="T"/>.</returns>
        public Task<T> Get<T>(string aPath) where T : new();

        /// <summary>
        /// Gets an object representing the value of the key:value pair from a given secrets path.
        /// </summary>
        /// <param name="aPath">Path of the secret.</param>
        /// <param name="aKey">Key to retrieve from the secret.</param>
        /// <returns>The value object for the given key.</returns>
        public Task<object> GetValueObject(string aPath, string aKey);

        /// <summary>
        /// Retrieves RabbitMQ credentials for a specific role.
        /// </summary>
        /// <param name="aRoleName">Name of the role.</param>
        /// <returns>Credentials for the specified role.</returns>
        public Task<IBasicCredentials> GetRabbitMQCredentials(string aRoleName);

        /// <summary>
        /// Retrieves the secret token for a specified token name.
        /// </summary>
        /// <param name="aTokenName">The name of the token whose secret should be retrieved.</param>
        /// <returns>A task representing the asynchronous operation, with the result being the secret token string for the specified token name.</returns>
        public Task<string> GetTokenSecret(string aTokenName);

        /// <summary>
        /// Retrieves the service key for a specified service name.
        /// </summary>
        /// <param name="aServiceName">The name of the service for which the key should be retrieved.</param>
        /// <returns>A task representing the asynchronous operation, with the result being the service key string for the spec
        public Task<string> GetServiceKey(string aServiceName);

        /// <summary>
        /// Checks the health status of the secret management service.
        /// </summary>
        /// <returns>A task representing the asynchronous operation, with the result indicating the health status (true for healthy, false otherwise).</returns>
        public Task<bool> GetIsHealthy();
    }
}