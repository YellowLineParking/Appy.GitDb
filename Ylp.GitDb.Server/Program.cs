using System;
using System.Configuration;
using Ylp.GitDb.Core;
using Ylp.GitDb.Local;

namespace Ylp.GitDb.Server
{
    class Program
    {
        public static void Main(string[] args)
        {
            var url = ConfigurationManager.AppSettings["server.url"];
            var serverLog = ConfigurationManager.AppSettings["server.log"];
            var gitRepoPath = ConfigurationManager.AppSettings["git.repository.path"];
            var gitLog = ConfigurationManager.AppSettings["git.log"];
            var app = App.Create(url, new LocalGitDb(gitRepoPath, new Logger(gitLog)), new Logger(serverLog));
            using (app.Start())
            {
                Console.WriteLine($"Server started on {url}");
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
            };
        }
    }
}
