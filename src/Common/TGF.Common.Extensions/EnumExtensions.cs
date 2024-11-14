using System.ComponentModel;
using System.Reflection;

namespace TGF.Common.Extensions {
    public static class EnumExtensions {

        public static string GetDescription(this Enum value) {
            var fieldInfo = value
            .GetType().GetField(value.ToString());

            var attribute = fieldInfo?.GetCustomAttribute<DescriptionAttribute>();

            return attribute != null ? attribute.Description : value.ToString();
        }
    }
}
