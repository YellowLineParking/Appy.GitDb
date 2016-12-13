using System.Threading.Tasks;
using Microsoft.Owin;
using Ylp.GitDb.Core;

namespace Ylp.GitDb.Server
{
    public class LoggingMiddleware : OwinMiddleware
    {
        public static ILogger Logger;
        public LoggingMiddleware(OwinMiddleware next) : base(next){ }
        public override async Task Invoke(IOwinContext context)
        {
            var url = context.Request.Uri;
            var method = context.Request.Method;
            await Next.Invoke(context);
            Logger.Trace($"REQUEST: {method} {url} => {context.Response.StatusCode}");
        }
    }
}