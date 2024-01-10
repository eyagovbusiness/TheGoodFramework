
namespace TGF.CA.Domain.Events
{
    /// <summary>
    /// Interface to support applying a specific <see cref="IDomainEvent"/> on this domain Entity.
    /// </summary>
    /// <typeparam name="TDomainEvent"></typeparam>
    public interface IApplyEvent<TDomainEvent>
        where TDomainEvent : IDomainEvent
    {
        /// <summary>
        /// Apply a <see cref="IDomainEvent"/> on this Entity.
        /// </summary>
        /// <param name="aDomainEvent">The domain event to apply.</param>
        void ApplyEvent(TDomainEvent aDomainEvent);
    }
}
