namespace TGF.Common.Patterns.StrategyPattern.PartitionStrategy {
    /// <summary>
    /// Implements a partition strategy that distributes elements evenly across partitions.
    /// </summary>
    public class EvenDistributionStrategy<T> : PartitionStrategyBase<T> {
        protected override IEnumerable<IEnumerable<T>> PerformSplit(IEnumerable<T> source, int partitionSize, bool parallelize) {
            //var total = source.Count();
            //var adjustedPartitionSize = partitionSize + (total % partitionSize > 0 ? 1 : 0);
            var sourceArray = source.ToArray(); // Convert to array for efficient access

            return parallelize
                ? EvenDistributionStrategy<T>.PartitionUsingArraySegment(sourceArray, partitionSize).AsParallel()
                : EvenDistributionStrategy<T>.PartitionUsingArraySegment(sourceArray, partitionSize);
        }

        /// <remarks>REMARK: This is not a pure even distribution since it has a micro-optimization to reduce the number of small "straggler" partitions, especially those with just one element. This means if there are 7 elements and the batch size is 3, it will increase the batch size to 4 having 4, 3 insrtead of 3, 3, 1. </remarks>
        //protected override IEnumerable<IEnumerable<T>> PerformSplit(IEnumerable<T> source, int partitionSize, bool parallelize) {
        //    var total = source.Count();
        //    var adjustedPartitionSize = partitionSize + (total % partitionSize > 0 ? 1 : 0);
        //    var sourceArray = source.ToArray(); // Convert to array for efficient access

        //    return parallelize
        //        ? PartitionUsingArraySegment(sourceArray, adjustedPartitionSize).AsParallel()
        //        : PartitionUsingArraySegment(sourceArray, adjustedPartitionSize);
        //}

        private static IEnumerable<IEnumerable<T>> PartitionUsingArraySegment(T[] source, int partitionSize) {
            for (var i = 0; i < source.Length; i += partitionSize) {
                yield return new ArraySegment<T>(source, i, Math.Min(partitionSize, source.Length - i));
            }
        }
    }
}
