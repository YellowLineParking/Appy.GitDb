using System;
using System.Threading;
using NLog;

namespace Appy.GitDb.Server.Logging
{
    public class LogContext : IDisposable
    {
        const string CorrelationIdKey = "lctx:correlationid";
        static readonly AsyncLocal<string> _correlationId = new();

        public string CorrelationId { get; }

        public LogContext(string correlationId = null)
        {
            CorrelationId = correlationId ?? Guid.NewGuid().ToString("N");
            SetCorrelationId(CorrelationId);
        }

        public void Dispose() =>
            Clear();

        public static void SetCorrelationId(string correlationId)
        {
            _correlationId.Value = correlationId;
            MappedDiagnosticsLogicalContext.Set(CorrelationIdKey, correlationId);
        }

        public static void Clear()
        {
            _correlationId.Value = null;
            MappedDiagnosticsLogicalContext.Clear();
        }

        public static string GetCorrelationId() =>
            _correlationId.Value ?? MappedDiagnosticsLogicalContext.Get(CorrelationIdKey);
    }
}
