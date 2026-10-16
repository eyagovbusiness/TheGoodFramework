using Microsoft.AspNetCore.Http.Features;
using System.IO.Pipelines;

namespace TGF.CA.Presentation.Middleware
{
    /// <summary>
    /// Supplies <see cref="SafeExceptionMiddleware"/> with evidence that failed-response body bytes may still be pending,
    /// so error handling can choose a safe fixed 500 or abort instead of accidentally flushing those bytes.
    /// </summary>
    /// <remarks>
    /// The server can hold bytes advanced through <see cref="PipeWriter"/> even before response headers have started;
    /// resetting status and headers does not prove those bytes are discardable. This request-local decorator tracks writes
    /// through its guarded writer and marks failed or ambiguous output operations as uncertain. It also sanitizes exceptions
    /// supplied to writer completion before the server receives them. Normal body bytes are not buffered or copied, and
    /// underlying cancellation, backpressure and streaming operations are delegated. The Stream is forwarded directly;
    /// this is not a complete inventory of server buffers or writes that bypass the guarded writer.
    /// </remarks>
    internal sealed class GuardedResponseBodyFeature(IHttpResponseBodyFeature aInner, ResponseLifecycleState aState) : IHttpResponseBodyFeature
    {
        private readonly IHttpResponseBodyFeature _inner = aInner;
        private readonly ResponseLifecycleState _state = aState;
        private readonly GuardedWriter _writer = new(aInner.Writer, aState);

        public Stream Stream => _inner.Stream;

        public PipeWriter Writer => _writer;

        public void DisableBuffering() => _inner.DisableBuffering();

        public async Task StartAsync(CancellationToken aCancellationToken = default)
        {
            try
            {
                await _inner.StartAsync(aCancellationToken);
            }
            catch
            {
                _state.MarkUncertain();
                throw;
            }
        }

        public async Task SendFileAsync(string aPath, long aOffset, long? aCount, CancellationToken aCancellationToken = default)
        {
            try
            {
                await _inner.SendFileAsync(aPath, aOffset, aCount, aCancellationToken);
            }
            catch
            {
                _state.MarkUncertain();
                throw;
            }
        }

        public async Task CompleteAsync()
        {
            try
            {
                await _inner.CompleteAsync();
            }
            catch
            {
                _state.MarkUncertain();
                throw;
            }
        }

        /// <summary>
        /// Tracks advanced writer bytes and uncertain flush/write outcomes for failure handling, while delegating normal writes
        /// and replacing completion exceptions that could expose sensitive details to server logging.
        /// </summary>
        private sealed class GuardedWriter(PipeWriter aInner, ResponseLifecycleState aState) : PipeWriter
        {
            // Underlying UnflushedBytes behavior is preserved as-is (including NotSupportedException): no synthetic claim about server bytes.
            public override bool CanGetUnflushedBytes => aInner.CanGetUnflushedBytes;

            public override long UnflushedBytes => aInner.UnflushedBytes;

            public override Memory<byte> GetMemory(int aSizeHint = 0) => aInner.GetMemory(aSizeHint);

            public override Span<byte> GetSpan(int aSizeHint = 0) => aInner.GetSpan(aSizeHint);

            public override void Advance(int aBytes)
            {
                aInner.Advance(aBytes);
                aState.AddPending(aBytes); // counted only after the underlying Advance succeeded
            }

            public override async ValueTask<FlushResult> FlushAsync(CancellationToken aCancellationToken = default)
            {
                FlushResult lResult;
                try
                {
                    lResult = await aInner.FlushAsync(aCancellationToken);
                }
                catch
                {
                    aState.MarkUncertain();
                    throw;
                }

                Observe(lResult, 0);
                return lResult;
            }

            public override async ValueTask<FlushResult> WriteAsync(ReadOnlyMemory<byte> aSource, CancellationToken aCancellationToken = default)
            {
                FlushResult lResult;
                try
                {
                    lResult = await aInner.WriteAsync(aSource, aCancellationToken);
                }
                catch
                {
                    aState.AddPending(aSource.Length); // implicit advance whose fate is unknown
                    aState.MarkUncertain();
                    throw;
                }

                Observe(lResult, aSource.Length);
                return lResult;
            }

            public override void CancelPendingFlush() => aInner.CancelPendingFlush();

            public override void Complete(Exception? aException = null)
            {
                MarkIfFaulted(aException);
                aInner.Complete(SafeFailures.Sanitize(aException));
            }

            public override ValueTask CompleteAsync(Exception? aException = null)
            {
                MarkIfFaulted(aException);
                return aInner.CompleteAsync(SafeFailures.Sanitize(aException));
            }

            private void MarkIfFaulted(Exception? aException)
            {
                if (aException is not null)
                {
                    aState.MarkUncertain();
                }
            }

            /// <summary>
            /// A clean, non-cancelled, non-completed flush hands the bytes to the server and clears them. A cancelled result keeps them pending and uncertain;
            /// a completed result is not proof of a safe discard, so it never clears pending bytes (it only marks uncertainty, which affects failure handling).
            /// </summary>
            private void Observe(FlushResult aResult, int aImplicitlyAdvanced)
            {
                if (aResult.IsCanceled || aResult.IsCompleted)
                {
                    aState.AddPending(aImplicitlyAdvanced);
                    aState.MarkUncertain();
                    return;
                }

                aState.ClearPending();
            }
        }
    }
}
