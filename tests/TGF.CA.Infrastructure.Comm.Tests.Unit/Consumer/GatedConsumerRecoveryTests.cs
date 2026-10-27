using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RabbitMQ.Client;
using TGF.CA.Infrastructure.Comm.Consumer;
using TGF.CA.Infrastructure.Comm.Consumer.Handler;
using TGF.CA.Infrastructure.Comm.Consumer.Host;
using TGF.CA.Infrastructure.Comm.Consumer.Manager;
using TGF.CA.Infrastructure.Comm.Messages;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Connection;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Consumer;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Settings;
using TGF.Common.Extensions;
using TGF.Common.Serialization;

namespace TGF.CA.Infrastructure.Comm.Tests.Unit.Consumer;

public class GatedConsumerRecoveryTests {
    [Fact]
    public async Task GatedConsumer_SubscribesAfterBrokerStartupExceedsTheOldRetryBudget() {
        var subscribed = NewSignal();
        var channel = CreateChannel(subscribed);
        var connection = CreateConnection(channel);
        var factory = new RecoveringConnectionFactory(connection.Object, failures: 12);
        using var provider = CreateProvider(factory);
        using var host = CreateHost(provider);

        try {
            await host.StartAsync(CancellationToken.None);
            await subscribed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(13, factory.Attempts);
            connection.Verify(value => value.Dispose(), Times.Never);
        }
        finally {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await host.StopAsync(timeout.Token);
        }
    }

    [Fact]
    public async Task GatedConsumer_ResubscribesAfterTheFactoryReplacesADeadConnection() {
        var firstSubscription = NewSignal();
        var secondSubscription = NewSignal();
        var firstChannel = CreateChannel(firstSubscription);
        var secondChannel = CreateChannel(secondSubscription);
        var first = CreateConnection(firstChannel);
        var second = CreateConnection(secondChannel);
        var factory = new RecoveringConnectionFactory(first.Object);
        using var provider = CreateProvider(factory);
        using var host = CreateHost(provider);

        try {
            await host.StartAsync(CancellationToken.None);
            await firstSubscription.Task.WaitAsync(TimeSpan.FromSeconds(5));
            factory.Connection = second.Object;
            firstChannel.Raise(value => value.ModelShutdown += null,
                new ShutdownEventArgs(ShutdownInitiator.Peer, 320, "broker restart"));

            await secondSubscription.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(2, factory.Attempts);
            firstChannel.Verify(value => value.Dispose(), Times.Once);
            first.Verify(value => value.Dispose(), Times.Never);
        }
        finally {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await host.StopAsync(timeout.Token);
        }

        secondChannel.Verify(value => value.Dispose(), Times.Once);
        second.Verify(value => value.Dispose(), Times.Never);
    }

    private static TaskCompletionSource<bool> NewSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static Mock<IModel> CreateChannel(TaskCompletionSource<bool> subscribed) {
        var channel = new Mock<IModel>();
        channel.Setup(value => value.BasicConsume(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<IBasicConsumer>()))
            .Callback(() => subscribed.TrySetResult(true))
            .Returns("consumer-tag");
        return channel;
    }

    private static Mock<IConnection> CreateConnection(Mock<IModel> channel) {
        var connection = new Mock<IConnection>();
        connection.Setup(value => value.CreateModel()).Returns(channel.Object);
        return connection;
    }

    private static ServiceProvider CreateProvider(IRabbitMQConnectionFactory factory) {
        var services = new ServiceCollection();
        services.AddSingleton<IMessageConsumer<IntegrationMessage>>(new RabbitMQMessageConsumer<IntegrationMessage>(
            new RabbitMQSettings { Consumer = new ConsumerSettings { IntegrationQueue = "manager.queue" } },
            factory, Mock.Of<IHandleMessage>(), Mock.Of<ISerializer>(),
            NullLogger<RabbitMQMessageConsumer<IntegrationMessage>>.Instance));
        return services.BuildServiceProvider();
    }

    private static GatedConsumerHostedService<IntegrationMessage> CreateHost(IServiceProvider provider)
        => new(new ConsumerManager<IntegrationMessage>(), provider,
            new RetryUtility(NullLogger<RetryUtility>.Instance), (_, _) => Task.FromResult(true),
            NullLogger<GatedConsumerHostedService<IntegrationMessage>>.Instance,
            TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(5));

    private sealed class RecoveringConnectionFactory(IConnection connection, int failures = 0) : IRabbitMQConnectionFactory {
        public IConnection Connection { get; set; } = connection;
        public int Attempts { get; private set; }

        public Task<IConnection> GetConnectionAsync() {
            if (++Attempts <= failures)
                throw new InvalidOperationException("broker unavailable");
            return Task.FromResult(Connection);
        }
    }
}
