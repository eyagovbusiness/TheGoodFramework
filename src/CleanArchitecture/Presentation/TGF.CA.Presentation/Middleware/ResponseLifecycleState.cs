namespace TGF.CA.Presentation.Middleware
{
    internal enum ResponsePhase
    {
        Normal = 0,
        PreparingSafeError = 1,
        Aborted = 2,
    }

    /// <summary>
    /// Coordinates the body and callback guards so an unexpected HTTP failure cannot resume normal failed-response output
    /// while <see cref="SafeExceptionMiddleware"/> prepares a fixed error response or aborts the connection.
    /// </summary>
    /// <remarks>
    /// Pending-byte and uncertainty flags decide whether a clean replacement response is safe; the phase determines whether
    /// owned OnStarting callbacks may run. State is isolated to one request and records only operations observed through
    /// these decorators. It is not a global response tracker or proof about bytes/callbacks outside their ownership.
    /// </remarks>
    internal sealed class ResponseLifecycleState
    {
        private int _phase;
        private long _pendingBytes;
        private int _uncertain;
        private int _callbackFailure;

        public ResponsePhase Phase => (ResponsePhase)Volatile.Read(ref _phase);

        /// <summary>Bytes successfully advanced (or implicitly advanced by WriteAsync) through the body decorator and not provably handed over.</summary>
        public long PendingBytes => Interlocked.Read(ref _pendingBytes);

        /// <summary>Sticky: a cancelled/completed/thrown write or flush, or a completed-with-exception writer, left the pending state unprovable.</summary>
        public bool UncertainOutput => Volatile.Read(ref _uncertain) != 0;

        /// <summary>Sticky: a normal-state downstream callback failed, so a clean response cannot be manufactured.</summary>
        public bool CallbackFailure => Volatile.Read(ref _callbackFailure) != 0;

        /// <summary>Set BEFORE any reset or write so downstream deferred callbacks cannot recreate failed-response values.</summary>
        public void EnterSafeErrorPreparation() => Interlocked.CompareExchange(ref _phase, (int)ResponsePhase.PreparingSafeError, (int)ResponsePhase.Normal);

        public void EnterAborted() => Volatile.Write(ref _phase, (int)ResponsePhase.Aborted);

        public void AddPending(long aBytes) => Interlocked.Add(ref _pendingBytes, aBytes);

        public void ClearPending() => Interlocked.Exchange(ref _pendingBytes, 0);

        public void MarkUncertain() => Volatile.Write(ref _uncertain, 1);

        public void MarkCallbackFailure() => Volatile.Write(ref _callbackFailure, 1);
    }

    /// <summary>Replaces response-writer and callback exceptions before they can carry request, secret or provider text to server logging.</summary>
    /// <remarks>Preserves cancellation signalling and its token, but intentionally omits original messages, inner exceptions and Data.</remarks>
    internal static class SafeFailures
    {
        public static Exception? Sanitize(Exception? aException)
            => aException switch
            {
                null => null,
                OperationCanceledException lCancelled => Cancelled(lCancelled.CancellationToken),
                _ => Failure(),
            };

        public static OperationCanceledException Cancelled(CancellationToken aToken) => new("The response operation was cancelled.", aToken);

        public static InvalidOperationException Failure() => new("The response operation failed.");
    }
}
