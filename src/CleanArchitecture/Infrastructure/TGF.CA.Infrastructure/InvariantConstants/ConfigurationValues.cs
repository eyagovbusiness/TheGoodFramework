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
    }
}
