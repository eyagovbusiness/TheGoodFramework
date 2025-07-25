using System.Web;
using TGF.CA.Infrastructure.InvariantConstants;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ.Connection;

internal class RabbitMQEnvConnectionStringProvider()
: IRabbitMQConnectionStringProvider {
    public Task<string> GetConnectionString() {
        var rabbitmqProtocol = HttpUtility.UrlEncode(Environment.GetEnvironmentVariable(EnvironmentVariableNames.RabbitMQ.RABBITMQ_PROTOCOL))
            ?? throw new InvalidOperationException($"[ERROR]: Secrets source type for Postgres was set as env variables but the expected env variable {EnvironmentVariableNames.RabbitMQ.RABBITMQ_PROTOCOL} was not set!");
        var rabbitmqUsername = HttpUtility.UrlEncode(Environment.GetEnvironmentVariable(EnvironmentVariableNames.RabbitMQ.RABBITMQ_USERNAME))
            ?? throw new InvalidOperationException($"[ERROR]: Secrets source type for Postgres was set as env variables but the expected env variable {EnvironmentVariableNames.RabbitMQ.RABBITMQ_USERNAME} was not set!");
        var rabbitmqHostname = HttpUtility.UrlEncode(Environment.GetEnvironmentVariable(EnvironmentVariableNames.RabbitMQ.RABBITMQ_HOSTNAME))
            ?? throw new InvalidOperationException($"[ERROR]: Secrets source type for Postgres was set as env variables but the expected env variable {EnvironmentVariableNames.RabbitMQ.RABBITMQ_HOSTNAME} was not set!");
        var rabbitmqPassword = HttpUtility.UrlEncode(Environment.GetEnvironmentVariable(EnvironmentVariableNames.RabbitMQ.RABBITMQ_PASSWORD))
            ?? throw new InvalidOperationException($"[ERROR]: Secrets source type for Postgres was set as env variables but the expected env variable {EnvironmentVariableNames.RabbitMQ.RABBITMQ_PASSWORD} was not set!");

        return Task.FromResult($"{rabbitmqProtocol}://{rabbitmqUsername}:{rabbitmqPassword}@{rabbitmqHostname}");
    }
}

