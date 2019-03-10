using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Appy.GitDb.Core.Interfaces;
using Appy.GitDb.Core.Model;
using Appy.GitDb.Local;
using Appy.GitDb.Remote;
using Appy.GitDb.Watcher;
using LibGit2Sharp;
using Xunit;
using Newtonsoft.Json.Linq;

#if NETFX
using Appy.GitDb.Server;
using Appy.GitDb.Server.Auth;
using Microsoft.Owin.Testing;
#endif

#if NETCORE
using Appy.GitDb.NetCore.Server.Auth;
using Appy.GitDb.NetCore.Server.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Appy.GitDb.NetCore.Server;
#endif

namespace Appy.GitDb.Tests.Utils
{
#if NETFX
    public abstract class WithRepo : IAsyncLifetime
    {
        protected IGitDb Subject;
        protected readonly string LocalPath = Path.GetTempPath() + Guid.NewGuid();
        protected Repository Repo;
        protected readonly Author Author = new Author("author", "author@mail.com");
        protected readonly int TransactionTimeout = 1;

        TestServer _server;
        HttpClient _client;

        protected static readonly User Admin = new User { UserName = "admin", Password = "admin", Roles = new[] { "admin", "read", "write" } };
        protected static readonly User ReadOnly = new User { UserName = "readonly", Password = "readonly", Roles = new[] { "read" } };
        protected static readonly User WriteOnly = new User { UserName = "writeonly", Password = "writeonly", Roles = new[] { "write" } };
        protected static readonly User ReadWrite = new User { UserName = "readwrite", Password = "readwrite", Roles = new[] { "read", "write" } };

        protected static readonly User None = new User { UserName = "", Password = "", Roles = new string[0] };

        readonly IEnumerable<User> _users = new List<User> { Admin, ReadOnly, WriteOnly, ReadWrite };

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
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{user.UserName}:{user.Password}")));

        public async Task InitializeAsync()
        {
            const string url = "http://localhost"; // this is a dummy url, requests are in-memory, not over the network               
            var app = App.Create(url, new LocalGitDb(LocalPath, transactionTimeout: TransactionTimeout), _users);
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
#endif

#if NETCORE

    public abstract class WithRepo : IAsyncLifetime
    {
        protected IGitDb Subject;
        protected readonly string LocalPath = Path.GetTempPath() + Guid.NewGuid();
        protected Repository Repo;
        protected readonly Author Author = new Author("author", "author@mail.com");
        protected readonly int TransactionTimeout = 1;

        TestServer _server;
        HttpClient _client;

        protected static readonly GitDbUserSetting Admin = new GitDbUserSetting { UserName = "admin", Password = "admin", Roles = new List<string> { "admin", "read", "write" } };
        protected static readonly GitDbUserSetting ReadOnly = new GitDbUserSetting { UserName = "readonly", Password = "readonly", Roles = new List<string> { "read" } };
        protected static readonly GitDbUserSetting WriteOnly = new GitDbUserSetting { UserName = "writeonly", Password = "writeonly", Roles = new List<string> { "write" } };
        protected static readonly GitDbUserSetting ReadWrite = new GitDbUserSetting { UserName = "readwrite", Password = "readwrite", Roles = new List<string> { "read", "write" } };
        protected static readonly GitDbUserSetting None = new GitDbUserSetting { UserName = "", Password = "", Roles = new List<string>() };
        readonly List<GitDbUserSetting> _users = new List<GitDbUserSetting> { Admin, ReadOnly, WriteOnly, ReadWrite};

        protected virtual Task Because() => Task.CompletedTask;

        static void deleteReadOnlyDirectory(string directory)
        {
            Directory.EnumerateDirectories(directory)
                .ForEach(deleteReadOnlyDirectory);
            Directory.EnumerateFiles(directory).Select(file => new FileInfo(file) { Attributes = FileAttributes.Normal })
                .ForEach(fi => fi.Delete());

            Directory.Delete(directory);
        }

        protected void WithUser(GitDbUserSetting user) =>
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{user.UserName}:{user.Password}")));

        static IEnumerable<KeyValuePair<string, string>> toDictionary(object o, string settingName) =>
            addPropsToDic(JObject.FromObject(o), new Dictionary<string, string>(), settingName);

        static Dictionary<string, string> addPropsToDic(JObject jo, Dictionary<string, string> dic, string prefix)
        {
            jo.Properties()
                .Aggregate(dic, (d, jt) =>
                {
                    var value = jt.Value;

                    if (value is JArray array)
                    {
                        for (var i = 0; i < array.Count; i++)
                        {
                            if (array[i].HasValues)
                                addPropsToDic((JObject)array[i], dic, $"{prefix}:{jt.Name}:{i}");                            
                            else
                                dic.Add($"{prefix}:{jt.Name}:{i}", array[i].ToString());                            
                        }

                        return dic;
                    }

                    var key = $"{prefix}:{jt.Name}";
                    if (value.HasValues)
                        return addPropsToDic((JObject)jt.Value, dic, key);

                    dic.Add(key, value.ToString());
                    return dic;
                });
            return dic;
        }

        public async Task InitializeAsync()
        {
            // api json options
            var settings = new GitDbSettings
            {
                GitHomePath = LocalPath,
                Users = _users,
                TransactionsTimeout = TransactionTimeout,
                Remote = new GitDbRemoteSettings()
            };              
            var settingsDic = toDictionary(settings, "GitDb");

            // host builder
            var hostBuilder = new WebHostBuilder()            
                .ConfigureAppConfiguration((hostingContext, config) => config
                    .AddInMemoryCollection(settingsDic)) // by memory
                .UseStartup<Startup>();

            // test server
            _server = new TestServer(hostBuilder);
            _client = _server.CreateClient();
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // subjects
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
#endif
}
