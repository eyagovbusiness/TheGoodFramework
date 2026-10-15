using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace TGF.CA.Presentation.Middleware
{
    /// <summary>
    /// Prevents downstream response callbacks from recreating failed-response headers during safe error handling,
    /// and prevents raw callback exceptions from escaping to server logging.
    /// </summary>
    /// <remarks>
    /// Clearing response headers does not remove OnStarting callbacks: they can run when the replacement 500 is written
    /// and add the failed request's values again. This request-local delegate registers each downstream callback immediately
    /// on the original feature, preserving normal server order and timing, but skips its OnStarting work once safe-error
    /// preparation or abort begins. OnCompleted cleanup still runs, with escaping exceptions replaced by fixed safe failures.
    /// Status, headers, body and HasStarted are forwarded without owning storage. Callbacks registered before this boundary
    /// are outside its ownership and are neither discoverable nor removable by this guard.
    /// </remarks>
    internal sealed class GuardedResponseFeature(IHttpResponseFeature aInner, ResponseLifecycleState aState) : IHttpResponseFeature
    {
        public int StatusCode { get => aInner.StatusCode; set => aInner.StatusCode = value; }

        public string? ReasonPhrase { get => aInner.ReasonPhrase; set => aInner.ReasonPhrase = value; }

        public IHeaderDictionary Headers { get => aInner.Headers; set => aInner.Headers = value; }

#pragma warning disable CS0618 // The interface member is obsolete but must still be forwarded unchanged (no body storage is owned here).
        public Stream Body { get => aInner.Body; set => aInner.Body = value; }
#pragma warning restore CS0618

        public bool HasStarted => aInner.HasStarted;

        public void OnStarting(Func<object, Task> aCallback, object aState2)
            => aInner.OnStarting(InvokeStartingAsync, new Registration(aCallback, aState2, aState));

        public void OnCompleted(Func<object, Task> aCallback, object aState2)
            => aInner.OnCompleted(InvokeCompletedAsync, new Registration(aCallback, aState2, aState));

        private static async Task InvokeStartingAsync(object aRegistration)
        {
            var lRegistration = (Registration)aRegistration;
            if (lRegistration.Lifecycle.Phase != ResponsePhase.Normal)
            {
                return; // owned downstream callback skipped: it must not recreate values of the failed response
            }

            try
            {
                await lRegistration.Callback(lRegistration.UserState);
            }
            catch (OperationCanceledException lCancelled)
            {
                lRegistration.Lifecycle.MarkCallbackFailure();
                throw SafeFailures.Cancelled(lCancelled.CancellationToken);
            }
            catch (Exception)
            {
                lRegistration.Lifecycle.MarkCallbackFailure();
                throw SafeFailures.Failure();
            }
        }

        private static async Task InvokeCompletedAsync(object aRegistration)
        {
            var lRegistration = (Registration)aRegistration;
            if (Interlocked.Exchange(ref lRegistration.Invoked, 1) != 0)
            {
                return;
            }

            // Cleanup always runs, even after an abort; only an escaping exception is replaced by a fixed one.
            try
            {
                await lRegistration.Callback(lRegistration.UserState);
            }
            catch (OperationCanceledException lCancelled)
            {
                throw SafeFailures.Cancelled(lCancelled.CancellationToken);
            }
            catch (Exception)
            {
                throw SafeFailures.Failure();
            }
        }

        private sealed class Registration(Func<object, Task> aCallback, object aUserState, ResponseLifecycleState aLifecycle)
        {
            public Func<object, Task> Callback { get; } = aCallback;

            public object UserState { get; } = aUserState;

            public ResponseLifecycleState Lifecycle { get; } = aLifecycle;

            public int Invoked;
        }
    }
}
