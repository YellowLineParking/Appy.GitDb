using System;
using System.Collections.Generic;
using System.Configuration;
using Ylp.GitDb.Core;
using Ylp.GitDb.Local;
using Ylp.GitDb.Server.Auth;

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
            var app = App.Create(url, new LocalGitDb(gitRepoPath, new Logger(gitLog)), new Logger(serverLog), new List<User>
            {
                new User{ UserName = "GitAdmin", Password = ConfigurationManager.AppSettings["GitAdmin"], Roles = new [] { "admin","read","write" }},
                new User{ UserName = "GitReader",Password = ConfigurationManager.AppSettings["GitAdmin"],Roles = new [] { "read" }},
                new User{ UserName = "GitWriter", Password = ConfigurationManager.AppSettings["GitWriter"] ,Roles = new [] { "write" }}
            });
            using (app.Start())
            {
                Console.WriteLine($"Server started on {url}");
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
            };
        }
    }
}
