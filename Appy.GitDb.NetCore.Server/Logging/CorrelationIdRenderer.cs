using System.Text;
using NLog;
using NLog.LayoutRenderers;
using NLog.Web.LayoutRenderers;

namespace Appy.GitDb.NetCore.Server.Logging
{
    [LayoutRenderer("correlationid")]
    public class CorrelationIdLayoutRenderer : AspNetLayoutRendererBase
    {
        protected override void DoAppend(StringBuilder builder, LogEventInfo logEvent)
        {
            builder.Append(LogContext.GetCorrelationId());
        }
    }
}
