using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Appy.GitDb.Core.Model;
using Appy.GitDb.Tests.Utils;
using Castle.Core.Internal;
using FluentAssertions;
using LibGit2Sharp;
using Xunit;
using MergeStatus = Appy.GitDb.Core.Model.MergeStatus;
using Reference = Appy.GitDb.Core.Model.Reference;

namespace Appy.GitDb.Tests
{
    public class MergingWhenThereAreNoConflicts : WithRepo
    {
        int _index;
        readonly List<int> _addedFiles = new List<int>();
        readonly List<int> _removedFiles = new List<int>();
        MergeInfo _firstMergeResult;
        MergeInfo _firstMergeExpected;
        MergeInfo _secondMergeResult;
        MergeInfo _secondMergeExpected;
        string _commitBeforeSecondMerge;
        protected override async Task Because()
        {
            await addItems("master");

            await Subject.CreateBranch(new Reference {Name = "test", Pointer = "master"});

            await addItems("test");

            await addItems("master");

            await Subject.CreateBranch(new Reference { Name = "test2", Pointer = "master" });

            await addItems("test2");

            await addItems("test");

            await removeItems("master", 1, 2);

            await removeItems("test", 8, 2);

            await removeItems("test2", 13, 2);

            await addItems("test2");

            _firstMergeResult = await Subject.MergeBranch("test2", "master", Author, "This is the merge commit for branch test2");

            _commitBeforeSecondMerge = Repo.Branches["master"].Tip.Sha;

            _firstMergeExpected = MergeInfo.NewSucceded("test2", "master", Repo.Branches["master"].Tip.Sha);

            _secondMergeResult = await Subject.MergeBranch("test", "master", Author, "This is the merge commit for branch test");

            _secondMergeExpected = MergeInfo.NewSucceded("test", "master", Repo.Branches["master"].Tip.Sha);
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
        public void MergesShouldSuccedWithValidInfo()
        {
            _firstMergeResult.ShouldBeEquivalentTo(_firstMergeExpected);
            _secondMergeResult.ShouldBeEquivalentTo(_secondMergeExpected);
        }

        [Fact]
        public async Task AddsTheCorrectFilesToMaster() =>
            (await Subject.GetFiles("master", "file"))
                          .Select(int.Parse)
                          .Should()
                          .Contain(_addedFiles);

        [Fact]
        public async Task RemovesTheCorrectFilesFromMaster() =>
            (await Subject.GetFiles("master", "file"))
                          .Select(int.Parse)
                          .Should()
                          .NotContain(_removedFiles);

        [Fact]
        public void CreatesOnlyOneCommitOnMaster() =>
            Repo.Branches["master"].Tip.Parents.Single().Sha
                                   .Should().Be(_commitBeforeSecondMerge);

        [Fact]
        public async Task DeletesTheMergedBranches() =>
           (await Subject.GetAllBranches()).Should().NotContain(new[]{"test", "test2"});
    }

    public class MergingWhenThereAreNoChanges : WithRepo
    {
        string _masterCommit;
        MergeInfo _mergeResult;
        MergeInfo _mergeExpected;

        protected override async Task Because()
        {
            foreach (var i in Enumerable.Range(0, 5))
                await Subject.Save("master", $"Added {i} (master)", new Document { Key = $"file\\key {i}", Value = i.ToString() }, Author);

            _masterCommit = Repo.Branches["master"].Tip.Sha;

            await Subject.CreateBranch(new Reference { Name = "test", Pointer = "master" });
            
            _mergeResult = await Subject.MergeBranch("test", "master", Author, "This is the merge commit for branch test");

            _mergeExpected = MergeInfo.NewSucceded("test", "master", string.Empty);
        }

        [Fact]
        public void ShouldSuccedWithValidInfo() =>
            _mergeResult.ShouldBeEquivalentTo(_mergeExpected);

        [Fact]
        public void DoesNotCreateACommitOnMaster() =>
            Repo.Branches["master"].Tip.Sha
                                   .Should().Be(_masterCommit);

