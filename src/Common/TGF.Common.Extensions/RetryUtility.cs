namespace TGF.Common.Extensions
{
    public static class RetryUtility
    {
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
        public static async Task<TlResult> ExecuteWithRetryAsync<TlResult>(
            Func<Task<TlResult>> aCondition,
            Func<TlResult, bool> aRetryCondition,
            int aMaxRetries = 3,
            int aDelayMilliseconds = 1000,
            CancellationToken aCancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(aCondition);
            ArgumentNullException.ThrowIfNull(aRetryCondition);

            int retryCount = 0;

            do
            {
                Task<TlResult> task;
                try
                {
                    task = aCondition();
                    var result = await task.ConfigureAwait(false);
                    if (!aRetryCondition(result))
                        return result;
                }
                catch (Exception)
                {
                    if (aCancellationToken.IsCancellationRequested || retryCount >= aMaxRetries)
                        throw;
                }

                await Task.Delay(aDelayMilliseconds, aCancellationToken).ConfigureAwait(false);
            }
            while (++retryCount <= aMaxRetries && !aCancellationToken.IsCancellationRequested);

            throw new InvalidOperationException("Max retry attempts exceeded.");
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
        public static Task<TlResult> ExecuteWithRetryAsync<TlResult>(
            Task<TlResult> task,
            Func<TlResult, bool> aRetryCondition,
            int aMaxRetries = 3,
            int aDelayMilliseconds = 1000,
            CancellationToken aCancellationToken = default)
        {
            return ExecuteWithRetryAsync(() => task, aRetryCondition, aMaxRetries, aDelayMilliseconds, aCancellationToken);
        }
    }
}
