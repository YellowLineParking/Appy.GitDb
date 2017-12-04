using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace Appy.GitDb.Server.Controllers
{
    [RoutePrefix("server")]
    public class GitProtocolController : ApiController
    {
        const string FlushMessage = "0000";
        readonly string _gitPath = ConfigurationManager.AppSettings["git.path"];
        readonly string _gitHomePath = ConfigurationManager.AppSettings["git.homePath"];
        readonly string _repoBasePath = ConfigurationManager.AppSettings["git.repository.basePath"];

        [HttpGet]
        [Route("{repository}.git")]
        [Authorize(Roles = "admin, read")]
        public IHttpActionResult GitUrl(string repository) =>
            Ok();

        [HttpGet]
        [Route("{repository}.git/info/refs")]
        [Authorize(Roles = "admin, read")]
        public IHttpActionResult InfoRefs(string repository, string service) => 
            new GitResult(
                Request,
                $"application/x-{service}-advertisement",
                (input, outStream) => executeServiceByName(repository, input, outStream, service.Substring(4), true, false),
                formatMessage($"# service={service}\n") + FlushMessage);

        [HttpPost, Route("{repository}.git/git-upload-pack")]
        [Authorize(Roles = "admin, read")]
        public IHttpActionResult UploadPack(string repository) => 
            new GitResult(Request,
                            "application/x-git-upload-pack-result",
                            (input, outStream) => executeServiceByName(repository, input, outStream, "upload-pack", false, true));

        [HttpPost, Route("{repository}.git/git-receive-pack")]
        [Authorize(Roles = "admin, write")]
        public IHttpActionResult ReceivePack(string repository) => 
            new GitResult(Request,
                            "application/x-git-receive-pack-result",
                            (input, outStream) => executeServiceByName(repository, input, outStream, "receive-pack", false, false));

        static string formatMessage(string input) => 
            (input.Length + 4).ToString("X").PadLeft(4, '0') + input;

        async Task executeServiceByName(string repository, Stream input, Stream output, string serviceName, bool addAdvertiseRefs, bool closeInput)
        {
            var repoPath = _repoBasePath + "\\" + repository;
            var args = serviceName + " --stateless-rpc";
            if (addAdvertiseRefs)
                args += " --advertise-refs";
            args += " \"" + repoPath + "\"";

            var info = new ProcessStartInfo(_gitPath, args)
            {
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(repoPath),
            };

            if (info.EnvironmentVariables.ContainsKey("HOME"))
                info.EnvironmentVariables.Remove("HOME");
            info.EnvironmentVariables.Add("HOME", _gitHomePath);

            using (var process = Process.Start(info))
            {
                await input.CopyToAsync(process.StandardInput.BaseStream);
                if (closeInput)
                    process.StandardInput.Close();
                else
                    process.StandardInput.Write('\0');

                await process.StandardOutput.BaseStream.CopyToAsync(output);
                process.WaitForExit();
            }
        }   
    }

    public class GitResult : IHttpActionResult
    {
        readonly HttpRequestMessage _request;
        readonly string _contentType;
        readonly string _advertiseRefsContent;
        readonly Func<Stream, Stream, Task> _executeGitCommand;

        public GitResult(HttpRequestMessage request, string contentType, Func<Stream, Stream, Task> executeGitCommand, string advertiseRefsContent = null)
        {
            _request = request;
            _contentType = contentType;
            _advertiseRefsContent = advertiseRefsContent;
            _executeGitCommand = executeGitCommand;
        }

        public async Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            var resp = _request.CreateResponse(HttpStatusCode.OK);
            var input = await _request.Content.ReadAsStreamAsync();
            resp.Content = new PushStreamContent(async (output, content, context) =>
            {
                if (_advertiseRefsContent != null)
                {
                    var writer = new StreamWriter(output);
                    writer.Write(_advertiseRefsContent);
                    await writer.FlushAsync();
                }
                await _executeGitCommand(input, output);
                output.Close();
            });

            resp.Content.Headers.Expires = DateTimeOffset.MinValue;
            resp.Headers.Add("pragma", "no-cache");
            resp.Headers.CacheControl = CacheControlHeaderValue.Parse("no-cache, max-age=0, must-revalidate");
            resp.Content.Headers.ContentType = new MediaTypeHeaderValue(_contentType);

            return resp;
        }
    }
}