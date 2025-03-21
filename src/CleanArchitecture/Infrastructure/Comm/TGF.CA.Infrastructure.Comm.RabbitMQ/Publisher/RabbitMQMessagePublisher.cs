using RabbitMQ.Client;
using System.Text;
using TGF.CA.Infrastructure.Comm.Messages;
using TGF.CA.Infrastructure.Comm.Publisher;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Connection;
using TGF.CA.Infrastructure.Comm.RabbitMQ.Settings;
using TGF.Common.Extensions;
using TGF.Common.Serialization;

namespace TGF.CA.Infrastructure.Comm.RabbitMQ.Publisher;

internal class RabbitMQMessagePublisher<TMessage>(RabbitMQSettings rabbitMQSettings, IRabbitMQConnectionFactory rabbitMQConnectionFactory, ISerializer serializer)
: IExternalMessagePublisher<TMessage>
where TMessage : IMessage {

    public async Task Publish(TMessage aMessage, string? aRoutingKey = null, CancellationToken aCancellationToken = default) {
        var lConnection = await rabbitMQConnectionFactory.GetConnectionAsync();
        using var lModel = lConnection.CreateModel();
        PublishSingle(aMessage, lModel, aRoutingKey);
    }

    public async Task PublishMany(IEnumerable<TMessage> aMessages, string? aRoutingKey = null, CancellationToken aCancellationToken = default) {
        var lConnection = await rabbitMQConnectionFactory.GetConnectionAsync();
        using var lModel = lConnection.CreateModel();

        aMessages.ForEach(lMessage => PublishSingle(lMessage, lModel, aRoutingKey));
    }

    private void PublishSingle(TMessage aMessage, IModel aModel, string? aRoutingKey) {
        var lProperties = aModel.CreateBasicProperties();
        lProperties.Persistent = true;
        lProperties.Type = RabbitMQMessagePublisher<TMessage>.RemoveVersion(aMessage.GetType());
        var lCorrectExchange = GetCorrectExchange();

        aModel.BasicPublish(exchange: lCorrectExchange,
            routingKey: aRoutingKey ?? "",
            basicProperties: lProperties,
            body: serializer.SerializeObjectToByteArray(aMessage)
        );
    }

    private string GetCorrectExchange()
    => (typeof(TMessage) == typeof(IntegrationMessage)
        ? rabbitMQSettings.Publisher?.IntegrationExchange
        : rabbitMQSettings.Publisher?.DomainExchange)
    ?? throw new ArgumentException("Please configure the Exchanges on the appsettings.");

    /// <summary>
    /// there is a limit of 255 characters on the type in RabbitMQ.
    /// in top of that the version will cause issues if it gets updated and the payload contains the old and so on.  
    /// </summary>
    private static string RemoveVersion(Type aType)
    => RabbitMQMessagePublisher<TMessage>.RemoveVersionFromQualifiedName(aType.AssemblyQualifiedName ?? "", 0);

    private static string RemoveVersionFromQualifiedName(string aAssemblyQualifiedName, int aIndexStart) {
        var lStringBuilder = new StringBuilder();
        var lIndexOfGenericClose = aAssemblyQualifiedName.IndexOf("]]", aIndexStart + 1, StringComparison.Ordinal);
        var lIndexOfVersion = aAssemblyQualifiedName.IndexOf(", Version", aIndexStart + 1, StringComparison.Ordinal);

        if (lIndexOfVersion < 0)
            return aAssemblyQualifiedName;

        lStringBuilder.Append(aAssemblyQualifiedName[aIndexStart..lIndexOfVersion]);

        if (lIndexOfGenericClose > 0)
            lStringBuilder.Append(RabbitMQMessagePublisher<TMessage>.RemoveVersionFromQualifiedName(aAssemblyQualifiedName, lIndexOfGenericClose));

        return lStringBuilder.ToString();
    }

}