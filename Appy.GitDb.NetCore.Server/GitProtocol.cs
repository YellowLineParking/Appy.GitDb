using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Protocols;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Appy.GitDb.Server
{
    //[Route("server")]
    //[ApiController]
    //public class GitProtocolController : ControllerBase
    //{
    //    const string FlushMessage = "0000";
    //    readonly string _gitPath = ConfigurationManager<>.AppSettings["git.path"];
    //    readonly string _gitHomePath = ConfigurationManager.AppSettings["git.homePath"];
    //    readonly string _repoPath = ConfigurationManager.AppSettings["git.repository.path"];

    //    [HttpGet]
    //    [Route("repository.git")]
    //    [Authorize(Roles = "admin, read")]
    //    public IActionResult GitUrl() =>
    //        Ok();

    //    [HttpGet]
    //    [Route("repository.git/info/refs")]
    //    [Authorize(Roles = "admin, read")]
    //    public IActionResult InfoRefs(string service) =>
    //        new GitResult($"application/x-{service}-advertisement",
    //            (input, outStream) => executeServiceByName(input, outStream, service.Substring(4), true, false),
    //            formatMessage($"# service={service}\n") + FlushMessage);

    //    [HttpPost, Route("repository.git/git-upload-pack")]
    //    [Authorize(Roles = "admin, read")]
    //    public IActionResult UploadPack() =>
    //        new GitResult("application/x-git-upload-pack-result",
    //            (input, outStream) => executeServiceByName(input, outStream, "upload-pack", false, true));

    //    [HttpPost, Route("repository.git/git-receive-pack")]
    //    [Authorize(Roles = "admin, write")]
    //    public IActionResult ReceivePack() =>
    //        new GitResult("application/x-git-receive-pack-result",
    //            (input, outStream) => executeServiceByName(input, outStream, "receive-pack", false, false));

    //    static string formatMessage(string input) =>
    //        (input.Length + 4).ToString("X").PadLeft(4, '0') + input;

    //    async Task executeServiceByName(Stream input, Stream output, string serviceName, bool addAdvertiseRefs, bool closeInput)
    //    {
    //        var args = serviceName + " --stateless-rpc";
    //        if (addAdvertiseRefs)
    //            args += " --advertise-refs";
    //        args += " \"" + _repoPath + "\"";

    //        var info = new ProcessStartInfo(_gitPath + @"\git.exe", args)
    //        {
    //            CreateNoWindow = true,
    //            RedirectStandardError = true,
    //            RedirectStandardInput = true,
    //            RedirectStandardOutput = true,
    //            UseShellExecute = false,
    //            WorkingDirectory = Path.GetDirectoryName(_repoPath),
    //        };

    //        if (info.EnvironmentVariables.ContainsKey("HOME"))
    //            info.EnvironmentVariables.Remove("HOME");
    //        info.EnvironmentVariables.Add("HOME", _gitHomePath);

    //        using (var process = Process.Start(info))
    //        {
    //            await input.CopyToAsync(process.StandardInput.BaseStream);
    //            if (closeInput)
    //                process.StandardInput.Close();
    //            else
    //                process.StandardInput.Write('\0');

    //            await process.StandardOutput.BaseStream.CopyToAsync(output);
    //            process.WaitForExit();
    //        }
    //    }
    //}

    // TODO (https://techblog.dorogin.com/server-sent-event-aspnet-core-a42dc9b9ffa9)
    //public class GitResult : IActionResult
    //{
    //    readonly string _contentType;
    //    readonly string _advertiseRefsContent;
    //    readonly Func<Stream, Stream, Task> _executeGitCommand;

    //    public GitResult(string contentType, Func<Stream, Stream, Task> executeGitCommand, string advertiseRefsContent = null)
    //    {
    //        _contentType = contentType;
    //        _advertiseRefsContent = advertiseRefsContent;
    //        _executeGitCommand = executeGitCommand;
    //    }

    //    public Task ExecuteResultAsync(ActionContext context)
    //    {
    //        var input = context.HttpContext.Request.Body;
    //        context.HttpContext.Response.GetTypedHeaders().ContentType = new MediaTypeHeaderValue(_contentType);

    //        resp.Content = new PushStreamContent(async (output, content, context) =>
    //        {
    //            if (_advertiseRefsContent != null)
    //            {
    //                var writer = new StreamWriter(output);
    //                writer.Write(_advertiseRefsContent);
    //                await writer.FlushAsync();
    //            }
    //            await _executeGitCommand(input, output);
    //            output.Close();
    //        });

    //        resp.Content.Headers.Expires = DateTimeOffset.MinValue;
    //        resp.Headers.Add("pragma", "no-cache");
    //        resp.Headers.CacheControl = CacheControlHeaderValue.Parse("no-cache, max-age=0, must-revalidate");
    //        resp.Content.Headers.ContentType = new MediaTypeHeaderValue(_contentType);

    //        return Task.CompletedTask;
    //    }

    //    //public async Task ExecuteResultAsync(ActionContext context)
    //    //{
    //    //    var objectResult = new ObjectResult(_result.Exception ?? _result.Data)
    //    //    {
    //    //        StatusCode = _result.Exception != null
    //    //            ? StatusCodes.Status500InternalServerError
    //    //            : StatusCodes.Status200OK
    //    //    };

    //    //    await objectResult.ExecuteResultAsync(context);
    //    //}
    //}
}