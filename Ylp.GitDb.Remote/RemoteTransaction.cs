using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Ylp.GitDb.Core.Interfaces;
using Ylp.GitDb.Core.Model;

namespace Ylp.GitDb.Remote
{
    class RemoteTransaction : ITransaction
    {
        readonly HttpClient _client;
        readonly string _transactionId;
        bool _isOpen;
        RemoteTransaction(HttpClient client, string transactionId)
        {
            _client = client;
            _transactionId = transactionId;
            _isOpen = true;
        }

        public static async Task<RemoteTransaction> Create(HttpClient client, string branch)
        {
            var transactionId = (await (await client.PostAsync($"/{branch}/transaction", new StringContent("", Encoding.UTF8))
                                                    .WhenSuccessful())
                                                    .Content
                                                    .ReadAsStringAsync())
                                                    .Replace("\"", "");
            return new RemoteTransaction(client, transactionId);
        }

        T executeIfOpen<T>(Func<T> action)
        {
            if (_isOpen)
                return action();
            throw new Exception("Transaction is not open");
        }

        public Task Add(Document document) =>
            executeIfOpen(() => _client.PostAsync($"/{_transactionId}/add", document).WhenSuccessful());

        public Task Add<T>(Document<T> document) => 
            Add(Document.From(document));

        public Task Delete(string key) =>
            executeIfOpen(() => _client.PostAsync($"/{_transactionId}/delete/{key}", new StringContent("", Encoding.UTF8)).WhenSuccessful());

        public Task DeleteMany(IEnumerable<string> keys) =>
            executeIfOpen(async () =>
             {
                 foreach (var batch in keys.Batch(50))
                     await _client.PostAsync($"/{_transactionId}/deleteMany", batch).WhenSuccessful();
             });
            

        public Task AddMany<T>(IEnumerable<Document<T>> documents) =>
            AddMany(documents.AsParallel().Select(Document.From));

        public Task AddMany(IEnumerable<Document> documents) =>
            executeIfOpen(async () =>
            {
                foreach (var batch in documents.Batch(50))
                    await _client.PostAsync($"/{_transactionId}/addMany", batch).WhenSuccessful();
            });

        public async Task<string> Commit(string message, Author author)
        {
            if (!_isOpen)
                throw new Exception("Transaction is not open");

            _isOpen = false;
            return await _client.PostAsync($"/{_transactionId}/commit", new CommitTransaction
            {
                Message = message,
                Author = author
            }).WhenSuccessful()
              .AsStringResponse();
        }

        public async Task Abort()
        {
            if (_isOpen)
                await _client.PostAsync($"/{_transactionId}/abort", new StringContent("", Encoding.UTF8));
            _isOpen = false;
        }

        public void Dispose() =>
            Abort().Wait();
    }
}
