using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Ylp.GitDb.Core.Model;
using Ylp.GitDb.Tests.Utils;
using Reference = Ylp.GitDb.Core.Model.Reference;

namespace Ylp.GitDb.Tests
{
    public class MergingWhenThereAreNoConflicts : WithRepo
    {
        int _index;
        readonly List<int> _addedFiles = new List<int>();
        readonly List<int> _removedFiles = new List<int>();
        string _commitBeforeSecondMerge;
        protected override async Task Because()
        {
            Console.WriteLine("Commit on master: " + await addItems("master"));

            await Subject.CreateBranch(new Reference {Name = "test", Pointer = "master"});

            Console.WriteLine("Commit on test: " + await addItems("test"));

            Console.WriteLine("Commit on master: " + await addItems("master"));

            await Subject.CreateBranch(new Reference { Name = "test2", Pointer = "master" });

            Console.WriteLine("Commit on test2: " + await addItems("test2"));

            Console.WriteLine("Commit on test: " + await addItems("test"));

            Console.WriteLine("Commit on master: " + await removeItems("master", 1, 2));

            Console.WriteLine("Commit on test: " + await removeItems("test", 8, 2));

            Console.WriteLine("Commit on test2: " + await removeItems("test2", 13, 2));

            Console.WriteLine("Commit on test2: " + await addItems("test2"));

            Console.WriteLine("Merge test2: " + await Subject.MergeBranch("test2", "master", Author, "This is the merge commit for branch test2"));

            _commitBeforeSecondMerge = Repo.Branches["master"].Tip.Sha;
            Console.WriteLine("Commit before second merge: " + _commitBeforeSecondMerge);

            Console.WriteLine("Merge test: " + await Subject.MergeBranch("test", "master", Author, "This is the merge commit for branch test"));
        }

        async Task<string> addItems(string branch)
        {
            var latestCommit = string.Empty;
            foreach (var i in Enumerable.Range(_index, 5))
            {
                latestCommit = await Subject.Save(branch, $"Added {i} ({branch})", new Document { Key = $"file\\key {i}", Value = i.ToString() }, Author);
                _removedFiles.Remove(i);
                _addedFiles.Add(i);
            }
            _index += 5;
            return latestCommit;
        }

        async Task<string> removeItems(string branch, int start, int count)
        {
            var latestCommit = string.Empty;
            foreach (var i in Enumerable.Range(start, count))
            {
                latestCommit = await Subject.Delete(branch, $"file\\key {i}", $"Deleted {i}({branch})", Author);
                _addedFiles.Remove(i);
                _removedFiles.Add(i);
            }
            return latestCommit;
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
            Repo.Branches["master"].Commits.Skip(1).Take(1).Single().Sha
                                   .Should().Be(_commitBeforeSecondMerge);

        [Fact]
        public async Task DeletesTheMergedBranches() =>
           (await Subject.GetAllBranches()).Should().NotContain(new[]{"test", "test2"});
    }

    public class MergingWhenThereAreNoChanges : WithRepo
    {
        string _masterCommit;
        protected override async Task Because()
        {
            foreach (var i in Enumerable.Range(0, 5))
                await Subject.Save("master", $"Added {i} (master)", new Document { Key = $"file\\key {i}", Value = i.ToString() }, Author);

            _masterCommit = Repo.Branches["master"].Tip.Sha;

            await Subject.CreateBranch(new Reference { Name = "test", Pointer = "master" });
            
            await Subject.MergeBranch("test", "master", Author, "This is the merge commit for branch test");
        }

        [Fact]
        public void DoesNotCreateACommitOnMaster() =>
            Repo.Branches["master"].Tip.Sha
                                   .Should().Be(_masterCommit);

        [Fact]
        public async Task DeletesTheMergedBranch() =>
            (await Subject.GetAllBranches()).Should().NotContain("test");
    }
}
