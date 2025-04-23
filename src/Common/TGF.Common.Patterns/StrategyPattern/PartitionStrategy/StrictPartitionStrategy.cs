namespace TGF.Common.Patterns.StrategyPattern.PartitionStrategy {
    /// <summary>
    /// Implements a partition strategy that distributes elements strictly according to the specified partition size.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class StrictPartitionStrategy<T> : PartitionStrategyBase<T> {
        protected override IEnumerable<IEnumerable<T>> PerformSplit(IEnumerable<T> source, int partitionSize, bool parallelize) => parallelize
                ? DefaultPartitionParallel(source, partitionSize)
                : DefaultPartition(source, partitionSize);

        private static IEnumerable<IEnumerable<T>> DefaultPartition(IEnumerable<T> source, int partitionSize) {
            using var enumerator = source.GetEnumerator();
            while (enumerator.MoveNext()) {
                yield return YieldPartition(enumerator, partitionSize);
            }
        }

        private static ParallelQuery<IEnumerable<T>> DefaultPartitionParallel(IEnumerable<T> source, int partitionSize) => source.AsParallel()
                .Select((item, index) => new { item, index })
                .GroupBy(x => x.index / partitionSize)
                .Select(group => group.Select(x => x.item));

        private static IEnumerable<T> YieldPartition(IEnumerator<T> enumerator, int size) {
            var count = 0;
            do {
                yield return enumerator.Current;
                count++;
            } while (count < size && enumerator.MoveNext());
        }
    }
}
