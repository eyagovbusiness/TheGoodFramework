using System.Runtime.Serialization;

namespace TheGoodFramework.Extensions
{
    /// <summary>
    /// Static class supporting extension methods for serializeation and deserializeation
    /// </summary>
    public static class Serialization
    {

        /// <summary>
        /// Uses the the best mempry/performance option to Try to serialize this object.
        /// </summary>
        /// <typeparam name="T">Serializable type of this object.</typeparam>
        /// <param name="aJsonObject">Serializable object.</param>
        /// <returns>UTF8 string representing the json serialized object.</returns>
        public static string TrySerialize8<T>(this T aJsonObject)
             where T : class, new()
        {
                return Utf8Json.JsonSerializer.ToJsonString(aJsonObject);
        }

        /// <summary>
        /// Uses the the best mempry/performance option to try to deserialize this object.
        /// </summary>
        /// <typeparam name="T">Serializable type of this object.</typeparam>
        /// <param name="aString">string representing the json serialized object.</param>
        /// <returns>The deserialized object into the T specified type.</returns>
        public static T TryDeSerialize8<T>(this string aString)
             where T : class, new()
        {
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<T>(aString) ?? new T();
            }
            catch { throw; }
        }

        #region WithFile

        /// <summary>
        /// Uses the the best mempry/performance option to try to deserialize a given json file to a given object.
        /// </summary>
        /// <typeparam name="T">Serializable target type of the returning object.</typeparam>
        /// <param name="aFilePathString">string representing the file path.</param>
        /// <returns>The deserialized object into the T specified type.</returns>
        public static T TryDeserializeFromFile<T>(this string aFilePathString)
            where T : class, new()
        {
            try
            {
                if (aFilePathString.IsNullOrWhiteSpace()
                && !(typeof(T).IsSerializable
                     || typeof(ISerializable)
                        .IsAssignableFrom(typeof(T))
                ))
                    return new T();

                string lJson = string.Empty;
                using (var lFileStream = File.OpenRead(aFilePathString))
                using (var lStreamReader = new StreamReader(lFileStream, new System.Text.UTF8Encoding(false)))
                    lJson = lStreamReader.ReadToEnd();

                return System.Text.Json.JsonSerializer.Deserialize<T>(lJson) ?? new T();//most efficient deserializer
            }
            catch { throw; }
        }

        /// <summary>
        /// Uses the the best mempry/performance option to try to deserialize a given json file to a given object asynchronously.
        /// </summary>
        /// <typeparam name="T">Serializable target type of the returning object.</typeparam>
        /// <param name="aFilePathString"><string representing the file path./param>
        /// <param name="aConfigureAwait"></param>
        /// <returns>Awaitable Task with the deserialized object into the T specified type.</returns>
        public static async Task<T> TryDeserializeFromFileAsync<T>(this string aFilePathString, bool aConfigureAwait = true)
             where T : class, new()
        {
            try
            {
                if (aFilePathString.IsNullOrWhiteSpace()
                && !(typeof(T).IsSerializable
                     || typeof(ISerializable)
                        .IsAssignableFrom(typeof(T))
                ))
                    return new T();

                string lJson = string.Empty;
                using (Stream lStream = File.OpenRead(aFilePathString))
                    return await System.Text.Json.JsonSerializer.DeserializeAsync<T>(lStream).ConfigureAwait(aConfigureAwait) ?? new T();//most efficient deserializer
            }
            catch { throw; }

        }

        /// <summary>
        /// Uses the the best mempry/performance option to try to serialize a given json file to a given object.
        /// </summary>
        /// <typeparam name="T">Serializable target type of the object to serialize.</typeparam>
        /// <param name="aJsonObject">Serializable object.</param>
        /// <param name="aFilePathString">string representing the file path.</param>
        public static void SerializeToFile<T>(this T aJsonObject, string aFilePathString)
             where T : class, new()
        {
            try
            {
                if (!(typeof(T).IsSerializable
                  || typeof(ISerializable)
                     .IsAssignableFrom(typeof(T))
               ))
                    throw new InvalidDataException("Error trying to serialize to file..");

                string lJson = string.Empty;
                using (Stream lStream = File.OpenRead(aFilePathString))
                    Utf8Json.JsonSerializer.Serialize(lStream, aJsonObject);//most efficient deserializer
            }
            catch { throw; }

        }

        /// <summary>
        /// Uses the the best mempry/performance option to try to serialize a given json file to a given object asynchronously.
        /// </summary>
        /// <typeparam name="T">Serializable target type of the object to serialize.</typeparam>
        /// <param name="aJsonObject">Serializable object.</param>
        /// <param name="aFilePathString">string representing the file path.</param>
        /// <param name="aConfigureAwait"></param>
        /// <returns>Awaitable Task</returns>
        public static async Task SerializeToFileAsync<T>(this T aJsonObject, string aFilePathString, bool aConfigureAwait = true)
            where T : class, new()
        {
            try
            {
                if (!(typeof(T).IsSerializable
                  || typeof(ISerializable)
                     .IsAssignableFrom(typeof(T))
               ))
                    throw new InvalidDataException("Error trying to serialize async to file..");

                string lJson = string.Empty;
                using (Stream lStream = File.OpenRead(aFilePathString))
                    await Utf8Json.JsonSerializer.SerializeAsync(lStream, aJsonObject).ConfigureAwait(aConfigureAwait);//most efficient deserializer
            }
            catch { throw; }

        }

        #endregion

    }
}
