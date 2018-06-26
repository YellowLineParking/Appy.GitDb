using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Appy.GitDb.Tests.Utils
{
    static class Utils
    {
        public static T Catch<T>(Action action) where T : Exception
        {
            try
            {
                action();
                return null;
            }
            catch (Exception ex)
            {
                var exception = ex as T;
                if (exception != null)
                    return exception;
                throw;
            }
        }

        public static async Task<T> Catch<T>(Func<Task> action) where T : Exception
        {
            try
            {
                await action();
                return null;
            }
            catch (Exception ex)
            {
                var exception = ex as T;
                if (exception != null)
                    return exception;
                throw;
            }
        }

        public static void ForEach<T>(this IEnumerable<T> collection, Action<T, int> action)
        {
            var index = 0;
            foreach (var item in collection)
                action(item, index++);
        }
    }
}
