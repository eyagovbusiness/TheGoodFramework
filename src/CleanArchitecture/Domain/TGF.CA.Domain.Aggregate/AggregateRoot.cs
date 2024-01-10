namespace TGF.CA.Domain.Aggregate
{
    /// <summary>
    /// Represents the aggregate root with event sourcing.
    /// </summary>
    public class AggregateRoot
    {
        private List<AggregateChange> _changes = [];
        public Guid Id { get; internal set; }

        private string AggregateType => GetType().Name;
        public int Version { get; set; } = 0;

        /// <summary>
        /// This flag is used to identify when an event is being loaded from the DB
        /// or when the event is being created as new
        /// </summary>
        private bool ReadingFromHistory { get; set; } = false;

        protected AggregateRoot(Guid aId)
        => Id = aId;

        internal void Initialize(Guid aId)
        {
            Id = aId;
            _changes = [];
        }

        public List<AggregateChange> GetUncommittedChanges()
        => _changes.Where(change => change.IsNew).ToList();

        public void MarkChangesAsCommitted()
        => _changes.Clear();

        protected void ApplyChange<T>(T aEventObject)
        {
            if (aEventObject == null)
                throw new ArgumentException("You cannot pass a null object into the aggregate");

            Version++;

            var lChange = new AggregateChange(
                aEventObject,
                Id,
                aEventObject.GetType(),
                $"{Id}:{Version}",
                Version,
                ReadingFromHistory != true
            );
            _changes.Add(lChange);

        }


        public void LoadFromHistory(IList<AggregateChange> aHistory)
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
