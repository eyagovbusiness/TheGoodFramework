namespace TGF.CA.Infrastructure.InvariantConstants {
    public readonly struct ConfigurationKeys {
        public readonly struct SecretsFiles {
            public const string Key = nameof(SecretsFiles);
            public const string SecretsPathEnvVar = $"{Key}:SecretsPathEnvVar";
            public readonly struct SecretsFileNames {
                public const string Key = $"{SecretsFiles.Key}:{nameof(SecretsFileNames)}";
                public const string PostgresSecrets = $"{Key}:PostgresSecrets";
            }
        }
        public readonly struct Database {
            public const string Key = nameof(Database);
            public const string DatabaseName = "Database:DatabaseName";
            public const string AuthType = "Database:AuthType";
            public const string UseSecretsManagerAndServiceDiscovery = $"{Key}:UseSecretsManagerAndServiceDiscovery";
        }
    }

}