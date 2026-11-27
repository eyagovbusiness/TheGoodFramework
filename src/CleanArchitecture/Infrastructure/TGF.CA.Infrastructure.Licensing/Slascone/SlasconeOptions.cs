using System.Text.Json.Serialization;

namespace TGF.CA.Infrastructure.Licensing.Slascone;

/// <summary>
/// Configuration options for SLASCONE licensing. 
/// </summary>
public sealed record SlasconeOptions {
    [JsonPropertyName("ApiBaseUrl")]
    public required string ApiBaseUrl { get; init; }
    [JsonPropertyName("IsvId")]
    public required Guid IsvId { get; init; }
    [JsonPropertyName("ProductId")]
    public required Guid ProductId { get; init; }
    [JsonPropertyName("StartupOptions")]
    public StartupOptions StartupOptions { get; init; } = new();

}
/// <summary>
/// Configuration options related to the startup licensing process.
/// </summary>
public sealed record StartupOptions {
    /// <summary>Maximum healthy-loop delay between license refreshes, in seconds.</summary>
    [JsonPropertyName("LicenseRefreshIntervalSeconds")]
    public int LicenseRefreshIntervalSeconds { get; init; } = 3600;
    /// <summary>Initial retry delay for failures and non-extending renewals. Renewal retries never wait past session expiration.</summary>
    [JsonPropertyName("InitialBackoffSeconds")]
    public int InitialBackoffSeconds { get; init; } = 2;
    /// <summary>Maximum exponential retry delay. Renewal retries reset when the session expiration advances.</summary>
    [JsonPropertyName("MaxBackoffSeconds")]
    public int MaxBackoffSeconds { get; init; } = 30;
}
