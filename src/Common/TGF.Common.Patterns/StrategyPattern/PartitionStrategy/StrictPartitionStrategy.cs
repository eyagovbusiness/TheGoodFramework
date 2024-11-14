namespace TGF.Common.Patterns.StrategyPattern.PartitionStrategy {
    public class StrictPartitionStrategy<T> : PartitionStrategyBase<T> {
        protected override IEnumerable<IEnumerable<T>> PerformSplit(IEnumerable<T> source, int partitionSize, bool parallelize) {
            return parallelize
                ? DefaultPartitionParallel(source, partitionSize)
                : DefaultPartition(source, partitionSize);
        }

        private static IEnumerable<IEnumerable<T>> DefaultPartition(IEnumerable<T> source, int partitionSize) {
            using var enumerator = source.GetEnumerator();
            while (enumerator.MoveNext()) {
                yield return YieldPartition(enumerator, partitionSize);
            }
        }

        private static IEnumerable<IEnumerable<T>> DefaultPartitionParallel(IEnumerable<T> source, int partitionSize) {
            return source.AsParallel()
                .Select((item, index) => new { item, index })
                .GroupBy(x => x.index / partitionSize)
                .Select(group => group.Select(x => x.item));
        }

        private static IEnumerable<T> YieldPartition(IEnumerator<T> enumerator, int size) {
            int count = 0;
            do {
                yield return enumerator.Current;
                count++;
            } while (count < size && enumerator.MoveNext());
        }
    }
}
