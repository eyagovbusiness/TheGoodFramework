namespace TGF.Common.ROP.Errors
{
    public static class ErrorExtensions
    {
        public static string GetHttpErrorListAsString(this IEnumerable<IHttpError> aHttpErrorList)
        {
            return string.Join($" {Environment.NewLine} ", aHttpErrorList);
        }
        public static string GetErrorListAsString(this IEnumerable<IError> aErrorList)
        {
            return string.Join($" {Environment.NewLine} ", aErrorList);
        }
        public static bool IsValidationError(this IError aError)
            => aError is ValidationError;
        public static bool IsValidationError(this IHttpError aHttpError)
            => aHttpError.Error is ValidationError;
    }
}