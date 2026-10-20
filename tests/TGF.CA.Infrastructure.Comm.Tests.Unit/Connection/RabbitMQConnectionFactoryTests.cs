using Moq;
using RabbitMQ.Client;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Connection;

namespace TGF.CA.Infrastructure.Comm.Tests.Unit.Connection;

/// <summary>
/// Regression coverage for SF-2975: a dead RabbitMQ connection used to be cached for the lifetime of the
/// process, so every later caller received a closed or disposed connection and the service never recovered.
/// </summary>
public class RabbitMQConnectionFactoryTests {
    private const string ConnectionString = "amqp://user:password@rabbit-host:5672/";

    [Fact]
    public async Task GetConnectionAsync_ReturnsTheSameConnection_WhileItStaysOpen() {
        var connection = CreateConnection(isOpen: true);
        var created = 0;
        var sut = CreateSut(_ => { created++; return connection.Object; });

        var first = await sut.GetConnectionAsync();
        var second = await sut.GetConnectionAsync();

        Assert.Same(first, second);
        Assert.Equal(1, created);
    }

    [Fact]
    public async Task GetConnectionAsync_ReplacesTheConnection_WhenTheCachedOneIsClosed() {
        var dead = CreateConnection(isOpen: false);
        var alive = CreateConnection(isOpen: true);
        var queue = new Queue<IConnection>([dead.Object, alive.Object]);
        var sut = CreateSut(_ => queue.Dequeue());

        var first = await sut.GetConnectionAsync();
        var second = await sut.GetConnectionAsync();

        Assert.Same(dead.Object, first);
        Assert.Same(alive.Object, second);
        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task GetConnectionAsync_ReplacesTheConnection_WhenTheCachedOneWasDisposed() {
        // A caller disposing the shared connection is exactly what broke production. Even then the factory
        // must hand out a working connection rather than the disposed instance.
        var disposed = new Mock<IConnection>();
        disposed.SetupGet(connection => connection.IsOpen).Throws(new ObjectDisposedException(nameof(IConnection)));
        var alive = CreateConnection(isOpen: true);
        var queue = new Queue<IConnection>([disposed.Object, alive.Object]);
        var sut = CreateSut(_ => queue.Dequeue());

        await sut.GetConnectionAsync();
        var second = await sut.GetConnectionAsync();

        Assert.Same(alive.Object, second);
    }

    [Fact]
    public async Task GetConnectionAsync_DoesNotLetAFailingDisposeOfTheDeadConnectionEscape() {
        var dead = new Mock<IConnection>();
        dead.SetupGet(connection => connection.IsOpen).Returns(false);
        dead.Setup(connection => connection.Dispose()).Throws(new InvalidOperationException("already gone"));
        var alive = CreateConnection(isOpen: true);
        var queue = new Queue<IConnection>([dead.Object, alive.Object]);
        var sut = CreateSut(_ => queue.Dequeue());

        await sut.GetConnectionAsync();
        var second = await sut.GetConnectionAsync();

        Assert.Same(alive.Object, second);
    }

    [Fact]
    public async Task GetConnectionAsync_CreatesASingleConnection_WhenCalledConcurrently() {
        var connection = CreateConnection(isOpen: true);
        var created = 0;
        var sut = CreateSut(_ => {
            Interlocked.Increment(ref created);
            return connection.Object;
        });

        var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(async () => await sut.GetConnectionAsync())));

        Assert.Equal(1, created);
        Assert.All(results, result => Assert.Same(connection.Object, result));
    }

    [Fact]
    public async Task DisposeAsync_ClosesTheOwnedConnection() {
        var connection = CreateConnection(isOpen: true);
        var sut = CreateSut(_ => connection.Object);
        await sut.GetConnectionAsync();

        await sut.DisposeAsync();

        connection.Verify(value => value.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GetConnectionAsync_Throws_AfterDisposal() {
        var connection = CreateConnection(isOpen: true);
        var sut = CreateSut(_ => connection.Object);
        await sut.GetConnectionAsync();
        await sut.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => sut.GetConnectionAsync());
    }

    private static RabbitMQConnectionFactory CreateSut(Func<ConnectionFactory, IConnection> createConnection)
    => new(new FixedConnectionStringProvider(ConnectionString), createConnection);

    private static Mock<IConnection> CreateConnection(bool isOpen) {
        var connection = new Mock<IConnection>();
        connection.SetupGet(value => value.IsOpen).Returns(isOpen);
        return connection;
    }

    // Hand written rather than mocked: the interface is internal to the RabbitMQ assembly, which the dynamic
    // proxy generator cannot see. This mirrors how the OmicsFlow Manager tests fake the same interfaces.
    private sealed class FixedConnectionStringProvider(string connectionString) : IRabbitMQConnectionStringProvider {
        public Task<string> GetConnectionString() => Task.FromResult(connectionString);
    }
}
