using System;
using NLog;

namespace Ylp.GitDb.Core
{
    public interface ILogger
    {
        void Debug(string format, params object[] args);
        void Debug(Exception exception, string format, params object[] args);
        void Error(string format, params object[] args);
        void Error(Exception exception, string format, params object[] args);
        void Fatal(string format, params object[] args);
        void Fatal(Exception exception, string format, params object[] args);
        void Info(string format, params object[] args);
        void Info(Exception exception, string format, params object[] args);
        void Trace(string format, params object[] args);
        void Trace(Exception exception, string format, params object[] args);
        void Warn(string format, params object[] args);
        void Warn(Exception exception, string format, params object[] args);
    }

    public class Log : Logger, ILogger
    {
        public static ILogger Create(string name) => 
            (ILogger)LogManager.GetLogger(name, typeof(Log));
    }
}