using TGF.Common.ROP.Errors;

namespace TGF.CA.Domain.Exceptions {
    /// <summary>
    /// Represents an exception that occurred in the domain.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="DomainException"/> class.
    /// </remarks>
    /// <param name="aError">The error containing the information about what happened.</param>
    public class DomainException(IError aError) : Exception(aError.Message) {

        /// <summary>
        /// Gets the error.
        /// </summary>
        public IError Error { get; } = aError;
    }
}
