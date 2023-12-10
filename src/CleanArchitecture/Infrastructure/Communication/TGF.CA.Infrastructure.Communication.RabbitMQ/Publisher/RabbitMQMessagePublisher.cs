using RabbitMQ.Client;
using System.Text;
using TGF.CA.Infrastructure.Communication.Messages;
using TGF.CA.Infrastructure.Communication.Publisher;
using TGF.CA.Infrastructure.Communication.RabbitMQ.Settings;
using TGF.Common.Serialization;

//code inspired from https://github.com/ElectNewt/Distribt
namespace TGF.CA.Infrastructure.Communication.RabbitMQ.Publisher;
public class RabbitMQMessagePublisher<TMessage> : IExternalMessagePublisher<TMessage>
    where TMessage : IMessage
{
    private readonly ISerializer _serializer;
    private readonly IRabbitMQSettingsFactory _rabbitMQSettingsFactory;
    private readonly Lazy<Task<RabbitMQSettings>> _settings;
    private readonly Lazy<Task<ConnectionFactory>> _connectionFactory;

    public RabbitMQMessagePublisher(ISerializer serializer, IRabbitMQSettingsFactory aRabbitMQSettingsFactory)
    {
        _serializer = serializer;
        _rabbitMQSettingsFactory = aRabbitMQSettingsFactory;
        _settings = new Lazy<Task<RabbitMQSettings>>(_rabbitMQSettingsFactory.GetRabbitMQSettingsAsync);
        _connectionFactory = new Lazy<Task<ConnectionFactory>>(GetConnectionFactory);
    }

    public async Task Publish(TMessage message, string? routingKey = null, CancellationToken cancellationToken = default)
    {
        var lConnectionFactory = await _connectionFactory.Value;
        using IConnection connection = lConnectionFactory.CreateConnection();
        using IModel model = connection.CreateModel(); 

        PublishSingle(message, model, routingKey);
    }

    public async Task PublishMany(IEnumerable<TMessage> messages, string? routingKey = null, CancellationToken cancellationToken = default)
    {
        using IConnection connection = (await _connectionFactory.Value).CreateConnection();
        using IModel model = connection.CreateModel();
        foreach (TMessage message in messages)
        {
            PublishSingle(message, model, routingKey);
        }
    }

    private async void PublishSingle(TMessage message, IModel model, string? routingKey)
    {
        var properties = model.CreateBasicProperties();
        properties.Persistent = true;
        properties.Type = RemoveVersion(message.GetType());
        var lCorrectExchange = await GetCorrectExchange();

        model.BasicPublish(exchange: lCorrectExchange,
            routingKey: routingKey ?? "",
            basicProperties: properties,
            body: _serializer.SerializeObjectToByteArray(message));
    }

    private async Task<string> GetCorrectExchange()
    {
        return (typeof(TMessage) == typeof(IntegrationMessage)
            ? (await _settings.Value).Publisher?.IntegrationExchange
            : (await _settings.Value).Publisher?.DomainExchange)
               ?? throw new ArgumentException("Please configure the Exchanges on the appsettings.");
    }

    /// <summary>
    /// there is a limit of 255 characters on the type in RabbitMQ.
    /// in top of that the version will cause issues if it gets updated and the payload contains the old and so on.  
    /// </summary>
    private string RemoveVersion(Type type)
    {
        return RemoveVersionFromQualifiedName(type.AssemblyQualifiedName ?? "", 0);
    }

    private string RemoveVersionFromQualifiedName(string assemblyQualifiedName, int indexStart)
    {
        var stringBuilder = new StringBuilder();
        var indexOfGenericClose = assemblyQualifiedName.IndexOf("]]", indexStart + 1, StringComparison.Ordinal);
        var indexOfVersion = assemblyQualifiedName.IndexOf(", Version", indexStart + 1, StringComparison.Ordinal);

        if (indexOfVersion < 0)
            return assemblyQualifiedName;

        stringBuilder.Append(assemblyQualifiedName.Substring(indexStart, indexOfVersion - indexStart));

        if (indexOfGenericClose > 0)
            stringBuilder.Append(RemoveVersionFromQualifiedName(assemblyQualifiedName, indexOfGenericClose));

        return stringBuilder.ToString();
    }

    private async Task<ConnectionFactory> GetConnectionFactory()
    {
        var lSettings = await _settings.Value;
        return new()
        {
            HostName = lSettings.Hostname,
            Password = lSettings.Credentials!.Password,
            UserName = lSettings.Credentials!.Username
        };
    }
}