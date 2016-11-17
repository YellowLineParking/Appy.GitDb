using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using GitTest.RemoteGitDb;
using Newtonsoft.Json;
using Ylp.GitDb.Core.Interfaces;
using Ylp.GitDb.Core.Model;

namespace Ylp.GitDb.Remote
{
    public class RemoteGitDb : IGitDb
    {
        readonly HttpClient _client;
        readonly string _baseUrl;
        public RemoteGitDb(HttpClient client, string url)
        {
            _baseUrl = url;
            _client = client;
        }

        public RemoteGitDb(string userName, string password, string url)
        {
            _baseUrl = url;
            _client = new HttpClient();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{userName}:{password}")));
        }

        string url(string resource) =>
            _baseUrl + resource;

        public Task<string> Get(string branch, string key) =>
            _client.GetAsync<string>(url($"/{branch}/document/{key}"));

        public async Task<T> Get<T>(string branch, string key) where T : class =>
            JsonConvert.DeserializeObject<T>(await _client.GetAsync<string>(url($"/{branch}/document/{key}")));

        public async Task<IReadOnlyCollection<T>> GetFiles<T>(string branch, string key) =>
            (await _client.GetAsync<List<string>>(url($"/{branch}/documents/{key}")))
                          .Select(JsonConvert.DeserializeObject<T>)
                          .ToArray();

        public async Task<IReadOnlyCollection<string>> GetFiles(string branch, string key) =>
            await _client.GetAsync<List<string>>(url($"/{branch}/documents/{key}"));

        public Task<string> Save(string branch, string message, Document document, Author author) =>
            _client.PostAsync(url($"/{branch}/document"), new SaveRequest
            {
                Message = message,
                Document = document,
                Author = author
            }).WhenSuccessful().AsStringResponse();

        public Task<string> Save<T>(string branch, string message, Document<T> document, Author author) =>
            Save(branch, message, Document.From(document), author);

        public Task<string> Delete(string branch, string key, string message, Author author) =>
             _client.PostAsync(url($"/{branch}/document/delete"), new DeleteRequest
             {
                 Message = message,
                 Key = key,
                 Author = author
             }).AsStringResponse();       

        public Task Tag(Reference reference) =>
            _client.PostAsync(url("/tag"), reference);

        public Task CreateBranch(Reference reference) =>
            _client.PostAsync(url("/branch"), reference);

        public Task<IEnumerable<string>> GetAllBranches() =>
            _client.GetAsync<IEnumerable<string>>(url("/branch"));

        public async Task<ITransaction> CreateTransaction(string branch) =>
            await RemoteTransaction.Create(_client, _baseUrl, branch);

        public void Dispose() => 
            _client.Dispose();
    }
}
