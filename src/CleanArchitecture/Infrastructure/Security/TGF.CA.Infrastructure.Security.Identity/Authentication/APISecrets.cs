namespace TGF.CA.Infrastructure.Security.Identity.Authentication
{
    public record APISecrets
    {
        public string SecretKey { get; init; } = null!;
    }
}