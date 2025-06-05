namespace TGF.CA.Infrastructure.InvariantConstants {
    public struct EnvironmentVariableNames {

        public const string SECRETS_PATH = "SECRETS_PATH";
        public const string AZURE_CLIENT_ID = "AZURE_CLIENT_ID";

        public struct Postgres {
            public const string PGHOST = "PGHOST";
            public const string PGPORT = "PGPORT";
            public const string PGUSER = "PGUSER";
            public const string PGPASSWORD = "PGPASSWORD";
            public const string PGDATABASE = "PGDATABASE";
        }

        public struct RabbitMQ {
            public const string RABBITMQ_HOSTNAME = "RABBITMQ_HOSTNAME";
            public const string RABBITMQ_USERNAME = "RABBITMQ_USERNAME";
            public const string RABBITMQ_PASSWORD = "RABBITMQ_PASSWORD";
        }
    }
}
