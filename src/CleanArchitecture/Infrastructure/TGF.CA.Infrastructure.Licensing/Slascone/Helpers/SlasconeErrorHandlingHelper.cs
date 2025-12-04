using Slascone.Client;
using System.Net;
using System.Runtime.CompilerServices;

namespace TGF.CA.Infrastructure.Licensing.Slascone.Helpers;

/// <summary>
/// Helper class to handle errors and standard retries while calling the SLASCONE API.
/// </summary>
internal static class SlasconeErrorHandlingHelper {
    /// <summary>
    /// Error type enum to differentiate between error categories
    /// </summary>
    public enum ErrorType {
        /// <summary>
        /// No error occurred
        /// </summary>
        None,

        /// <summary>
        /// Business logic or validation error (e.g., invalid input, conflict)
        /// </summary>
        Functional,

        /// <summary>
        /// Technical error in the system (e.g., internal server error) or network or connectivity issue
        /// </summary>
        Network
    }

    /// <summary>
    /// Wait time between retries
    /// </summary>
    private static readonly TimeSpan RetryWaitTime = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Do max 1 retry
    /// </summary>
    private const int MaxRetryCount = 1;

    /// <summary>
    /// Call a SLASCONE API endpoint with standard retry logic
    /// </summary>
    /// <typeparam name="TIn">Type of input argument</typeparam>
    /// <typeparam name="TOut">Type of result</typeparam>
    /// <param name="func">SLASCONE API endpoint call</param>
    /// <param name="argument">Input argument</param>
    /// <param name="callerMemberName">Caller member name for error message if necessary</param>
    /// <returns></returns>
    internal static async Task<(TOut data, ErrorType errorType, ErrorResultObjects error, string message)> Execute<TIn, TOut>(
        Func<TIn, Task<ApiResponse<TOut>>> func,
        TIn argument,
        [CallerMemberName] string callerMemberName = "")
        where TOut : class {
        string errorMessage = null!;
        ApiResponse<TOut> result = null!;

        try {
            int retryCountdown = MaxRetryCount;

            while (0 <= retryCountdown) {
                // Call the SLASCONE API endpoint
                result = await func.Invoke(argument).ConfigureAwait(false);

                if ((int)HttpStatusCode.OK == result.StatusCode) {
                    // Success
                    return (result.Result, ErrorType.None, null!, null!);
                } else if ((int)HttpStatusCode.Conflict == result.StatusCode) {
                    // Functional error: Return error message
                    return (null!, ErrorType.Functional, result.Error, $"{callerMemberName} received an error: {result.Error.Message} (Id: {result.Error.Id})");
                } else if ((int)HttpStatusCode.Unauthorized == result.StatusCode
                            || (int)HttpStatusCode.Forbidden == result.StatusCode) {
                    // Unauthorized or forbidden: Return error message
                    return (null!, ErrorType.Network, null!, $"{callerMemberName} received an error: Not authorized");
                }

                // Transient error: Wait 15 seconds and try again
                --retryCountdown;
                if (0 <= retryCountdown) {
                    await Task.Delay(RetryWaitTime).ConfigureAwait(false);
                }
            }

            errorMessage = $"{callerMemberName} received an error after {MaxRetryCount} retries:  {result.StatusCode} (Id: {result.Message})";
        }
        catch (Exception ex) {
            errorMessage = $"{callerMemberName} threw an exception: {ex.Message}";
        }

        return (result.Result, ErrorType.Network, result.Error, errorMessage);
    }
}

