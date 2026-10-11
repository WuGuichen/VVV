using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace MxFramework.NoEngineTests.Determinism
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string suite = args.Length > 0 ? args[0] : "core";
            if (suite != "core" && suite != "restore" && suite != "network" && suite != "regressions" && suite != "allocation" && suite != "all")
            { Console.Error.WriteLine("Usage: [core|restore|network|regressions|allocation|all] [result.xml]"); return 2; }
            var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
            runner.Load(Assembly.GetExecutingAssembly(), new Dictionary<string, object> { ["NumberOfTestWorkers"] = 0 });
            var filter = new SuiteFilter(suite);
            int count = runner.CountTestCases(filter);
            if (count == 0) { Console.Error.WriteLine("No tests selected: " + suite); return 2; }
            Console.WriteLine("Suite=" + suite + "; selected=" + count + "; source-linked production; NUnit 3.13.1; no Unity");
            ITestResult result = runner.Run(TestListener.NULL, filter);
            string xml = result.ToXml(true).OuterXml;
            string output = args.Length > 1 ? args[1] : Path.Combine("TestResults", "determinism-" + suite + ".xml");
            string parent = Path.GetDirectoryName(Path.GetFullPath(output));
            Directory.CreateDirectory(parent);
            File.WriteAllText(output, xml);
            PrintFailures(result);
            Console.WriteLine("Passed=" + result.PassCount + "; failed=" + result.FailCount + "; skipped=" + result.SkipCount + "; seconds=" + result.Duration.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
            Console.WriteLine("Result=" + Path.GetFullPath(output));
            return result.FailCount == 0 && result.PassCount + result.SkipCount == count ? 0 : 1;
        }

        private static void PrintFailures(ITestResult result)
        {
            if (!result.Test.IsSuite && result.ResultState.Status == TestStatus.Failed)
                Console.Error.WriteLine("FAIL " + result.FullName + Environment.NewLine + result.Message + Environment.NewLine + result.StackTrace);
            foreach (ITestResult child in result.Children) PrintFailures(child);
        }

        private sealed class SuiteFilter : TestFilter
        {
            private readonly string _suite;
            public SuiteFilter(string suite) { _suite = suite; }
            public override TNode AddToXml(TNode parentNode, bool recursive) => parentNode.AddElement("determinism-suite", _suite);
            public override bool Match(ITest test)
            {
                if (test.IsSuite) return false;
                string name = test.FullName;
                bool regression = name.Contains("DeterminismRegression", StringComparison.Ordinal);
                if (_suite == "regressions") return regression;
                if (regression) return _suite == "all";
                bool allocation = name.Contains("Allocat", StringComparison.Ordinal) && !name.Contains("FutureAllocation", StringComparison.Ordinal);
                if (_suite == "allocation") return allocation;
                if (allocation) return _suite == "all";
                bool network = name.StartsWith("MxFramework.Tests.Network.", StringComparison.Ordinal);
                bool restore = name.Contains("SaveState", StringComparison.Ordinal) || name.Contains("Snapshot", StringComparison.Ordinal) || name.Contains("Restore", StringComparison.Ordinal) || name.Contains("Checkpoint", StringComparison.Ordinal);
                if (_suite == "all") return true;
                if (_suite == "network") return network;
                if (network) return false;
                return _suite == "restore" ? restore : !restore;
            }
        }
    }
}
