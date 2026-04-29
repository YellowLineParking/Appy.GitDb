using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NLog;

namespace Appy.GitDb.Server.Logging
{
    public class LoggingMiddleware : IMiddleware
    {
        public static Logger Logger = LogManager.GetLogger("server-log");

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var correlationId = context.Request.Headers["X-FORWARD-CORRELATIONID"].FirstOrDefault()
                                ?? Guid.NewGuid().ToString("N");

            LogContext.SetCorrelationId(correlationId);
            context.Response.Headers["correlationid"] = correlationId;

            var watch = Stopwatch.StartNew();
            Exception caughtException = null;

            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                caughtException = ex;
                throw;
            }
            finally
            {
                watch.Stop();
                var userName = context.User?.Identity?.Name ?? "(anonymous)";

                var level = caughtException != null || context.Response.StatusCode >= 500
                    ? LogLevel.Error
                    : context.Response.StatusCode >= 400
                        ? LogLevel.Warn
                        : LogLevel.Trace;

                Logger.Log(new LogEventInfo
                {
                    Level = level,
                    Exception = caughtException,
                    LoggerName = "http-log",
                    Properties =
                    {
                        { "user", userName },
                        { "method", context.Request.Method },
                        { "url", context.Request.Path.Value },
                        { "querystring", context.Request.QueryString.Value },
                        { "statuscode", context.Response.StatusCode },
                        { "duration", watch.Elapsed.TotalMilliseconds },
                        { "useragent", context.Request.Headers["User-Agent"].FirstOrDefault() ?? string.Empty },
                        { "host", context.Request.Host.Value }
                    }
                });
            }
        }
    }
}
