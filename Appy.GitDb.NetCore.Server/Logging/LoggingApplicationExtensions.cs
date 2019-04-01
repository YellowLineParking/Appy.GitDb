using Microsoft.AspNetCore.Builder;

namespace Appy.GitDb.NetCore.Server.Logging
{
    public static class LoggingApplicationExtensions
    {
        public static IApplicationBuilder UseAppyLogging(this IApplicationBuilder app) =>
            app.UseMiddleware<AppyLoggingMiddleware>();
    }
}