using System.Text.Json.Serialization;
using TGF.CA.Domain.ExternalContracts;

namespace TGF.CA.Infrastructure.DB.PostgreSQL {
    internal record PostgreSQLConnectionSecret : IBasicCredentials {
        [JsonPropertyName("host")]
        public string Host { get; set; } = default!;
        [JsonPropertyName("port")]
        public string Port { get; set; } = default!;
        [JsonPropertyName("databaseName")]
        public string DatabaseName { get; set; } = default!;

        public string Username { get; set; } = default!;
        public string Password { get; set; } = default!;

        public string ToConnectionString(string? databaseNameOverride = null, string? passwordOverride = null)
            => $"Host={Host};Port={Port};Username={Username};Password={passwordOverride ?? Password};Database={databaseNameOverride ?? DatabaseName};";
    }
}
