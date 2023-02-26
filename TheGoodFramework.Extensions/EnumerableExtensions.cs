using System.Collections.Concurrent;

namespace TheGoodFramework.Common.Extensions
{
    public static class EnumerableExtensions
    {

        /// <summary>
        /// Check whether the IEnumerable has any items.
        /// </summary>
        /// <typeparam name="T">Type of elements in IEnumerable</typeparam>
        /// <param name="aEnumerable">Source IEnumerable</param>
        /// <returns>Returns true when <paramref name="aEnumerable"/> is empty.</returns>
        public static bool IsEmpty<T>(this IEnumerable<T> aEnumerable)
        {
            return !aEnumerable.Any();
        }

        /// <summary>
        /// gets this list materialized into Array if it was not, or return the materialized list itself in case it was.
        /// </summary>
        /// <typeparam name="T">Type of the Enumerable</typeparam>
        /// <param name="source">Enumeable source to check if it is materialized or not</param>
        /// <returns>Array or ICollection<T> depending if the aSource was materialized or not.</returns>
        public static ICollection<T> MaterializeToArray<T>(this IEnumerable<T> aSource)
        {
            return aSource as ICollection<T> ?? aSource.ToArray();
        }

        /// <summary>
        /// Check whether the IEnumerable has any items or is null.
        /// </summary>
        /// <typeparam name="T">Type of elements in IEnumerable</typeparam>
        /// <param name="aEnumerable"></param>
        /// <returns>Returns true when <paramref name="aEnumerable"/> is null or empty.</returns>
        public static bool IsNullOrEmpty<T>(this IEnumerable<T> aEnumerable)
        {
            return aEnumerable == null || !aEnumerable.Any();
        }

        /// <summary>
        /// For each item of list executes defined Action.
        /// </summary>
        /// <typeparam name="T">Type of list item</typeparam>
        /// <param name="aList">List of items</param>
        /// <param name="aAction">Action for each item</param>
        public static void ForEach<T>(this IEnumerable<T> aList, Action<T> aAction)
        {
            foreach (T aItem in aList)
                aAction(aItem);
        }

        /// <summary>
        /// Async version of the Parallel.ForEach() method as IEnumerable extension to speed-up asynchronous operations respect to the default parallel loop
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="aSourceList"></param>
        /// <param name="aDegreeOfParallelization"></param>
        /// <param name="aBody">Async function to loop.</param>
        /// <param name="aCancellationToken">Cancellation token that throws <see cref="OperationCanceledException"/> exception breaking the loop.</param>
        /// <returns>Task related to this operation(await this method, avoid using Task.Result).</returns>
        public static Task ParallelForEachAsync<T>(
            this IEnumerable<T> aSourceList,
            byte aDegreeOfParallelization,
            Func<T, Task> aBody,
            CancellationToken aCancellationToken = default
            )
        {
            //Creates a method that rturns a task to perform from a given partition enumerator asynchronous execution of the ForEach body function.
            async Task AwaitPartition(IEnumerator<T> lPartition)
            {
                using (lPartition)
                    while (lPartition.MoveNext())
                    {
                        aCancellationToken.ThrowIfCancellationRequested();
                        await aBody(lPartition.Current);
                    }


            }
            //Creates devides the list into aDegreeOfParallelization and run in parallel AwaitPartition method awaiting for all of them to finish before returning
            return Task.WhenAll(
                        Partitioner.Create(aSourceList)
                                   .GetPartitions(aDegreeOfParallelization)
                                   .AsParallel()
                                   .Select(AwaitPartition)
                        );
        }

    }
}
