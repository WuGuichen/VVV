using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
        runner.Load(Assembly.GetExecutingAssembly(), new Dictionary<string, object> { ["NumberOfTestWorkers"] = 0 });
        ITestResult result = runner.Run(TestListener.NULL, TestFilter.Empty);
        string path = args.Length > 0 ? args[0] : Path.Combine(".agent", "accessory-second-batch-20261012.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        File.WriteAllText(path, result.ToXml(true).OuterXml);
        PrintFailures(result);
        Console.WriteLine($"Second-batch pure rules: passed={result.PassCount}, failed={result.FailCount}, skipped={result.SkipCount}. No Unity, integration or publication validation.");
        Console.WriteLine(Path.GetFullPath(path));
        return result.PassCount > 0 && result.FailCount == 0 && result.SkipCount == 0 ? 0 : 1;
    }

    private static void PrintFailures(ITestResult result)
    {
        if (!result.Test.IsSuite && result.ResultState.Status == TestStatus.Failed)
            Console.Error.WriteLine("FAIL " + result.FullName + Environment.NewLine + result.Message + Environment.NewLine + result.StackTrace);
        foreach (ITestResult child in result.Children) PrintFailures(child);
    }
}
