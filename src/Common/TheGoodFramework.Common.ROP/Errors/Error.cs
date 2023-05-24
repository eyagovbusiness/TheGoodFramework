using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace TGF.Common.ROP.Errors
{
    /// <summary>
    /// Common iterface for Error and possible future different types of errors.
    /// </summary>
    [JsonObject]
    public interface IError
    {
        [JsonPropertyName("Code")]
        string Code { get; }
        [JsonPropertyName("Message")]
        string Message { get; }
    }

    /// <summary>
    /// Struct representing an error with an error Code and error Message.
    /// </summary>
    public readonly struct Error : IError
    {
        public string Code { get; }
        public string Message { get; }
        public Error(string aCode, string aMessage)
        {
            Code = aCode;
            Message = aMessage;
        }

        public override string ToString()
        {
            return $"{Code}: {Message}";
        }
    }
}
