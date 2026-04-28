using System.Collections.Generic;
using Appy.GitDb.Local;
using Appy.GitDb.Server.Auth;
using Appy.GitDb.Server.Logging;
using Microsoft.Extensions.Configuration;

namespace Appy.GitDb.Server
{
    class Program
    {
        public static void Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            var url = config["server.url"] ?? "http://+:9500";
            var gitRepoPath = config["git.repository.path"];
            var remoteName = config["remote.name"];
            var remoteUrl = config["remote.url"];
            var userName = config["remote.user.name"];
            var userEmail = config["remote.user.email"];
            var password = config["remote.user.password"];
            var remoteBranchSync = config["remote.branch.sync"];
            if (!int.TryParse(config["transactions.timeout"], out int transactionTimeout))
                transactionTimeout = 10;

            var app = App.Create(url, new LocalGitDb(gitRepoPath, remoteName, remoteUrl, userName, userEmail, password, remoteBranchSync, transactionTimeout), new List<User>
            {
                new User { UserName = "GitAdmin",  Password = config["GitAdmin"],  Roles = new[] { "admin", "read", "write" } },
                new User { UserName = "GitReader", Password = config["GitAdmin"],  Roles = new[] { "read" } },
                new User { UserName = "GitWriter", Password = config["GitWriter"], Roles = new[] { "write" } }
            });

            app.Run();
        }
    }
}
