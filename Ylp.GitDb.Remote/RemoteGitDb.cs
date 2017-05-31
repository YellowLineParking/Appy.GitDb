﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Ylp.GitDb.Core.Interfaces;
using Ylp.GitDb.Core.Model;

namespace Ylp.GitDb.Remote
{
    public class RemoteGitDb : IGitDb
    {
        readonly HttpClient _client;

        public RemoteGitDb(HttpClient client)
        {
            _client = client;
        }

        public RemoteGitDb(string userName, string password, string url)
        {
            _client = new HttpClient{BaseAddress = new Uri(url)};
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{userName}:{password}")));
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }


        string urlEncode(string value) =>
            HttpUtility.UrlEncode(value);

        public Task<string> Get(string branch, string key) =>
            _client.GetAsync<string>($"/{branch}/document/{urlEncode(key)}");

        public async Task<T> Get<T>(string branch, string key) where T : class
        {
                var result = await Get(branch, key);
                return string.IsNullOrEmpty(result)
                ? null
                : JsonConvert.DeserializeObject<T>(result);
        }

        public async Task<IReadOnlyCollection<T>> GetFiles<T>(string branch, string key) =>
            (await _client.GetAsync<List<string>>($"/{branch}/documents/{key}"))
                          .Select(JsonConvert.DeserializeObject<T>)
                          .ToArray();

        public async Task<IReadOnlyCollection<string>> GetFiles(string branch, string key) =>
            await _client.GetAsync<List<string>>($"/{branch}/documents/{key}");

        public Task<string> Save(string branch, string message, Document document, Author author) =>
            _client.PostAsync($"/{branch}/document", new SaveRequest
            {
                Message = message,
                Document = document,
                Author = author
            }).WhenSuccessful().AsStringResponse();

        public Task<string> Save<T>(string branch, string message, Document<T> document, Author author) =>
            Save(branch, message, Document.From(document), author);

        public Task<string> Delete(string branch, string key, string message, Author author) =>
             _client.PostAsync($"/{branch}/document/delete", new DeleteRequest
             {
                 Message = message,
                 Key = key,
                 Author = author
             }).AsStringResponse();       

        public Task Tag(Reference reference) =>
            _client.PostAsync("/tag", reference);

        public Task CreateBranch(Reference reference) =>
            _client.PostAsync("/branch", reference);

        public Task<IEnumerable<string>> GetAllBranches() =>
            _client.GetAsync<IEnumerable<string>>("/branch");

        public async Task<ITransaction> CreateTransaction(string branch) =>
            await RemoteTransaction.Create(_client, branch);

        public void Dispose() => 
            _client.Dispose();

        public Task<string> MergeBranch(string source, string target, Author author, string message) =>
            _client.PostAsync("/merge", new MergeRequest {Target = target, Source = source, Author = author, Message = message})
                   .WhenSuccessful()
                   .AsStringResponse();
    }
}
