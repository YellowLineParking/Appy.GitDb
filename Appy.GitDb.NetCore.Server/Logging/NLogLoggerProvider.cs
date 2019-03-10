using Microsoft.Extensions.Logging;
using System;

namespace Appy.GitDb.NetCore.Server.Logging
{
    public static class NLogLoggingBuilderExtensions
    {
        public static void AddNLogProvider(this ILoggingBuilder builder) =>         
            builder.AddProvider(new NLogLoggerProvider());        
    }

    public class NLogLoggerProvider : ILogger, ILoggerProvider
    {
        NLog.Logger _logger;

        static NLog.LogLevel getNLogLevel(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Critical: return NLog.LogLevel.Fatal;
                case LogLevel.Debug: return NLog.LogLevel.Debug;
                case LogLevel.Error: return NLog.LogLevel.Error;
                case LogLevel.Information: return NLog.LogLevel.Info;
                case LogLevel.Trace: return NLog.LogLevel.Trace;
                case LogLevel.Warning: return NLog.LogLevel.Warn;
                default: return NLog.LogLevel.Off;
            }
        }

        void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (_logger == null)
                return;

            var nlogLevel = getNLogLevel(logLevel);
            var logText = formatter(state, exception);
            _logger.Log(nlogLevel, logText);
        }

        bool ILogger.IsEnabled(LogLevel logLevel)
        {
            if (_logger == null)            
                return false;   
                        
            var nlogLevel = getNLogLevel(logLevel);
            return _logger.IsEnabled(nlogLevel);
        }

        IDisposable ILogger.BeginScope<TState>(TState state) => null;        

        ILogger ILoggerProvider.CreateLogger(string categoryName)
        {
            try
            {
                var logger = new NLogLoggerProvider
                {
                    _logger = NLog.LogManager.GetCurrentClassLogger()
                };
                return logger;
            }
            catch
            {
                // return new ConsoleLogger(); // you must implement this
                return null;
            }
        }

        public void Dispose()
        {
        }
    }
}