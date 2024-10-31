namespace TGF.Common.Patterns.StrategyPattern.PartitionStrategy {
    public interface IPartitionStrategy<T> {
        IEnumerable<IEnumerable<T>> Split(IEnumerable<T> source, int partitionSize, bool parallelize);
    }
}
