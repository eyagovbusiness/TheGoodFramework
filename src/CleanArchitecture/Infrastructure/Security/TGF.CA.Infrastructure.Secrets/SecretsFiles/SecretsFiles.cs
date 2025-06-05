
using TGF.CA.Infrastructure.InvariantConstants;

namespace TGF.CA.Infrastructure.Secrets.SecretsFiles {

    /// <summary>
    /// Provides methods to retrieve secret values from files stored in a specified directory or configured path.
    /// </summary>
    public static partial class SecretsFiles {

        private static readonly string SecretsPath = Environment.GetEnvironmentVariable(EnvironmentVariableNames.SECRETS_PATH)
            ?? throw new InvalidOperationException($"[ERROR]: {EnvironmentVariableNames.SECRETS_PATH} environment variable is not set!");

        /// <summary>
        /// Retrieves the secret value from a file stored in the <see cref="EnvironmentVariableNames.SECRETS_PATH"/> directory.
        /// </summary>
        /// <param name="secretName">The name of the secret file.</param>
        /// <returns>The secret value as a string.</returns>
        public static string GetSecret(string secretName) =>
            File.ReadAllText(GetSecretFilePath(secretName)).Trim();

        /// <summary>
        /// Retrieves asynchronously the secret value from a file stored in the <see cref="EnvironmentVariableNames.SECRETS_PATH"/> directory.
        /// </summary>
        /// <param name="secretName">The name of the secret file.</param>
        /// <returns>The secret value as a string.</returns>
        public static async Task<string> GetSecretAsync(string secretName) =>
            (await File.ReadAllTextAsync(GetSecretFilePath(secretName))).Trim();

        /// <summary>
        /// Retrieves the value for a specific key from a key-value secret file.
        /// </summary>
        /// <param name="secretFileName">The name of the secret file.</param>
        /// <param name="key">The key to look up.</param>
        /// <returns>The value for the specified key.</returns>
        public static async Task<string> GetSecretValueFromKey(string secretFileName, string key) {
            var lines = await File.ReadAllLinesAsync(GetSecretFilePath(secretFileName));
            foreach (var line in lines) {
                var parts = line.Split(':', 2);
                if (parts.Length == 2 && parts[0].Trim() == key)
                    return parts[1].Trim();
            }
            throw new KeyNotFoundException($"Key '{key}' not found in secret file '{secretFileName}'.");
        }

        /// <summary>
        /// Retrieves all key-value pairs from a key-value secret file.
        /// </summary>
        /// <param name="secretFileName">The name of the secret file.</param>
        /// <returns>A dictionary of all key-value pairs.</returns>
        public static async Task<IDictionary<string, string>> GetAllSecretKeyValues(string secretFileName) {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = await File.ReadAllLinesAsync(GetSecretFilePath(secretFileName));
            foreach (var line in lines) {
                var parts = line.Split(':', 2);
                if (parts.Length == 2)
                    dict[parts[0].Trim()] = parts[1].Trim();
            }
            return dict;
        }

        /// <summary>
        /// Resolves the full path of the secret file, ensuring <see cref="EnvironmentVariableNames.SECRETS_PATH"/> is set.
        /// </summary>
        /// <param name="secretName">The name of the secret file.</param>
        /// <returns>The full path to the secret file.</returns>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="EnvironmentVariableNames.SECRETS_PATH"/> is not set.</exception>
        /// <exception cref="FileNotFoundException">Thrown if the secret file does not exist.</exception>
        private static string GetSecretFilePath(string secretName) {
            var secretFilePath = Path.Combine(SecretsPath, secretName);
            return !File.Exists(secretFilePath)
                ? secretFilePath :
                throw new FileNotFoundException($"[ERROR]: Secret file '{secretFilePath}' not found.");
        }

    }
}
