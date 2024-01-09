using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TGF.Common.Serialization.Converters
{
    //maybe good to use it on the top of the Integration message class insted of having to add it in the serializer to avoid referencing ingra library.
    ///// <summary>
    ///// Integration message is a complex geeric type so it need custom json converter
    ///// </summary>
    //public class IntegrationMessageConverter : JsonConverter
    //{
    //    public override bool CanConvert(Type objectType)
    //    {
    //        return objectType.IsGenericType &&
    //               objectType.GetGenericTypeDefinition() == typeof(IntegrationMessage<>);
    //    }

    //    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    //    {
    //        // Get the actual type of TMessageContent
    //        var contentArg = objectType.GetGenericArguments()[0];
    //        var concreteType = typeof(IntegrationMessage<>).MakeGenericType(contentArg);

    //        // Deserialize to the specific IntegrationMessage<TMessageContent>
    //        return serializer.Deserialize(reader, concreteType);
    //    }

    //    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    //    {
    //        // Serialize the IntegrationMessage<TMessageContent> object
    //        serializer.Serialize(writer, value);
    //    }

    //    public override bool CanRead => true;
    //    public override bool CanWrite => true;
    //}

}
