using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Net.Sockets;
using TGF.CA.Infrastructure.InvariantConstants;
using TGF.CA.Infrastructure.Secrets;
using TGF.CA.Infrastructure.Secrets.SecretsFiles;
using Consul;
using System.Security.Authentication;
using VaultSharp.Core;

namespace TGF.CA.Infrastructure.DB.PostgreSQL.Startup;

/// <summary>Fixed reasons why startup connection settings could not be resolved. Carries no provider text.</summary>
public enum PostgreSQLStartupSourceFailure {
    /// <summary>A required configuration key, environment variable or secret file/field is missing.</summary>
    MissingConfiguration,
    /// <summary>A value exists but cannot be used (unparsable port, unknown source type, malformed secret).</summary>
    InvalidConfiguration,
    /// <summary>The configured source or authentication mode is not supported by the startup path.</summary>
    Unsupported,
    /// <summary>A remote source (secrets manager, discovery, token endpoint) could not be reached: a typed transport failure.</summary>
    SecretSourceUnavailable,
    /// <summary>A remote source answered but refused/failed the request (credentials, permissions, unusable identity).</summary>
    SecretSourceRejected
}

/// <summary>
/// The only failure the startup resolver reports. It deliberately has no inner exception, no provider message and no data: the original exception is
/// inspected in memory at the conversion point and dropped.
/// </summary>
public sealed class PostgreSQLStartupSourceException(PostgreSQLStartupSourceFailure failure) : Exception("The PostgreSQL startup connection settings could not be resolved.") {
    public PostgreSQLStartupSourceFailure Failure { get; } = failure;

    /// <summary>True only for a typed transport failure of a remote source: the single resolver failure an outage policy may retry.</summary>
    public bool IsTransportFailure => Failure == PostgreSQLStartupSourceFailure.SecretSourceUnavailable;
}

/// <summary>Resolved startup connection settings. Holds a secret: it never prints it.</summary>
public sealed class PostgreSQLStartupTarget {
    internal PostgreSQLStartupTarget(NpgsqlConnectionStringBuilder builder) => Builder = builder;

    /// <summary>Startup-only connection settings: pooling off, multiplexing off, bounded connect (15 s) and command (30 s) timeouts.</summary>
    public NpgsqlConnectionStringBuilder Builder { get; }

    public override string ToString() => nameof(PostgreSQLStartupTarget);
}

/// <summary>
/// Explicit, FRESH and cancellable startup resolver of the PostgreSQL connection identity. It reuses the existing source semantics
/// (File: PostgresSecrets file incl. its DbName; EnvVariable: PGHOST/PGPORT/PGUSER/PGPASSWORD/PGDATABASE; SecretsManager: discovered host/port,
/// Vault credentials and configured DatabaseName; Azure ManagedIdentity: the File secret with a workload-identity access token as password,
/// resolved BEFORE the connection is opened) without the cached singleton lazy, the default retry loop or the pooled interceptor path.
/// </summary>
/// <remarks>Every call re-reads its source; a failed attempt can never poison a later one. Source failures are converted to <see cref="PostgreSQLStartupSourceException"/>.</remarks>
public sealed class PostgreSQLStartupConnectionResolver {
    /// <summary>Existing audience used by the workload-identity interceptor.</summary>
    internal const string AzureDatabaseTokenScope = "https://ossrdbms-aad.database.windows.net/.default";

    internal const int ConnectionTimeoutSeconds = 15;
    internal const int CommandTimeoutSeconds = 30;

    private readonly IConfiguration _configuration;
    private readonly ISecretFilesService _secretFiles;
    private readonly TimeSpan _sourceTimeout;
    private readonly Func<CancellationToken, Task<string>> _azureAccessToken;

