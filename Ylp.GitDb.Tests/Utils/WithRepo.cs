using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Castle.Core.Internal;
using LibGit2Sharp;
using Microsoft.Owin.Testing;
using Moq.AutoMock;
using Ylp.GitDb.Core;
using Ylp.GitDb.Core.Interfaces;
using Ylp.GitDb.Core.Model;
using Ylp.GitDb.Local;
using Ylp.GitDb.Remote;
using Ylp.GitDb.Server;
using Ylp.GitDb.Server.Auth;

namespace Ylp.GitDb.Tests.Utils
{
    public abstract class WithRepo : IDisposable
    {
        protected readonly IGitDb Subject;
        protected readonly string LocalPath = Path.GetTempPath() + Guid.NewGuid();
        protected readonly Repository Repo;
        protected readonly Author Author = new Author("author", "author@mail.com");
        readonly TestServer _server;
        readonly HttpClient _client;

        protected static readonly User Admin = new User { UserName = "admin", Password = "admin", Roles = new[] { "admin", "read", "write" } };
        protected static readonly User ReadOnly = new User { UserName = "readonly", Password = "readonly", Roles = new[] { "read" } };
        protected static readonly User WriteOnly = new User { UserName = "writeonly", Password = "writeonly", Roles = new[] { "write" } };
        protected static readonly User ReadWrite = new User { UserName = "readwrite", Password = "readwrite", Roles = new[] { "read", "write" } };

        protected static readonly User None = new User { UserName = "", Password = "", Roles = new string[0] };

        readonly IEnumerable<User> _users = new List<User> { Admin, ReadOnly, WriteOnly, ReadWrite };

        protected void WithUser(User user) =>
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{user.UserName}:{user.Password}")));

        protected WithRepo()
        {
            const string url = "http://localhost"; // this is a dummy url, requests are in-memory, not over the network
            var app = App.Create(url, new LocalGitDb(LocalPath, new AutoMocker().Get<ILogger>()), new AutoMocker().Get<ILogger>(), _users);
            _server = TestServer.Create(app.Configuration);
            _client = _server.HttpClient;
            WithUser(Admin);
            Subject = new RemoteGitDb(_client, url);
            Repo = new Repository(LocalPath);
            Because().Wait();
        }

        protected virtual Task Because() => Task.CompletedTask;

        static void deleteReadOnlyDirectory(string directory)
        {
            Directory.EnumerateDirectories(directory)
                .ForEach(deleteReadOnlyDirectory);
            Directory.EnumerateFiles(directory).Select(file => new FileInfo(file) { Attributes = FileAttributes.Normal })
                .ForEach(fi => fi.Delete());

            Directory.Delete(directory);
        }
        public void Dispose()
        {
            _server.Dispose();
            Subject.Dispose();
            Repo.Dispose();
            deleteReadOnlyDirectory(LocalPath);
        }
    }
}