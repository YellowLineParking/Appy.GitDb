using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Newtonsoft.Json;
using NLog;
using Ylp.GitDb.Core.Interfaces;
using Ylp.GitDb.Core.Model;
using Reference = Ylp.GitDb.Core.Model.Reference;


namespace Ylp.GitDb.Local
{
    public class LocalGitDb : IGitDb
    {
        readonly int _transactionTimeout;
        readonly Logger _logger;
        readonly string _remoteUrl;
        readonly string _userName;
        readonly Repository _repo;
        readonly Dictionary<string, DateTime> _branchesWithTransaction = new Dictionary<string, DateTime>();
        readonly Dictionary<string, object> _branchLocks;
        readonly PushOptions _pushOptions;

        public LocalGitDb(string path, string remoteUrl = null, string userName = null, string userEmail = null, string password = null, int transactionTimeout = 10)
        {
            _transactionTimeout = transactionTimeout;
            _logger = LogManager.GetCurrentClassLogger();
            
            _remoteUrl = string.IsNullOrEmpty(remoteUrl) ? null : remoteUrl;
            _userName = string.IsNullOrEmpty(userName) ? null : userName;
            userEmail = string.IsNullOrEmpty(userEmail) ? null : userEmail;
            password = string.IsNullOrEmpty(password) ? null : password;

            _logger.Trace("Starting local git db");

            CredentialsHandler credentials = (url, fromUrl, types) => new UsernamePasswordCredentials { Username = _userName, Password = password };


            if (!Directory.Exists(path))
            {
                if (!string.IsNullOrEmpty(_remoteUrl))
                {
                    _logger.Trace($"No repository exists on disk and there's a remote URL, cloning the repo from {_remoteUrl}");
                    Repository.Clone(_remoteUrl, path, new CloneOptions {IsBare = true, CredentialsProvider = credentials});
                }
                else
                {
                    _logger.Trace($"No repository exists on disk and there's not remote URL, initializing a bare repository at {path}");
                    Repository.Init(path, true);
                }
            }
                
            _repo = new Repository(path);

            if (!string.IsNullOrEmpty(_remoteUrl))
            {
                _pushOptions = new PushOptions { CredentialsProvider = credentials };
                
                if(_repo.Network.Remotes["origin"] != null)
                    _repo.Network.Remotes.Remove("origin");

                _repo.Network.Remotes.Add("origin", _remoteUrl);
                _repo.Branches
                     .Select(b => b.FriendlyName)
                     .ToList()
                     .ForEach(push);
            }
            
            if (!_repo.Branches.Any())
            {
                var sha = commitTree("master", new TreeDefinition(), getSignature(new Author(_userName ?? "Default", userEmail ?? "default@mail.com")), "init", true);
                _logger.Trace($"Repository contains no branches, created an initial commit for branch master with sha {sha}");
                _repo.Branches.Add("master", sha);
            }

            _branchLocks = _repo.Branches.ToDictionary(branch => branch.FriendlyName, branch => new object());
        }

        bool isTransactionInProgress(string branch)
        {
            if (!_branchesWithTransaction.ContainsKey(branch))
                return false;

            if (_branchesWithTransaction[branch] < DateTime.Now)
            {
                _branchesWithTransaction.Remove(branch);
                return false;
            }

            return true;
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

            return commit.Sha;
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
                (_repo.Branches[branch]?.Tip[key]?.Target as Tree)?
                      .Where(entry => entry.TargetType == TreeEntryTargetType.Blob)
                      .Select(entry => entry.Target)
                      .Cast<Blob>()
                      .Select(blob => blob.GetContentText())
                      .ToList() ?? 
                new List<string>()));

        public Task<string> Save(string branch, string message, Document document, Author author)
        {
            if (string.IsNullOrEmpty(document.Key))
            {
                _logger.Warn("Could not save document with empty key");
                throw new ArgumentException("key cannot be empty");
            }

            if (isTransactionInProgress(branch))
            {
                var exceptionMessage = $"There is a transaction in progress for branch {branch}. Complete the transaction first.";
                _logger.Warn(exceptionMessage);
                throw new ArgumentException(exceptionMessage);
            }
                
            var blob = addBlob(document.Value);
            
            lock (getLock(branch))
            {
                var tree = TreeDefinition.From(_repo.Branches[branch].Tip);
                addBlobToTree(document.Key, blob, tree);
                var sha = commitTree(branch, tree, getSignature(author), message);
                _logger.Trace($"Added {document.Key} on branch {branch} with commit {sha}");
                push(branch);
                return Task.FromResult(sha);
            }
        }


