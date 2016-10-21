using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Ylp.GitDb.Core.Interfaces;
using Ylp.GitDb.Core.Model;

namespace Ylp.GitDb.Benchmark
{
    static class Measure
    {
        static readonly Author Author = new Author("name", "email");
        

        public static double WritesPerSecond(Func<IGitDb> createDb, int batchSize, int keySize, int filesInRepo)
        {
            var db = createDb();
            var totalItems = batchSize*3;
            Console.Write($"| {batchSize.ToString().PadLeft(10)} | {keySize.ToString().PadLeft(8)} | {filesInRepo.ToString().PadLeft(15)} | ");
            var branchName = Guid.NewGuid().ToString("N");
            db.CreateBranch(new Reference {Name = branchName, Pointer = filesInRepo.ToString()});
            
            var totalTime = Utils.GenerateItems(totalItems, keySize, batchSize).Select(docs =>
                                                                                   (Func<Task>) (async () =>
                                                                                    {
                                                                                        using (var t = await db.CreateTransaction(branchName))
                                                                                        {
                                                                                            await t.AddMany(docs);
                                                                                            await t.Commit("message", Author);
                                                                                        }
                                                                                    })
                                                                                )
                                                                               .ToList()
                                                                               .Select(measurement)
                                                                               .ToList()
                                                                               .Sum()
                                                                               .TotalSeconds;
            var writesPerSecond = Math.Round(totalItems/totalTime);
            Console.WriteLine(writesPerSecond.ToString().PadLeft(13) + " |");
            db.Dispose();
            return writesPerSecond;
        }

        static TimeSpan measurement(Func<Task> action)
        {
            var watch = new Stopwatch();
            watch.Start();
            action().Wait();
            watch.Stop();
            return TimeSpan.FromTicks(watch.ElapsedTicks);
        }
    }

    public static class Extensions
    {
        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (var item in source)
                action(item);
        }
        public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> source, int size)
        {
            T[] bucket = null;
            var count = 0;

            foreach (var item in source)
            {
                if (bucket == null)
                    bucket = new T[size];

                bucket[count++] = item;
                if (count != size)
                    continue;

                yield return bucket;

                bucket = null;
                count = 0;
            }

            if (bucket != null && count > 0)
                yield return bucket.Take(count);
        }

        public static List<List<T>> ToNestedLists<T>(this IEnumerable<IEnumerable<T>> source) =>
            source.Select(s => s.ToList()).ToList();

        public static TimeSpan Sum(this IEnumerable<TimeSpan> source) =>
            source.Aggregate(new TimeSpan(0), (p, v) => p.Add(v));
    }
}
