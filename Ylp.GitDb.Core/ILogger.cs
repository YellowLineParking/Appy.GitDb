using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Ylp.GitDb.Core
{
    public interface ILogger
    {
        Task Log(string message);
    }

    public class Logger : ILogger
    {
        public readonly string FileName;
        static readonly object LockObj = new object();

        public Logger(string fileName)
        {
            FileName = fileName;
        }

        public Task Log(string message)
        {
            lock(LockObj)
                File.AppendAllText(FileName, $"{DateTime.Now.ToString("HH:mm:ss")}: {message}\n");

            return Task.CompletedTask;
        }
    }
}