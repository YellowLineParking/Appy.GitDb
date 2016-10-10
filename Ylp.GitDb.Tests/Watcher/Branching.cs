using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Ylp.GitDb.Core.Model;
using Ylp.GitDb.Tests.Utils;
using Ylp.GitDb.Watcher;

namespace Ylp.GitDb.Tests.Watcher
{
    public class AddingABranch : WithWatcher
    {
        protected override Task Because() =>
            GitDb.CreateBranch(new Reference {Name = "test", Pointer = "master"});

        [Fact]
        public void RaisesABranchAddedEvent() =>
            Subject.ShouldRaise("BranchAdded")
                   .WithArgs<BranchAdded>(args => args.BaseBranch == "master" &&
                                                  args.BranchName == "test" && 
                                                  args.Commit == Repo.Branches["master"].Tip.Sha);
    }

    public class RemovingABranch : WithWatcher
    {
        protected override Task Because()
        {
            Repo.Branches.Remove("master");
            return base.Because();
        }

        [Fact]
        public void RaisesABranchAddedEvent() =>
            Subject.ShouldRaise("BranchRemoved")
                   .WithArgs<BranchRemoved>(args => args.BranchName == "master");
    }
}
