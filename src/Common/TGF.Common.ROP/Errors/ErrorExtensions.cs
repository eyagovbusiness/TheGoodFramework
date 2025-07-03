using FluentValidation;

namespace TGF.Common.ROP.Errors {
    public static class ErrorExtensions {
        public static string GetHttpErrorListAsString(this IEnumerable<IHttpError> aHttpErrorList)
            => string.Join($" {Environment.NewLine} ", aHttpErrorList);
        public static string GetErrorListAsString(this IEnumerable<IError> aErrorList)
            => string.Join($" {Environment.NewLine} ", aErrorList);
        public static bool IsValidationError(this IError aError)
            => aError is ValidationError;
        public static bool IsValidationError(this IHttpError aHttpError)
            => aHttpError.Error is ValidationError;

        /// <summary>
        /// Specifies a custom <see cref="ValidationError"/> to use when validation fails. Only applies to the rule that directly precedes it.
        /// </summary>
        /// <param name="aFluentValidationRule">The current rule</param>
        /// <param name="aValidationErrorROP">The ROP <see cref="ValidationError"/> to use.</param>
        /// <returns></returns>
        public static IRuleBuilderOptions<T, TProperty> WithROPError<T, TProperty>(this IRuleBuilderOptions<T, TProperty> aFluentValidationRule, ValidationError aValidationErrorROP)
            => aFluentValidationRule.WithErrorCode(aValidationErrorROP.Code)
            .WithMessage(aValidationErrorROP.Message);

    }
}