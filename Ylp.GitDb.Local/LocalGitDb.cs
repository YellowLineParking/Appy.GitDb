﻿using System;
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

        public LocalGitDb(string path, ILogger logger, string remoteUrl = null, string userName = null, string userEmail = null, string password = null)
        {
            _logger = logger;
            _remoteUrl = string.IsNullOrEmpty(remoteUrl) ? null : remoteUrl;
            _userName = string.IsNullOrEmpty(userName) ? null : userName;
            _userEmail = string.IsNullOrEmpty(userEmail) ? null : userEmail;
            _password = string.IsNullOrEmpty(password) ? null : password;
            _path = path;

            _logger.Trace("Starting local git db");

            CredentialsHandler credentials = (url, fromUrl, types) => new UsernamePasswordCredentials { Username = _userName, Password = _password };


            if (!Directory.Exists(path))
            {
                if (_remoteUrl != null)
                {
                    _logger.Trace($"No repsotiory exists on disk, cloning the repo from {_remoteUrl}");
                    Repository.Clone(_remoteUrl, _path, new CloneOptions {IsBare = true, CredentialsProvider = credentials});
                }
                else
                {
                    _logger.Trace($"No repsotiory exists on disk, initializing a bare repository at {path}");
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
                var sha = commitTree("master", new TreeDefinition(), getSignature(new Author(_userName ?? "Default", _userEmail ?? "default@mail.com")), "init", true);
                _logger.Trace($"Repository contains no branches, created an initial commit for branch master with sha {sha}");
                _repo.Branches.Add("master", sha);
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
            if (string.IsNullOrEmpty(document.Key))
            {
                _logger.Warn("Could not save document with empty key");
                throw new ArgumentException("key cannot be empty");
            }

            if (_branchesWithTransaction.Contains(branch))
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

        public Task Tag(Reference reference) =>
            Task.FromResult(_repo.Tags.Add(reference.Name, reference.Pointer));

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
            {
                var exceptionMessage = $"There is a transaction in progress for branch {branch}. Complete the transaction first.";
                _logger.Warn(exceptionMessage);
                throw new ArgumentException(exceptionMessage);
            }

            _branchesWithTransaction.Add(branch);
            var tree = TreeDefinition.From(_repo.Branches[branch].Tip);

            return Task.FromResult((ITransaction)new Transaction(
                add: document =>
                {
                    addBlobToTree(document.Key, addBlob(document.Value), tree);
                    _logger.Trace($"Added blob with key {document.Key} to transaction on {branch}");
                    return Task.CompletedTask;
                },
                commit: (message, author) =>
                {
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
                _logger.Info($"Pushing branch {branch} to {_remoteUrl} with user name {_userName}");

                var localBranch = _repo.Branches[branch];
                _repo.Branches.Update(localBranch, b => b.Remote = "origin", b => b.UpstreamBranch = localBranch.CanonicalName);
                _repo.Network.Push(localBranch, _pushOptions);
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