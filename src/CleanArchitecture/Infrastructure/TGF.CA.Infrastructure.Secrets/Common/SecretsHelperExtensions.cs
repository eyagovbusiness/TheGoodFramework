using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TGF.CA.Infrastructure.Secrets.Common
{
    /// <summary>
    /// Class to support conversion from the Dictionary from Vault when reading secrets to a given object. 
    /// </summary>
    internal static class SecretsHelperExtensions
    {
        /// <summary>
        /// Gets a new instance of the specified type that maps to the secrets dictionary, where the dictionary keys are the object property names and the values are the porperty values.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="aSource">Dictionary from reading vault secrets.</param>
        /// <returns>An instance of <see cref="{T}"/></returns>
        internal static T ToObject<T>(this IDictionary<string, object> aSource)
            where T : new()
        {
            var lObject = new T();
            var lObjectType = lObject.GetType();

            foreach (var lItem in aSource)
            {
                var property = lObjectType.GetProperty(lItem.Key);
                if (property != null)
                {
                    var lValue = lItem.Value is JsonElement jsonElement ? GetJsonValue(jsonElement, property.PropertyType) : lItem.Value;
                    property.SetValue(lObject, lValue);
                }
            }
            return lObject;
        }
        /// <summary>
        /// Gets a new instance of an object that correponds to the provided Type, from the given JsonElement.
        /// </summary>
        /// <param name="aJsonElement"></param>
        /// <param name="aPropertyType"></param>
        /// <returns>An object of type from the context property.</returns>
        private static object GetJsonValue(JsonElement aJsonElement, Type aPropertyType)
        {
            if (aPropertyType == typeof(ulong) || aPropertyType == typeof(ulong?))
                return Convert.ToUInt64(aJsonElement.ToString());

            // Add other special cases if needed in the future
            return aJsonElement.ToString();
        }
    }



}
