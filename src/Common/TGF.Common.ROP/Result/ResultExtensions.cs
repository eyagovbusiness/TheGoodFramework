using TGF.Common.ROP.Errors;
using TGF.Common.ROP.HttpResult;

namespace TGF.Common.ROP.Result
{
    public static class ResultExtensions
    {
        public static bool HasValidationErrors<T>(this IResult<T> aResult)
            => aResult.ErrorList.Any(error => error is ValidationError);

        #region SerializationExtensions
        public static string Serialize<T>(this IHttpResult<T> aResult) => Utf8Json.JsonSerializer.ToJsonString(aResult);
        public static IHttpResult<T>? DeserializeHttpResult<T>(string aResultSerializedString) => System.Text.Json.JsonSerializer.Deserialize<IHttpResult<T>>(aResultSerializedString) ?? default;
        public static IResult<T>? DeserializeResult<T>(string aResultSerializedString) => System.Text.Json.JsonSerializer.Deserialize<IResult<T>>(aResultSerializedString) ?? default;
        #endregion

    }
}
