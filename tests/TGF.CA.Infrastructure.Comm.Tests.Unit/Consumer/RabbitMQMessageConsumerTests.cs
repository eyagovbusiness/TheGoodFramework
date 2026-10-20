using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RabbitMQ.Client;
using TGF.CA.Infrastructure.Comm.Consumer.Handler;
using TGF.CA.Infrastructure.Comm.Messages;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Connection;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Consumer;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Settings;
using TGF.Common.Serialization;

namespace TGF.CA.Infrastructure.Comm.Tests.Unit.Consumer;

/// <summary>
/// Regression coverage for SF-2975: the consumer used to wrap the shared connection in a <c>using</c>, so when
/// consumption unwound it disposed the connection owned by the factory and shared with the publishers and the
/// health checks. Everything else then failed with ObjectDisposedException until the process was restarted.
/// </summary>
public class RabbitMQMessageConsumerTests {

    [Fact]
    public async Task Consume_ExitsWhenTheChannelCloses_SoTheHostCanResubscribe() {
        var channel = new Mock<IModel>();
        var connection = CreateConnection(channel);
        var sut = CreateSut(connection);
        using var cancellation = new CancellationTokenSource();
        var consuming = sut.StartAsync(cancellation.Token);

        channel.Raise(value => value.ModelShutdown += null,
            new ShutdownEventArgs(ShutdownInitiator.Peer, 320, "broker restart"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => consuming.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal("The RabbitMQ consumer channel closed.", exception.Message);
        channel.Verify(value => value.Dispose(), Times.Once);
        connection.Verify(value => value.Dispose(), Times.Never);
    }

    [Fact]
    public async Task Consume_DoesNotDisposeTheSharedConnection_WhenConsumptionIsCancelled() {
        var channel = new Mock<IModel>();
        var connection = CreateConnection(channel);
        var sut = CreateSut(connection);

        using var cancellation = new CancellationTokenSource();
        var consuming = sut.StartAsync(cancellation.Token);
        cancellation.Cancel();
        await consuming;

        connection.Verify(value => value.Dispose(), Times.Never);
    }

    [Fact]
    public async Task Consume_DisposesTheChannelItOwns_WhenConsumptionIsCancelled() {
        var channel = new Mock<IModel>();
        var connection = CreateConnection(channel);
        var sut = CreateSut(connection);

        using var cancellation = new CancellationTokenSource();
        var consuming = sut.StartAsync(cancellation.Token);
        cancellation.Cancel();
        await consuming;

        channel.Verify(value => value.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Consume_DoesNotDisposeTheSharedConnection_WhenSubscribingFails() {
        var channel = new Mock<IModel>();
        channel.Setup(value => value.BasicConsume(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<IBasicConsumer>()))
            .Throws(new OperationInterruptedException());
        var connection = CreateConnection(channel);
        var sut = CreateSut(connection);

        await Assert.ThrowsAsync<OperationInterruptedException>(() => sut.StartAsync(CancellationToken.None));

        connection.Verify(value => value.Dispose(), Times.Never);
    }

    private static Mock<IConnection> CreateConnection(Mock<IModel> channel) {
        var connection = new Mock<IConnection>();
        connection.SetupGet(value => value.IsOpen).Returns(true);
        connection.Setup(value => value.CreateModel()).Returns(channel.Object);
        return connection;
    }

    private static RabbitMQMessageConsumer<IntegrationMessage> CreateSut(Mock<IConnection> connection) {
        var settings = new RabbitMQSettings {
            Consumer = new ConsumerSettings { IntegrationQueue = "manager.queue" }
        };

        return new RabbitMQMessageConsumer<IntegrationMessage>(
            settings,
            new FixedConnectionFactory(connection.Object),
            Mock.Of<IHandleMessage>(),
            Mock.Of<ISerializer>(),
            NullLogger<RabbitMQMessageConsumer<IntegrationMessage>>.Instance);
    }

    // Hand written rather than mocked: the interface is internal to the RabbitMQ assembly, which the dynamic
    // proxy generator cannot see. This mirrors how the OmicsFlow Manager tests fake the same interfaces.
    private sealed class FixedConnectionFactory(IConnection connection) : IRabbitMQConnectionFactory {
        public Task<IConnection> GetConnectionAsync() => Task.FromResult(connection);
    }

    private sealed class OperationInterruptedException : Exception {
        public OperationInterruptedException() : base("broker closed the channel") { }
    }
}
