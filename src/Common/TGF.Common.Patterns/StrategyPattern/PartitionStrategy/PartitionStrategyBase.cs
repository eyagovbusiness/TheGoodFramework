using System.Collections.Concurrent;

namespace TGF.Common.Patterns.StrategyPattern.PartitionStrategy {
    public abstract class PartitionStrategyBase<T> : IPartitionStrategy<T> {
        // Optional cache for partition results
        private static readonly ConcurrentDictionary<(Type, int, bool), IEnumerable<IEnumerable<T>>> _partitionCache = new();

        public IEnumerable<IEnumerable<T>> Split(IEnumerable<T> source, int partitionSize, bool parallelize) {
            if (partitionSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(partitionSize), "Partition size must be greater than zero.");

            // Check cache
            if (_partitionCache.TryGetValue((typeof(T), partitionSize, parallelize), out var cachedResult)) {
                return cachedResult;
            }

            // Perform the split using the concrete strategy implementation
            var partitions = PerformSplit(source, partitionSize, parallelize);

            // Cache the result
            _partitionCache[(typeof(T), partitionSize, parallelize)] = partitions;

            return partitions;
        }

        // Abstract method for concrete strategies to implement
        protected abstract IEnumerable<IEnumerable<T>> PerformSplit(IEnumerable<T> source, int partitionSize, bool parallelize);
    }
}
