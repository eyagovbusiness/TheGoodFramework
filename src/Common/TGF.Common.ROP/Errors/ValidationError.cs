
namespace TGF.Common.ROP.Errors
{

    /// <summary>
    /// Struct representing an error with an error Code and error Message.
    /// </summary>
    public readonly struct ValidationError : IError
    {
        public string Code { get; }
        public string Message { get; }
        public ValidationError(string aCode, string aMessage)
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
