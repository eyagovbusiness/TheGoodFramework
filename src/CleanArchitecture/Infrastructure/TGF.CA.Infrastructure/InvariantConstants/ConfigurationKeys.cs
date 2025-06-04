namespace TGF.CA.Infrastructure.InvariantConstants {
    public readonly struct ConfigurationKeys {
        public readonly struct AppMetadata {
            public const string Key = nameof(AppMetadata);
            public const string AppName = $"{Key}:AppName";
            public const string ServiceName = $"{Key}:ServiceName";

        }
        public readonly struct Logging {
            public const string Key = nameof(Logging);
            public readonly struct LogLevel {
                public const string Key = $"{Logging.Key}:{nameof(LogLevel)}";
                public const string Default = $"{Key}:Default";
                public const string Microsoft = $"{Key}:Microsoft";
                public const string AppNamespace = $"{Key}:AppNamespace";
            }
            public readonly struct Console {
                public const string Key = $"{Logging.Key}:{nameof(Console)}";
                public const string Provider = $"{Key}:Provider";
            }
        }
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