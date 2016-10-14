using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Ylp.GitDb.Core.Model;
using Ylp.GitDb.Tests.Utils;
using Ylp.GitDb.Watcher;

namespace Ylp.GitDb.Tests.Watcher
{
    public class AddingAFile : WithWatcher
    {
        protected override Task Because() =>
            GitDb.Save("master", "save this", new Document {Key = "key", Value = "value"}, Author);

        [Fact]
        public void RaisesBranchChangedEvent() =>
            Subject.ShouldRaise("BranchChanged")
                   .WithArgs<BranchChanged>(args => args.BranchName == "master" &&
                                                    args.Added.Any(item => item.Key == "key" && item.Value == "value"));
    }

    public class RemovingAFile : WithWatcher
    {
        protected override Task Setup() =>
            GitDb.Save("master", "save this", new Document { Key = "key", Value = "value" }, Author);

        protected override Task Because() => 
            GitDb.Delete("master", "key", "message", Author);

        [Fact]
        public void RaisesBranchChangedEvent() =>
            Subject.ShouldRaise("BranchChanged")
                   .WithArgs<BranchChanged>(args => args.BranchName == "master" &&
                                                    args.Deleted.Any(item => item.Key == "key"));
    }

    public class ModifyingAFile : WithWatcher
    {
        protected override Task Setup() =>
            GitDb.Save("master", "save this", new Document { Key = "key", Value = "value" }, Author);

        protected override Task Because() =>
            GitDb.Save("master", "save this", new Document { Key = "key", Value = "value2" }, Author);

        [Fact]
        public void RaisesBranchChangedEvent() =>
            Subject.ShouldRaise("BranchChanged")
                   .WithArgs<BranchChanged>(args => args.BranchName == "master" &&
                                                    args.Modified.Any(item => item.Key == "key" &&
                                                                              item.Value == "value2" &&
                                                                              item.OldValue == "value"));   
    }

    public class RenamingAFile : WithWatcher
    {
        protected override Task Setup() =>
            GitDb.Save("master", "save this", new Document { Key = "key", Value = "value" }, Author);

        protected override async Task Because()
        {
            using (var t = await GitDb.CreateTransaction("master"))
            {
                await t.Add(new Document {Key = "subdir\\key", Value = "value"});
                await t.Delete("key");
                await t.Commit("message", Author);
            }
        }

        [Fact]
        public void RaisesBranchChangedEvent() =>
            Subject.ShouldRaise("BranchChanged")
                   .WithArgs<BranchChanged>(args => args.BranchName == "master" &&
                                                    args.Renamed.Any(item => item.Key == "subdir\\key" &&
                                                                             item.OldKey == "key" && 
                                                                             item.Value == "value" &&
                                                                             item.OldValue == "value"));
    }
}
