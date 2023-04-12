using MediatR;
using System.Collections.Immutable;
using System.Net;
using TGF.Common.ROP.Errors;
using TGF.Common.ROP.HttpResult;

namespace TGF.Common.ROP.Result
{

    /// <summary>
    /// Internal class that represents the unit of information while returning the result of operations in Reailway Oriented Programming.
    /// </summary>
    /// <typeparam name="T">Type of the result Value.</typeparam>
    internal class Result<T> : IResult<T>
    {
        public T Value { get; }
        public bool IsSuccess => ErrorList.Length == 0;
        public ImmutableArray<IError> ErrorList { get; }

        public Result(T aValue) : base()
        {
            Value = aValue;
            ErrorList = ImmutableArray<IError>.Empty;
        }
#pragma warning disable CS8601 // Possible null reference assignment.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public Result(ImmutableArray<IError> aErrorList)
        {
            Value = default(T);

            if (aErrorList.Length == 0)
                throw new InvalidOperationException("Can't create a failure Result without errors.");

            ErrorList = aErrorList;
        }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning restore CS8601 // Possible null reference assignment.

    }

    /// <summary>
    /// Static class to provide extension methods of Result type instances and create new instances of Results with success or failure(s).
    /// </summary>
    public static class Result
    {

        #region Result

        public static IResult<T> Success<T>(T aValue)
            => new Result<T>(aValue);

        public static IResult<T> Failure<T>(ImmutableArray<IError> aErrorList)
            => new Result<T>(aErrorList);

        public static IResult<T> Failure<T>(IError aError)
            => new Result<T>(ImmutableArray.Create(aError));

        #endregion

        #region HttpResult

        public static IHttpResult<T> Success<T>(T aValue, HttpStatusCode aStatusCode)
            => new HttpResult<T>(aValue, aStatusCode);

        public static IHttpResult<T> SuccessHttp<T>(T aValue, HttpStatusCode aStatusCode = HttpStatusCode.OK)
            => new HttpResult<T>(aValue, aStatusCode);

        public static IHttpResult<T> Failure<T>(ImmutableArray<IError> aErrorList, HttpStatusCode aStatusCode)
            => new HttpResult<T>(aErrorList, aStatusCode);

        public static IHttpResult<T> Failure<T>(IHttpError aHttpError)
            => new HttpResult<T>(ImmutableArray.Create(aHttpError.Error), aHttpError.StatusCode);

        public static IHttpResult<T> Failure<T>(ImmutableArray<IHttpError> aHttpErrorList)
            => new HttpResult<T>(aHttpErrorList.Select(e => e.Error).ToImmutableArray(), aHttpErrorList.First().StatusCode);

        public static IHttpResult<Unit> CancellationTokenResult(CancellationToken aCancellationToken)
            => aCancellationToken.IsCancellationRequested
               ? Failure<Unit>(CommonErrors.CancellationToken.Cancelled)
               : SuccessHttp(Unit.Value);

        public static Task<IHttpResult<Unit>> CancellationTokenResultAsync(CancellationToken aCancellationToken)
            => Task.FromResult(CancellationTokenResult(aCancellationToken));

#pragma warning disable CS8603 // Possible null reference return.
        /// <summary>
        /// Tries to parse an IResult into an IHttpResult, this will only work if the IResult parameter is an instance of HttpResult.
        /// <exception cref="Exception"></exception>
        public static IHttpResult<T> TryHttpResultParse<T>(this IResult<T> aResult)
            => aResult != null && aResult is IHttpResult<T>
               ? aResult as IHttpResult<T>
               : throw new Exception($"TryParse failed from IResult<{nameof(T)}> to IHttpResult<{nameof(T)}>");
#pragma warning restore CS8603 // Possible null reference return.

        #endregion

        #region SerializationExtensions
        public static string Serialize<T>(this IHttpResult<T> aResult) => Utf8Json.JsonSerializer.ToJsonString(aResult);
        public static IHttpResult<T>? DeserializeHttpResult<T>(string aResultSerializedString) => System.Text.Json.JsonSerializer.Deserialize<IHttpResult<T>>(aResultSerializedString) ?? default;
        public static IResult<T>? DeserializeResult<T>(string aResultSerializedString) => System.Text.Json.JsonSerializer.Deserialize<IResult<T>>(aResultSerializedString) ?? default;
        #endregion

    }

}