using RabbitMQ.Client;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ.Connection;

/// <summary>
/// Provides the single RabbitMQ connection shared by the publishers, the consumers and the health checks.
/// </summary>
/// <remarks>
/// The connection returned here is owned by this factory and MUST NOT be disposed by callers: it is shared
/// process wide. Callers own only the channels (<see cref="IModel"/>) they create from it.
/// <para>
/// The connection is re-established transparently when the cached one is no longer usable (broker restart,
/// network drop, or a caller having disposed it). Caching a dead connection forever was the cause of a
/// permanent readiness outage: every later call handed out the same closed/disposed instance, so retries
/// could never succeed and the service stayed unhealthy until the process was restarted.
/// </para>
/// </remarks>
internal sealed class RabbitMQConnectionFactory : IRabbitMQConnectionFactory, IAsyncDisposable {
    private readonly IRabbitMQConnectionStringProvider _rabbitMQConnectionStringProvider;
    private readonly Func<ConnectionFactory, IConnection> _createConnection;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private ConnectionFactory? _connectionFactory;
    private IConnection? _connection;
    private bool _disposed;

    public RabbitMQConnectionFactory(IRabbitMQConnectionStringProvider rabbitMQConnectionStringProvider)
        : this(rabbitMQConnectionStringProvider, GetSingleConnection) { }

    /// <summary>
    /// Test seam: lets a test substitute connection creation so the reconnection behaviour can be verified
    /// without a live broker. Production code uses the single argument constructor.
    /// </summary>
    internal RabbitMQConnectionFactory(IRabbitMQConnectionStringProvider rabbitMQConnectionStringProvider, Func<ConnectionFactory, IConnection> createConnection) {
        _rabbitMQConnectionStringProvider = rabbitMQConnectionStringProvider;
        _createConnection = createConnection;
    }

    public async Task<IConnection> GetConnectionAsync() {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Fast path: the shared connection is alive, no synchronization needed.
        var lCurrentConnection = _connection;
        if (IsUsable(lCurrentConnection))
            return lCurrentConnection!;

        await _connectionLock.WaitAsync().ConfigureAwait(false);
        try {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Another caller may have reconnected while we were waiting on the lock.
            if (IsUsable(_connection))
                return _connection!;

            DiscardQuietly(_connection);
            _connection = null;

            _connectionFactory ??= await GetConnectionFactory(_rabbitMQConnectionStringProvider).ConfigureAwait(false);
            _connection = _createConnection(_connectionFactory);
            return _connection;
        }
        finally {
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync() {
        if (_disposed)
            return;

        await _connectionLock.WaitAsync().ConfigureAwait(false);
        try {
            if (_disposed)
                return;

            _disposed = true;
            DiscardQuietly(_connection);
            _connection = null;
        }
        finally {
            _connectionLock.Release();
            _connectionLock.Dispose();
        }
    }

    #region
    private static async Task<ConnectionFactory> GetConnectionFactory(IRabbitMQConnectionStringProvider rabbitMQConnectionStringProvider) {
        var formattedUri = (await rabbitMQConnectionStringProvider.GetConnectionString()).Replace("https://", string.Empty);//remove protocol of present in hostname: amqps://{username}:{password}@{hostname}:{port}/{vhost}
        return new ConnectionFactory {
            Uri = new Uri(formattedUri),
            // Let the client transparently rebuild the connection, its channels and its topology when the
            // broker restarts. Explicit so the behaviour does not silently change with a client upgrade.
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
        };
    }

    private static IConnection GetSingleConnection(ConnectionFactory connectionFactory)
    => connectionFactory.CreateConnection();

    /// <summary>
    /// A connection is usable only while it is open. A disposed connection may throw instead of reporting
    /// its state, so that case is treated as "not usable" rather than propagated to the caller.
    /// </summary>
    private static bool IsUsable(IConnection? connection) {
        if (connection is null)
            return false;
        try {
            return connection.IsOpen;
        }
        catch (ObjectDisposedException) {
            return false;
        }
    }

    /// <summary>
    /// Releases a connection we are about to replace. Disposing an already dead connection is expected to
    /// fail, and such a failure must never prevent us from establishing the replacement.
    /// </summary>
    private static void DiscardQuietly(IConnection? connection) {
        if (connection is null)
            return;
        try {
            connection.Dispose();
        }
        catch (Exception) {
            // Intentionally ignored: the connection is being discarded precisely because it is unusable.
        }
    }
    #endregion

}
