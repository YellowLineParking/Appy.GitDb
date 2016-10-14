using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;
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
        readonly Repository _repo;
        readonly List<string> _branchesWithTransaction = new List<string>();
        readonly Dictionary<string, object> _branchLocks;
        
        public LocalGitDb(string path, ILogger logger)
        {
            _logger = logger;
            if (!Directory.Exists($"{path}/.git"))
                Repository.Init(path);
            
            _repo = new Repository(path);
            if (!_repo.Branches.Any())
                _repo.Branches.Add("master", commitTree("master", new TreeDefinition(), getSignature(new Author("Default", "default@mail.com")), "Init", true));

            _branchLocks = _repo.Branches.ToDictionary(branch => branch.FriendlyName, branch => new object());
        }

        static Signature getSignature(Author author) =>
            new Signature(author.Name, author.Email, DateTimeOffset.Now);

        Blob  addBlob(string value)
        {
            Blob blob;
            using (var stream = new MemoryStream())
            using (var streamWriter = new StreamWriter(stream, Encoding.UTF8))
            {
                streamWriter.Write(value);
                streamWriter.Flush();
                stream.Position = 0;
                blob = _repo.ObjectDatabase.CreateBlob(stream);
            }
            return blob;
        }

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
            if (!_repo.HasChanges(previousCommit?.Tree, tree) && !commitEmpty)
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
                (_repo.Branches[branch].Tip[key].Target as Tree)?
                     .Where(entry => entry.TargetType == TreeEntryTargetType.Blob)
                     .Select(entry => entry.Target)
                     .Cast<Blob>()
                     .Select(blob => blob.GetContentText())
                     .ToList() ?? 
                new List<string>()));

       

        public Task<string> Save(string branch, string message, Document document, Author author)
        {
            if(_branchesWithTransaction.Contains(branch))
                throw new Exception("There is a transaction in progress for this branch. Complete the transaction first.");
            var blob = addBlob(document.Value);
            lock (_branchLocks[branch])
            {
                var tree = TreeDefinition.From(_repo.Branches[branch].Tip);
                addBlobToTree(document.Key, blob, tree);
                var sha = commitTree(branch, tree, getSignature(author), message);
                _logger.Log($"Added {document.Key} on branch {branch}");
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
                return Task.FromResult(sha);
            }
        }

        public Task Tag(Reference reference) =>
            Task.FromResult(_repo.Tags.Add(reference.Name, reference.Pointer));

        public Task CreateBranch(Reference reference) =>
            Task.FromResult(_repo.Branches.Add(reference.Name, reference.Pointer));

        public Task<IEnumerable<string>> GetAllBranches() =>
            Task.FromResult(_repo.Branches.Select(b => b.FriendlyName));

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

        public void Dispose() =>
            _repo.Dispose();
    }
}