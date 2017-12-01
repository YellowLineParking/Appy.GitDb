using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Appy.GitDb.Core.Interfaces;
using Appy.GitDb.Core.Model;

namespace Appy.GitDb.Local
{
    public class Transaction : ITransaction
    {
        readonly Func<Document, Task> _add;
        readonly Func<string, Author, Task<string>> _commit;
        readonly Func<Task> _abort;
        readonly Func<string, Task> _delete;
        bool _isOpen;

        public Transaction(Func<Document, Task> add,
                           Func<string, Author, Task<string>> commit,
                           Func<Task> abort,
                           Func<string, Task> delete)
        {
            _add = add;
            _commit = commit;
            _abort = abort;
            _delete = delete;
            _isOpen = true;
        }

        T executeIfOpen<T>(Func<T> action)
        {
            if (_isOpen)
                return action();
            throw new Exception("Transaction is not open");
        }

        public Task Add(Document document) =>
            executeIfOpen(() => _add(document));

        public Task Add<T>(Document<T> document) =>
            executeIfOpen(() => _add(Document.From(document)));

        public Task Delete(string key) =>
            executeIfOpen(() => _delete(key));

        public Task DeleteMany(IEnumerable<string> keys) =>
            executeIfOpen(() => Task.WhenAll(keys.Select(Delete)));

        public Task AddMany<T>(IEnumerable<Document<T>> documents) =>
            executeIfOpen(() => Task.WhenAll(documents.Select(Add)));

        public Task AddMany(IEnumerable<Document> documents) =>
            executeIfOpen(() => Task.WhenAll(documents.Select(Add)));

        public async Task<string> Commit(string message, Author author)
        {
            var sha = await executeIfOpen(() => _commit(message, author));
            _isOpen = false;
            return sha;
        }

        public async Task Abort()
        {
            await executeIfOpen(_abort);
            _isOpen = false;
        }
         
        public void Dispose()
        {
            if (_isOpen)
                Abort().Wait();
        }
    }
}