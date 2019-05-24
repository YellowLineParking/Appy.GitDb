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
        readonly Func<IEnumerable<Document>, Task> _addMany;
        readonly Func<string, Author, Task<string>> _commit;
        readonly Func<Task> _abort;
        readonly Func<string, Task> _delete;
        readonly Func<IEnumerable<string>, Task> _deleteMany;
        bool _isOpen;

        public Transaction(Func<Document, Task> add,
                           Func<IEnumerable<Document>, Task> addMany,
                           Func<string, Author, Task<string>> commit,
                           Func<Task> abort,
                           Func<string, Task> delete,
                           Func<IEnumerable<string>, Task> deleteMany)
        {
            _add = add;
            _addMany = addMany;
            _commit = commit;
            _abort = abort;
            _delete = delete;
            _deleteMany = deleteMany;
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
            executeIfOpen(() => _deleteMany(keys));

        public Task AddMany<T>(IEnumerable<Document<T>> documents) =>
            executeIfOpen(() => _addMany(documents.Select(Document.From)));

        public Task AddMany(IEnumerable<Document> documents) =>
            executeIfOpen(() => _addMany(documents));

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