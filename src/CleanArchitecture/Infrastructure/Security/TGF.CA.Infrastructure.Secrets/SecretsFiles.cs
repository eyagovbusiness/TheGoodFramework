
namespace TGF.CA.Infrastructure.Secrets {
    public static class SecretsFiles {

        /// <summary>
        /// Retrieves the secret value from a file stored in the SECRETS_PATH directory.
        /// </summary>
        /// <param name="secretName">The name of the secret file.</param>
        /// <returns>The secret value as a string.</returns>
        public static string GetSecret(string secretName) =>
            File.ReadAllText(GetSecretFilePath(secretName)).Trim();

        /// <summary>
        /// Retrieves asynchronously the secret value from a file stored in the SECRETS_PATH directory.
        /// </summary>
        /// <param name="secretName">The name of the secret file.</param>
        /// <returns>The secret value as a string.</returns>
        public static async Task<string> GetSecretAsync(string secretName) =>
            (await File.ReadAllTextAsync(GetSecretFilePath(secretName))).Trim();

        /// <summary>
        /// Resolves the full path of the secret file, ensuring SECRETS_PATH is set.
        /// </summary>
        /// <param name="secretName">The name of the secret file.</param>
        /// <returns>The full path to the secret file.</returns>
        /// <exception cref="InvalidOperationException">Thrown if SECRETS_PATH is not set.</exception>
        /// <exception cref="FileNotFoundException">Thrown if the secret file does not exist.</exception>
        private static string GetSecretFilePath(string secretName) {
            var secretsPath = Environment.GetEnvironmentVariable(InfrastructureInvariantConstants.SECRETS_PATH)
                ?? throw new InvalidOperationException($"[ERROR] {InfrastructureInvariantConstants.SECRETS_PATH} environment variable is not set!");

            var secretFilePath = Path.Combine(secretsPath, secretName);

            if (!File.Exists(secretFilePath))
                throw new FileNotFoundException($"[ERROR] Secret file '{secretFilePath}' not found.");

            return secretFilePath;
        }
    }
}
