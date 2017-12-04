using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Appy.GitDb.Core.Interfaces;
using Appy.GitDb.Core.Model;
using Newtonsoft.Json;

namespace Appy.GitDb.Remote
{
    public class RemoteGitDb : IGitDb
    {
        readonly int _batchSize = 50;
        readonly HttpClient _client;
        readonly string _name;

        public RemoteGitDb(HttpClient client, string name)
        {
            _client = client;
            _name = name;
        }

        public RemoteGitDb(string name, string userName, string password, string url, int batchSize = 50)
        {
            _name = name;
			_batchSize = batchSize;
            _client = new HttpClient{BaseAddress = new Uri(url)};
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{userName}:{password}")));
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }


        string urlEncode(string value) =>
            HttpUtility.UrlEncode(value);

        public Task<string> Get(string branch, string key) =>
            _client.GetAsync<string>($"/data/{_name}/{branch}/document/{urlEncode(key)}");

        public async Task<T> Get<T>(string branch, string key) where T : class
        {
                var result = await Get(branch, key);
                return string.IsNullOrEmpty(result)
                ? null
                : JsonConvert.DeserializeObject<T>(result);
        }

        public async Task<IReadOnlyCollection<T>> GetFiles<T>(string branch, string key) =>
            (await _client.GetAsync<List<string>>($"/data/{_name}/{branch}/documents/{key}"))
                          .Select(JsonConvert.DeserializeObject<T>)
                          .ToArray();

        public async Task<IReadOnlyCollection<string>> GetFiles(string branch, string key) =>
            await _client.GetAsync<List<string>>($"/data/{_name}/{branch}/documents/{key}");

        public Task<string> Save(string branch, string message, Document document, Author author) =>
            _client.PostAsync($"/data/{_name}/{branch}/document", new SaveRequest
            {
                Message = message,
                Document = document,
                Author = author
            }).WhenSuccessful().AsStringResponse();

        public Task<string> Save<T>(string branch, string message, Document<T> document, Author author) =>
            Save(branch, message, Document.From(document), author);

        public Task<string> Delete(string branch, string key, string message, Author author) =>
             _client.PostAsync($"/data/{_name}/{branch}/document/delete", new DeleteRequest
             {
                 Message = message,
                 Key = key,
                 Author = author
             }).WhenSuccessful()
               .AsStringResponse();       

        public Task Tag(Reference reference) =>
            _client.PostAsync($"/data/{_name}/tag", reference)
                   .WhenSuccessful();

        public Task DeleteTag(string tag) =>
            _client.DeleteAsync($"/data/{_name}/tag/{tag}")
                   .WhenSuccessful();

        public Task CreateBranch(Reference reference) =>
            _client.PostAsync($"/data/{_name}/branch", reference)
                   .WhenSuccessful();

        public Task<IEnumerable<string>> GetAllBranches() =>
            _client.GetAsync<IEnumerable<string>>($"/data/{_name}/branch");

        public async Task<ITransaction> CreateTransaction(string branch) =>
            await RemoteTransaction.Create(_client, _name, branch, _batchSize);

        public void Dispose() => 
            _client.Dispose();

        public Task<string> MergeBranch(string source, string target, Author author, string message) =>
            _client.PostAsync($"/data/{_name}/merge", new MergeRequest {Target = target, Source = source, Author = author, Message = message})
                   .WhenSuccessful()
                   .AsStringResponse();

        public Task DeleteBranch(string branch) =>
            _client.DeleteAsync($"/data/{_name}/{branch}")
                   .WhenSuccessful();

        public async Task<Diff> Diff(string reference, string reference2) =>
            JsonConvert.DeserializeObject<Diff>(await _client.GetAsync($"/data/{_name}/diff/{reference}/{reference2}")
                       .WhenSuccessful()
                       .AsStringResponse());

        public async Task<List<CommitInfo>> Log(string reference, string reference2) =>
            JsonConvert.DeserializeObject<List<CommitInfo>>(await _client.GetAsync($"/data/{_name}/log/{reference}/{reference2}")
                .WhenSuccessful()
                .AsStringResponse());

        public Task CloseTransactions(string branch) =>
             _client.PostAsync($"/data/{_name}/{branch}/transactions/close", null)
                    .WhenSuccessful()
                    .AsStringResponse();
    }
}
