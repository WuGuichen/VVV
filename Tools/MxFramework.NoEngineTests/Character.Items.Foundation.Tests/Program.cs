using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        int passed = 0, failed = 0;
        string filter = args.Length == 0 ? string.Empty : args[0];
        var tests = Assembly.GetExecutingAssembly().GetTypes()
            .Where(type => type.Namespace == "MxFramework.Tests.Items")
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.IsDefined(typeof(TestAttribute), true) || method.IsDefined(typeof(TestCaseAttribute), true))
                .SelectMany(method => Cases(type, method)))
            .OrderBy(test => test.Name, StringComparer.Ordinal);
        foreach (var test in tests)
        {
            string name = test.Name;
            if (!name.Contains(filter, StringComparison.Ordinal)) continue;
            try
            {
                object result = test.Method.Invoke(Activator.CreateInstance(test.Type), test.Arguments);
                if (result is Task task)
                {
                    await task;
                    result = task.GetType().GetProperty("Result")?.GetValue(task);
                }
                if (test.Case?.HasExpectedResult == true) Assert.AreEqual(test.Case.ExpectedResult, result, name);
                passed++; Console.WriteLine("PASS " + name);
            }
            catch (Exception error)
            {
                if (error is TargetInvocationException invocation) error = invocation.InnerException;
                failed++; Console.WriteLine("FAIL " + name + "\n" + error);
            }
        }
        Console.WriteLine($"RESULT passed={passed} failed={failed} filter={filter}");
        return failed != 0 || passed == 0 ? 1 : 0;
    }

    private static IEnumerable<Invocation> Cases(Type type, MethodInfo method)
    {
        var cases = method.GetCustomAttributes<TestCaseAttribute>(true).ToArray();
        if (cases.Length == 0) yield return new Invocation(type, method, null);
        else foreach (var testCase in cases) yield return new Invocation(type, method, testCase);
    }

    private sealed class Invocation
    {
        internal Invocation(Type type, MethodInfo method, TestCaseAttribute testCase)
        {
            Type = type; Method = method; Case = testCase;
            Arguments = testCase?.Arguments ?? Array.Empty<object>();
            Name = type.Name + "." + (testCase?.TestName ?? method.Name);
            if (testCase != null && string.IsNullOrEmpty(testCase.TestName))
                Name += "(" + string.Join(", ", Arguments.Select(value => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null")) + ")";
        }
        internal Type Type { get; }
        internal MethodInfo Method { get; }
        internal TestCaseAttribute Case { get; }
        internal object[] Arguments { get; }
        internal string Name { get; }
    }
}
