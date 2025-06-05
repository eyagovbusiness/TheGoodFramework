namespace TGF.CA.Infrastructure.InvariantConstants {
    public struct ConfigurationValues {
        public struct Database {
            public struct Provider {
                public const string Self_hosted = "Self_hosted";
                public const string Azure = "Azure";
                public const string AWS = "AWS";
            }
            public struct AuthType {
                public const string UsernameAndPassword = "UsernameAndPassword";
                public const string ManagedIdentity = "ManagedIdentity";
                public const string IAM_Roles = "IAM_Roles";
            }
        }
        public struct Logging {
            public struct LogLevel {
                public const string Debug = "Debug";
                public const string Information = "Information";
                public const string Warning = "Warning";
                public const string Error = "Error";
            }
            public struct Console {
                public struct Provider {
                    public const string Serilog = "Serilog";
                    public const string OpenTelemetry = "OpenTelemetry";
                }
            }
        }
    }
}
