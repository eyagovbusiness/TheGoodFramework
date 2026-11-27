using Slascone.Client;

namespace TGF.CA.Infrastructure.Licensing.Slascone.Helpers;

internal static class LicenseSessionValidity {
    internal static bool IsValid(SessionStatusDto? session, DateTimeOffset now)
        => session is { Is_session_valid: true } && session.Session_valid_until > now;
}
