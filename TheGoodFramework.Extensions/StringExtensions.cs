namespace TheGoodFramework.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// Provides string.IsNullOrEmpty(string? value) method as string object extrension
        /// </summary>
        public static bool IsNullOrEmpty(this string? aString)
        {
            return string.IsNullOrEmpty(aString);
        }

        /// <summary>
        /// Provides string.IsNullOrWhiteSpace(string? value) method as string object extrension
        /// </summary>
        public static bool IsNullOrWhiteSpace(this string? aString)
        {
            return string.IsNullOrWhiteSpace(aString);
        }

        /// <summary>
        /// Converts string into the equivalent T Enum type or its default value in case conversion fails.
        /// </summary>
        public static T ToEnum<T>(this string aString)
            where T : struct
        {
            T lResult;
            if (!aString.IsNullOrEmpty())
                Enum.TryParse(aString, true, out lResult);
            else
                lResult = default;

            return lResult;
        }
    }
}