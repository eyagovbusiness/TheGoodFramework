using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using TGF.CA.Infrastructure.InvariantConstants;

namespace TGF.CA.Infrastructure.Secrets.SecretsFiles {

    public static partial class SecretsFiles {

        /// <summary>
        /// Retrieves the secret string value from a file stored in the secrets file specified in appsettings.
        /// </summary>
        /// <param name="config">Configuration to access the appsettings.</param>
        /// <param name="key">The name of the secret file.</param>
        /// <returns>The secret value as a string.</returns>
        public static string GetSecretFromConfig(IConfiguration config, string key) =>
            File.ReadAllText(GetSecretFilePath(config, key)).Trim();

        /// <summary>
        /// Retrieves the secret <typeparamref name="T"/> value from a file stored in the secrets file specified in appsettings.
        /// </summary>
        /// <param name="config">Configuration to access the appsettings.</param>
        /// <param name="key">The name of the secret file.</param>
        /// <typeparam name="T">The type into which deserialize the secret.</typeparam>
        /// <returns>The secret value string deserialized into <typeparamref name="T"/>.</returns>
        /// <exception cref="NullReferenceException">Thrown if the deserialization of the secret string resulted into a null object.</exception>
        public static T GetSecretFromConfig<T>(IConfiguration config, string key)
            where T : class, new() {
            var secretJson = GetSecretFromConfig(config, key);
            return JsonConvert.DeserializeObject<T>(secretJson)
                ?? throw new NullReferenceException($"[ERROR] Deserialization of secret '{key}' returned null.");
        }

        /// <summary>
        /// Retrieves asynchronously the secret string value from a file stored in the secrets file specified in appsettings.
        /// </summary>
        /// <param name="config">Configuration to access the appsettings.</param>
        /// <param name="key">The name of the secret file.</param>
        /// <returns>The secret value as a string.</returns>
        public static async Task<string> GetSecretFromConfigAsync(IConfiguration config, string key) =>
            (await File.ReadAllTextAsync(GetSecretFilePath(config, key))).Trim();

        /// <summary>
        /// Retrieves asynchronously the secret <typeparamref name="T"/> value from a file stored in the secrets file specified in appsettings.
        /// </summary>
        /// <param name="config">Configuration to access the appsettings.</param>
        /// <param name="key">The name of the secret file.</param>
        /// <typeparam name="T">The type into which deserialize the secret.</typeparam>
        /// <returns>The secret value string deserialized into <typeparamref name="T"/>.</returns>
        /// <exception cref="NullReferenceException">Thrown if the deserialization of the secret string resulted into a null object.</exception>
        public static async Task<T> GetSecretFromConfigAsync<T>(IConfiguration config, string key)
            where T : class, new() {
            var secretJson = await GetSecretFromConfigAsync(config, key);
            return JsonConvert.DeserializeObject<T>(secretJson)
                ?? throw new NullReferenceException($"[ERROR] Deserialization of secret '{key}' returned null.");
        }

        /// <summary>
        /// Resolves the full path of the secret file using the configuration.
        /// </summary>
        /// <param name="config">Configuration to access the appsettings.</param>
        /// <param name="configurationKey">The name of the secret file from <see cref="IConfiguration"/>.</param>
        /// <returns>The full path to the secret file.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the environment variable is not set in configuration or not found.</exception>
        /// <exception cref="KeyNotFoundException">Thrown if the secret key is not found in configuration.</exception>
        /// <exception cref="FileNotFoundException">Thrown if the secret file does not exist.</exception>
        /// <remarks>
        /// Reads from config the name of the env variable with the secrets folder path and then reads the value. 
        /// Finally reads from config the file name of the secret and builds the full path, verify the file xists and return the full path fo the secret.
        /// </remarks>
        private static string GetSecretFilePath(IConfiguration config, string configurationKey) {
            var secretsPathEnvVar = config[ConfigurationKeys.SecretsFiles.SecretsPathEnvVar]
                ?? throw new InvalidOperationException($"[ERROR] {ConfigurationKeys.SecretsFiles.SecretsPathEnvVar} is not set in configuration.");

            var secretsPath = Environment.GetEnvironmentVariable(secretsPathEnvVar)
                ?? throw new InvalidOperationException($"[ERROR] Environment variable '{secretsPathEnvVar}' is not set!");

            var secretName = config[configurationKey]
                ?? throw new KeyNotFoundException($"[ERROR] Secret name key '{configurationKey}' not found in configuration.");

            var secretFilePath = Path.Combine(secretsPath, secretName);

            return !File.Exists(secretFilePath)
                ? throw new FileNotFoundException($"[ERROR] Secret file '{secretFilePath}' not found.")
                : secretFilePath;
        }

    }
}