        public Task<string> Save<T>(string branch, string message, Document<T> document, Author author) =>
            Save(branch, message, Document.From(document), author);

        public Task<string> Delete(string branch, string key, string message, Author author)
        {
            lock (getLock(branch))
            {
                var tree = TreeDefinition.From(_repo.Branches[branch].Tip);
                deleteKeyFromTree(key, tree);
                var sha = commitTree(branch, tree, getSignature(author), message);
                _logger.Info($"Deleted {key} on branch {branch} with commit {sha}");
                push(branch);
                return Task.FromResult(sha);
            }
        }

        public Task Tag(Reference reference)
        {
            var result = _repo.Tags.Add(reference.Name, reference.Pointer);
            _logger.Trace($"Created tag {reference.Name} at commit {reference.Pointer}");
            return Task.FromResult(result);
        }
            
        public Task CreateBranch(Reference reference)
        {
            _repo.Branches.Add(reference.Name, reference.Pointer);
            _branchLocks.Add(reference.Name, new object());
            _logger.Trace($"Created branch {reference.Name} at commit {reference.Pointer}");
            push(reference.Name);
            return Task.CompletedTask;
        }

        public Task<string> MergeBranch(string source, string target, Author author, string message)
        {
            
            var signature = getSignature(author);
            var targetBranch = _repo.Branches[target];
            var sourceBranch = _repo.Branches[source];

            if (isTransactionInProgress(target))
            {
                var exceptionMessage = $"There is a transaction in progress for branch {target}. Complete the transaction first.";
                _logger.Warn(exceptionMessage);
                throw new ArgumentException(exceptionMessage);
            }

            lock (getLock(target))
            {
                var mergeRes = _repo.ObjectDatabase.MergeCommits(sourceBranch.Tip, targetBranch.Tip, new MergeTreeOptions { FailOnConflict = true });
                if (mergeRes.Status != MergeTreeStatus.Succeeded)
                {
                    var logMessage = $"Could not merge {source} into {target} because of conflicts. Please merge manually";
                    _logger.Trace(logMessage);
                    throw new NotSupportedException(logMessage);
                }

                _repo.Branches.Remove(sourceBranch);

                var previousCommit = targetBranch.Tip;
                var tree = mergeRes.Tree;

                if (previousCommit != null && previousCommit.Tree.Id == tree.Id)
                    return Task.FromResult(string.Empty);

                var ancestors = previousCommit != null ? new List<Commit> { previousCommit } : new List<Commit>();
                var commit = _repo.ObjectDatabase.CreateCommit(signature, signature, message, tree, ancestors, false);

                _repo.Refs.UpdateTarget(_repo.Refs[targetBranch.CanonicalName], commit.Id);

                _logger.Trace($"Squashed and merged {source} into {target} and removed {source} with message {message}");

                push(target);

                return Task.FromResult(commit.Sha);
            }
        }

        public Task DeleteBranch(string branch)
        {
            if (!_branchLocks.ContainsKey(branch))
                _branchLocks.Add(branch, new object());

            lock (_branchLocks[branch])
            {
                _repo.Branches.Remove(branch);
                if (_branchLocks.ContainsKey(branch))
                    _branchLocks.Remove(branch);
            }

            return Task.CompletedTask;
        }

        public Task<Core.Model.Diff> Diff(string reference, string reference2)
        {
            var tree1 = getTreeFromReference(reference);
            var tree2 = getTreeFromReference(reference2);

            if (tree1 == null || tree2 == null)
            {
                var invalidRefs = (tree1 == null ? reference : string.Empty) + " " + (tree2 == null ? reference2 : string.Empty);
                  var message = $"Could not perform diff between {reference} and {reference2}: invalid reference(s): {invalidRefs}";
                _logger.Warn(message);
                throw new ArgumentException(message);
            }

            var diff = _repo.Diff.Compare<TreeChanges>(tree1, tree2);

            return Task.FromResult(new Core.Model.Diff
            {
                Added = diff.Added.Select(a => new ItemAdded
                {
                    Key = a.Path,
                }).ToList(),
                Modified = diff.Modified.Select(m => new ItemModified
                {
                    Key = m.Path,
                }).ToList(),
                Renamed = diff.Renamed.Select(r => new ItemRenamed
                {
                    Key = r.Path,
                    OldKey = r.OldPath
                }).ToList(),
                Deleted = diff.Deleted.Select(d => new ItemDeleted { Key = d.Path }).ToList()
            });
        }

