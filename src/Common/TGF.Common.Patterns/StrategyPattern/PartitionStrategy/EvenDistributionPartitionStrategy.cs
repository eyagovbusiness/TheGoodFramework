namespace TGF.Common.Patterns.StrategyPattern.PartitionStrategy {
    public class EvenDistributionStrategy<T> : PartitionStrategyBase<T> {
        protected override IEnumerable<IEnumerable<T>> PerformSplit(IEnumerable<T> source, int partitionSize, bool parallelize) {
            int total = source.Count();
            int adjustedPartitionSize = partitionSize + (total % partitionSize > 0 ? 1 : 0);
            var sourceArray = source.ToArray(); // Convert to array for efficient access

            return parallelize
                ? PartitionUsingArraySegment(sourceArray, adjustedPartitionSize).AsParallel()
                : PartitionUsingArraySegment(sourceArray, adjustedPartitionSize);
        }

        private IEnumerable<IEnumerable<T>> PartitionUsingArraySegment(T[] source, int partitionSize) {
            for (int i = 0; i < source.Length; i += partitionSize) {
                yield return new ArraySegment<T>(source, i, Math.Min(partitionSize, source.Length - i));
            }
        }
    }
}
