using TGF.Common.Serialization.Converters;

namespace TGF.Common.Serialization;

using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;

public class Serializer : ISerializer
{
    private static readonly Encoding Encoding = new UTF8Encoding(false);
    private static readonly JsonSerializerSettings DefaultSerializerSettings = new JsonSerializerSettings
    {
        TypeNameHandling = TypeNameHandling.Auto
    };
    private const int DefaultBufferSize = 1024;
    private readonly JsonSerializer _jsonSerializer;

    public Serializer() : this(DefaultSerializerSettings) { }

    public Serializer(JsonSerializerSettings serializerSettings)
    {
        _jsonSerializer = JsonSerializer.Create(serializerSettings);
        //_jsonSerializer.Converters.Add(new IntegrationMessageConverter());
    }

    public T DeserializeObject<T>(string input)
    {
        using var stringReader = new StringReader(input);
        using var jsonReader = new JsonTextReader(stringReader);
        return _jsonSerializer.Deserialize<T>(jsonReader) ?? throw new InvalidOperationException("Deserialization returned null.");
    }

    public T DeserializeObject<T>(byte[] input) where T : class
    {
        return DeserializeByteArrayToObject<T>(input) as T ?? throw new InvalidOperationException("Deserialization returned null.");
    }

    public object DeserializeObject(byte[] input, Type type)
    {
        using var memoryStream = new MemoryStream(input, false);
        using var streamReader = new StreamReader(memoryStream, Encoding, false, DefaultBufferSize, true);
        using var reader = new JsonTextReader(streamReader);
        return _jsonSerializer.Deserialize(reader, type) ?? throw new InvalidOperationException("Deserialization returned null.");
    }

    private object DeserializeByteArrayToObject<T>(byte[] input)
    {
        using var memoryStream = new MemoryStream(input, false);
        using var streamReader = new StreamReader(memoryStream, Encoding, false, DefaultBufferSize, true);
        using var reader = new JsonTextReader(streamReader);
        return _jsonSerializer.Deserialize(reader, typeof(T)) ?? throw new InvalidOperationException("Deserialization returned null.");
    }

    public string SerializeObject<T>(T obj)
    {
        var stringBuilder = new StringBuilder();
        using (var stringWriter = new StringWriter(stringBuilder))
        using (var jsonWriter = new JsonTextWriter(stringWriter))
        {
            _jsonSerializer.Serialize(jsonWriter, obj);
        }
        return stringBuilder.ToString();
    }

    public byte[] SerializeObjectToByteArray<T>(T obj)
    {
        using var memoryStream = new MemoryStream();
        using (var streamWriter = new StreamWriter(memoryStream, Encoding, DefaultBufferSize, true))
        using (var jsonWriter = new JsonTextWriter(streamWriter))
        {
            jsonWriter.Formatting = _jsonSerializer.Formatting;
            _jsonSerializer.Serialize(jsonWriter, obj, typeof(T));
        }
        return memoryStream.ToArray();
    }
}
