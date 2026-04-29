using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Appy.GitDb.Server
{
    [ApiController]
    [Route("server")]
    public class GitProtocolController : ControllerBase
    {
        const string FlushMessage = "0000";
        readonly string _gitPath;
        readonly string _gitHomePath;
        readonly string _repoPath;

        public GitProtocolController(IConfiguration config)
        {
            _gitPath = config["git.path"];
            _gitHomePath = config["git.homePath"];
            _repoPath = config["git.repository.path"];
        }

        [HttpGet("repository.git")]
        [Authorize(Roles = "admin, read")]
        public IActionResult GitUrl() => Ok();

        [HttpGet("repository.git/info/refs")]
        [Authorize(Roles = "admin, read")]
        public IActionResult InfoRefs([FromQuery] string service)
        {
            var contentType = $"application/x-{service}-advertisement";
            var preamble = formatMessage($"# service={service}\n") + FlushMessage;
            return new GitStreamResult(
                contentType,
                async (input, output) =>
                {
                    var writer = new StreamWriter(output, leaveOpen: true);
                    writer.Write(preamble);
                    await writer.FlushAsync();
                    await executeServiceByName(input, output, service.Substring(4), true, false);
                });
        }

        [HttpPost("repository.git/git-upload-pack")]
        [Authorize(Roles = "admin, read")]
        public IActionResult UploadPack() =>
            new GitStreamResult(
                "application/x-git-upload-pack-result",
                (input, output) => executeServiceByName(input, output, "upload-pack", false, true));

        [HttpPost("repository.git/git-receive-pack")]
        [Authorize(Roles = "admin, write")]
        public IActionResult ReceivePack() =>
            new GitStreamResult(
                "application/x-git-receive-pack-result",
                (input, output) => executeServiceByName(input, output, "receive-pack", false, false));

        static string formatMessage(string input) =>
            (input.Length + 4).ToString("X").PadLeft(4, '0') + input;

        async Task executeServiceByName(Stream input, Stream output, string serviceName, bool addAdvertiseRefs, bool closeInput)
        {
            var args = new StringBuilder(serviceName).Append(" --stateless-rpc");
            if (addAdvertiseRefs)
                args.Append(" --advertise-refs");
            args.Append($" \"{_repoPath}\"");

            var info = new ProcessStartInfo(Path.Combine(_gitPath, "git.exe"), args.ToString())
            {
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(_repoPath),
            };

            if (info.EnvironmentVariables.ContainsKey("HOME"))
                info.EnvironmentVariables.Remove("HOME");
            info.EnvironmentVariables.Add("HOME", _gitHomePath);

            using var process = Process.Start(info);
            await input.CopyToAsync(process.StandardInput.BaseStream);
            if (closeInput)
                process.StandardInput.Close();
            else
                process.StandardInput.Write('\0');

            await process.StandardOutput.BaseStream.CopyToAsync(output);
            process.WaitForExit();
        }
    }

    public class GitStreamResult : IActionResult
    {
        readonly string _contentType;
        readonly Func<Stream, Stream, Task> _execute;

        public GitStreamResult(string contentType, Func<Stream, Stream, Task> execute)
        {
            _contentType = contentType;
            _execute = execute;
        }

        public async Task ExecuteResultAsync(ActionContext context)
        {
            var response = context.HttpContext.Response;
            response.ContentType = _contentType;
            response.Headers["Pragma"] = "no-cache";
            response.Headers["Cache-Control"] = "no-cache, max-age=0, must-revalidate";
            response.Headers["Expires"] = DateTimeOffset.MinValue.ToString("R");

            var input = context.HttpContext.Request.Body;
            await _execute(input, response.Body);
        }
    }
}
