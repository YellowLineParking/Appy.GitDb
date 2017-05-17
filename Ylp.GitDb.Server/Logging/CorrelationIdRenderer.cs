using System.Text;
using NLog;
using NLog.LayoutRenderers;

namespace Ylp.GitDb.Server.Logging
{
    [LayoutRenderer("correlationid")]
    public class CorrelationIdLayoutRenderer : LayoutRenderer
    {
        protected override void Append(StringBuilder builder, LogEventInfo logEvent)
        {
            builder.Append(LogContext.GetCorrelationId());
        }
    }
}
