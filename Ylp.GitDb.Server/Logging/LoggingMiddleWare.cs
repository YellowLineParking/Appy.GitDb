using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Owin;
using NLog;

namespace Ylp.GitDb.Server.Logging
{
    public class LoggingMiddleware : OwinMiddleware
    {
        public static Logger Logger;
        public LoggingMiddleware(OwinMiddleware next) : base(next){ }
        public override async Task Invoke(IOwinContext context)
        {
            var correlationid = context.Request.Headers["X-FORWARD-CORRELATIONID"] ?? Guid.NewGuid().ToString("N");

            LogContext.SetCorrelationId(correlationid);

            context.Response.OnSendingHeaders(state =>
            {
                var ctx = state as IOwinContext;
                if (ctx == null) return;
                if (!ctx.Response.Headers.ContainsKey("correlationid"))
                    ctx.Response.Headers.Add("correlationid", new[] { correlationid });
            }, context);

            var watch = new Stopwatch();
            watch.Start();

            await Next.Invoke(context);

            watch.Stop();

            var identity = context.Request.User?.Identity;
            var userName = identity?.Name;
            var user = !string.IsNullOrEmpty(userName)
                ? userName
                : "(anonymous)";

            var exception = context.Environment.ContainsKey("exception")
                ? (Exception)context.Environment["exception"]
                : null;

            LogLevel level;
            if (exception != null || context.Response.StatusCode >= 500)
                level = LogLevel.Error;
            else if (context.Response.StatusCode >= 400)
                level = LogLevel.Warn;
            else
                level = LogLevel.Trace;
           

            Logger.Log(new LogEventInfo
            {
                Level = level,
                Exception = exception,
                LoggerName = "http-log",
                Properties =
                {
                    { "user", user},
                    { "method", context.Request.Method},
                    { "url", context.Request.Uri.AbsolutePath},
                    { "querystring", context.Request.Uri.Query},
                    { "request-headers", toLogString(context.Request.Headers)},
                    { "response-headers", toLogString(context.Response.Headers) },
                    { "statuscode", context.Response.StatusCode },
                    { "reason", context.Response.ReasonPhrase },
                    { "duration", watch.Elapsed.TotalMilliseconds },
                    { "useragent", getValue(context.Request.Headers, "User-Agent") },
                    { "host", context.Request.Uri.Host }
                }
            });
        }

        static string toLogString(IHeaderDictionary headers) =>
            string.Join("\n", headers.Select(kv => kv.Key + ": " + string.Join(" ", kv.Value)));

        static string getValue(IHeaderDictionary headers, string key) =>
            headers.ContainsKey(key)
                ? string.Join(" ", headers[key])
                : string.Empty;
    }
}