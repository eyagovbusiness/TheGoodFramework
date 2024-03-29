﻿using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Serialization;

namespace TGF.Common.Serialization
{
    public static class XMLSerializer
    {
        public static async Task SerializeToFileAsync<T>(this T obj, string aXmlFilePath)
            where T : class, new()
        {
            var serializer = new XmlSerializer(typeof(T));
            using var stream = new FileStream(aXmlFilePath, FileMode.Create);
            using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Async = true });
            serializer.Serialize(writer, obj);
            await writer.FlushAsync();
        }
        public static class XMLDeserializer
        {
            public static Task<T> DeserializeObjectAsync<T>(string aXmlFilePath)
                where T : class, new()
            {
                using StringReader lReader = new StringReader(aXmlFilePath);
                using XmlReader lXmlReader = XmlReader.Create(lReader);
                DataContractSerializer lSerializer = new(typeof(T));
                T lTargetObject = lSerializer.ReadObject(lXmlReader) as T ?? new T();
                return Task.FromResult(lTargetObject);
            }
        }
    }
}
