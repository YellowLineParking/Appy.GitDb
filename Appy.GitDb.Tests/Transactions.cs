using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Appy.GitDb.Core.Model;
using Appy.GitDb.Tests.Utils;
using FluentAssertions;
using LibGit2Sharp;
using Xunit;
using Reference = Appy.GitDb.Core.Model.Reference;

namespace Appy.GitDb.Tests
{
    public class AddingItemsInATransaction : WithRepo
    {
        readonly IEnumerable<Document> _docs = Enumerable.Range(0, 3)
                                                         .Select(i => new Document { Key = i.ToString(), Value = i.ToString() });
        const string Message = "added a file";
        const string Branch = "master";

        protected override async Task Because()
        {
            using (var t = await Subject.CreateTransaction(Branch))
            {
                await t.AddMany(_docs);
                await t.Commit(Message, Author);
            }
        }

        [Fact]
        public void CreatesACommitWithTheCorrectAuthor() =>
           Repo.Branches[Branch].Tip.HasTheCorrectMetaData(Message, Author);

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

        protected override async Task Because()
        {
            await Subject.Save(Branch, "msg", new Document {Key = Key, Value = Value}, Author);

            using (var t = await Subject.CreateTransaction(Branch))
            {
                await t.Delete(Key);
                await t.Commit("Deleted file", Author);
            }
        }

        [Fact]
        public async Task RemovesTheItemFromTheBranch() =>
            (await Subject.Get(Branch, Key)).Should().BeNull();
    }

    public class OpeningTwoTransactionsSimultaneouslyOnTheSameBranch : WithRepo
    {
        Exception _result;
        const string Branch = "master";

        protected override async Task Because()
        {
            using (await Subject.CreateTransaction(Branch))
                _result = await Utils.Utils.Catch<Exception>(() => Subject.CreateTransaction(Branch));
        }

        [Fact]
        public void ThrowsAnException() =>
            _result.Should().NotBeNull();
    }

    public class OpeningTwoTransactionsSimultaneouslyOnDifferentBranches : WithRepo
    {
        Exception _result;
        const string Branch = "master";
        const string Branch2 = "develop";

