using Microsoft.Extensions.Logging;

namespace TGF.Common.Extensions {
    public interface IRetryUtility {
        Task<TlResult> ExecuteWithRetryAsync<TlResult>(Func<Task<TlResult>> aCondition, Func<TlResult, bool> aRetryCondition, int aMaxRetries = 3, int aDelayMilliseconds = 1000, CancellationToken aCancellationToken = default);
        Task<TlResult> ExecuteWithRetryAsync<TlResult>(Task<TlResult> task, Func<TlResult, bool> aRetryCondition, int aMaxRetries = 3, int aDelayMilliseconds = 1000, CancellationToken aCancellationToken = default);
    }

    public class RetryUtility(ILogger<RetryUtility> logger) : IRetryUtility {
        /// <summary>
        /// Executes a task with retry logic based on a specified condition. It retries the task if the condition is met and stops if the condition is not met or the maximum number of retries is reached.
        /// </summary>
        /// <typeparam name="TlResult">The type of the result returned by the task.</typeparam>
        /// <param name="aCondition">A function that returns the task to be executed.</param>
        /// <param name="aRetryCondition">A function that determines whether to retry the task based on the result it returns.</param>
        /// <param name="aMaxRetries">The maximum number of retries allowed (default is 3).</param>
        /// <param name="aDelayMilliseconds">The delay in milliseconds between retries (default is 1000).</param>
        /// <param name="aCancellationToken">The cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task representing the asynchronous operation, containing the result of the type <typeparamref name="TlResult"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if either <paramref name="aCondition"/> or <paramref name="aRetryCondition"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the retry attempts exceed the maximum limit.</exception>
        /// <exception cref="Exception">Propagates any exceptions thrown by the task being retried, except for those handled by retries and cancellation.</exception>
        public async Task<TlResult> ExecuteWithRetryAsync<TlResult>(
            Func<Task<TlResult>> aCondition,
            Func<TlResult, bool> aRetryCondition,
            int aMaxRetries = 3,
            int aDelayMilliseconds = 1000,
            CancellationToken aCancellationToken = default) {
            ArgumentNullException.ThrowIfNull(aCondition);
            ArgumentNullException.ThrowIfNull(aRetryCondition);

            var retryCount = 1;

            do {
                Task<TlResult> task;
                try {
                    logger.LogInformation("[RETRY UTILITY] Executing task with retry, attempt number {AttemptNumber} of {MaxRetries}", retryCount, aMaxRetries);
                    task = aCondition();
                    var result = await task.ConfigureAwait(false);
                    if (!aRetryCondition(result)) {
                        logger.LogInformation("[RETRY UTILITY] Task with retry executed successfully during attempt number {AttemptNumber} of {MaxRetries}", retryCount, aMaxRetries);
                        return result;
                    }
                }
                catch (Exception exception) {
                    logger.LogWarning(exception, "Exception thrown by during the {RetryUtilityName} duyring retry number {retryCount}:", nameof(RetryUtility), retryCount);
                    if (aCancellationToken.IsCancellationRequested || retryCount >= aMaxRetries) {
                        logger.LogError("Max retry attempts exceeded by the {RetryUtilityName}", nameof(RetryUtility));
                        throw;
                    }
                }

                await Task.Delay(aDelayMilliseconds, aCancellationToken).ConfigureAwait(false);
            }
            while (++retryCount <= aMaxRetries && !aCancellationToken.IsCancellationRequested);

            throw new InvalidOperationException($"Max retry attempts exceeded by the {nameof(RetryUtility)}");
        }

        /// <summary>
        /// Convenience method to execute an existing task with retry logic.
        /// </summary>
        /// <typeparam name="TlResult">The type of the result returned by the task.</typeparam>
        /// <param name="task">The task to be retried.</param>
        /// <param name="aRetryCondition">A function that determines whether to retry the task based on the result it returns.</param>
        /// <param name="aMaxRetries">The maximum number of retries allowed (default is 3).</param>
        /// <param name="aDelayMilliseconds">The delay in milliseconds between retries (default is 1000).</param>
        /// <param name="aCancellationToken">The cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task representing the asynchronous operation, containing the result of the type <typeparamref name="TlResult"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="task"/> or <paramref name="aRetryCondition"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the retry attempts exceed the maximum limit.</exception>
        /// <exception cref="Exception">Propagates any exceptions thrown by the task being retried, except for those handled by retries and cancellation.</exception>
        public Task<TlResult> ExecuteWithRetryAsync<TlResult>(
            Task<TlResult> task,
            Func<TlResult, bool> aRetryCondition,
            int aMaxRetries = 3,
            int aDelayMilliseconds = 1000,
            CancellationToken aCancellationToken = default) => ExecuteWithRetryAsync(() => task, aRetryCondition, aMaxRetries, aDelayMilliseconds, aCancellationToken);
    }
}
