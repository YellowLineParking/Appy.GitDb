using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LibGit2Sharp;
using NLog;

namespace Ylp.GitDb.Watcher
{
    public delegate void BranchAddedHandler(BranchAdded branchAdded);
    public delegate void BranchRemovedHandler(BranchRemoved branchRemoved);
    public delegate void BranchChangedHandler(BranchChanged branchChanged);

    public class Watcher : IDisposable
    {
        readonly Logger _logger;
        readonly int _interval;
        readonly Repository _repo;
        Timer _timer;
        BranchDictionary _branches;

        public event BranchAddedHandler BranchAdded;
        public event BranchRemovedHandler BranchRemoved;
        public event BranchChangedHandler BranchChanged;

        public Watcher(string path, int interval)
        {
            _logger = LogManager.GetLogger("watcher");
            _interval = interval;
            _repo = new Repository(path);
            _branches = _repo.Branches
                             .Where(b => !b.IsRemote)
                             .ToBranchDictionary();
        }

        public void Start(IEnumerable<BranchInfo> branchInfo)
        {
            var previousBranches = branchInfo.ToBranchDictionary();

            raiseEventsForDifferences(previousBranches, _branches);

            _timer = new Timer(check, null, _interval, Timeout.Infinite);
        }

        void check(object state)
        {
            var previousBranches = _branches;
            var currentBranches = _repo.Branches
                                       .Where(b => !b.IsRemote)
                                       .ToBranchDictionary();
            _branches = currentBranches;

            raiseEventsForDifferences(previousBranches, currentBranches);

            _timer.Change(_interval, Timeout.Infinite);
        }

        void raiseEventsForDifferences(BranchDictionary previousBranches, BranchDictionary currentBranches)
        {
            currentBranches.Where(current => previousBranches.HasBranch(current.Name) && current.Commit != previousBranches[current.Name].Commit)
                           .ForEach(branchInfo => raiseBranchChanged(previousBranches, branchInfo));

            currentBranches.Where(current => !previousBranches.HasBranch(current.Name))
                           .ForEach(raiseBranchAdded);

            previousBranches.Where(current => !currentBranches.HasBranch(current.Name))
                            .ForEach(raiseBranchDeleted);
        }

        void raiseBranchChanged(BranchDictionary previousBranches, BranchInfo branch)
        {
            try
            {
                var previousCommit = _repo.Lookup<Commit>(previousBranches[branch.Name].Commit);
                var currentCommit = _repo.Lookup<Commit>(branch.Commit);
                _logger.Trace($"Found differences on branch {branch}, starting diff between {previousCommit.Sha} and {currentCommit.Sha}");
                var result = _repo.Diff.Compare<TreeChanges>(previousCommit.Tree, currentCommit.Tree);
                _logger.Info($"Finished diff on branch {branch.Name}, found {result.Added.Count()} added items, {result.Deleted.Count()} deleted items, {result.Renamed.Count()} renamed items and {result.Modified.Count()} modified items");

                var eventInfo = getBranchChanged<BranchChanged>(result, branch);
                eventInfo.PreviousCommit = previousCommit.Sha;

                BranchChanged?.Invoke(eventInfo);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error while checking for changes on branch {branch}. Previous commit is {previousBranches[branch.Name].Commit}, Current commit is {branch.Commit}");
                throw;
            }
        }

        void raiseBranchAdded(BranchInfo branch)
        {
            try
            {
                _logger.Info($"Detected a new branch {branch}");
                var currentCommit = _repo.Lookup<Commit>(branch.Commit);
                var previousCommit = currentCommit;
                string baseBranch;
                _logger.Trace("Searching a base branch");

                do
                {
                    baseBranch = _repo.Branches.FirstOrDefault(b => b.Tip.Sha == previousCommit.Sha && b.FriendlyName != branch.Name)?.FriendlyName;
                    if (baseBranch == null)
                        previousCommit = previousCommit.Parents.FirstOrDefault();
                } while (baseBranch == null && previousCommit != null);

                if (previousCommit == null)
                {
                    var otherBranch = _repo.Branches.FirstOrDefault(b => b.FriendlyName != branch.Name);
                    if (otherBranch != null)
                    {
                        previousCommit = otherBranch.Tip;
                        baseBranch = otherBranch.FriendlyName;
                        _logger.Trace($"Could not find a base branch for the newly created branch {branch}, taking {baseBranch} and starting diff between {previousCommit.Sha} and {currentCommit.Sha}");
                    }
                    else
                    {
                        _logger.Error($"Could not find any base branch for the newly created branch {branch}, skipping raising an event");
                        return;
                    }
                }
                else
                {
                    _logger.Trace($"Found base branch {baseBranch} for {branch}, starting diff between {previousCommit.Sha} and {currentCommit.Sha}");
                }

                var result = _repo.Diff.Compare<TreeChanges>(previousCommit.Tree, currentCommit.Tree);
                _logger.Info($"Finished diff, found {result.Added.Count()} added items, {result.Deleted.Count()} deleted items, {result.Renamed.Count()} renamed items and {result.Modified.Count()} modified items");

                var eventInfo = getBranchChanged<BranchAdded>(result, branch);
                eventInfo.BaseBranch = baseBranch;
                BranchAdded?.Invoke(eventInfo);

            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error while checking new branch {branch}");
                throw;
            }
        }

        void raiseBranchDeleted(BranchInfo branchInfo)
        {
            try
            {
                _logger.Info($"Detected deletion of branch {branchInfo.Name}");
                BranchRemoved?.Invoke(new BranchRemoved { Branch = branchInfo });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error while checking deleted branch {branchInfo.Name}");
                throw;
            }
        }

        T getBranchChanged<T>(TreeChanges changes, BranchInfo branchInfo) where T : BranchModification, new() => 
            new T
            {
                Branch = branchInfo,
                Added = changes.Added.Select(a => new ItemAdded
                {
                    Key = a.Path,
                    Value = getBlobValue(a.Oid)
                }).ToList(),
                Modified = changes.Modified.Select(m => new ItemModified
                {
                    Key = m.Path,
                    Value = getBlobValue(m.Oid),
                    OldValue = getBlobValue(m.OldOid)
                }).ToList(),
                Renamed = changes.Renamed.Select(r => new ItemRenamed
                {
                    Key = r.Path,
                    Value = getBlobValue(r.Oid),
                    OldValue = getBlobValue(r.OldOid),
                    OldKey = r.OldPath
                }).ToList(),
                Deleted = changes.Deleted.Select(d => new ItemDeleted {Key = d.Path, Value = getBlobValue(d.OldOid)}).ToList()
            };

        string getBlobValue(ObjectId objectId) =>
            _repo.Lookup<Blob>(objectId).GetContentText();

        public void Dispose() =>
            _timer?.Dispose();
    }
}