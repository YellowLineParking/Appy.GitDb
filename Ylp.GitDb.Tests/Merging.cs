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
    public class WhenThereAreNoConflicts : WithRepo
    {
        int _index;
        readonly List<int> _addedFiles = new List<int>();
        readonly List<int> _removedFiles = new List<int>();
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

            await Subject.MergeBranch("test2", "master", Author, "This is the merge commit for branch test2");

            _commitBeforeSecondMerge = Repo.Branches["master"].Tip.Sha;

            await Subject.MergeBranch("test", "master", Author, "This is the merge commit for branch test");
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
        public async Task ContainsAllAddedFiles() =>
            (await Subject.GetFiles("master", "file"))
                          .Select(int.Parse)
                          .Should()
                          .Contain(_addedFiles);

        [Fact]
        public async Task DoesNotContainAnyRemovedFiles() =>
            (await Subject.GetFiles("master", "file"))
                          .Select(int.Parse)
                          .Should()
                          .NotContain(_removedFiles);

        [Fact]
        public void CreatesOnlyOneCommitOnMaster() =>
            Repo.Branches["master"].Commits.Skip(1).Take(1).Single().Sha
                                   .Should().Be(_commitBeforeSecondMerge);

        [Fact]
        public async Task DeletesTheBranches() =>
           (await Subject.GetAllBranches()).Should().NotContain(new[]{"test", "test2"});
    }

    public class WhenThereAreNoChanges : WithRepo
    {
        string _masterCommit;
        protected override async Task Because()
        {
            foreach (var i in Enumerable.Range(0, 5))
                await Subject.Save("master", $"Added {i} (master)", new Document { Key = $"file\\key {i}", Value = i.ToString() }, Author);

            _masterCommit = Repo.Branches["master"].Tip.Sha;

            await Subject.CreateBranch(new Reference { Name = "test", Pointer = "master" });
            
            await Subject.MergeBranch("test", "master", Author, "This is the merge commit for branch test");
            MoveToNormalRepo("d:\\testmerge");
        }

        [Fact]
        public void DoesNotCreateACommitOnMaster() =>
            Repo.Branches["master"].Tip.Sha
                                   .Should().Be(_masterCommit);

        [Fact]
        public async Task DeletesTheOriginalBranch() =>
            (await Subject.GetAllBranches()).Should().NotContain("test");
    }
}
