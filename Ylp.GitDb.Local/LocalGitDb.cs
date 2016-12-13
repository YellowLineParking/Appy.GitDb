using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Newtonsoft.Json;
using Ylp.GitDb.Core;
using Ylp.GitDb.Core.Interfaces;
using Ylp.GitDb.Core.Model;
using Reference = Ylp.GitDb.Core.Model.Reference;


namespace Ylp.GitDb.Local
{
    public class LocalGitDb : IGitDb
    {
        readonly ILogger _logger;
        readonly string _remoteUrl;
        readonly string _userName;
        readonly string _userEmail;
        readonly string _password;
        Repository _repo;
        readonly List<string> _branchesWithTransaction = new List<string>();
        readonly Dictionary<string, object> _branchLocks;
        readonly string _path;
        readonly PushOptions _pushOptions;
        readonly PullOptions _pullOptions;
        readonly CloneOptions _cloneOptions;

        public LocalGitDb(string path, ILogger logger, string remoteUrl = null, string userName = null, string userEmail = null, string password = null)
        {
            _logger = logger;
            _remoteUrl = string.IsNullOrEmpty(remoteUrl) ? null : remoteUrl;
            _userName = string.IsNullOrEmpty(userName) ? null : userName;
            _userEmail = string.IsNullOrEmpty(userEmail) ? null : userEmail;
            _password = string.IsNullOrEmpty(password) ? null : password;
            _path = path;

            CredentialsHandler credentials = (url, fromUrl, types) => new UsernamePasswordCredentials { Username = _userName, Password = _password };


            if (!Directory.Exists(path))
            {
                if (_remoteUrl != null)
                    Repository.Clone(_remoteUrl, _path, new CloneOptions { IsBare = true, CredentialsProvider = credentials });
                else
                    Repository.Init(path, true);
            }
                
            _repo = new Repository(path);

            if (!string.IsNullOrEmpty(_remoteUrl))
            {
                _pushOptions = new PushOptions { CredentialsProvider = credentials };
                var fetchOptions = new FetchOptions {CredentialsProvider = credentials, TagFetchMode = TagFetchMode.All};
                var mergeOptions = new MergeOptions {FastForwardStrategy = FastForwardStrategy.FastForwardOnly};
                _pullOptions = new PullOptions {FetchOptions = fetchOptions, MergeOptions = mergeOptions};
                _repo.Network.Remotes.Add("origin", _remoteUrl);
                pull();
            }
            
            if (!_repo.Branches.Any())
            {
                var tree = commitTree("master", new TreeDefinition(), getSignature(new Author(_userName ?? "Default", _userEmail ?? "default@mail.com")), "init", true);
                _repo.Branches.Add("master", tree);
            }

            _branchLocks = _repo.Branches.ToDictionary(branch => branch.FriendlyName, branch => new object());
        }

        static Signature getSignature(Author author) =>
            new Signature(author.Name, author.Email, DateTimeOffset.Now);

        Blob  addBlob(string value) =>
            _repo.ObjectDatabase.CreateBlob(new MemoryStream(Encoding.UTF8.GetBytes(value ?? string.Empty)));
        
        static void addBlobToTree(string key, Blob blob, TreeDefinition tree)
        {
            lock (tree)
                tree.Add(key, blob, Mode.NonExecutableFile);
        }

        static void deleteKeyFromTree(string key, TreeDefinition tree)
        {
            lock (tree)
                tree.Remove(key);
        }

        string commitTree(string branch, TreeDefinition treeDefinition, Signature signature, string message, bool commitEmpty = false)
        {
            var branchObj = _repo.Branches.SingleOrDefault(b => b.FriendlyName == branch);

            var previousCommit = branchObj?.Tip;
            var tree = _repo.ObjectDatabase.CreateTree(treeDefinition);

            if (previousCommit != null && previousCommit.Tree.Id == tree.Id && !commitEmpty)
                return string.Empty;

            var ancestors = previousCommit != null ? new List<Commit> { previousCommit } : new List<Commit>();
            var commit = _repo.ObjectDatabase.CreateCommit(signature, signature, message, tree, ancestors, false);

            if (branchObj == null)
                _repo.Refs.UpdateTarget(_repo.Refs.Head, commit.Id, string.Empty);
            else
                _repo.Refs.UpdateTarget(_repo.Refs[branchObj.CanonicalName], commit.Id);

            var sha = commit.Sha;

            flushRepo();

            return sha;
        }

        public Task<string> Get(string branch, string key) => 
            Task.FromResult((_repo.Branches[branch].Tip[key]?.Target as Blob)?.GetContentText());

        public async Task<T> Get<T>(string branch, string key) where T : class =>
            (await Get(branch, key))?.As<T>();

        public async Task<IReadOnlyCollection<T>> GetFiles<T>(string branch, string key) =>
            (await GetFiles(branch, key)).Select(JsonConvert.DeserializeObject<T>)
                                         .ToArray();

