using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Appy.GitDb.Core.Model;
using Appy.GitDb.Tests.Utils;
using FluentAssertions;
using LibGit2Sharp;
using Xunit;
using RebaseResult = Appy.GitDb.Core.Model.RebaseResult;
using Reference = Appy.GitDb.Core.Model.Reference;

namespace Appy.GitDb.Tests
{
    public class RebasingWhenThereAreNoConflicts : WithRepo
    {
        int _index;
        readonly List<int> _addedFiles = new List<int>();
        readonly List<int> _removedFiles = new List<int>();
        RebaseInfo _rebaseResult;

        protected override async Task Because()
        {
            await addItems("master");

            await Subject.CreateBranch(new Reference { Name = "test", Pointer = "master" });

            await addItems("test");

            await addItems("master");

            await addItems("test");

            await removeItems("master", 1, 2);

            await removeItems("test", 8, 2);

            _rebaseResult = await Subject.RebaseBranch("test", "master", Author, "This is the rebase commit for branch test");

        }

        async Task addItems(string branch)
        {
            foreach (var i in Enumerable.Range(_index, 5))
            {
                await Subject.Save(branch, $"Added {i} ({branch})", new Document { Key = $"file\\key {i}", Value = i.ToString() }, Author);
                _removedFiles.Remove(i);
                _addedFiles.Add(i);
            }

            _index += 5;
        }

        async Task removeItems(string branch, int start, int count)
        {
            foreach (var i in Enumerable.Range(start, count))
            {
                await Subject.Delete(branch, $"file\\key {i}", $"Deleted {i}({branch})", Author);
                _addedFiles.Remove(i);
                _removedFiles.Add(i);
            }
        }

        [Fact]
        public void RebaseShouldSuccedWithValidInfo() =>
            _rebaseResult.ShouldBeEquivalentTo(RebaseInfo.Succeeded("test", "master", Repo.Branches["test"].Tip.Sha));

        [Fact]
        public async Task AddsTheCorrectFilesToTheBranch() =>
            (await Subject.GetFiles("test", "file"))
            .Select(int.Parse)
            .Should()
            .Contain(_addedFiles);

        [Fact]
        public async Task RemovesTheCorrectFilesFromTheBranch() =>
            (await Subject.GetFiles("test", "file"))
            .Select(int.Parse)
            .Should()
            .NotContain(_removedFiles);
    }



    public class RebasingWhenThereAreNoChanges : WithRepo
    {
        string _masterCommit;
        RebaseInfo _rebaseResult;

        protected override async Task Because()
        {
            foreach (var i in Enumerable.Range(0, 5))
                await Subject.Save("master", $"Added {i} (master)", new Document { Key = $"file\\key {i}", Value = i.ToString() }, Author);

            _masterCommit = Repo.Branches["master"].Tip.Sha;

            await Subject.CreateBranch(new Reference { Name = "test", Pointer = "master" });

            _rebaseResult = await Subject.RebaseBranch("test", "master", Author, "This is the rebase commit for branch test");
        }

        [Fact]
        public void RebaseShouldSuccedWithValidInfo() =>
            _rebaseResult.ShouldBeEquivalentTo(RebaseInfo.Succeeded("test", "master", string.Empty));

        [Fact]
        public void DoesNotCreateACommitOnSourceBranch() =>
            Repo.Branches["test"].Tip.Sha
                .Should().Be(_masterCommit);
    }

    public class RebasingWhenThereConflictsWithChangedValues : WithRepo
    {
        RebaseInfo _rebaseResult;

        protected override async Task Because()
        {
            await Subject.CreateBranch(new Reference { Name = "test", Pointer = "master" });

            await addItem("master");

            await addItem("test", changeValuesForKeys: true);

            _rebaseResult = await Subject.RebaseBranch("test", "master", Author, "This is the rebase commit for branch test");
        }

        async Task addItem(string branch, bool changeValuesForKeys = false)
        {
            foreach (var i in Enumerable.Range(1, 2))
                await Subject.Save(branch, $"Added {i} ({branch})", new Document { Key = $"file\\key {i}", Value = MapToDocumentValue(i, changeValuesForKeys) }, Author);
        }

        static string MapToDocumentValue(int value, bool multiple = false) =>
            (multiple
                ? value * 2
                : value).ToString();

        [Fact]
        public void ShouldNotSucceedAndReturnConflicts()
        {
            _rebaseResult.Conflicts.ForEach((c, i) =>
            {
                var sourceBlob = Repo.Lookup<Blob>(new ObjectId(c.SourceSha));
                var targetBlob = Repo.Lookup<Blob>(new ObjectId(c.TargetSha));
                var sourceValue = sourceBlob.GetContentText();
                var targetValue = targetBlob.GetContentText();
                var value = i + 1;

                sourceValue.Should().Be(MapToDocumentValue(value, true));
                targetValue.Should().Be(MapToDocumentValue(value));

                c.SourceSha = null;
                c.TargetSha = null;
            });

            _rebaseResult.ShouldBeEquivalentTo(new RebaseInfo
            {
                Message = "Could not rebase test onto master because of conflicts. Please merge manually",
                SourceBranch = "test",
                TargetBranch = "master",
                Status = RebaseResult.Conflicts,
                Conflicts = Enumerable.Range(1, 2).Select(i => new ConflictInfo { Type = ConflictType.Change, Path = $"file\\key {i}" }).ToList()
            });
        }
    }


    public class RebasingWhenThereConflictsWithRemovedValues : WithRepo
    {
        RebaseInfo _rebaseResult;
        protected override async Task Because()
        {
            await addItems("master");

            await Subject.CreateBranch(new Reference { Name = "test", Pointer = "master" });

            await removeItems("test", 1, 2);

            await addItems("master", changeValuesForKeys: true);

            _rebaseResult = await Subject.RebaseBranch("test", "master", Author, "This is the rebase commit for branch test");

        }

        async Task addItems(string branch, bool changeValuesForKeys = false)
        {
            foreach (var i in Enumerable.Range(1, 2))
                await Subject.Save(branch, $"Added {i} ({branch})", new Document { Key = $"file\\key {i}", Value = MapToDocumentValue(i, changeValuesForKeys) }, Author);
        }

        async Task removeItems(string branch, int start, int count)
        {
            foreach (var i in Enumerable.Range(start, count))
                await Subject.Delete(branch, $"file\\key {i}", $"Deleted {i}({branch})", Author);
        }

        static string MapToDocumentValue(int value, bool multiple = false) => (multiple ? value * 2 : value).ToString();

        [Fact]
        public void ShouldNotSucceedAndReturnConflicts()
        {
            _rebaseResult.Conflicts.ForEach((c, i) =>
            {
                var targetBlob = Repo.Lookup<Blob>(new ObjectId(c.TargetSha));
                var targetValue = targetBlob.GetContentText();
                var value = i + 1;

                c.SourceSha.Should().BeNull();
                targetValue.Should().Be(MapToDocumentValue(value, true));

                c.TargetSha = null;
            });

            _rebaseResult.ShouldBeEquivalentTo(new RebaseInfo
            {
                Message = "Could not rebase test onto master because of conflicts. Please merge manually",
                SourceBranch = "test",
                TargetBranch = "master",
                Status = RebaseResult.Conflicts,
                Conflicts = Enumerable.Range(1, 2).Select(i => new ConflictInfo { Type = ConflictType.Remove, Path = $"file\\key {i}" }).ToList()
            });
        }
    }
}
