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
        public async Task<TResult> ExecuteWithRetryAsync<TResult>(
            Func<Task<TResult>> operation,
            Func<TResult, bool> shouldRetry,
            int maxRetries = 5,
            int initialDelayMs = 1000,
            CancellationToken ct = default) {
            ArgumentNullException.ThrowIfNull(operation);
            ArgumentNullException.ThrowIfNull(shouldRetry);

            for (int attempt = 1; attempt <= maxRetries; attempt++) {
                try {
                    logger.LogInformation("[RETRY] Attempt {Attempt} of {MaxRetries}", attempt, maxRetries);

                    TResult result = await operation().ConfigureAwait(false);

                    // If the condition says "don't retry", or this is our last stand, return the result
                    if (!shouldRetry(result) || attempt == maxRetries) {
                        return result;
                    }
                }
                catch (Exception ex) when (attempt < maxRetries && !ct.IsCancellationRequested) {
                    logger.LogWarning(ex, "[RETRY] Exception on attempt {Attempt}", attempt);
                }

                // Calculate Delay: (initialDelay * 2^(attempt-1)) + Jitter
                // Attempt 1 failure -> delay 1000ms. Attempt 2 failure -> delay 2000ms.
                var factor = Math.Pow(1.5, attempt - 1); // Reduced exponent
                var delay = TimeSpan.FromMilliseconds(initialDelayMs * factor);

                // Adding a small random jitter (0-200ms) prevents synchronized retries
                var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 200));

                await Task.Delay(delay + jitter, ct).ConfigureAwait(false);
            }

            // This part is technically unreachable due to the logic above, 
            // but kept for compiler satisfaction or specific fall-through logic.
            throw new OperationCanceledException("Retry failed or was cancelled.");
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
