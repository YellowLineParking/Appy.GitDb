using System;
using System.Linq;
using System.Threading.Tasks;
using Appy.GitDb.Core.Model;
using Appy.GitDb.Tests.Utils;
using FluentAssertions;
using Xunit;

namespace Appy.GitDb.Tests.Watcher
{
    public class AddingABranchAtTheSameCommitAsAnotherBranch : WithWatcher
    {
        protected override Task Because() =>
            GitDb.CreateBranch(new Reference {Name = "test", Pointer = "master"});

        [Fact]
        public void RaisesABranchAddedEvent() =>
           BranchAdded.Should().Contain(args => args.BaseBranch == "master" &&
                                                args.Branch.Name == "test" && 
                                                args.Branch.Commit == Repo.Branches["master"].Tip.Sha);
    }

    public class AddingABranchAtACommitFromADifferentBranch : WithWatcher
    {
        string _pointer;

        protected override Task Because()
        {
            var rnd = new Random().Next(0, Repo.Branches["master"].Commits.Count());
            _pointer = Repo.Branches["master"].Commits.Skip(rnd).First().Sha;
            return GitDb.CreateBranch(new Reference { Name = "test", Pointer = _pointer });
        }
            

        [Fact]
        public void RaisesABranchAddedEvent() =>
            BranchAdded.Should().Contain(args => args.BaseBranch == "master" &&
                                                 args.Branch.Name == "test" &&
                                                 args.Branch.Commit == _pointer &&
                                                 args.Deleted.Any());
    }

    public class RemovingABranch : WithWatcher
    {
        protected override async Task Because()
        {
            Repo.Branches.Add("test", "master");
            await Task.Delay(10);
            Repo.Branches.Remove("test");
        }

        [Fact]
        public void RaisesABranchRemovedEvent() =>
            BranchRemoved.Should().Contain(args => args.Branch.Name == "test");
    }
}
