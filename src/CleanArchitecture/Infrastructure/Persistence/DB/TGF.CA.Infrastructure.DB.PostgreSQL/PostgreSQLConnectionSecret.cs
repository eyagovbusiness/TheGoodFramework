using TGF.CA.Domain.ExternalContracts;

namespace TGF.CA.Infrastructure.DB.PostgreSQL {
    internal record PostgreSQLConnectionSecret : IBasicCredentials {
        public string Host { get; set; } = default!;
        public string Port { get; set; } = default!;
        public string DbName { get; set; } = default!;
        public string Username { get; set; } = default!;
        public string Password { get; set; } = default!;

        public string ToConnectionString(string? databaseNameOverride = null, string? passwordOverride = null)
            => $"Host={Host};Port={Port};Username={Username};Password={passwordOverride ?? Password};Database={databaseNameOverride ?? DbName};";
    }
}
