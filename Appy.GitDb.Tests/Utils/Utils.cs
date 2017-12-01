using System;
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
    }
}
