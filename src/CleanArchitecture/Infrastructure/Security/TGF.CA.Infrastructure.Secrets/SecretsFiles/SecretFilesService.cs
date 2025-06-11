using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using TGF.CA.Infrastructure.InvariantConstants;

namespace TGF.CA.Infrastructure.Secrets.SecretsFiles {

    public class SecretFilesService(IConfiguration configuration) : ISecretFilesService {

        #region ISecretFilesService
        public string GetSecretFromConfig(string key) =>
            File.ReadAllText(GetSecretFilePath(key)).Trim();

        public T GetSecretFromConfig<T>(string key)
            where T : class, new() {
            var secretJson = GetSecretFromConfig(key);
            return JsonConvert.DeserializeObject<T>(secretJson)
                ?? throw new NullReferenceException($"[ERROR]: Deserialization of secret '{key}' returned null.");
        }

        public async Task<string> GetSecretFromConfigAsync(string key) =>
            (await File.ReadAllTextAsync(GetSecretFilePath(key))).Trim();

        public async Task<T> GetSecretFromConfigAsync<T>(string key)
            where T : class, new() {
            var secretJson = await GetSecretFromConfigAsync(key);
            return JsonConvert.DeserializeObject<T>(secretJson)
                ?? throw new NullReferenceException($"[ERROR]: Deserialization of secret '{key}' returned null.");
        }

        public async Task<string> GetSecretValueFromKey(string secretFileName, string key) {
            var lines = await File.ReadAllLinesAsync(GetSecretFilePath(secretFileName));
            foreach (var line in lines) {
                var parts = line.Split(':', 2);
                if (parts.Length == 2 && parts[0].Trim() == key)
                    return parts[1].Trim();
            }
            throw new KeyNotFoundException($"Key '{key}' not found in secret file '{secretFileName}'.");
        }

        public async Task<IDictionary<string, string>> GetAllSecretKeyValues(string secretFileName) {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = await File.ReadAllLinesAsync(GetSecretFilePath(secretFileName));
            foreach (var line in lines) {
                var parts = line.Split(':', 2);
                if (parts.Length == 2)
                    dict[parts[0].Trim()] = parts[1].Trim();
            }
            return dict;
        }

        #endregion
        #region Static
        /// <summary>
        /// Retrieves the secret value from a file stored in the <see cref="EnvironmentVariableNames.SECRETS_PATH"/> directory.
        /// </summary>
        /// <param name="secretName">The name of the secret file.</param>
        /// <returns>The secret value as a string.</returns>
        public static async Task<string?> GetSecretFromStaticSECRETS_PATH(string secretName) =>
           (await File.ReadAllTextAsync(GetSecretFilePathStaticSECRETS_PATH(secretName))).Trim();
        #endregion

        #region Private
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
        private string GetSecretFilePath(string configurationKey) {
            var secretsPathEnvVar = configuration[ConfigurationKeys.SecretsFiles.SecretsPathEnvVar]
                ?? throw new InvalidOperationException($"[ERROR]: {ConfigurationKeys.SecretsFiles.SecretsPathEnvVar} is not set in configuration.");

            var secretsPath = Environment.GetEnvironmentVariable(secretsPathEnvVar)
                ?? throw new InvalidOperationException($"[ERROR]: Environment variable '{secretsPathEnvVar}' is not set!");

            var secretName = configuration[configurationKey]
                ?? throw new KeyNotFoundException($"[ERROR]: Secret name key '{configurationKey}' not found in configuration.");

            var secretFilePath = Path.Combine(secretsPath, secretName.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            return !File.Exists(secretFilePath)
                ? throw new FileNotFoundException($"[ERROR]: Secret file '{secretFilePath}' not found.")
                : secretFilePath;
        }

        /// <summary>
        /// Resolves the full path of the secret file, ensuring <see cref="EnvironmentVariableNames.SECRETS_PATH"/> is set.
        /// </summary>
        /// <param name="secretName">The name of the secret file.</param>
        /// <returns>The full path to the secret file.</returns>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="EnvironmentVariableNames.SECRETS_PATH"/> is not set.</exception>
        /// <exception cref="FileNotFoundException">Thrown if the secret file does not exist.</exception>
        private static string GetSecretFilePathStaticSECRETS_PATH(string secretName) {
            var SecretsPath = Environment.GetEnvironmentVariable(EnvironmentVariableNames.SECRETS_PATH)
                ?? throw new InvalidOperationException($"[ERROR]: {EnvironmentVariableNames.SECRETS_PATH} environment variable is not set!");
            var secretFilePath = Path.Combine(SecretsPath, secretName);
            return !File.Exists(secretFilePath)
                ? secretFilePath :
                throw new FileNotFoundException($"[ERROR]: Secret file '{secretFilePath}' not found.");
        }
        #endregion
    }
}
