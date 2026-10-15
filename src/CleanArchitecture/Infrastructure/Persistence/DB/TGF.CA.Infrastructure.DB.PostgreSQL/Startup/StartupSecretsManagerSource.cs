using Consul;
using Microsoft.Extensions.Configuration;
using TGF.CA.Application;
using TGF.CA.Infrastructure.Discovery;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;

namespace TGF.CA.Infrastructure.DB.PostgreSQL.Startup;

/// <summary>Fixed typed conversion of an attempt-owned source timeout. Carries no provider text; the only timeout that is retry authority.</summary>
internal sealed class StartupSourceTimeoutException() : Exception("A startup secret source call exceeded its bound.");

/// <summary>
/// Delegating handler owned by ONE startup source call. It links the owned attempt token to the request, and transfers the response BODY under the
/// same token before returning, because the SDKs send without a token and deserialize after headers.
/// </summary>
/// <remarks>It wraps the handler the SDK itself provides (TLS, certificate and proxy behavior are preserved) and never replaces it.</remarks>
internal sealed class StartupOwnedTransportHandler(HttpMessageHandler inner, CancellationToken owned) : DelegatingHandler(inner) {
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(owned, cancellationToken);
        var response = await base.SendAsync(request, linked.Token).ConfigureAwait(false);
        try {
            var original = response.Content;
            var body = await original.ReadAsByteArrayAsync(linked.Token).ConfigureAwait(false);
            var buffered = new ByteArrayContent(body);
            foreach (var header in original.Headers) {
                buffered.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            response.Content = buffered;
            original.Dispose();
            return response;
        }
        catch {
            response.Dispose();
            throw;
        }
    }
}

/// <summary>Runs one startup source phase under a linked owned+timeout token, awaiting it COMPLETELY (no abandonment).</summary>
internal static class StartupSourceCall {
    internal const int DefaultTimeoutSeconds = 15;

    public static async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> operation, TimeSpan timeout, CancellationToken owned) {
        owned.ThrowIfCancellationRequested();
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(owned, timeoutSource.Token);
        try {
            return await operation(linked.Token).ConfigureAwait(false);
        }
        catch (Exception) when (owned.IsCancellationRequested) {
            // Owned cancellation always wins over whatever the SDK surfaced.
            throw new OperationCanceledException(owned);
        }
        catch (Exception exception) when (IsCancellationShaped(exception) && (timeoutSource.IsCancellationRequested || IsTimeoutShaped(exception))) {
            throw new StartupSourceTimeoutException();
        }
    }

    private static bool IsCancellationShaped(Exception exception) {
        for (var depth = 0; exception is not null && depth < 8; depth++) {
            if (exception is OperationCanceledException) {
                return true;
            }

            exception = exception switch {
                AggregateException { InnerExceptions.Count: 1 } aggregate => aggregate.InnerExceptions[0],
                HttpRequestException or IOException => exception.InnerException!,
                _ => null!
            };
        }

        return false;
    }

    // HttpClient's own timeout surfaces as a TaskCanceledException caused by a TimeoutException.
    private static bool IsTimeoutShaped(Exception exception) => exception is TaskCanceledException { InnerException: TimeoutException };
}

/// <summary>
/// Fresh, attempt-owned SecretsManager source: Consul discovery (existing <c>Discovery:Address</c> semantics, first catalog result, default transport
/// security) followed by one KV v2 read through the existing VaultSharp token authentication. No global discovery cache, no cached Vault client, no
/// legacy retry loop and no logging of addresses. Every transport is private to the call and disposed after the SDK task completed.
/// </summary>
internal static class StartupSecretsManagerSource {
    internal const string VaultTokenVariable = "VAULT_TOKEN";
    internal const string DiscoveryAddressKey = "Discovery:Address";
    internal const string SecretMountPoint = "secret";
    internal const string PostgresSecretPath = "postgres";

    public static async Task<(string Host, int Port, string Username, string Password)> ReadAsync(IConfiguration configuration, TimeSpan timeout, CancellationToken owned) {
        var discoveryAddress = configuration[DiscoveryAddressKey];
        var vaultToken = Environment.GetEnvironmentVariable(VaultTokenVariable);
        if (string.IsNullOrWhiteSpace(discoveryAddress) || string.IsNullOrWhiteSpace(vaultToken)) {
            throw new PostgreSQLStartupSourceException(PostgreSQLStartupSourceFailure.MissingConfiguration);
        }

        if (!Uri.TryCreate(discoveryAddress, UriKind.Absolute, out var consulAddress)) {
            throw new PostgreSQLStartupSourceException(PostgreSQLStartupSourceFailure.InvalidConfiguration);
        }

        var (database, vaultAddress) = await StartupSourceCall.RunAsync(ct => DiscoverAsync(consulAddress, ct), timeout, owned).ConfigureAwait(false);
        var credentials = await StartupSourceCall.RunAsync(ct => ReadVaultAsync(vaultAddress, vaultToken, timeout, ct), timeout, owned).ConfigureAwait(false);
        return (database.Server, database.Port, credentials.Username, credentials.Password);
    }

    private static async Task<(DiscoveryData Database, string VaultAddress)> DiscoverAsync(Uri consulAddress, CancellationToken cancellationToken) {
        using var http = new HttpClient(new StartupOwnedTransportHandler(new HttpClientHandler(), cancellationToken), disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
        // The Consul client refuses an HttpClient that does not accept JSON (the library sets it itself only on its own client).
        http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        using var consul = new ConsulClient(new ConsulClientConfiguration { Address = consulAddress }, http);

        var database = First(await consul.Catalog.Service(InfraServicesRegistry.PostgreSQL, cancellationToken).ConfigureAwait(false));
        var vault = First(await consul.Catalog.Service(InfraServicesRegistry.VaultSecretsManager, cancellationToken).ConfigureAwait(false));
        // Same address shape as the existing discovery service: ServiceAddress plus ":port" when a port is registered.
        var vaultAddress = vault.Port != 0 ? $"{vault.Server}:{vault.Port}" : vault.Server;
        return (database, vaultAddress);
    }

    private static DiscoveryData First(QueryResult<CatalogService[]> result) {
        var service = result.Response?.FirstOrDefault();
        return service is null || string.IsNullOrWhiteSpace(service.ServiceAddress)
            ? throw new PostgreSQLStartupSourceException(PostgreSQLStartupSourceFailure.MissingConfiguration)
            : new DiscoveryData(service.ServiceAddress, service.ServicePort);
    }

    private static async Task<(string Username, string Password)> ReadVaultAsync(string vaultAddress, string vaultToken, TimeSpan timeout, CancellationToken cancellationToken) {
        HttpClient? transport = null;
        try {
            var settings = new VaultClientSettings(vaultAddress, new TokenAuthMethodInfo(vaultToken)) {
                VaultServiceTimeout = timeout,
                // The SDK supplies its configured handler (TLS/certificates/proxy); only the token-linking wrapper is ours.
                MyHttpClientProviderFunc = handler => transport = new HttpClient(new StartupOwnedTransportHandler(handler, cancellationToken), disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan }
            };
            var client = new VaultClient(settings);
            var secret = await client.V1.Secrets.KeyValue.V2.ReadSecretAsync(path: PostgresSecretPath, mountPoint: SecretMountPoint).ConfigureAwait(false);
            var data = new Dictionary<string, object?>(secret.Data.Data, StringComparer.OrdinalIgnoreCase);
            return (data.GetValueOrDefault("Username")?.ToString() ?? string.Empty, data.GetValueOrDefault("Password")?.ToString() ?? string.Empty);
        }
        finally {
            transport?.Dispose();
        }
    }
}
