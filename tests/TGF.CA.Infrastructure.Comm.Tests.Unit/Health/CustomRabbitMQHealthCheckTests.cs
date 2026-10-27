using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RabbitMQ.Client;
using TGF.CA.Infrastructure.Comm.RabbitMQ;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Connection;
using TGF.Common.Extensions;

namespace TGF.CA.Infrastructure.Comm.Tests.Unit.Health;

/// <summary>
/// Regression coverage for SF-2975: the health check cached a probe bound to one connection instance, so once
/// that connection died it reported Unhealthy forever and readiness never recovered without a pod restart.
/// </summary>
public class CustomRabbitMQHealthCheckTests {

    [Fact]
    public async Task CheckHealthAsync_RecoversToHealthy_AfterTheDeadConnectionIsReplaced() {
        var dead = CreateConnection(isOpen: false);
        var alive = CreateConnection(isOpen: true);
        var factory = new SwappableConnectionFactory(dead.Object);
        var sut = new CustomRabbitMQHealthCheck(factory, CreateRetryUtility());

        var whileDead = await sut.CheckHealthAsync(NewContext());
        factory.Swap(alive.Object);
        var afterRecovery = await sut.CheckHealthAsync(NewContext());

        Assert.Equal(HealthStatus.Unhealthy, whileDead.Status);
        Assert.Equal(HealthStatus.Healthy, afterRecovery.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_ReusesTheProbe_WhileTheConnectionIsUnchanged() {
        var alive = CreateConnection(isOpen: true);
        var factory = new SwappableConnectionFactory(alive.Object);
        var sut = new CustomRabbitMQHealthCheck(factory, CreateRetryUtility());

        await sut.CheckHealthAsync(NewContext());
        await sut.CheckHealthAsync(NewContext());

        // One channel per probe creation only: the check must not open a new connection on every request.
        Assert.Equal(1, factory.ConnectionRequestsReturningNewInstance);
    }

    [Fact]
    public async Task CheckHealthAsync_ReportsUnhealthy_AndDoesNotThrow_WhenTheBrokerIsUnreachable() {
        var factory = new ThrowingConnectionFactory();
        var sut = new CustomRabbitMQHealthCheck(factory, CreateRetryUtility());

        var result = await sut.CheckHealthAsync(NewContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    private static HealthCheckContext NewContext()
    => new() { Registration = new HealthCheckRegistration("rabbitmq", Mock.Of<IHealthCheck>(), HealthStatus.Unhealthy, null) };

    private static IRetryUtility CreateRetryUtility()
    => new RetryUtility(NullLogger<RetryUtility>.Instance);

    private static Mock<IConnection> CreateConnection(bool isOpen) {
        var connection = new Mock<IConnection>();
        connection.SetupGet(value => value.IsOpen).Returns(isOpen);
        if (isOpen)
            connection.Setup(value => value.CreateModel()).Returns(Mock.Of<IModel>());
        else
            connection.Setup(value => value.CreateModel()).Throws(new InvalidOperationException("connection is closed"));
        return connection;
    }

    private sealed class SwappableConnectionFactory(IConnection connection) : IRabbitMQConnectionFactory {
        private IConnection _connection = connection;
        private IConnection? _lastReturned;

        public int ConnectionRequestsReturningNewInstance { get; private set; }

        public void Swap(IConnection replacement) => _connection = replacement;

        public Task<IConnection> GetConnectionAsync() {
            if (!ReferenceEquals(_lastReturned, _connection)) {
                ConnectionRequestsReturningNewInstance++;
                _lastReturned = _connection;
            }
            return Task.FromResult(_connection);
        }
    }

    private sealed class ThrowingConnectionFactory : IRabbitMQConnectionFactory {
        public Task<IConnection> GetConnectionAsync() => throw new InvalidOperationException("broker unreachable");
    }
}
