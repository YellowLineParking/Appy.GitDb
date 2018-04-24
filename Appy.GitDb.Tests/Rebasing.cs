using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Appy.GitDb.Core.Model;
using Appy.GitDb.Tests.Utils;
using FluentAssertions;
using Xunit;
using Reference = Appy.GitDb.Core.Model.Reference;

namespace Appy.GitDb.Tests
{
    public class RebasingWhenThereAreNoConflicts : WithRepo
    {
        int _index;
        readonly List<int> _addedFiles = new List<int>();
        readonly List<int> _removedFiles = new List<int>();

        protected override async Task Because()
        {
            await addItems("master");

            await Subject.CreateBranch(new Reference {Name = "test", Pointer = "master"});

            await addItems("test");

            await addItems("master");

            await addItems("test");

            await removeItems("master", 1, 2);

            await removeItems("test", 8, 2);

            await Subject.RebaseBranch("test", "master", Author, "This is the rebase commit for branch test");
        }

        async Task addItems(string branch)
        {
            foreach (var i in Enumerable.Range(_index, 5))
            {
                await Subject.Save(branch, $"Added {i} ({branch})", new Document {Key = $"file\\key {i}", Value = i.ToString()}, Author);
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
        protected override async Task Because()
        {
            foreach (var i in Enumerable.Range(0, 5))
                await Subject.Save("master", $"Added {i} (master)", new Document { Key = $"file\\key {i}", Value = i.ToString() }, Author);

            _masterCommit = Repo.Branches["master"].Tip.Sha;

            await Subject.CreateBranch(new Reference { Name = "test", Pointer = "master" });
            
            await Subject.RebaseBranch("test", "master", Author, "This is the rebase commit for branch test");
        }

        [Fact]
        public void DoesNotCreateACommitOnSourceBranch() =>
            Repo.Branches["test"].Tip.Sha
                                   .Should().Be(_masterCommit);
    }
}
