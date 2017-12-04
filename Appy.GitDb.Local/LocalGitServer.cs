using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Appy.GitDb.Core.Interfaces;

namespace Appy.GitDb.Local
{
    public class LocalGitServer : IGitServer
    {
        readonly string _remoteUrl;
        readonly string _userName;
        readonly string _userEmail;
        readonly string _password;
        readonly int _transactionTimeout;
        readonly string _basePath;
        readonly Dictionary<string, IGitDb> _databases = new Dictionary<string, IGitDb>();


        public LocalGitServer(string basePath, string remoteUrl = null, string userName = null, string userEmail = null, string password = null, int transactionTimeout = 10)
        {
            _basePath = basePath;
            _remoteUrl = remoteUrl;
            _userName = userName;
            _userEmail = userEmail;
            _password = password;
            _transactionTimeout = transactionTimeout;
        }

        string dbPath(string name) =>
            _basePath + "\\" + name;

        public Task CreateDatabase(string name)
        {
            var path = dbPath(name);
            if(Directory.Exists(path))
                throw new Exception("Path already exists");

            _databases.Add(name, new LocalGitDb(_basePath + "\\" + name, _remoteUrl, _userName, _userEmail, _password, _transactionTimeout));
            return Task.CompletedTask;
        }

        static void deleteReadOnlyDirectory(string directory)
        {
            foreach(var subdir in Directory.EnumerateDirectories(directory))
                deleteReadOnlyDirectory(subdir);
            
            foreach(var fi in Directory.EnumerateFiles(directory).Select(file => new FileInfo(file) { Attributes = FileAttributes.Normal }))
                fi.Delete();

            Directory.Delete(directory);
        }

        public Task DeleteDatabase(string name)
        {
            var path = dbPath(name);
            if(Directory.Exists(path))
                deleteReadOnlyDirectory(path);

            if(_databases.ContainsKey(name))
                _databases.Remove(name);

            return Task.CompletedTask;
        }


        public Task<IGitDb> GetDatabase(string name)
        {
            var path = dbPath(name);
            if(!Directory.Exists(path))
                throw new Exception("Repository does not exist");

            if (!_databases.ContainsKey(name))
                _databases.Add(name, new LocalGitDb(_basePath + "\\" + name, _remoteUrl, _userName, _userEmail, _password, _transactionTimeout));

            return Task.FromResult(_databases[name]);
        }

        public Task<List<string>> GetDatabases() =>
            Task.FromResult(Directory.GetDirectories(_basePath).Select(Path.GetFileName).ToList());
    }
}
