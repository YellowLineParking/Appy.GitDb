using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LibGit2Sharp;
using Ylp.GitDb.Core;

namespace Ylp.GitDb.Watcher
{
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

        public event BranchAddedHandler BranchAdded;
        public event BranchRemovedHandler BranchRemoved;
        public event BranchChangedHandler BranchChanged;

        public Watcher(string path, ILogger logger, int interval)
        {
            _logger = logger;
            _interval = interval;
            _repo = new Repository(path);
            _branches = _repo.Branches.Where(b => !b.IsRemote).ToDictionary(b => b.FriendlyName, b => b.Tip.Sha);
        }

        public void Start(IEnumerable<BranchInfo> branchInfo)
        {
            var previousBranches = branchInfo.ToDictionary(bi => bi.Name, bi => bi.Commit);

            raiseEventsForDifferences(previousBranches, _branches);

            _timer = new Timer(check, null, _interval, Timeout.Infinite);
        }

        void check(object state)
        {
            var previousBranches = _branches;
            var currentBranches = _repo.Branches
                .Where(b => !b.IsRemote)
                .ToDictionary(b => b.FriendlyName, b => b.Tip.Sha);
            _branches = currentBranches;

            raiseEventsForDifferences(previousBranches, currentBranches);

            _timer.Change(_interval, Timeout.Infinite);
        }

        void raiseEventsForDifferences(IReadOnlyDictionary<string, string> previousBranches, IReadOnlyDictionary<string, string> currentBranches)
        {
            currentBranches.Where(current => previousBranches.ContainsKey(current.Key) && current.Value != previousBranches[current.Key])
                           .ToList()
                           .ForEach(current =>
                            {
                                try
                                {
                                    var previousCommit = _repo.Lookup<Commit>(previousBranches[current.Key]);
                                    var currentCommit = _repo.Lookup<Commit>(current.Value);
                                    _logger.Trace($"Found differences on branch {current.Key}, starting diff between {previousCommit.Sha} and {currentCommit.Sha}");
                                    var result = _repo.Diff.Compare<TreeChanges>(previousCommit.Tree, currentCommit.Tree);
                                    _logger.Info($"Finished diff on branch {current.Key}, found {result.Added.Count()} added items, {result.Deleted.Count()} deleted items, {result.Renamed.Count()} renamed items and {result.Modified.Count()} modified items");

                                    BranchChanged?.Invoke(new BranchChanged
                                    {
                                        PreviousCommit = previousCommit.Sha,
                                        Branch = new BranchInfo {Name = current.Key, Commit = current.Value},
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
                                        Deleted = result.Deleted.Select(d => new ItemDeleted {Key = d.Path, Value = getBlobValue(d.OldOid)}).ToList()
                                    });
                                }
                                catch (Exception ex)
                                {
                                    _logger.Error(ex, $"Error while checking for changes on branch {current.Key}. Previous commit is {previousBranches[current.Key]}, Current commit is {current.Value}");
                                    throw;
                                }
                            });

            currentBranches.Where(current => !previousBranches.ContainsKey(current.Key))
                           .ToList()
                           .ForEach(current =>
                           {
                               try
                               {
                                   _logger.Info($"Detected a new branch {current.Key}");
                                   var currentCommit = _repo.Lookup<Commit>(current.Value);
                                   var previousCommit = currentCommit;
                                   string baseBranch = null;
                                   _logger.Trace($"Searching a base branch");
                                   do
                                   {
                                       baseBranch = _repo.Branches.FirstOrDefault(b => b.Tip.Sha == previousCommit.Sha && b.FriendlyName != current.Key)?.FriendlyName;
                                        if(baseBranch == null)
                                           previousCommit = previousCommit.Parents.FirstOrDefault();
                                   } while (baseBranch == null && previousCommit != null);

                                   if (previousCommit == null)
                                   {
                                       var otherBranch = _repo.Branches.FirstOrDefault(b => b.FriendlyName != current.Key);
                                       if (otherBranch != null)
                                       {
                                           previousCommit = otherBranch.Tip;
                                           baseBranch = otherBranch.FriendlyName;
                                           _logger.Trace($"Could not find a base branch for the newly created branch {current.Key}, taking {baseBranch} and starting diff between {previousCommit.Sha} and {currentCommit.Sha}");
                                       }
                                       else
                                       {
                                           _logger.Error($"Could not find any base branch for the newly created branch {current.Key}, skipping raising an event");
                                           return;
                                       }
                                   }
                                   else
                                   {
                                       _logger.Trace($"Found base branch {baseBranch} for {current.Key}, starting diff between {previousCommit.Sha} and {currentCommit.Sha}");
                                   }

                                   var result = _repo.Diff.Compare<TreeChanges>(previousCommit.Tree, currentCommit.Tree);
                                   _logger.Info($"Finished diff, found {result.Added.Count()} added items, {result.Deleted.Count()} deleted items, {result.Renamed.Count()} renamed items and {result.Modified.Count()} modified items");

                                   BranchAdded?.Invoke(new BranchAdded
                                   {
                                       Branch = new BranchInfo {Name = current.Key, Commit = current.Value},
                                       Commit = current.Value,
                                       BaseBranch = baseBranch,
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
                           
                               }
                               catch (Exception ex)
                               {
                                   _logger.Error(ex, $"Error while checking new branch {current.Key}");
                                   throw;
                               }
                           });

            previousBranches.Where(current => !currentBranches.ContainsKey(current.Key))
                            .ToList()
                            .ForEach(current =>
                            {
                                try
                                {
                                    _logger.Info($"Detected deletion of branch {current.Key}");
                                    BranchRemoved?.Invoke(new BranchRemoved { Branch = new BranchInfo { Name = current.Key, Commit = current.Value } });
                                }
                                catch (Exception ex)
                                {
                                    _logger.Error(ex, $"Error while checking deleted branch {current.Key}");
                                    throw;
                                }
                           });
        }

        string getBlobValue(ObjectId objectId) =>
            _repo.Lookup<Blob>(objectId).GetContentText();            

    public void Dispose() =>
            _timer?.Dispose();
    }
}