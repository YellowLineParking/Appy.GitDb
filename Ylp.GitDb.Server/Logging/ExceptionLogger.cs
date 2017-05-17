using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.ExceptionHandling;

namespace Ylp.GitDb.Server.Logging
{
    public class ExceptionLogger : IExceptionLogger
    {
        public Task LogAsync(ExceptionLoggerContext context, CancellationToken cancellationToken)
        {
            context.Request.GetOwinContext().Set("exception", context.Exception);
            return Task.CompletedTask;
        }
    }
}
