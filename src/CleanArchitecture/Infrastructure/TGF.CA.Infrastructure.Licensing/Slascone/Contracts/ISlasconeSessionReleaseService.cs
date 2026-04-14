namespace TGF.CA.Infrastructure.Licensing.Slascone.Contracts;

public interface ISlasconeSessionReleaseService {
    Task CloseSessionAsync(string clientId, string sessionId, CancellationToken cancellationToken = default);
}
