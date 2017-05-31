using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Castle.Core.Internal;
using LibGit2Sharp;
using Microsoft.Owin.Testing;
using Xunit;
using Ylp.GitDb.Core.Interfaces;
using Ylp.GitDb.Core.Model;
using Ylp.GitDb.Local;
using Ylp.GitDb.Remote;
using Ylp.GitDb.Server;
using Ylp.GitDb.Server.Auth;

namespace Ylp.GitDb.Tests.Utils
{
    public abstract class WithRepo : IAsyncLifetime
    {
        protected IGitDb Subject;
        protected readonly string LocalPath = Path.GetTempPath() + Guid.NewGuid();
        protected Repository Repo;
        protected readonly Author Author = new Author("author", "author@mail.com");
        TestServer _server;
        HttpClient _client;

        protected static readonly User Admin = new User {UserName = "admin", Password = "admin", Roles = new[] {"admin", "read", "write"}};
        protected static readonly User ReadOnly = new User { UserName = "readonly", Password = "readonly", Roles = new[] { "read" } };
        protected static readonly User WriteOnly = new User { UserName = "writeonly", Password = "writeonly", Roles = new[] { "write" } };
        protected static readonly User ReadWrite = new User { UserName = "readwrite", Password = "readwrite", Roles = new[] { "read", "write" } };
        
        protected static readonly User None = new User { UserName = "", Password = "", Roles = new string[0] };

        readonly IEnumerable<User> _users = new List<User> {Admin, ReadOnly, WriteOnly, ReadWrite};

        protected virtual Task Because() => Task.CompletedTask;

        static void deleteReadOnlyDirectory(string directory)
        {
            Directory.EnumerateDirectories(directory)
                .ForEach(deleteReadOnlyDirectory);
            Directory.EnumerateFiles(directory).Select(file => new FileInfo(file) { Attributes = FileAttributes.Normal })
                .ForEach(fi => fi.Delete());

            Directory.Delete(directory);
        }

        protected void WithUser(User user) =>
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{user.UserName}:{user.Password}")));
       
        public async Task InitializeAsync()
        {
            const string url = "http://localhost"; // this is a dummy url, requests are in-memory, not over the network
            var app = App.Create(url, new LocalGitDb(LocalPath), _users);
            _server = TestServer.Create(app.Configuration);
            _client = _server.HttpClient;
            WithUser(Admin);     
            Subject = new RemoteGitDb(_client);
            Repo = new Repository(LocalPath);
            await Because();
        }

        public Task DisposeAsync()
        {
            _server.Dispose();
            Subject.Dispose();
            Repo.Dispose();
            deleteReadOnlyDirectory(LocalPath);
            return Task.CompletedTask;
        }

        // Use this method to inspect the resulting repository
        protected void MoveToNormalRepo(string baseDir)
        {
            if (Directory.Exists(baseDir))
                deleteReadOnlyDirectory(baseDir);

            Repository.Clone(LocalPath, baseDir, new CloneOptions
            {
                BranchName = "master",
                IsBare = false
            });
        }
    }
}