        [Fact]
        public async Task DeletesTheMergedBranch() =>
            (await Subject.GetAllBranches()).Should().NotContain("test");
    }

    public class MergingWhenThereConflictsWithChangedValues : WithRepo
    {
        MergeInfo _mergeResult;
        protected override async Task Because()
        {
            await Subject.CreateBranch(new Reference { Name = "test", Pointer = "master" });

            await addItem("master");

            await addItem("test", changeValuesForKeys: true);
            
            _mergeResult = await Subject.MergeBranch("test", "master", Author, "This is the merge commit for branch test");
        }

        async Task addItem(string branch, bool changeValuesForKeys = false)
        {
            foreach (var i in Enumerable.Range(1, 2))
                await Subject.Save(branch, $"Added {i} ({branch})", new Document { Key = $"file\\key {i}", Value = MapToDocumentValue(i, changeValuesForKeys) }, Author);
        }

        private static string MapToDocumentValue(int value, bool multiple = false) =>
            (!multiple ? value : value * 2).ToString();

        [Fact]
        public void ShouldNotSucceedAndReturnConflicts()
        {
            _mergeResult.Conflicts.ForEach((c, i) =>
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

            var mergeExpected = new MergeInfo
            {
                Message = "Could not merge test into master because of conflicts. Please merge manually",
                SourceBranch = "test",
                TargetBranch = "master",
                Status = MergeStatus.Conflicts,
                Conflicts = Enumerable.Range(1, 2).Select(i => new ConflictInfo{ Type = ConflictType.Change, Path = $"file\\key {i}"}).ToList()
            };
            _mergeResult.ShouldBeEquivalentTo(mergeExpected);
        }
    }

    public class MergingWhenThereConflictsWithRemovedValues : WithRepo
    {
        MergeInfo _mergeResult;
        protected override async Task Because()
        {
            await addItems("master");

            await Subject.CreateBranch(new Reference { Name = "test", Pointer = "master" });

            await removeItems("test", 1, 2);

            await addItems("master", changeValuesForKeys: true);

            _mergeResult = await Subject.MergeBranch("test", "master", Author, "This is the merge commit for branch test");

        }

        async Task addItems(string branch, bool changeValuesForKeys = false)
        {
            foreach (var i in Enumerable.Range(1, 2))
            {
                await Subject.Save(branch, $"Added {i} ({branch})", new Document { Key = $"file\\key {i}", Value = MapToDocumentValue(i, changeValuesForKeys) }, Author);
            }
        }

        async Task removeItems(string branch, int start, int count)
        {
            foreach (var i in Enumerable.Range(start, count))
            {
                await Subject.Delete(branch, $"file\\key {i}", $"Deleted {i}({branch})", Author);
            }
        }

        private static string MapToDocumentValue(int value, bool multiple = false) =>
            (!multiple ? value : value * 2).ToString();


        [Fact]
        public void ShouldNotSucceedAndReturnConflicts()
        {
            _mergeResult.Conflicts.ForEach((c, i) =>
            {
                var sourceBlob = Repo.Lookup<Blob>(new ObjectId(c.SourceSha));
                var targetBlob = Repo.Lookup<Blob>(new ObjectId(c.TargetSha));
                var sourceValue = sourceBlob.GetContentText();
                var targetValue = targetBlob.GetContentText();
                var value = i + 1;

                sourceValue.Should().Be(MapToDocumentValue(value));
                targetValue.Should().Be(MapToDocumentValue(value, true));

                c.SourceSha = null;
                c.TargetSha = null;
            });

            var mergeExpected = new MergeInfo
            {
                Message = "Could not merge test into master because of conflicts. Please merge manually",
                SourceBranch = "test",
                TargetBranch = "master",
                Status = MergeStatus.Conflicts,
                Conflicts = Enumerable.Range(1, 2).Select(i => new ConflictInfo { Type = ConflictType.Remove, Path = $"file\\key {i}" }).ToList()
            };
            _mergeResult.ShouldBeEquivalentTo(mergeExpected);
        }
    }

}
