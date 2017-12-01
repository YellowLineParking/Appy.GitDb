using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;
using NLog;

namespace Appy.GitDb.Watcher
{
    public class Watcher : IDisposable
    {
        readonly Logger _logger;
        readonly int _interval;
        readonly Func<BranchAdded, Task> _branchAdded;
        readonly Func<BranchChanged, Task> _branchChanged;
        readonly Func<BranchRemoved, Task> _branchRemoved;
        readonly Repository _repo;
        Timer _timer;
        BranchDictionary _branches;

        public Watcher(string path, int interval, Func<BranchAdded, Task> branchAdded, Func<BranchChanged, Task> branchChanged, Func<BranchRemoved, Task> branchRemoved)
        {
            _logger = LogManager.GetLogger("watcher");
            _interval = interval;
            _branchAdded = branchAdded;
            _branchChanged = branchChanged;
            _branchRemoved = branchRemoved;
            _repo = new Repository(path);
            _branches = _repo.Branches
                             .Where(b => !b.IsRemote)
                             .ToBranchDictionary();
        }

        public async Task Start(IEnumerable<BranchInfo> branchInfo)
        {
            var previousBranches = branchInfo.ToBranchDictionary();

            await raiseEventsForDifferences(previousBranches, _branches);

            _timer = new Timer(state => check().Wait(), null, _interval, Timeout.Infinite);
        }

        async Task check()
        {
            var previousBranches = _branches;
            var currentBranches = _repo.Branches
                                       .Where(b => !b.IsRemote)
                                       .ToBranchDictionary();
            _branches = currentBranches;

            await raiseEventsForDifferences(previousBranches, currentBranches);

            _timer.Change(_interval, Timeout.Infinite);
        }

        async Task raiseEventsForDifferences(BranchDictionary previousBranches, BranchDictionary currentBranches)
        {
            foreach (var branchInfo in currentBranches.Where(current => previousBranches.HasBranch(current.Name) && current.Commit != previousBranches[current.Name].Commit))
                await raiseBranchChanged(previousBranches, branchInfo);
                           

            foreach (var branchInfo in currentBranches.Where(current => !previousBranches.HasBranch(current.Name)))
                await raiseBranchAdded(branchInfo);
                           

            foreach (var branchInfo in previousBranches.Where(current => !currentBranches.HasBranch(current.Name)))
                await raiseBranchDeleted(branchInfo);
        }

        async Task raiseBranchChanged(BranchDictionary previousBranches, BranchInfo branch)
        {
            try
            {
                var previousCommit = _repo.Lookup<Commit>(previousBranches[branch.Name].Commit);
                var currentCommit = _repo.Lookup<Commit>(branch.Commit);
                _logger.Trace($"Found differences on branch {branch.Name}, starting diff between {previousCommit.Sha} and {currentCommit.Sha}");
                var result = _repo.Diff.Compare<TreeChanges>(previousCommit.Tree, currentCommit.Tree);
                _logger.Info($"Finished diff on branch {branch.Name}, found {result.Added.Count()} added items, {result.Deleted.Count()} deleted items, {result.Renamed.Count()} renamed items and {result.Modified.Count()} modified items");

                var eventInfo = getBranchChanged<BranchChanged>(result, branch);
                eventInfo.PreviousCommit = previousCommit.Sha;

                await _branchChanged(eventInfo);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error while checking for changes on branch {branch}. Previous commit is {previousBranches[branch.Name].Commit}, Current commit is {branch.Commit}");
                throw;
            }
        }

        async Task raiseBranchAdded(BranchInfo branch)
        {
            try
            {
                _logger.Info($"Detected a new branch {branch.Name}");
                var currentCommit = _repo.Lookup<Commit>(branch.Commit);
                var previousCommit = _repo.Branches["master"].Tip;
                
                var result = _repo.Diff.Compare<TreeChanges>(previousCommit.Tree, currentCommit.Tree);
                _logger.Info($"Finished diff, found {result.Added.Count()} added items, {result.Deleted.Count()} deleted items, {result.Renamed.Count()} renamed items and {result.Modified.Count()} modified items");

                var eventInfo = getBranchChanged<BranchAdded>(result, branch);
                eventInfo.BaseBranch = "master";
                await _branchAdded(eventInfo);

            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error while checking new branch {branch}");
                throw;
            }
        }

        async Task raiseBranchDeleted(BranchInfo branchInfo)
        {
            try
            {
                _logger.Info($"Detected deletion of branch {branchInfo.Name}");
                await _branchRemoved(new BranchRemoved {Branch = branchInfo});
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
                    GetValue = () => getBlobValue(a.Oid)
                }).ToList(),
                Modified = changes.Modified.Select(m => new ItemModified
                {
                    Key = m.Path,
                    GetValue = () => getBlobValue(m.Oid),
                    GetOldValue = () => getBlobValue(m.OldOid)
                }).ToList(),
                Renamed = changes.Renamed.Select(r => new ItemRenamed
                {
                    Key = r.Path,
                    GetValue = () => getBlobValue(r.Oid),
                    GetOldValue = () => getBlobValue(r.OldOid),
                    OldKey = r.OldPath
                }).ToList(),
                Deleted = changes.Deleted.Select(d => new ItemDeleted {Key = d.Path, GetValue = () => getBlobValue(d.OldOid)}).ToList()
            };

        string getBlobValue(ObjectId objectId) =>
            _repo.Lookup<Blob>(objectId).GetContentText();

        public void Dispose() =>
            _timer?.Dispose();
    }
}