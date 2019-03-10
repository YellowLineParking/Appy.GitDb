using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Appy.GitDb.NetCore.Server.Logging
{
    public partial class HttpGlobalExceptionFilter : IExceptionFilter
    {
        readonly IHostingEnvironment _env;
        readonly ILogger<HttpGlobalExceptionFilter> _logger;

        public HttpGlobalExceptionFilter(IHostingEnvironment env, ILogger<HttpGlobalExceptionFilter> logger)
        {
            _env = env;
            _logger = logger;
        }

        public void OnException(ExceptionContext context)
        {
            context.HttpContext.Items["exception"] = context.Exception;
            // context.ExceptionHandled = true;
        }
    }
}