        public Task<IReadOnlyCollection<string>> GetFiles(string branch, string key) =>
            Task.FromResult((IReadOnlyCollection<string>) (
                (_repo.Branches[branch].Tip[key].Target as Tree)?
                     .Where(entry => entry.TargetType == TreeEntryTargetType.Blob)
                     .Select(entry => entry.Target)
                     .Cast<Blob>()
                     .Select(blob => blob.GetContentText())
                     .ToList() ?? 
                new List<string>()));

        public Task<string> Save(string branch, string message, Document document, Author author)
        {
            if(string.IsNullOrEmpty(document.Key))
                throw new ArgumentException("key cannot be empty");

            if(_branchesWithTransaction.Contains(branch))
                throw new Exception("There is a transaction in progress for this branch. Complete the transaction first.");
            var blob = addBlob(document.Value);
            lock (_branchLocks[branch])
            {
                var tree = TreeDefinition.From(_repo.Branches[branch].Tip);
                addBlobToTree(document.Key, blob, tree);
                var sha = commitTree(branch, tree, getSignature(author), message);
                _logger.Log($"Added {document.Key} on branch {branch}");
                push(branch);
                return Task.FromResult(sha);
            }
        }


        public Task<string> Save<T>(string branch, string message, Document<T> document, Author author) =>
            Save(branch, message, Document.From(document), author);

        public Task<string> Delete(string branch, string key, string message, Author author)
        {
            lock (_branchLocks[branch])
            {
                var tree = TreeDefinition.From(_repo.Branches[branch].Tip);
                deleteKeyFromTree(key, tree);
                var sha = commitTree(branch, tree, getSignature(author), message);
                _logger.Log($"Deleted {key} on branch {branch}");
                push(branch);
                return Task.FromResult(sha);
            }
        }

        public Task Tag(Reference reference)
        {
            var result = _repo.Tags.Add(reference.Name, reference.Pointer);
            pushTags();
            return Task.FromResult(result);
        }
            

        public Task CreateBranch(Reference reference)
        {
            _repo.Branches.Add(reference.Name, reference.Pointer);
            _branchLocks.Add(reference.Name, new object());
            push(reference.Name);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<string>> GetAllBranches() =>
            Task.FromResult(_repo.Branches.Where(b => !b.IsRemote).Select(b => b.FriendlyName));

        public Task<ITransaction> CreateTransaction(string branch)
        {
            if (_branchesWithTransaction.Contains(branch))
                throw new Exception($"A transaction is already in progress for branch {branch}");

            _branchesWithTransaction.Add(branch);
            var tree = TreeDefinition.From(_repo.Branches[branch].Tip);

            return Task.FromResult((ITransaction)new Transaction(
                add: document =>
                {
                    addBlobToTree(document.Key, addBlob(document.Value), tree);
                    _logger.Log($"Added blob with key {document.Key} to transaction on {branch}");
                    return Task.CompletedTask;
                },
                commit: (message, author) =>
                {
                    lock (_branchLocks[branch])
                    {
                        var sha = commitTree(branch, tree, getSignature(author), message);
                        _branchesWithTransaction.Remove(branch);
                        _logger.Log($"Commited transaction on {branch}");
                        push(branch);
                        return Task.FromResult(sha);
                    }
                },
                abort: () =>
                {
                    _branchesWithTransaction.Remove(branch);
                    _logger.Log($"Aborted transaction on {branch}");
                    return Task.CompletedTask;
                },
                delete: key =>
                {
                    deleteKeyFromTree(key, tree);
                    _logger.Log($"Removed blob with key {key} in transaction  on {branch}");
                    return Task.CompletedTask;
                }));
        }

        void pull()
        {
            if (string.IsNullOrEmpty(_remoteUrl)) return;
            _repo.Network.Pull(new Signature(_userName, _userEmail, DateTimeOffset.Now), _pullOptions);
        }

        void push(string branch)
        {
            if (string.IsNullOrEmpty(_remoteUrl)) return;
            _repo.Network.Push(_repo.Branches[branch], _pushOptions);
        }

        void pushTags()
        {
            if (string.IsNullOrEmpty(_remoteUrl)) return;
            _repo.Tags
                 .ToList()
                 .ForEach(tag => _repo.Network.Push(_repo.Network.Remotes["origin"], tag.CanonicalName, _pushOptions));
            
        }

        // This is a hack to bypass a memory leak in LibGit2Sharp
        // Whenever we commit a tree the treedefinition doesn't seem to get disposed correctly
        // This leads to an increase in memory usage and ultimately an OutOfMemoryException
        // The case has been submitted to LibGit2Sharp: https://github.com/libgit2/libgit2sharp/issues/1378
        int _commitCount = 0;
        void flushRepo()
        {
            _commitCount++;
            if (_commitCount > 100)
            {
                _commitCount = 0;
                _repo?.Dispose();
                _repo = new Repository(_path);
            }
        }

        public void Dispose() =>
            _repo.Dispose();
    }
}