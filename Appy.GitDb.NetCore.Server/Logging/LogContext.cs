using System;
using System.Collections.Concurrent;
using System.Threading;
using NLog;

namespace Appy.GitDb.NetCore.Server.Logging
{
    /// <summary>
    /// Provides a way to set contextual data that flows with the call and 
    /// async context of a test or invocation.
    /// </summary>
    public static class NetStandardCallContext
    {
        static ConcurrentDictionary<string, AsyncLocal<object>> state = new ConcurrentDictionary<string, AsyncLocal<object>>();

        /// <summary>
        /// Stores a given object and associates it with the specified name.
        /// </summary>
        /// <param name="name">The name with which to associate the new item in the call context.</param>
        /// <param name="data">The object to store in the call context.</param>
        public static void SetData(string name, object data) =>
            state.GetOrAdd(name, _ => new AsyncLocal<object>()).Value = data;

        /// <summary>
        /// Retrieves an object with the specified name from the <see cref="CallContext"/>.
        /// </summary>
        /// <param name="name">The name of the item in the call context.</param>
        /// <returns>The object in the call context associated with the specified name, or <see langword="null"/> if not found.</returns>
        public static object GetData(string name) =>
            state.TryGetValue(name, out AsyncLocal<object> data) ? data.Value : null;
    }

    public static class NetStandardCallContext<T>
    {
        static ConcurrentDictionary<string, AsyncLocal<T>> state = new ConcurrentDictionary<string, AsyncLocal<T>>();

        /// <summary>
        /// Stores a given object and associates it with the specified name.
        /// </summary>
        /// <param name="name">The name with which to associate the new item in the call context.</param>
        /// <param name="data">The object to store in the call context.</param>
        public static void SetData(string name, T data) =>
            state.GetOrAdd(name, _ => new AsyncLocal<T>()).Value = data;

        /// <summary>
        /// Retrieves an object with the specified name from the <see cref="CallContext"/>.
        /// </summary>
        /// <typeparam name="T">The type of the data being retrieved. Must match the type used when the <paramref name="name"/> was set via <see cref="SetData{T}(string, T)"/>.</typeparam>
        /// <param name="name">The name of the item in the call context.</param>
        /// <returns>The object in the call context associated with the specified name, or a default value for <typeparamref name="T"/> if none is found.</returns>
        public static T GetData(string name) =>
            state.TryGetValue(name, out AsyncLocal<T> data) ? data.Value : default(T);
    }

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
            NetStandardCallContext.SetData(CorrelationIdKey, correlationId);
            MappedDiagnosticsLogicalContext.Set(CorrelationIdKey, correlationId);
        }

        public static void Clear()
        {
            NetStandardCallContext.SetData(CorrelationIdKey, null);
            MappedDiagnosticsLogicalContext.Clear();
        }

        public static string GetCorrelationId() => get(CorrelationIdKey);

        static string get(string key) =>
            NetStandardCallContext.GetData(key) as string ?? MappedDiagnosticsLogicalContext.Get(key);
    }
}
