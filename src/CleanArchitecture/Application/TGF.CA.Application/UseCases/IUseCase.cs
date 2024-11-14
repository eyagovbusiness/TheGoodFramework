
namespace TGF.CA.Application.UseCases
{
    /// <summary>
    /// Defines a use case with an asynchronous execution method.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response produced by the use case.</typeparam>
    /// <typeparam name="TRequest">The type of the request consumed by the use case.</typeparam>
    public interface IUseCase<TResponse, TRequest>
    {
        /// <summary>
        /// Executes the use case asynchronously.
        /// </summary>
        /// <param name="request">The request object containing the parameters for the use case.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the response from the use case.</returns>
        Task<TResponse> ExecuteAsync(TRequest request, CancellationToken cancellationToken = default);
    }
}
