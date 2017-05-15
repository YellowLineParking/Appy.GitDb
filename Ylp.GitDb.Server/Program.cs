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
            var gitRepoPath = ConfigurationManager.AppSettings["git.repository.path"];
            var remoteUrl = ConfigurationManager.AppSettings["remote.url"];
            var userName = ConfigurationManager.AppSettings["remote.user.name"];
            var userEmail = ConfigurationManager.AppSettings["remote.user.email"];
            var password = ConfigurationManager.AppSettings["remote.user.password"];
            var repoLog = Log.Create("git-repository");
            var serverLog = Log.Create("git-server");
            var app = App.Create(url, new LocalGitDb(gitRepoPath, repoLog, remoteUrl, userName, userEmail, password), serverLog, new List<User>
            {
                new User{ UserName = "GitAdmin", Password = "LCz8ovCZiddM4FGH1T3V", Roles = new [] { "admin","read","write" }},
                new User{ UserName = "GitReader",Password = "IUFYTF2oPuK04OfnVl5H",Roles = new [] { "read" }},
                new User{ UserName = "GitWriter", Password = "4yzvqhPkHPZbSbuGN4aQ6b",Roles = new [] { "write" }}
            });
            using (app.Start())
            {
                serverLog.Info($"Server started on {url}, with repo at {gitRepoPath}");
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
            }
        }
    }
}
