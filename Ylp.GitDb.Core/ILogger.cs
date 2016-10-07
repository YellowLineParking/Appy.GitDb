using System;
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

        public Logger(string fileName)
        {
            FileName = fileName;
        }

        public Task Log(string message)
        {
            File.AppendAllText(FileName, $"{DateTime.Now.ToString("HH:mm:ss")}: {message}\n");
            return Task.CompletedTask;
        }
    }
}