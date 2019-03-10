using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Appy.GitDb.Core.Interfaces;
using Appy.GitDb.Local;
using CsvHelper;
using CsvHelper.Configuration;

namespace Appy.GitDb.Benchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            var libGitSharpVersion = Assembly.LoadFile(Path.GetFullPath("LibGit2Sharp.dll")).GetName().Version.ToString();
            
            Directory.SetCurrentDirectory("..\\..\\");
            if (!Directory.Exists("TestRepo"))
            {
                Console.WriteLine();
                Console.WriteLine("Creating the test repository on disk");
                Utils.ExtractRepo("Data\\repo.bundle", "TestRepo");
            }
                
            var currentEntries = getCurrentEntries();
            var computerName = getInput("What computer are you running on?", currentEntries.Select(e => e.ComputerName).Distinct());

            Console.WriteLine();
            Console.WriteLine("Running tests ...");
            Console.WriteLine();
            Console.WriteLine("-----------------------------------------------------------");
            Console.WriteLine("| BATCH SIZE | KEY SIZE | REPOSITORY SIZE | WRITES/SECOND |");
            Console.WriteLine("-----------------------------------------------------------");

            Func<IGitDb> db = () => new LocalGitDb("TestRepo");
            currentEntries.Add(new CsvEntry
            {
                ComputerName = computerName,
                Commit = Utils.GetCurrentCommitHashOfCodeRepo(),
                Time = DateTime.Now,
                LibGit2SharpVersion = libGitSharpVersion,
                Value1 =  Measure.WritesPerSecond(db, 100,  1, 10000),
                Value2 =  Measure.WritesPerSecond(db, 1000, 1, 10000),
                Value3 =  Measure.WritesPerSecond(db, 100,  3, 10000),
                Value4 =  Measure.WritesPerSecond(db, 1000, 3, 10000),
                Value5 =  Measure.WritesPerSecond(db, 100,  1, 100000),
                Value6 =  Measure.WritesPerSecond(db, 1000, 1, 100000),
                Value7 =  Measure.WritesPerSecond(db, 100,  3, 100000),
                Value8 =  Measure.WritesPerSecond(db, 1000, 3, 10000),
                Value9 =  Measure.WritesPerSecond(db, 100,  1, 1000000),
                Value10 = Measure.WritesPerSecond(db, 1000, 1, 1000000),
                Value11 = Measure.WritesPerSecond(db, 100,  3, 1000000),
                Value12 = Measure.WritesPerSecond(db, 1000, 3, 1000000)
            });
            Console.WriteLine("-----------------------------------------------------------");

            Console.WriteLine();
            Console.WriteLine("Saving results ...");
            saveCsv(currentEntries);

            Console.WriteLine();
            Console.WriteLine("Deleting test data ...");
            Utils.DeleteDirectory("TestRepo");
            File.Delete("measure.log");

            Console.WriteLine();
            Console.WriteLine("Done. Press any key to exit.");
            Console.ReadKey();
        }

        static string getInput(string text, IEnumerable<string> options)
        {
            var opts = options.ToList();
            Console.WriteLine(text);
            var dict = opts.ToDictionary(opts.IndexOf, key => key);
            opts.ForEach(option => Console.WriteLine($"{opts.IndexOf(option)}:{option}"));
            var answer = Console.ReadLine();
            int intAnswer;
            return int.TryParse(answer, out intAnswer) && dict.ContainsKey(intAnswer) 
                    ? dict[intAnswer] 
                    : answer;
        }

        static List<CsvEntry> getCurrentEntries()
        {
            if (!File.Exists("Benchmark.csv"))
                saveCsv(new List<CsvEntry>());

            using (var reader = new StreamReader("Benchmark.csv"))
            using (var csvReader = new CsvReader(reader))
            {
                csvReader.Configuration.Delimiter = ";";
                csvReader.Configuration.RegisterClassMap<EntryMap>();
                return csvReader.GetRecords<CsvEntry>().ToList();
            }
                
        }

        static void saveCsv(List<CsvEntry> entries)
        {
            using (var writer = new StreamWriter("Benchmark.csv"))
            using (var csvWriter = new CsvWriter(writer))
            {
                csvWriter.Configuration.Delimiter = ";";
                csvWriter.Configuration.RegisterClassMap<EntryMap>();
                csvWriter.WriteRecords(entries);
            }
        }
    }

    class CsvEntry
    {
        public string ComputerName { get; set; }
        public string Commit { get; set; }
        public DateTime Time { get; set; }
        public string LibGit2SharpVersion { get; set; }
        public double Value1 { get; set; }
        public double Value2 { get; set; }
        public double Value3 { get; set; }
        public double Value4 { get; set; }
        public double Value5 { get; set; }
        public double Value6 { get; set; }
        public double Value7 { get; set; }
        public double Value8 { get; set; }
        public double Value9 { get; set; }
        public double Value10 { get; set; }
        public double Value11 { get; set; }
        public double Value12 { get; set; }
    }

    sealed class EntryMap : ClassMap<CsvEntry>
    {
        public EntryMap()
        {
            Map(entry => entry.ComputerName).Name("Computer Name");
            Map(entry => entry.Commit).Name("Commit");
            Map(entry => entry.LibGit2SharpVersion).Name("LibGit2Sharp Version");
            Map(entry => entry.Time).Name("Time");
            Map(entry => entry.Value1).Name( "Value  1:  100-1-  10000");
            Map(entry => entry.Value2).Name( "Value  2: 1000-1-  10000");
            Map(entry => entry.Value3).Name( "Value  3:  100-3-  10000");
            Map(entry => entry.Value4).Name( "Value  4: 1000-3-  10000");
            Map(entry => entry.Value5).Name( "Value  5:  100-1- 100000");
            Map(entry => entry.Value6).Name( "Value  6: 1000-1- 100000");
            Map(entry => entry.Value7).Name( "Value  7:  100-3- 100000");
            Map(entry => entry.Value8).Name( "Value  8: 1000-3- 100000");
            Map(entry => entry.Value9).Name( "Value  9:  100-1-1000000");
            Map(entry => entry.Value10).Name("Value 10: 1000-1-1000000");
            Map(entry => entry.Value11).Name("Value 11:  100-3-1000000");
            Map(entry => entry.Value12).Name("Value 12: 1000-3-1000000");
        }
    }
}
