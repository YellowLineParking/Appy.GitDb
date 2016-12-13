using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.ExceptionHandling;
using Ylp.GitDb.Core;

namespace Ylp.GitDb.Server
{
    public class ExceptionLogger : IExceptionLogger
    {
        readonly ILogger _logger;

        public ExceptionLogger(ILogger logger)
        {
            _logger = logger;
        }
        public async Task LogAsync(ExceptionLoggerContext context, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                _logger.Error(context.Exception, "Unhandled exception");
            });
        }
    }
}
