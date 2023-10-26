using FluentValidation;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Net;
using TGF.Common.ROP.Errors;

namespace TGF.Common.ROP.HttpResult
{
    /// <summary>
    /// Provides extension methods for <see cref="IHttpResult{T}"/> validate objects and conditionally modify the path with the provided <see cref="IHttpError"/> list based on validation results.
    /// </summary>
    public static class ValidateSwitchExtensions
    {

        /// <summary>
        /// Performs a validation of the provided object with the provided validator. If the validation fails, 
        /// it returns a failure with the resulting validation errors. Otherwise, it returns the original asynchronous result.
        /// </summary>
        /// <typeparam name="T">The type of the result value.</typeparam>
        /// <typeparam name="Tval">The type of object to validate.</typeparam>
        /// <param name="aThisResult">The <see cref="IHttpResult{T}"/> to verify.</param>
        /// <param name="aObjectToValidate">The object to validate.</param>
        /// <param name="aValidator">The validator to use.</param>
        /// <returns>Either the original result or a failure, depending on the validation result.</returns>
        public static async Task<IHttpResult<T>> Validate<T,Tval>(this Task<IHttpResult<T>> aThisResult, Tval aObjectToValidate, AbstractValidator<Tval> aValidator)
        {
            var lThisResult = await aThisResult;
            var lValidationResult = aValidator.Validate(aObjectToValidate);
            return lThisResult.IsSuccess && lValidationResult.IsValid
                ? lThisResult
                : Result.Result.Failure<T>(lValidationResult.Errors.Select(e => GetValidationError(e.ErrorMessage))
                                           .ToImmutableArray());
        }

        /// <summary>
        /// Get a list of validation results and if any of the validation results was not valid, 
        /// it returns a failure with the all the failed validation errors. Otherwise, it returns the original asynchronous result.
        /// </summary>
        /// <typeparam name="T">The type of the result value.</typeparam>
        /// <param name="aThisResult">The <see cref="IHttpResult{T}"/> to verify.</param>
        /// <param name="aValidationResultList">The list of validation results, all of them must be valid to continue in the happy path.</param>
        /// <returns>Either the original result or a failure, depending on the validation result.</returns>
        public static async Task<IHttpResult<T>> Validate<T>(this Task<IHttpResult<T>> aThisResult, List<FluentValidation.Results.ValidationResult> aValidationResultList)
        {
            var lThisResult = await aThisResult;
            var lFailedValidationResultList = aValidationResultList.Where(res => !res.IsValid).ToArray();
            return lThisResult.IsSuccess && lFailedValidationResultList.Length == 0
                ? lThisResult
                : Result.Result.Failure<T>(lFailedValidationResultList.SelectMany(validationResult => validationResult.Errors.Select(e => GetValidationError(e.ErrorMessage)))
                            .ToImmutableArray());
        }

        /// <summary>
        /// Performs a validation of the provided object with the provided validator. If the validation fails, 
        /// it returns a failure with the resulting validation errors. Otherwise, it returns the original result.
        /// </summary>
        /// <typeparam name="T">The type of the result value.</typeparam>
        /// <typeparam name="Tval">The type of object to validate.</typeparam>
        /// <param name="aThisResult">The <see cref="IHttpResult{T}"/> to verify.</param>
        /// <param name="aObjectToValidate">The object to validate.</param>
        /// <param name="aValidator">The validator to use.</param>
        /// <returns>Either the original result or a failure, depending on the validation result.</returns>
        public static IHttpResult<T> Validate<T,Tval>(this IHttpResult<T> aThisResult, Tval aObjectToValidate, IValidator<Tval> aValidator)
        {
            var lValidationResult = aValidator.Validate(aObjectToValidate);
            return aThisResult.IsSuccess && lValidationResult.IsValid
                ? aThisResult
                : Result.Result.Failure<T>(lValidationResult.Errors.Select(e => GetValidationError(e.ErrorMessage))
                                           .ToImmutableArray());
        }

        /// <summary>
        /// Get a list of validation results and if any of the validation results was not valid, 
        /// it returns a failure with the all the failed validation errors. Otherwise, it returns the original result.
        /// </summary>
        /// <typeparam name="T">The type of the result value.</typeparam>
        /// <param name="aThisResult">The <see cref="IHttpResult{T}"/> to verify.</param>
        /// <param name="aValidationResultList">The list of validation results, all of them must be valid to continue in the happy path.</param>
        /// <returns>Either the original result or a failure, depending on the validation result.</returns>
        public static IHttpResult<T> Validate<T>(this IHttpResult<T> aThisResult, List<FluentValidation.Results.ValidationResult> aValidationResultList)
        {
            var lFailedValidationResultList = aValidationResultList.Where(res => !res.IsValid).ToArray();
            return aThisResult.IsSuccess && lFailedValidationResultList.Length == 0
                ? aThisResult
                : Result.Result.Failure<T>(lFailedValidationResultList.SelectMany(validationResult => validationResult.Errors.Select(e => GetValidationError(e.ErrorMessage)))
                            .ToImmutableArray());
        }

        private static IHttpError GetValidationError(string aValidationErrorString)
        {
            var lParts = aValidationErrorString.Split(new[] { ':' }, 2);
            var lError = lParts.Length == 2
                ? new ValidationError(lParts[0].Trim(), lParts[1].Trim())
                : new ValidationError("Unknown", aValidationErrorString);

            return new HttpError(lError, HttpStatusCode.BadRequest);
        }

    }
}
