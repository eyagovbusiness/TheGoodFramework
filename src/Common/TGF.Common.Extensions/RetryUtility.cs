using System;
using System.Threading.Tasks;
using System.Threading;

namespace TGF.Common.Extensions
{
    public static class RetryUtility
    {
        public static async Task<TlResult> ExecuteWithRetryAsync<TlResult>(
            Func<Task<TlResult>> aCondition,
            Func<TlResult, bool> aRetryCondition,
            int aMaxRetries = 3,
            int aDelayMilliseconds = 1000,
            CancellationToken aCancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(aCondition);
            ArgumentNullException.ThrowIfNull(aRetryCondition);
            int lRetryCount = 0;

            do
            {
                try
                {
                    var lResult = await aCondition().ConfigureAwait(false);
                    if (!aRetryCondition(lResult))
                        return lResult;
                }
                catch (Exception)
                {
                    if (aCancellationToken.IsCancellationRequested || lRetryCount >= aMaxRetries)
                        throw;
                }

                await Task.Delay(aDelayMilliseconds, aCancellationToken).ConfigureAwait(false);
            }
            while (++lRetryCount <= aMaxRetries && !aCancellationToken.IsCancellationRequested);

            throw new InvalidOperationException("Max retry attempts exceeded.");
        }
    }


}