        protected override async Task Because()
        {
            await Subject.CreateBranch(new Reference {Name = Branch2, Pointer = Branch});
            using (await Subject.CreateTransaction(Branch))
                _result = await Utils.Utils.Catch<Exception>(async () =>
                {
                    using (await Subject.CreateTransaction(Branch2)) {}
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
        protected override async Task Because()
        {
            using (var t = await Subject.CreateTransaction("master"))
            {
                await t.Add(new Document {Key = Key, Value = "value"});
                await t.Abort();
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
        Exception _exception;
        protected override async Task Because()
        {
            using (var t = await Subject.CreateTransaction(Branch))
            {
                await t.Add(new Document { Key = Key, Value = "value" });
                await t.Abort();
                _exception = await Utils.Utils.Catch<Exception>(() => t.Commit("message",Author));
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
        Exception _exception;
        protected override async Task Because()
        {
            using (await Subject.CreateTransaction(Branch))
                _exception = await Utils.Utils.Catch<Exception>(() => Subject.Save(Branch, "msg", new Document {Key = Key, Value = "value"}, Author));
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
        protected override async Task Because()
        {
            using (var t = await Subject.CreateTransaction(Branch))
                await t.Commit(Message, Author);
        }

        [Fact]
        public void DoesNotCreateACommit() =>
            Repo.Branches[Branch].Tip.Message.Should().NotBe(Message);
    }

    public class AbortingAllTransactionsForABranch : WithRepo
    {
        const string Branch = "master";
        const string Key = "key";
        protected override async Task Because()
        {
            var t = await Subject.CreateTransaction("master");
            await t.Add(new Document { Key = Key, Value = "value" });

            await Subject.CloseTransactions(Branch);
        }

        [Fact]
        public void DoesNotCreateACommit() =>
            Subject.Get(Branch, Key).Result.Should().BeNull();

        [Fact]
        public async Task AllowsOpeningANewTransaction() =>
            (await Utils.Utils.Catch<Exception>(() => Subject.CreateTransaction(Branch))).Should().BeNull();
    }

    public class AddingAnItemToAnExpiredTransactionWhenNoItemsHaveBeenSaved : WithRepo
    {
        Exception _exception;
        protected override async Task Because()
        {
            using (var t = await Subject.CreateTransaction("master"))
            {
                await t.Add(new Document { Key = "123", Value = "value" });
                Thread.Sleep(TransactionTimeout * 2 * 1000);
                _exception = await Utils.Utils.Catch<Exception>(() => t.Add(new Document { Key = "456", Value = "value" }));
            }
        }

        [Fact]
        public void DoesNotThrowAnException() =>
            _exception.Should().BeNull();
    }

    public class AddingAnItemToAnExpiredTransactionWhenItemsHaveBeenSaved : WithRepo
    {
        Exception _exception;

        protected override async Task Because()
        {
            using (var t = await Subject.CreateTransaction("master"))
            {
                await t.Add(new Document { Key = "123", Value = "value" });
                Thread.Sleep(TransactionTimeout * 2 * 1000);
                await Subject.Save("master", "message", new Document { Key = "key", Value = "value" }, Author);
                _exception = await Utils.Utils.Catch<Exception>(() => t.Add(new Document { Key = "456", Value = "value" }));
            }
        }

        [Fact]
        public void ThrowsAnException() =>
            _exception.Should().BeOfType<ArgumentException>();
    }

    public class AddingManyItemsToAnExpiredTransactionWhenNoItemsHaveBeenSaved : WithRepo
    {
        Exception _exception;
        protected override async Task Because()
        {
            using (var t = await Subject.CreateTransaction("master"))
            {
                await t.Add(new Document { Key = "123", Value = "value" });
                Thread.Sleep(TransactionTimeout * 2 * 1000);
                _exception = await Utils.Utils.Catch<Exception>(() => t.AddMany(new List<Document> { new Document { Key = "456", Value = "value" } }));
            }
        }

        [Fact]
        public void DoesNotThrowAnException() =>
            _exception.Should().BeNull();
    }

    public class AddingManyItemsToAnExpiredTransactionWhenItemsHaveBeenSaved : WithRepo
    {
        Exception _exception;

        protected override async Task Because()
        {
            using (var t = await Subject.CreateTransaction("master"))
            {
                await t.Add(new Document { Key = "123", Value = "value" });
                Thread.Sleep(TransactionTimeout * 2 * 1000);
                await Subject.Save("master", "message", new Document { Key = "key", Value = "value" }, Author);
                _exception = await Utils.Utils.Catch<Exception>(() => t.AddMany(new List<Document> { new Document { Key = "456", Value = "value" }}));
            }
        }

        [Fact]
        public void ThrowsAnException() =>
            _exception.Should().BeOfType<ArgumentException>();
    }

    public class DeletingAnItemFromAnExpiredTransactionWhenNoItemsHaveBeenSaved : WithRepo
    {
        Exception _exception;

        protected override async Task Because()
        {
            using (var t = await Subject.CreateTransaction("master"))
            {
                await t.Add(new Document { Key = "123", Value = "value" });
                Thread.Sleep(TransactionTimeout * 2 * 1000);
                _exception = await Utils.Utils.Catch<Exception>(() => t.Delete("key"));
            }
        }

        

        [Fact]
        public void DoesNotThrowAnException() =>
            _exception.Should().BeNull();
    }

    public class DeletingAnItemFromAnExpiredTransactionWhenItemsHaveBeenSaved : WithRepo
    {
        Exception _exception;

        protected override async Task Because()
        {
            using (var t = await Subject.CreateTransaction("master"))
            {
                await t.Add(new Document { Key = "123", Value = "value" });
                Thread.Sleep(TransactionTimeout * 2 * 1000);
                await Subject.Save("master", "message", new Document { Key = "key", Value = "value" }, Author);
                _exception = await Utils.Utils.Catch<Exception>(() => t.Delete("key"));
            }
        }

        [Fact]
        public void ThrowsAnException() =>
            _exception.Should().BeOfType<ArgumentException>();
    }

    public class DeletingManyItemsFromAnExpiredTransactionWhenNoItemsHaveBeenSaved : WithRepo
    {
        Exception _exception;

        protected override async Task Because()
        {
            using (var t = await Subject.CreateTransaction("master"))
            {
                await t.Add(new Document { Key = "123", Value = "value" });
                Thread.Sleep(TransactionTimeout * 2 * 1000);
                _exception = await Utils.Utils.Catch<Exception>(() => t.DeleteMany(new List<string> { "key" }));
            }
        }

        [Fact]
        public void DoesNotThrowAnException() =>
            _exception.Should().BeNull();
    }

    public class DeletingManyItemsFromAnExpiredTransactionWhenItemsHaveBeenSaved : WithRepo
    {
        Exception _exception;

        protected override async Task Because()
        {
            using (var t = await Subject.CreateTransaction("master"))
            {
                await t.Add(new Document { Key = "123", Value = "value" });
                Thread.Sleep(TransactionTimeout * 2 * 1000);
                await Subject.Save("master", "message", new Document { Key = "key", Value = "value" }, Author);
                _exception = await Utils.Utils.Catch<Exception>(() => t.DeleteMany(new List<string>{"key"}));
            }
        }

        [Fact]
        public void ThrowsAnException() =>
            _exception.Should().BeOfType<ArgumentException>();
    }

    public class CommittingAnExpiredTransactionWhenNoItemsHaveBeenSaved : WithRepo
    {
        Exception _exception;

        protected override async Task Because()
        {
            using (var t = await Subject.CreateTransaction("master"))
            {
                await t.Add(new Document { Key = "123", Value = "value" });
                Thread.Sleep(TransactionTimeout * 2 * 1000);
                _exception = await Utils.Utils.Catch<Exception>(() => t.Commit("message", Author));
            }
        }

        [Fact]
        public void DoesNotThrowAnException() =>
            _exception.Should().BeNull();
    }

    public class CommittingAnExpiredTransactionWhenItemsHaveBeenSaved : WithRepo
    {
        Exception _exception;

        protected override async Task Because()
        {
            using (var t = await Subject.CreateTransaction("master"))
            {
                await t.Add(new Document { Key = "123", Value = "value" });
                Thread.Sleep(TransactionTimeout * 2 * 1000);
                await Subject.Save("master", "message", new Document { Key = "key", Value = "value" }, Author);
                _exception = await Utils.Utils.Catch<Exception>(() => t.Commit("message", Author));
            }
        }

        [Fact]
        public void ThrowsAnException() =>
            _exception.Should().BeOfType<ArgumentException>();
    }
}
