using System;
using System.Runtime.Remoting.Messaging;
using NLog;

namespace Ylp.GitDb.Server.Logging
{
    public class LogContext : IDisposable
    {
        const string CorrelationIdKey = "lctx:correlationid";

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
            CallContext.LogicalSetData(CorrelationIdKey, correlationId);
            MappedDiagnosticsLogicalContext.Set(CorrelationIdKey, correlationId);
        }

        public static void Clear()
        {
            CallContext.LogicalSetData(CorrelationIdKey, null);
            MappedDiagnosticsLogicalContext.Clear();
        }

        public static string GetCorrelationId() => get(CorrelationIdKey);

        static string get(string key) => 
            CallContext.LogicalGetData(key) as string ?? MappedDiagnosticsLogicalContext.Get(key);
    }
}
