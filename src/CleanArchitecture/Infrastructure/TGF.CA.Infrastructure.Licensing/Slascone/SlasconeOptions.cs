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
    [JsonPropertyName("InitialBackoffSeconds")]
    public int InitialBackoffSeconds { get; init; } = 2;
    [JsonPropertyName("MaxBackoffSeconds")]
    public int MaxBackoffSeconds { get; init; } = 30;
}

