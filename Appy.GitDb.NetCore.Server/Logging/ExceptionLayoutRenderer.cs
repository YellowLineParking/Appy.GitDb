using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NLog;
using NLog.Config;
using NLog.LayoutRenderers;
using NLog.Web.LayoutRenderers;

namespace Appy.GitDb.NetCore.Server.Logging
{
    [LayoutRenderer("appy-exception")]
    class ExceptionLayoutRenderer : AspNetLayoutRendererBase
    {
        [RequiredParameter]
        public string Field { get; set; }

        protected override void DoAppend(StringBuilder builder, LogEventInfo logEvent)
        {
            if (logEvent.Exception == null)
                return;

            builder.Append(typeof(Signature).GetProperty(Field)?.GetValue(logEvent.Exception.GetSignature(), null));
        }
    }

    class Signature
    {
        public Signature(Exception exception)
        {
            ErrorType = exception.GetType().FullName;
            ClassName = exception.TargetSite == null ? null : exception.TargetSite.DeclaringType?.FullName;
            MethodName = exception.TargetSite == null ? null : exception.TargetSite.Name;
            AssemblyName = exception.TargetSite == null ? null : exception.TargetSite.DeclaringType?.Assembly.GetName().Name;
            StackTrace = exception.StackTrace;
            Message = exception.Message;

            // signatures
            StackTraceSignature = exception.GetCleanStackTrace(1).GetSignature();
            MessageSignature = exception.GetCleanMessage().GetSignature();
            MethodSignature = (ClassName + "." + MethodName).GetSignature();
            ExceptionSignature = $"{MethodSignature.Substring(0, 7)}_{MessageSignature.Substring(0, 7)}_{StackTraceSignature}";
        }
        public string ErrorType { get; }
        public string ClassName { get; }
        public string MethodName { get; }
        public string AssemblyName { get; }
        public string Message { get; }
        public string StackTrace { get; }
        public string MethodSignature { get; }
        public string MessageSignature { get; }
        public string StackTraceSignature { get; }
        public string ExceptionSignature { get; }
    }

    static class ExceptionExtensions
    {
        internal static string GetSignature(this string inputString)
        {
            var sb = new StringBuilder();
            foreach (var b in MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(inputString)))
                sb.Append(b.ToString("X2"));

            return sb.ToString();
        }

        internal static string GetCleanStackTrace(this Exception ex, int folderDepth)
        {
            if (string.IsNullOrEmpty(ex.StackTrace))
                return string.Empty;

            var stackTrace = ex.StackTrace;

            var matches = new Regex(@"in ([^ ]*\\.*\.cs)[ :]").Matches(stackTrace);

            foreach (Match match in matches)
            {
                var path = match.Groups[1].Value;

                // trim the number of folders
                var pathParts = path.Split('\\').ToList();
                pathParts.Reverse();
                pathParts = pathParts.Take(folderDepth + 1).ToList(); // add 1 to get the file name
                pathParts.Reverse();

                var newPath = string.Join("\\", pathParts);

                stackTrace = stackTrace.Replace(path, newPath);
            }


            return stackTrace;
        }
        internal static string GetCleanMessage(this Exception ex) =>
            Regex.Replace(ex.Message, @"\[[^\]]*\]|\{[^\}]*\}", "[...]", RegexOptions.Multiline | RegexOptions.IgnoreCase);

        internal static Signature GetSignature(this Exception exception) =>
            new Signature(exception);
    }
}
