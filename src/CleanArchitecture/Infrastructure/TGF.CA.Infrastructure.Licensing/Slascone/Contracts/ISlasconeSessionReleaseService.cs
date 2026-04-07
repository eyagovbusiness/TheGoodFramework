namespace TGF.CA.Infrastructure.Licensing.Slascone.Contracts;

public interface ISlasconeSessionReleaseService {
    Task CloseSessionAsync(Guid clientId, string sessionId, CancellationToken cancellationToken = default);
}
