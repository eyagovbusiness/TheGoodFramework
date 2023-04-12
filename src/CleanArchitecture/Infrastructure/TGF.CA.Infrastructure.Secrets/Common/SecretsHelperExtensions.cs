using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TGF.CA.Infrastructure.Secrets.Common
{
    internal static class SecretsHelperExtensions
    {
        internal static T ToObject<T>(this IDictionary<string, object> aSource)
            where T : new()
        {
            var lObject = new T();
            var lObjectType = lObject.GetType();

            foreach (var item in aSource)
            {
                lObjectType
                    .GetProperty(item.Key)!
                    .SetValue(lObject, item.Value, null);
            }
            return lObject;
        }
    }
}
