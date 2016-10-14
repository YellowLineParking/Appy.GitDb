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
                new User{ UserName = "GitAdmin", Password = "LCz8ovCZiddM4FGH1T3V", Roles = new [] { "admin","read","write" }},
                new User{ UserName = "GitReader",Password = "IUFYTF2oPuK04OfnVl5H",Roles = new [] { "read" }},
                new User{ UserName = "GitWriter", Password = "4yzvqhPkHPZbSbuGN4aQ6b",Roles = new [] { "write" }}
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
