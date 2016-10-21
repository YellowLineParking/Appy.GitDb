using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;
using Ylp.GitDb.Core;
using Ylp.GitDb.Core.Model;
using Ylp.GitDb.Local;
using Reference = Ylp.GitDb.Core.Model.Reference;

namespace Ylp.GitDb.Benchmark
{
    class Utils
    {
        public static void BundleRepo(string directory, string target) =>
            runPowerShellScript("bundle_repo.ps1", $"-gitFolder {directory} -outputPath {target}");

        public static void ExtractRepo(string bundleFile, string targetDirectory) =>
            runPowerShellScript("extract_repo.ps1", $"-bundleFile {bundleFile} -targetDir {targetDirectory}");

        public static string GetCurrentCommitHashOfCodeRepo()
        {
            var repo = new Repository("../");
            var hash = repo.Head.Tip.Sha.Substring(0, 7);
            repo.Dispose();
            return hash;
        }

        public static void DeleteDirectory(string directory)
        {
            Directory.EnumerateDirectories(directory).ForEach(DeleteDirectory);
            Directory.EnumerateFiles(directory).Select(file => new FileInfo(file) { Attributes = FileAttributes.Normal }).ForEach(fi => fi.Delete());
            Directory.Delete(directory);
        }

        public static async Task CreateTestRepo(string directory)
        {
            const int batchSize = 10000;
            const int documentCount = 1000 * 1000;
            var index = 0;
            var db = new LocalGitDb(directory, new Logger("measure.log"));
            var author = new Author("name", "email");
            foreach (var batch in GenerateItems(documentCount, 3, batchSize))
            {
                Console.WriteLine($"\rWriting {index * batchSize} of {documentCount}");
                index++;
                using (var t = await db.CreateTransaction("master"))
                {
                    await t.AddMany(batch);
                    await t.Commit("message", author);
                }
                await db.Tag(new Reference { Name = (batchSize * index).ToString(), Pointer = "master" });
            }
                         
            db.Dispose();
        }

        static void runPowerShellScript(string script, string parameters)
        {
            Console.WriteLine($"Executing {script} {parameters}");
            var baseDir = Path.GetFullPath(".\\");
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    WorkingDirectory = baseDir,
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Unrestricted -File {baseDir}Scripts\\{script} {parameters}",
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };
            process.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
            process.ErrorDataReceived += (sender, args) => Console.WriteLine(args.Data);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
        }

        public static IEnumerable<List<Document>> GenerateItems(int count, int keyLength, int batchSize) =>
           Enumerable.Range(0, count)
                     .Select(i => createDocument(keyLength))
                     .Batch(batchSize)
                     .ToNestedLists();

        static Document createDocument(int keyLength) =>
            new Document
            {
                Key = getRandomKey(keyLength),
                Value = getRandomValue()
            };

        static readonly Dictionary<int, List<string>> KeysByDepth = new Dictionary<int, List<string>>();
        static readonly Random Rnd = new Random();
        static string getRandomKey(int keyDepth)
        {
            var key = "";
            for (var depth = 0; depth < keyDepth; depth++)
            {
                if (!KeysByDepth.ContainsKey(depth))
                    KeysByDepth.Add(depth, Enumerable.Range(0, 10)
                                          .Select(j => Guid.NewGuid().ToString())
                                          .ToList());

                key += KeysByDepth[depth][Rnd.Next(0, 10)] + "\\";
            }
            key += Guid.NewGuid().ToString();
            return key;
        }
        const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        static string getRandomValue() =>
            new string(Enumerable.Repeat(Chars, 1000)
                                 .Select(s => s[Rnd.Next(s.Length)]).ToArray());
    }
}
