using Slascone.Client;
using TGF.CA.Infrastructure.Licensing.Slascone.Helpers;

namespace TGF.CA.Infrastructure.Licensing.Tests.Unit.Infrastructure;

public class LicenseSessionValidityTests {
    [Theory]
    [InlineData(true, 1, true)]
    [InlineData(true, 0, false)]
    [InlineData(true, -1, false)]
    [InlineData(false, 1, false)]
    [InlineData(false, 0, false)]
    [InlineData(false, -1, false)]
    public void IsValid_RequiresProviderApprovalAndStrictlyFutureExpiration(bool isValid, int expirationTicks, bool expected) {
        var now = new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
        var session = new SessionStatusDto {
            Is_session_valid = isValid,
            Session_valid_until = now.AddTicks(expirationTicks),
        };

        Assert.Equal(expected, LicenseSessionValidity.IsValid(session, now));
    }

    [Fact]
    public void IsValid_MissingSessionIsInvalid()
        => Assert.False(LicenseSessionValidity.IsValid(null, DateTimeOffset.UtcNow));
}
