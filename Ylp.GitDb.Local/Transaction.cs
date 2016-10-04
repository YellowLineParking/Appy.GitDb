using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ylp.GitDb.Core.Interfaces;
using Ylp.GitDb.Core.Model;

namespace Ylp.GitDb.Local
{
    public class Transaction : ITransaction
    {
        readonly Func<Document, Task> _add;
        readonly Func<string, Author, Task<string>> _commit;
        readonly Func<Task> _abort;
        readonly Func<string, Task> _delete;
        bool _isOpen = false;

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

        public Task Add(Document document) =>
            _add(document);

        public Task Add<T>(Document<T> document) =>
            _add(Document.From(document));

        public Task Delete(string key) =>
            _delete(key);

        public Task DeleteMany(IEnumerable<string> keys) =>
            Task.WhenAll(keys.Select(Delete));

        public Task AddMany<T>(IEnumerable<Document<T>> documents) =>
            Task.WhenAll(documents.Select(Add));

        public Task AddMany(IEnumerable<Document> documents) =>
            Task.WhenAll(documents.Select(Add));

        public async Task<string> Commit(string message, Author author)
        {
            var sha = await _commit(message, author);
            _isOpen = false;
            return sha;
        }

        public async Task Abort()
        {
            await _abort();
            _isOpen = false;
        }
         
        public void Dispose()
        {
            if (!_isOpen)
                Abort().Wait();
        }
    }
}