
namespace TGF.CA.Infrastructure.Secrets.SecretsFiles {
    /// <summary>  
    /// Provides methods to interact with secret files, including retrieving key-value pairs,  
    /// fetching secret values, and deserializing secrets into specific types.  
    /// </summary>  
    public interface ISecretFilesService {

        /// <summary>  
        /// Retrieves all key-value pairs from a key-value secret file.  
        /// </summary>  
        /// <param name="secretFileName">The name of the secret file.</param>  
        /// <returns>A dictionary of all key-value pairs.</returns>  
        Task<IDictionary<string, string>> GetAllSecretKeyValues(string secretFileName);

        /// <summary>  
        /// Retrieves the secret string value from a file stored in the secrets file specified in appsettings.  
        /// </summary>  
        /// <param name="key">The name of the secret file.</param>  
        /// <returns>The secret value as a string.</returns>  
        string GetSecretFromConfig(string key);

        /// <summary>  
        /// Retrieves the secret <typeparamref name="T"/> value from a file stored in the secrets file specified in appsettings.  
        /// </summary>  
        /// <param name="key">The name of the secret file.</param>  
        /// <typeparam name="T">The type into which deserialize the secret.</typeparam>  
        /// <returns>The secret value string deserialized into <typeparamref name="T"/>.</returns>  
        /// <exception cref="NullReferenceException">Thrown if the deserialization of the secret string resulted into a null object.</exception>  
        T GetSecretFromConfig<T>(string key) where T : class, new();

        /// <summary>  
        /// Retrieves asynchronously the secret string value from a file stored in the secrets file specified in appsettings.  
        /// </summary>  
        /// <param name="key">The name of the secret file.</param>  
        /// <returns>The secret value as a string.</returns>  
        Task<string> GetSecretFromConfigAsync(string key);

        /// <summary>  
        /// Retrieves asynchronously the secret <typeparamref name="T"/> value from a file stored in the secrets file specified in appsettings.  
        /// </summary>  
        /// <param name="key">The name of the secret file.</param>  
        /// <typeparam name="T">The type into which deserialize the secret.</typeparam>  
        /// <returns>The secret value string deserialized into <typeparamref name="T"/>.</returns>  
        /// <exception cref="NullReferenceException">Thrown if the deserialization of the secret string resulted into a null object.</exception>  
        Task<T> GetSecretFromConfigAsync<T>(string key) where T : class, new();

        /// <summary>  
        /// Retrieves the value for a specific key from a key-value secret file.  
        /// </summary>  
        /// <param name="secretFileName">The name of the secret file.</param>  
        /// <param name="key">The key to look up.</param>  
        /// <returns>The value for the specified key.</returns>  
        Task<string> GetSecretValueFromKey(string secretFileName, string key);
    }
}