        Tree getTreeFromReference(string reference) => 
            (_repo.Lookup<Commit>(reference)?.Tree ?? 
            _repo.Branches[reference]?.Tip?.Tree) ?? 
            ((Commit) _repo.Tags[reference]?.PeeledTarget)?.Tree;

        public Task<IEnumerable<string>> GetAllBranches() =>
            Task.FromResult(_repo.Branches.Where(b => !b.IsRemote).Select(b => b.FriendlyName));

        public Task<ITransaction> CreateTransaction(string branch)
        {
            if (isTransactionInProgress(branch))
            {
                var exceptionMessage = $"There is a transaction in progress for branch {branch}. Complete the transaction first.";
                _logger.Warn(exceptionMessage);
                throw new ArgumentException(exceptionMessage);
            }

            _branchesWithTransaction.Add(branch, DateTime.Now.AddSeconds(_transactionTimeout));
            var tree = TreeDefinition.From(_repo.Branches[branch].Tip);

            void EnsureTransactionInProgress()
            {
                if (!_branchesWithTransaction.ContainsKey(branch))
                {
                    var exceptionMessage = $"Transaction does not exist for branch {branch} or has timed out";
                    _logger.Warn(exceptionMessage);
                    throw new ArgumentException(exceptionMessage);
                }
                _branchesWithTransaction[branch] = DateTime.Now.AddSeconds(_transactionTimeout);
            }

            return Task.FromResult((ITransaction)new Transaction(
                add: document =>
                {
                    EnsureTransactionInProgress();
                    addBlobToTree(document.Key, addBlob(document.Value), tree);
                    _logger.Trace($"Added blob with key {document.Key} to transaction on {branch}");
                    return Task.CompletedTask;
                },
                commit: (message, author) =>
                {
                    EnsureTransactionInProgress();
                    lock (getLock(branch))
                    {
                        var sha = commitTree(branch, tree, getSignature(author), message);
                        _branchesWithTransaction.Remove(branch);
                        _logger.Info($"Commited transaction on {branch} with commit {sha}");
                        push(branch);
                        return Task.FromResult(sha);
                    }
                },
                abort: () =>
                {
                    _branchesWithTransaction.Remove(branch);
                    _logger.Info($"Aborted transaction on {branch}");
                    return Task.CompletedTask;
                },
                delete: key =>
                {
                    EnsureTransactionInProgress();
                    deleteKeyFromTree(key, tree);
                    _logger.Trace($"Removed blob with key {key} in transaction  on {branch}");
                    return Task.CompletedTask;
                }));
        }

        void push(string branch)
        {
            if (string.IsNullOrEmpty(_remoteUrl)) return;

            Task.Run(() =>
            {
                _logger.Trace($"Pushing branch {branch} to {_remoteUrl} with user name {_userName}");

                try
                {
                    var localBranch = _repo.Branches[branch];
                    _repo.Branches.Update(localBranch, b => b.Remote = "origin", b => b.UpstreamBranch = localBranch.CanonicalName);
                    _repo.Network.Push(localBranch, _pushOptions);
                    _logger.Trace($"Pushed branch {branch} to {_remoteUrl} with user name {_userName}");
                }
                catch (Exception e)
                {
                    _logger.Error(e);
                }
            });
        }

        object getLock(string branch)
        {
            lock (_branchLocks)
            {
                if (!_branchLocks.ContainsKey(branch))
                    _branchLocks.Add(branch, new object());
                return _branchLocks[branch];
            }
        }

        public void Dispose() =>
            _repo.Dispose();

        public Task CloseTransactions(string branch)
        {
            _branchesWithTransaction.Remove(branch);
            _logger.Info($"Aborted transaction on {branch}");
            return Task.CompletedTask;
        }
    }
}