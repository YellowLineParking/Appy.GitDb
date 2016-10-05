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

    public class DeletingItemsFromATransaction : WithRepo
    {
        const string Key = "Key";
        const string Value = "Value";
        const string Branch = "master";
        readonly Author _author = new Author("author", "author@mail.com");
        public DeletingItemsFromATransaction()
        {
            Subject.Save(Branch, "msg", new Document {Key = Key, Value = Value}, _author).Wait();

            using (var t = Subject.CreateTransaction(Branch))
            {
                t.Delete(Key).Wait();
                t.Commit("Deleted file", _author);
            }
        }

        [Fact]
        public void RemovesTheItemFromTheBranch() =>
            Subject.Get(Branch, Key).Result.Should().BeNull();
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

    public class AbortingATransaction : WithRepo
    {
        const string Branch = "master";
        const string Key = "key";
        public AbortingATransaction()
        {
            using (var t = Subject.CreateTransaction("master"))
            {
                t.Add(new Document {Key = Key, Value = "value"});
                t.Abort();
            }
        }

        [Fact]
        public void DoesNotCreateACommit() =>
            Subject.Get(Branch, Key).Result.Should().BeNull();
    }

    public class AddingAFileToAnAbortedTransaction : WithRepo
    {
        const string Branch = "master";
        const string Key = "key";
        readonly Exception _exception;
        public AddingAFileToAnAbortedTransaction()
        {
            using (var t = Subject.CreateTransaction(Branch))
            {
                t.Add(new Document { Key = Key, Value = "value" });
                t.Abort();
                _exception = Catch<Exception>(() => t.Commit("message", new Author("name", "email")).Wait());
            }
        }

        [Fact]
        public void DoesNotCreateACommit() =>
            Subject.Get(Branch, Key).Result.Should().BeNull();

        [Fact]
        public void ThrowsAnException() =>
            _exception.Should().NotBeNull();
    }

    public class SavingAFileWhileATransactionIsInProgress : WithRepo
    {
        const string Branch = "master";
        const string Key = "key";
        readonly Exception _exception;
        public SavingAFileWhileATransactionIsInProgress()
        {
            using (var t = Subject.CreateTransaction(Branch))
            {
                _exception = Catch<Exception>(() =>Subject.Save(Branch, "msg", new Document {Key = Key, Value = "value"}, new Author("name", "email")).Wait());
            }
        }

        [Fact]
        public void DoesNotCreateACommit() =>
            Subject.Get(Branch, Key).Result.Should().BeNull();

        [Fact]
        public void ThrowsAnException() =>
           _exception.Should().NotBeNull();
    }

    public class CommitingATransactionWithoutFiles : WithRepo
    {
        const string Branch = "master";
        const string Message = "message";
        public CommitingATransactionWithoutFiles()
        {
            using (var t = Subject.CreateTransaction(Branch))
                t.Commit(Message, new Author("name", "email"));
        }

        [Fact]
        public void DoesNotCreateACommit() =>
            Repo.Branches[Branch].Tip.Message.Should().NotBe(Message);
    }
}
