namespace TGF.CA.Infrastructure.InvariantConstants {
    public readonly struct ConfigurationKeys {
        public readonly struct SecretsFiles {
            public const string Key = nameof(SecretsFiles);
            public const string SecretsPathEnvVar = $"{Key}:SecretsPathEnvVar";
            public readonly struct SecretsFileNames {
                public const string Key = $"{SecretsFiles.Key}:{nameof(SecretsFileNames)}";
                public const string PostgresSecrets = $"{Key}:PostgresSecrets";
                public const string RabbitMQConnectionString = $"{Key}:RabbitMQConnectionString";
                public const string CloudStorage = $"{Key}:CloudStorage";

            }
        }
        public readonly struct Database {
            public const string Key = nameof(Database);
            public const string DatabaseName = $"{Key}:DatabaseName";
            public const string AuthType = $"{Key}:AuthType";
            public const string SecretsSourceType = $"{Key}:SecretsSourceType";
        }
        public readonly struct CloudStorage {
            public const string Key = nameof(CloudStorage);
            public const string AuthType = $"{Key}:AuthType";
            public const string SecretsSourceType = $"{Key}:SecretsSourceType";
        }
    }

}