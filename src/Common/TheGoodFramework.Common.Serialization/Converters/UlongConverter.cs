using Newtonsoft.Json;
using System.Formats.Asn1;

namespace TGF.Common.Serialization.Converters
{
    public class UlongConverter : JsonConverter<ulong>
    {
        public override ulong ReadJson(JsonReader aReader, Type aObjectType, ulong aExistingValue, bool aHasExistingValue, JsonSerializer aSerializer)
        {
            if(aReader?.Value == null)
                throw new JsonSerializationException($"Failed to deserialize {aObjectType.Name} from JSON. JsonReader.Value was null!!");
            if (aReader.TokenType == JsonToken.String)
            {
                string lStringValue = (string)aReader.Value;
                if (ulong.TryParse(lStringValue, out ulong lResult))
                    return lResult;
            }
            throw new JsonSerializationException($"Failed to deserialize {aObjectType.Name} from JSON.");
        }

        public override void WriteJson(JsonWriter aWriter, ulong aValue, JsonSerializer aSerializer)
            =>  aWriter.WriteValue(aValue.ToString());

    }
}