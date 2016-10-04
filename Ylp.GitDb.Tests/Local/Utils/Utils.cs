using System;

namespace Ylp.GitDb.Tests.Local.Utils
{
    class Utils
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
    }
}
