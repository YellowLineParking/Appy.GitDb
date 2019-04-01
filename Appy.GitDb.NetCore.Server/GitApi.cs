using Appy.GitDb.Core.Interfaces;
using Appy.GitDb.Core.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Document = Appy.GitDb.Core.Model.Document;

namespace Appy.GitDb.NetCore.Server
{
    [ApiController]
    public class GitApiController : ControllerBase
    {
        readonly IGitDb _gitDb;

        public GitApiController(IGitDb gitDb) =>        
            _gitDb = gitDb;

        [Route("{branch}/document/{*key}")]
        [HttpGet]
        [Authorize(Roles = "admin, read")]
        public Task<IActionResult> Get(string branch, string key) =>
            result(() => _gitDb.Get(branch, key));

        [Route("{branch}/documents/{*key}")]
        [HttpGet]
        [Authorize(Roles = "admin, read")]
        public Task<IActionResult> GetFiles(string branch, string key) =>
            result(() => _gitDb.GetFiles(branch, key));

        [Route("{branch}/{start}/{pageSize}/documents/{*key}")]
        [HttpGet]
        [Authorize(Roles = "admin, read")]
        public Task<IActionResult> GetFilesPaged(string branch, string key, int start, int pageSize) =>
            result(() => _gitDb.GetFilesPaged(branch, key, start, pageSize));

        [Route("{branch}/document")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IActionResult> Save(string branch, [FromBody] SaveRequest request) =>
            result(() => _gitDb.Save(branch, request.Message, request.Document, request.Author));

        [Route("{branch}/document/delete")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IActionResult> Delete(string branch, [FromBody] DeleteRequest request) =>
            result(() => _gitDb.Delete(branch, request.Key, request.Message, request.Author));

        [Route("{branch}/transactions/close")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IActionResult> CloseTransactions(string branch) =>
            result(() => _gitDb.CloseTransactions(branch));

        [Route("tag")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IActionResult> Tag([FromBody] Reference reference) =>
            result(() => _gitDb.Tag(reference));

        [Route("branch")]
        [HttpGet]
        [Authorize(Roles = "admin,read")]
        public Task<IActionResult> GetBranches() =>
            result(() => _gitDb.GetAllBranches());

        [Route("branch")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IActionResult> CreateBranch([FromBody] Reference reference) =>
            result(() => _gitDb.CreateBranch(reference));

        static readonly Dictionary<string, ITransaction> _transactions = new Dictionary<string, ITransaction>();

        [Route("{branch}/transaction")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IActionResult> CreateTransaction(string branch) =>
            result(async () =>
            {
                var trans = await _gitDb.CreateTransaction(branch);
                var transactionId = Guid.NewGuid().ToString();
                _transactions.Add(transactionId, trans);
                return transactionId;
            });

        [Route("{branch}")]
        [HttpDelete]
        [Authorize(Roles = "admin,write")]
        public Task<IActionResult> DeleteBranch(string branch) =>
           result(() => _gitDb.DeleteBranch(branch));

        [Route("tag/{tag}")]
        [HttpDelete]
        [Authorize(Roles = "admin,write")]
        public Task<IActionResult> DeleteTag(string tag) =>
            result(() => _gitDb.DeleteTag(tag));

        [Route("{transactionId}/add")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IActionResult> AddToTransaction(string transactionId, Document document) =>
            result(() => _transactions[transactionId].Add(document));

        [Route("{transactionId}/addmany")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IActionResult> AddToTransaction(string transactionId, List<Document> documents) =>
            result(() => _transactions[transactionId].AddMany(documents));

        [Route("{transactionId}/delete/{key}")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IActionResult> DeleteInTransaction(string transactionId, string key) =>
            result(() => _transactions[transactionId].Delete(key));

        [Route("{transactionId}/deleteMany")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IActionResult> DeleteInTransaction(string transactionId, List<string> keys) =>
            result(() => _transactions[transactionId].DeleteMany(keys));

        [Route("{transactionId}/commit")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IActionResult> CommitTransaction(string transactionId, [FromBody] CommitTransaction commit) =>
            result(async () =>
            {
                var transaction = _transactions[transactionId];
                var sha = await transaction.Commit(commit.Message, commit.Author);
                _transactions.Remove(transactionId);
                return sha;
            });

        [Route("{transactionId}/abort")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IActionResult> AbortTransaction(string transactionId) =>
            result(async () =>
            {
                var transaction = _transactions[transactionId];
                await transaction.Abort();
                _transactions.Remove(transactionId);
            });

        [Route("merge")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IActionResult> Merge(MergeRequest mergeRequest) =>
           result(() => _gitDb.MergeBranch(mergeRequest.Source, mergeRequest.Target, mergeRequest.Author, mergeRequest.Message));

        [Route("{branch}/rebase")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IActionResult> Rebase(RebaseRequest rebaseRequest) =>
            result(() => _gitDb.RebaseBranch(rebaseRequest.Source, rebaseRequest.Target, rebaseRequest.Author, rebaseRequest.Message));

        [Route("diff/{reference}/{reference2}")]
        [HttpGet]
        [Authorize(Roles = "admin,read")]
        public Task<IActionResult> Diff(string reference, string reference2) =>
            result(() => _gitDb.Diff(reference, reference2));

        [Route("log/{reference}/{reference2}")]
        [HttpGet]
        [Authorize(Roles = "admin,read")]
        public Task<IActionResult> Log(string reference, string reference2) =>
            result(() => _gitDb.Log(reference, reference2));

        async Task<IActionResult> result<T>(Func<Task<T>> action)
        {
            try
            {
                return Ok(await action());
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (NotSupportedException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        async Task<IActionResult> result(Func<Task> action)
        {
            try
            {
                await action();
                return Ok();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (NotSupportedException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}