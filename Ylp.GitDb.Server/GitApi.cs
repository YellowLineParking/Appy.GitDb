using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Ylp.GitDb.Core.Interfaces;
using Ylp.GitDb.Core.Model;

namespace Ylp.GitDb.Server
{
    public class GitApiController : ApiController
    {
        readonly IGitDb _gitDb;

        public GitApiController(IGitDb gitDb)
        {
            _gitDb = gitDb;
        }

        [Route("{branch}/document/{*key}")]
        [HttpGet]
        [Authorize(Roles = "admin, read")]
        public Task<IHttpActionResult> Get(string branch, string key) =>
            Result(() => _gitDb.Get(branch, key));

        [Route("{branch}/documents/{*key}")]
        [HttpGet]
        [Authorize(Roles = "admin, read")]
        public Task<IHttpActionResult> GetFiles(string branch, string key) =>
            Result(() => _gitDb.GetFiles(branch, key));

        [Route("{branch}/document")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> Save(string branch, [FromBody] SaveRequest request) =>
            Result(() => _gitDb.Save(branch, request.Message, request.Document, request.Author));

        [Route("{branch}/document/delete")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> Delete(string branch, [FromBody] DeleteRequest request) =>
            Result(() => _gitDb.Delete(branch, request.Key, request.Message, request.Author));

        [Route("tag")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> Tag([FromBody] Reference reference) =>
            Result(() => _gitDb.Tag(reference));

        [Route("branch")]
        [HttpGet]
        [Authorize(Roles = "admin,read")]
        public Task<IHttpActionResult> GetBranches() =>
            Result(() => _gitDb.GetAllBranches());

        [Route("branch")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> CreateBranch([FromBody] Reference reference) =>
            Result(() => _gitDb.CreateBranch(reference));

        static readonly Dictionary<string, ITransaction> transactions = new Dictionary<string, ITransaction>();

        [Route("{branch}/transaction")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> CreateTransaction(string branch) =>
            Result(async () =>
            {
                var trans = await _gitDb.CreateTransaction(branch);
                var transactionId = Guid.NewGuid().ToString();
                transactions.Add(transactionId, trans);
                return transactionId;
            });

        [Route("{transactionId}/add")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> AddToTransaction(string transactionId, Document document) =>
            Result(() => transactions[transactionId].Add(document));

        [Route("{transactionId}/addmany")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> AddToTransaction(string transactionId, List<Document> documents) =>
            Result(() => transactions[transactionId].AddMany(documents));


        [Route("{transactionId}/delete/{key}")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> DeleteInTransaction(string transactionId, string key) =>
            Result(() => transactions[transactionId].Delete(key));

        [Route("{transactionId}/deleteMany")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> DeleteInTransaction(string transactionId, List<string> keys) =>
            Result(() => transactions[transactionId].DeleteMany(keys));


        [Route("{transactionId}/commit")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> CommitTransaction(string transactionId, [FromBody] CommitTransaction commit) =>
            Result(async () =>
            {
                var transaction = transactions[transactionId];
                var sha = await transaction.Commit(commit.Message, commit.Author);
                transactions.Remove(transactionId);
                return sha;
            });

        [Route("{transactionId}/abort")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> AbortTransaction(string transactionId) =>
            Result(async () =>
            {
                var transaction = transactions[transactionId];
                await transaction.Abort();
                transactions.Remove(transactionId);
            });


        async Task<IHttpActionResult> Result<T>(Func<Task<T>> action)
        {
            try
            {
                return Ok(await action());
            }
            catch (ArgumentException ex)
            {
                throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(ex.Message) });
            }
        }

        async Task<IHttpActionResult> Result(Func<Task> action)
        {
            try
            {
                await action();
                return Ok();
            }
            catch (ArgumentException ex)
            {
                throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.BadRequest) {Content = new StringContent(ex.Message)});
            }
        }

    }
}