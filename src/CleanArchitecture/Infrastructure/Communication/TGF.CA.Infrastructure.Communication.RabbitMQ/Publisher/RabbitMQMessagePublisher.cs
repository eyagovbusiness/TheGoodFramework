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

    public async Task Publish(TMessage aMessage, string? aRoutingKey = null, CancellationToken aCancellationToken = default)
    {
        using IConnection lConnection = (await _connectionFactory.Value).CreateConnection();
        using IModel lModel = lConnection.CreateModel();
        PublishSingle(aMessage, lModel, aRoutingKey);
    }

    public async Task PublishMany(IEnumerable<TMessage> aMessages, string? aRoutingKey = null, CancellationToken aCancellationToken = default)
    {
        using IConnection lConnection = (await _connectionFactory.Value).CreateConnection();
        using IModel lModel = lConnection.CreateModel();
        foreach (TMessage lMessage in aMessages)
            PublishSingle(lMessage, lModel, aRoutingKey);
    }

    private async void PublishSingle(TMessage aMessage, IModel aModel, string? aRoutingKey)
    {
        var lProperties = aModel.CreateBasicProperties();
        lProperties.Persistent = true;
        lProperties.Type = RemoveVersion(aMessage.GetType());
        var lCorrectExchange = await GetCorrectExchange();

        aModel.BasicPublish(exchange: lCorrectExchange,
            routingKey: aRoutingKey ?? "",
            basicProperties: lProperties,
            body: _serializer.SerializeObjectToByteArray(aMessage));
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
    private string RemoveVersion(Type aType)
    {
        return RemoveVersionFromQualifiedName(aType.AssemblyQualifiedName ?? "", 0);
    }

    private string RemoveVersionFromQualifiedName(string aAssemblyQualifiedName, int aIndexStart)
    {
        var lStringBuilder = new StringBuilder();
        var lIndexOfGenericClose = aAssemblyQualifiedName.IndexOf("]]", aIndexStart + 1, StringComparison.Ordinal);
        var lIndexOfVersion = aAssemblyQualifiedName.IndexOf(", Version", aIndexStart + 1, StringComparison.Ordinal);

        if (lIndexOfVersion < 0)
            return aAssemblyQualifiedName;

        lStringBuilder.Append(aAssemblyQualifiedName.Substring(aIndexStart, lIndexOfVersion - aIndexStart));

        if (lIndexOfGenericClose > 0)
            lStringBuilder.Append(RemoveVersionFromQualifiedName(aAssemblyQualifiedName, lIndexOfGenericClose));

        return lStringBuilder.ToString();
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