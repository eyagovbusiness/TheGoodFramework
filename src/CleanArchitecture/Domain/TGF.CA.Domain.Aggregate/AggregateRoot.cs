using TGF.CA.Domain.Primitives;

namespace TGF.CA.Domain.Aggregate
{
    /// <summary>
    /// Represents the aggregate root with event sourcing.
    /// </summary>
    /// <typeparam name="TKey">
    /// The type of the primary key for the entity. TKey must be a value type
    /// (struct) and implement IEquatable<TKey> for efficient equality comparison.
    /// Examples of valid types include int, long, Guid, etc.
    /// </typeparam>
    public class Aggregate<TKey>
        where TKey : struct, IEquatable<TKey>
    {
        private List<AggregateChange<TKey>> _changes = [];

        //public Entity<TKey> RootEntitiy { get; internal set; }
        //public TKey Id => RootEntitiy.Id;
        public TKey Id { get; set; }

        private string AggregateType => GetType().Name;
        public int Version { get; set; } = 0;

        /// <summary>
        /// This flag is used to identify when an event is being loaded from the DB
        /// or when the event is being created as new
        /// </summary>
        private bool ReadingFromHistory { get; set; } = false;

        protected Aggregate(TKey aId)
        => Id = aId;

        internal void Initialize(TKey aId)
        {
            Id = aId;
            _changes = [];
        }

        public List<AggregateChange<TKey>> GetUncommittedChanges()
        => _changes.Where(change => change.IsNew).ToList();

        public void MarkChangesAsCommitted()
        => _changes.Clear();

        protected void ApplyChange<T>(T aEventObject)
        {
            if (aEventObject == null)
                throw new ArgumentException("You cannot pass a null object into the aggregate");

            Version++;

            var lChange = new AggregateChange<TKey>(
                aEventObject,
                Id,
                aEventObject.GetType(),
                $"{Id}:{Version}",
                Version,
                ReadingFromHistory != true
            );
            _changes.Add(lChange);

        }


        public void LoadFromHistory(IList<AggregateChange<TKey>> aHistory)
        {
            if (!aHistory.Any())
                return;

            ReadingFromHistory = true;
            foreach (var lChange in aHistory)
                ApplyChanges(lChange.Content);

            ReadingFromHistory = false;

            Version = aHistory.Last().Version;

            void ApplyChanges<T>(T aEventObject)
                => this.AsDynamic()!.Apply(aEventObject);
        }

    }
}
