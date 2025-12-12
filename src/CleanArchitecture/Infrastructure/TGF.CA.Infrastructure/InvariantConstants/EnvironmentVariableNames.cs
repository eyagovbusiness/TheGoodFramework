namespace TGF.CA.Infrastructure.InvariantConstants {
    public struct EnvironmentVariableNames {
        /// <summary>
        /// AVOID USING THIS VARIABLE DIRECTLY UNLESS THERE IS NO OTHER CHOICE, USE <see cref="ConfigurationKeys.SecretsFiles.SecretsPathEnvVar"/> INSTEAD WHENEVER IS POSSIBLE.
        /// </summary>
        public const string SECRETS_PATH = "SECRETS_PATH";
        public const string AZURE_CLIENT_ID = "AZURE_CLIENT_ID";
        public const string SOFTWARE_VERSION = "SOFTWARE_VERSION";

        public const string AWS_REGION = "AWS_REGION";

        public struct Postgres {
            public const string PGHOST = "PGHOST";
            public const string PGPORT = "PGPORT";
            public const string PGUSER = "PGUSER";
            public const string PGPASSWORD = "PGPASSWORD";
            public const string PGDATABASE = "PGDATABASE";
        }

        public struct RabbitMQ {
            public const string RABBITMQ_PROTOCOL = "RABBITMQ_PROTOCOL";
            public const string RABBITMQ_HOSTNAME = "RABBITMQ_HOSTNAME";
            public const string RABBITMQ_USERNAME = "RABBITMQ_USERNAME";
            public const string RABBITMQ_PASSWORD = "RABBITMQ_PASSWORD";
        }
    }
}
