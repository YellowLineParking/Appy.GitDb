using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using LibGit2Sharp;
using Xunit;
using Ylp.GitDb.Core.Model;
using Ylp.GitDb.Tests.Local.Utils;
using static Ylp.GitDb.Tests.Local.Utils.Utils;
using Reference = Ylp.GitDb.Core.Model.Reference;

namespace Ylp.GitDb.Tests.Local
{
    public class AddingItemsInATransaction : WithRepo
    {
        readonly List<Document> _docs;
        const string Message = "added a file";
        const string Branch = "master";
        readonly Author _author = new Author("author", "author@mail.com");
        public AddingItemsInATransaction()
        {
            _docs = Enumerable.Range(0, 3)
                              .Select(i => new Document {Key = i.ToString(), Value = i.ToString()})
                              .ToList();

            using (var t = Subject.CreateTransaction(Branch))
            {
                t.AddMany(_docs);
                t.Commit(Message, _author);
            }
        }

        [Fact]
        public void CreatesACommitWithTheCorrectAuthor() =>
           Repo.Branches[Branch].Tip.HasTheCorrectMetaData(Message, _author);

        [Fact]
        public void CreatesASingleCommitWithAllKeys() =>
            Repo.Branches[Branch].Tip.Tree
                .Select(e => new Document { Key = e.Path, Value = ((Blob)e.Target).GetContentText() })
                .ShouldBeEquivalentTo(_docs);
    }

    public class OpeningTwoTransactionsSimultaneouslyOnTheSameBranch : WithRepo
    {
        readonly Exception _result;
        const string Branch = "master";

        public OpeningTwoTransactionsSimultaneouslyOnTheSameBranch()
        {
            using (Subject.CreateTransaction(Branch))
                _result = Catch<Exception>(() => Subject.CreateTransaction(Branch));
        }

        [Fact]
        public void ThrowsAnException() =>
            _result.Should().NotBeNull();
    }

    public class OpeningTwoTransactionsSimultaneouslyOnDifferentBranches : WithRepo
    {
        readonly Exception _result;
        const string Branch = "master";
        const string Branch2 = "develop";

        public OpeningTwoTransactionsSimultaneouslyOnDifferentBranches()
        {
            Subject.CreateBranch(new Reference() {Name = Branch2, Pointer = Branch});
            using (Subject.CreateTransaction(Branch))
                _result = Catch<Exception>(() =>
                {
                    using (Subject.CreateTransaction(Branch2)) {}
                });
        }

        [Fact]
        public void DoesNotThrowAnException() =>
            _result.Should().BeNull();
    }
}
