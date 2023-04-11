using Newtonsoft.Json;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using TGF.Common.ROP.Errors;

namespace TGF.Common.ROP.Result
{
    /// <summary>
    /// Public interface for any <see cref="Result{T}"/>.
    /// </summary>
    [JsonObject]
    public interface IResult<T>
    {
        [JsonPropertyName("Value")]
        T Value { get; }
        [JsonPropertyName("IsSuccess")]
        bool IsSuccess { get; }
        [JsonPropertyName("ErrorList")]
        ImmutableArray<IError> ErrorList { get; }
    }
}
