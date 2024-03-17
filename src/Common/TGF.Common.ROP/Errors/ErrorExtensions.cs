using FluentValidation;

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

        /// <summary>
        /// Specifies a custom ROP IError to use when validation fails. Only applies to the rule that directly precedes it.
        /// </summary>
        /// <param name="aFluentValidationRule">The current rule</param>
        /// <param name="aROPError">The ROP IError to use</param>
        /// <returns></returns>
        public static IRuleBuilderOptions<T, TProperty> WithROPError<T, TProperty>(this IRuleBuilderOptions<T, TProperty> aFluentValidationRule, IError aROPError)
        => aFluentValidationRule.WithErrorCode(aROPError.Code)
                .WithMessage(aROPError.Message);

    }
}