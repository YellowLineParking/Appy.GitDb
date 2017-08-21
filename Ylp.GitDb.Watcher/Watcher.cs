using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LibGit2Sharp;
using Ylp.GitDb.Core;

namespace Ylp.GitDb.Watcher
{
    public delegate void InitializedHandler(Initialized initialized);
    public delegate void BranchAddedHandler(BranchAdded branchAdded);
    public delegate void BranchRemovedHandler(BranchRemoved branchRemoved);
    public delegate void BranchChangedHandler(BranchChanged branchChanged);
    
    public class Watcher : IDisposable
    {
        readonly ILogger _logger;
        readonly int _interval;
        readonly Repository _repo;
        Timer _timer;
        Dictionary<string, string> _branches;

        public event InitializedHandler Initialized;
        public event BranchAddedHandler BranchAdded;
        public event BranchRemovedHandler BranchRemoved;
        public event BranchChangedHandler BranchChanged;

        public Watcher(string path, ILogger logger, int interval)
        {
            _logger = logger;
            _interval = interval;
            _repo = new Repository(path);
            _branches = _repo.Branches.ToDictionary(b => b.FriendlyName, b => b.Tip.Sha);
        }

        public void Start()
        {
            Initialized?.Invoke(new Initialized { Branches = _branches.Keys.ToArray() });
            _timer = new Timer(check, null, _interval, Timeout.Infinite);
        }

        void check(object state)
        {
            var previousBranches = _branches;
            var currentBranches = _repo.Branches
                                       .ToDictionary(b => b.FriendlyName, b => b.Tip.Sha);
            _branches = currentBranches;

            currentBranches.Where(current => previousBranches.ContainsKey(current.Key) && current.Value != previousBranches[current.Key])
                           .ToList()
                           .ForEach(current =>
                           {
                               var previousTree = _repo.Lookup<Commit>(previousBranches[current.Key]).Tree;
                               var currentTree = _repo.Lookup<Commit>(current.Value).Tree;
                               _logger.Log($"Found differences on branch {current.Key}, starting diff");
                               var result = _repo.Diff.Compare<TreeChanges>(previousTree, currentTree);
                               _logger.Log($"Finished diff on branch {current.Key}");

                               BranchChanged?.Invoke(new BranchChanged
                               {
                                   BranchName = current.Key,
                                   Added = result.Added.Select(a => new ItemAdded
                                   {
                                       Key = a.Path,
                                       Value = getBlobValue(a.Oid)
                                   }).ToList(),
                                   Modified = result.Modified.Select(m => new ItemModified
                                   {
                                       Key = m.Path,
                                       Value = getBlobValue(m.Oid),
                                       OldValue = getBlobValue(m.OldOid)
                                   }).ToList(),
                                   Renamed = result.Renamed.Select(r => new ItemRenamed
                                   {
                                       Key = r.Path,
                                       Value = getBlobValue(r.Oid),
                                       OldValue = getBlobValue(r.OldOid),
                                       OldKey = r.OldPath
                                   }).ToList(),
                                   Deleted = result.Deleted.Select(d => new ItemDeleted { Key = d.Path, Value = getBlobValue(d.OldOid) }).ToList()
                               });
                           });

            currentBranches.Where(current => !previousBranches.ContainsKey(current.Key))
                           .ToList()
                           .ForEach(current =>
                            {
                                var baseBranch = _repo.Branches.FirstOrDefault(b => b.Tip.Sha == current.Value && b.FriendlyName != current.Key)?.FriendlyName;
                                _logger.Log($"Detected a new branch {current.Key} based on {baseBranch}");
                                BranchAdded?.Invoke(new BranchAdded
                                {
                                    BranchName = current.Key,
                                    Commit = current.Value,
                                    BaseBranch = baseBranch
                                });
                            });

            previousBranches.Where(current => !currentBranches.ContainsKey(current.Key))
                           .ToList()
                           .ForEach(current =>
                            {
                                _logger.Log($"Detected deletion of branch {current.Key}");
                                BranchRemoved?.Invoke(new BranchRemoved {BranchName = current.Key});
                            });
            _timer = new Timer(check, null, _interval, Timeout.Infinite);
        }

        string getBlobValue(ObjectId objectId) =>
            _repo.Lookup<Blob>(objectId).GetContentText();

        public void Dispose() =>
            _timer?.Dispose();
    }
}