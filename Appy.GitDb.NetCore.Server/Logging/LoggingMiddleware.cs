using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using NLog;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Appy.GitDb.NetCore.Server.Logging
{
    public class LoggingMiddleware
    {
        public static Logger Logger;
        readonly RequestDelegate _next;

        public LoggingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var correlationid = context.Request.Headers.TryGetValue("X-FORWARD-CORRELATIONID", out var correlationHeader)
                ? correlationHeader.ToString()
                : Guid.NewGuid().ToString("N");

            LogContext.SetCorrelationId(correlationid);

            context.Response.OnStarting(state =>
            {
                var ctx = state as HttpContext;
                if (ctx == null)
                    return Task.FromResult(0);

                if (!ctx.Response.Headers.ContainsKey("correlationid"))
                    ctx.Response.Headers.Add("correlationid", new[] { correlationid });

                return Task.FromResult(0);
            }, context);

            var watch = new Stopwatch();
            watch.Start();

            await _next.Invoke(context);

            watch.Stop();

            var identity = context.User?.Identity;
            var userName = identity?.Name;
            var user = !string.IsNullOrEmpty(userName)
                ? userName
                : "(anonymous)";

            var exception = context.Items.ContainsKey("exception")
                ? (Exception)context.Items["exception"]
                : null;

            LogLevel level;
            if (exception != null || context.Response.StatusCode >= 500)
                level = LogLevel.Error;
            else if (context.Response.StatusCode >= 400)
                level = LogLevel.Warn;
            else
                level = LogLevel.Trace;

            var requestUri = mapToUri(context.Request);

            Logger.Log(new LogEventInfo
            {
                Level = level,
                Exception = exception,
                LoggerName = "http-log",
                Properties =
                {
                    { "user", user},
                    { "method", context.Request.Method},
                    { "url", requestUri.AbsolutePath},
                    { "querystring", requestUri.Query},
                    { "request-headers", toLogString(context.Request.Headers)},
                    { "response-headers", toLogString(context.Response.Headers) },
                    { "statuscode", context.Response.StatusCode },
                    { "reason", context.Response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase },
                    { "duration", watch.Elapsed.TotalMilliseconds },
                    { "useragent", getValue(context.Request.Headers, "User-Agent") },
                    { "host", requestUri.Host }
                }
            });
        }

        static Uri mapToUri(HttpRequest request)
        {
            var builder = new UriBuilder();
            builder.Scheme = request.Scheme;
            builder.Host = request.Host.Host;
            if (request.Host.Port.HasValue)
                builder.Port = request.Host.Port.Value;
            builder.Path = request.Path;
            builder.Query = request.QueryString.ToUriComponent();

            return builder.Uri;
        }

        static string toLogString(IHeaderDictionary headers) =>
            string.Join("\n", headers.Select(kv => kv.Key + ": " + string.Join(" ", kv.Value)));

        static string getValue(IHeaderDictionary headers, string key) =>
            headers.ContainsKey(key)
                ? string.Join(" ", headers[key])
                : string.Empty;
    }
}