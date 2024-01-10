
namespace TGF.CA.Domain.Events
{
    public interface IApplyEvent<TDomainEvent>
        where TDomainEvent : IDomainEvent
    {
        void ApplyEvent(TDomainEvent aDomainEvent);
    }
}
