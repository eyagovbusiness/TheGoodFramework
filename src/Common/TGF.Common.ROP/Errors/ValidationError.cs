
namespace TGF.Common.ROP.Errors {

    /// <summary>
    /// Struct representing an error with an error Code and error Message.
    /// </summary>
    public readonly struct ValidationError(string aCode, string aMessage) : IError {
        public string Code { get; } = aCode;
        public string Message { get; } = aMessage;

        public override string ToString() => $"{Code}: {Message}";
    }
}
