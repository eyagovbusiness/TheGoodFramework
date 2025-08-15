using System.Security.Claims;

namespace TGF.CA.Infrastructure.Identity.Authorization {
    /// <summary>
    /// Provides a comparer for <see cref="Claim"/> objects.
    /// </summary>
    public class ClaimComparer : IEqualityComparer<Claim> {
        public bool Equals(Claim? x, Claim? y)
        => x?.Type == y?.Type && x?.Value == y?.Value;

        public int GetHashCode(Claim obj)
        => HashCode.Combine(obj.Type, obj.Value);
    }
}