    /// <param name="sourceTimeout">Bound of each remote source phase (discovery, Vault read, workload token). Defaults to 15 s.</param>
    public PostgreSQLStartupConnectionResolver(
        IConfiguration configuration,
        ISecretFilesService secretFiles,
        Func<CancellationToken, Task<string>>? azureAccessTokenProvider = null,
        TimeSpan? sourceTimeout = null) {
        _configuration = configuration;
        _secretFiles = secretFiles;
        _sourceTimeout = sourceTimeout ?? TimeSpan.FromSeconds(StartupSourceCall.DefaultTimeoutSeconds);
        _azureAccessToken = azureAccessTokenProvider ?? GetAzureAccessTokenAsync;
    }
    /// <summary>Resolves the target for ONE attempt. Cancellation is never converted: it escapes as <see cref="OperationCanceledException"/>.</summary>
    public async Task<PostgreSQLStartupTarget> ResolveAsync(bool azureManagedIdentity, CancellationToken cancellationToken) {
        try {
            var sourceText = _configuration[ConfigurationKeys.Database.SecretsSourceType];
            if (string.IsNullOrWhiteSpace(sourceText)) {
                throw Failure(PostgreSQLStartupSourceFailure.MissingConfiguration);
            }

            if (!Enum.TryParse<SecretsSourceTypeEnum>(sourceText, ignoreCase: false, out var source) || !Enum.IsDefined(source)) {
                throw Failure(PostgreSQLStartupSourceFailure.Unsupported);
            }

            var builder = azureManagedIdentity
                ? await ResolveAzureAsync(source, cancellationToken)
                : source switch {
                    SecretsSourceTypeEnum.File => await ResolveFileAsync(cancellationToken),
                    SecretsSourceTypeEnum.EnvVariable => ResolveEnvironment(),
                    SecretsSourceTypeEnum.SecretsManager => await ResolveSecretsManagerAsync(cancellationToken),
                    _ => throw Failure(PostgreSQLStartupSourceFailure.Unsupported)
                };

            cancellationToken.ThrowIfCancellationRequested();
            return new PostgreSQLStartupTarget(builder);
        }
        catch (PostgreSQLStartupSourceException) {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch (Exception exception) {
            // Only the documented REMOTE owned transports (Consul/Vault, the Azure token call) reach this point with a raw exception; the local file read
            // is converted at its own boundary. Inspected in memory only, replaced by a fixed typed failure without inner exception.
            throw Failure(Classify(exception));
        }
    }

    private async Task<NpgsqlConnectionStringBuilder> ResolveFileAsync(CancellationToken cancellationToken) {
        var secret = await ReadFileSecretAsync(cancellationToken);
        return Build(secret.Host, secret.Port, secret.Username, secret.Password, secret.DbName);
    }

    private async Task<NpgsqlConnectionStringBuilder> ResolveAzureAsync(SecretsSourceTypeEnum source, CancellationToken cancellationToken) {
        // Existing identity semantics: workload identity authenticates against the settings of the PostgresSecrets file.
        if (source != SecretsSourceTypeEnum.File) {
            throw Failure(PostgreSQLStartupSourceFailure.Unsupported);
        }

        var secret = await ReadFileSecretAsync(cancellationToken);
        var accessToken = await StartupSourceCall.RunAsync(_azureAccessToken, _sourceTimeout, cancellationToken);
        return Build(secret.Host, secret.Port, secret.Username, accessToken, secret.DbName);
    }

    /// <summary>
    /// The LOCAL secret-file read/parse (File source and the file identity of the Azure path). Any failure here, including a bare <see cref="IOException"/>,
    /// is a terminal fixed source-configuration failure: a local file is not a remote transport and never an outage (design D7).
    /// </summary>
    private async Task<PostgreSQLConnectionSecret> ReadFileSecretAsync(CancellationToken cancellationToken) {
        try {
            return await _secretFiles.GetSecretFromConfigAsync<PostgreSQLConnectionSecret>(ConfigurationKeys.SecretsFiles.SecretsFileNames.PostgresSecrets, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch (Exception exception) {
            throw Failure(ClassifyLocal(exception));
        }
    }

    /// <summary>Local (file) failures only: never <see cref="PostgreSQLStartupSourceFailure.SecretSourceUnavailable"/> or any retryable reason.</summary>
    internal static PostgreSQLStartupSourceFailure ClassifyLocal(Exception exception)
        => exception switch {
            FileNotFoundException or DirectoryNotFoundException or KeyNotFoundException => PostgreSQLStartupSourceFailure.MissingConfiguration,
            InvalidOperationException when exception.GetType() == typeof(InvalidOperationException) && exception.InnerException is null => PostgreSQLStartupSourceFailure.MissingConfiguration,
            _ => PostgreSQLStartupSourceFailure.InvalidConfiguration
        };

    private static NpgsqlConnectionStringBuilder ResolveEnvironment() {
        static string Required(string name) {
            var value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrWhiteSpace(value) ? throw Failure(PostgreSQLStartupSourceFailure.MissingConfiguration) : value;
        }

        return Build(Required(EnvironmentVariableNames.Postgres.PGHOST), Required(EnvironmentVariableNames.Postgres.PGPORT), Required(EnvironmentVariableNames.Postgres.PGUSER),
            Required(EnvironmentVariableNames.Postgres.PGPASSWORD), Required(EnvironmentVariableNames.Postgres.PGDATABASE));
    }

    private async Task<NpgsqlConnectionStringBuilder> ResolveSecretsManagerAsync(CancellationToken cancellationToken) {
        var databaseName = _configuration[ConfigurationKeys.Database.DatabaseName];
        var (host, port, username, password) = await StartupSecretsManagerSource.ReadAsync(_configuration, _sourceTimeout, cancellationToken);
        return Build(host, port.ToString(System.Globalization.CultureInfo.InvariantCulture), username, password, databaseName);
    }

    private static NpgsqlConnectionStringBuilder Build(string? host, string? port, string? username, string? password, string? database) {
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(port) || string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(database)) {
            throw Failure(PostgreSQLStartupSourceFailure.MissingConfiguration);
        }

        if (!int.TryParse(port, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var portNumber) || portNumber is < 1 or > 65535) {
            throw Failure(PostgreSQLStartupSourceFailure.InvalidConfiguration);
        }

        try {
            return new NpgsqlConnectionStringBuilder {
                Host = host,
                Port = portNumber,
                Username = username,
                Password = password,
                Database = database,
                // One dedicated physical session: never pooled, never multiplexed, bounded connect and command time. Overall retry time is owned elsewhere.
                Pooling = false,
                Multiplexing = false,
                Timeout = ConnectionTimeoutSeconds,
                CommandTimeout = CommandTimeoutSeconds
            };
        }
        catch (ArgumentException) {
            throw Failure(PostgreSQLStartupSourceFailure.InvalidConfiguration);
        }
    }

    private static PostgreSQLStartupSourceException Failure(PostgreSQLStartupSourceFailure failure) => new(failure);

    /// <summary>
    /// Provenance-restricted conversion of a source exception (design D7/ARCH04). The chain is walked outer to inner to a bounded depth over RECOGNIZED
    /// wrappers only; the first terminal evidence wins (TLS/credential, a status-bearing SDK exception, configuration/parse), a status alone is never
    /// retry authority, and only a typed transport leaf (socket/IO) or the fixed typed source timeout is an outage. Owned cancellation never reaches here
    /// (it propagates), a non-owned cancellation is terminal, and any unknown wrapper ends the walk terminally.
    /// </summary>
    internal static PostgreSQLStartupSourceFailure Classify(Exception exception) {
        var current = exception;
        for (var depth = 0; current is not null && depth < 8; depth++) {
            switch (current) {
                case StartupSourceTimeoutException:
                    return PostgreSQLStartupSourceFailure.SecretSourceUnavailable;
                case AuthenticationException or CredentialUnavailableException or AuthenticationFailedException:
                    return PostgreSQLStartupSourceFailure.SecretSourceRejected;
                case VaultApiException or ConsulRequestException:
                    return PostgreSQLStartupSourceFailure.SecretSourceRejected;
                case FileNotFoundException or DirectoryNotFoundException or KeyNotFoundException:
                    return PostgreSQLStartupSourceFailure.MissingConfiguration;
                case System.Text.Json.JsonException or Newtonsoft.Json.JsonException or NullReferenceException or FormatException or ArgumentException:
                    return PostgreSQLStartupSourceFailure.InvalidConfiguration;
                case OperationCanceledException:
                    return PostgreSQLStartupSourceFailure.InvalidConfiguration;
                case SocketException:
                    return PostgreSQLStartupSourceFailure.SecretSourceUnavailable;
                case IOException { InnerException: null }:
                    return PostgreSQLStartupSourceFailure.SecretSourceUnavailable;
                case InvalidOperationException when current.GetType() == typeof(InvalidOperationException) && current.InnerException is null:
                    // Existing source code reports a missing environment variable / configuration value this way.
                    return PostgreSQLStartupSourceFailure.MissingConfiguration;
                case AggregateException { InnerExceptions.Count: 1 } aggregate:
                    current = aggregate.InnerExceptions[0];
                    continue;
                case HttpRequestException or IOException:
                    current = current.InnerException!;
                    continue;
                default:
                    return PostgreSQLStartupSourceFailure.InvalidConfiguration;
            }
        }

        // A recognized wrapper chain that ended without transport evidence (for example a bare status-only HttpRequestException) is not an outage.
        return PostgreSQLStartupSourceFailure.SecretSourceRejected;
    }

    private static async Task<string> GetAzureAccessTokenAsync(CancellationToken cancellationToken) {
        var token = await new DefaultAzureCredential().GetTokenAsync(new TokenRequestContext([AzureDatabaseTokenScope]), cancellationToken);
        return token.Token;
    }
}