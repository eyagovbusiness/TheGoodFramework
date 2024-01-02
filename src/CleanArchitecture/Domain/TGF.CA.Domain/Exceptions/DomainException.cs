using TGF.CA.Domain.Primitives;
using TGF.Common.ROP.Errors;

namespace TGF.CA.Domain.Core.Exceptions
{
    /// <summary>
    /// Represents an exception that occurred in the domain.
    /// </summary>
    public class DomainException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DomainException"/> class.
        /// </summary>
        /// <param name="error">The error containing the information about what happened.</param>
        public DomainException(IError error)
            : base(error.Message)
            => Error = error;

        /// <summary>
        /// Gets the error.
        /// </summary>
        public IError Error { get; }
    }
}
