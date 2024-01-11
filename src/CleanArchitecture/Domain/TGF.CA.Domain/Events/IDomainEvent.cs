//using TGF.CA.Domain.Primitives;

namespace TGF.CA.Domain.Events
{
    /// <summary>
    /// Part of EventSourcing. Represents the interface for an event that is raised within the domain.
    /// Any domain event should implement this interface.
    /// </summary>
    public interface IDomainEvent
    {
    }

    /// <summary>
    /// Part of EventSourcing. Represents the interface for an event that is raised within the domain.
    /// Any domain event should implement this interface.
    /// </summary>
    /// <remarks>No use for now!!</remarks>
    //public interface IDomainEvent<TEntity>
    //where TEntity : Entity
    //{
    //}
}
