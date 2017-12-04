using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Appy.GitDb.Core.Interfaces;
using Appy.GitDb.Core.Model;

namespace Appy.GitDb.Remote
{
    class RemoteTransaction : ITransaction
    {
        readonly HttpClient _client;
        readonly string _name;
        readonly string _transactionId;
        readonly int _batchSize;
        bool _isOpen;
        RemoteTransaction(HttpClient client, string name, string transactionId, int batchSize)
        {
            _client = client;
            _name = name;
            _transactionId = transactionId;
            _batchSize = batchSize;
            _isOpen = true;
        }

        public static async Task<RemoteTransaction> Create(HttpClient client, string repo, string branch, int batchSize)
        {
            var transactionId = (await (await client.PostAsync($"/data/{repo}/{branch}/transaction", new StringContent("", Encoding.UTF8))
                                                    .WhenSuccessful())
                                                    .Content
                                                    .ReadAsStringAsync())
                                                    .Replace("\"", "");
            return new RemoteTransaction(client, repo, transactionId, batchSize);
        }

        T executeIfOpen<T>(Func<T> action)
        {
            if (_isOpen)
                return action();
            throw new Exception("Transaction is not open");
        }

        public Task Add(Document document) =>
            executeIfOpen(() => _client.PostAsync($"/data/{_name}/{_transactionId}/add", document).WhenSuccessful());

        public Task Add<T>(Document<T> document) => 
            Add(Document.From(document));

        public Task Delete(string key) =>
            executeIfOpen(() => _client.PostAsync($"/data/{_name}/{_transactionId}/delete/{key}", new StringContent("", Encoding.UTF8)).WhenSuccessful());

        public Task DeleteMany(IEnumerable<string> keys) =>
            executeIfOpen(async () =>
             {
                 foreach (var batch in keys.Batch(_batchSize))
                     await _client.PostAsync($"/data/{_name}/{_transactionId}/deleteMany", batch).WhenSuccessful();
             });
            

        public Task AddMany<T>(IEnumerable<Document<T>> documents) =>
            AddMany(documents.AsParallel().Select(Document.From));

        public Task AddMany(IEnumerable<Document> documents) =>
            executeIfOpen(async () =>
            {
                foreach (var batch in documents.Batch(_batchSize))
                    await _client.PostAsync($"/data/{_name}/{_transactionId}/addMany", batch).WhenSuccessful();
            });

        public async Task<string> Commit(string message, Author author)
        {
            if (!_isOpen)
                throw new Exception("Transaction is not open");

            _isOpen = false;
            return await _client.PostAsync($"/data/{_name}/{_transactionId}/commit", new CommitTransaction
            {
                Message = message,
                Author = author
            }).WhenSuccessful()
              .AsStringResponse();
        }

        public async Task Abort()
        {
            if (_isOpen)
                await _client.PostAsync($"/data/{_name}/{_transactionId}/abort", new StringContent("", Encoding.UTF8));
            _isOpen = false;
        }

        public void Dispose() =>
            Abort().Wait();
    }
}
