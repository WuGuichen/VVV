using System.Diagnostics;
using System.Reflection;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

/// <summary>LowFreqAlloc. Builds disposable managed fixtures; never changes Unity outputs.</summary>
internal static class TacticalCardAssemblyIsolationTests
{
    internal static int Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "MxCardAssemblyIsolation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        int passed = 0;
        void Check(bool result, string reason) { if (!result) throw new InvalidOperationException(reason); passed++; }
        void Reject(Action action, string reason)
        {
            bool rejected = false;
            try { action(); } catch (IOException error) { rejected = error.Message.Contains("ASSEMBLY_SNAPSHOT_UNAVAILABLE"); }
            Check(rejected, reason);
        }
        try
        {
            string first = Build(root, "v1", "Fixture.Dependency", "public static class Dependency { public static string Read() => \"v1\"; }");
            string second = Build(root, "v2", "Fixture.Dependency", "public static class Dependency { public static string Read() => \"v2\"; }");
            string main = Build(root, "main", "Fixture.Main", "public static class Entry { public static string Read() => Dependency.Read(); }", first);
            string live = Path.Combine(root, "live"); Directory.CreateDirectory(live);
            string sourceMain = Path.Combine(live, "Fixture.Main.dll"), sourceDependency = Path.Combine(live, "Fixture.Dependency.dll");
            File.Copy(main, sourceMain); File.Copy(first, sourceDependency);
            var candidates = new Dictionary<string, string> { ["Fixture.Dependency"] = sourceDependency };
            string originalHash;
            using (var context = new TacticalCardAssemblyLoadContext(sourceMain, candidates))
            {
                originalHash = context.SnapshotHash;
                Check(context.AssemblyCount == 2, "Dependency closure captured before invoking code.");
                foreach (string file in context.SourcePaths)
                {
                    using var access = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    Check(access.CanWrite, "Source image must allow exclusive write access while context lives.");
                }
                File.WriteAllBytes(sourceMain, File.ReadAllBytes(main));
                File.WriteAllBytes(sourceDependency, File.ReadAllBytes(second));
                string renamed = sourceMain + ".old"; File.Move(sourceMain, renamed); File.Delete(renamed); File.Copy(main, sourceMain);
                Check(Read(context) == "v1", "Lazy dependency must use captured bytes after source replacement.");
                Check(context.LoadFromAssemblyName(new AssemblyName("Fixture.Dependency")).Location == "", "Simple-name loads use the frozen stream image.");
                Check(context.Assemblies.All(assembly => assembly.Location == ""), "No assembly in the isolated context is path loaded.");
                using var newer = new TacticalCardAssemblyLoadContext(sourceMain, candidates);
                Check(Read(newer) == "v2", "New query must observe replacement dependency.");
                Check(newer.SnapshotHash != originalHash, "Dependency-only changes must invalidate the fingerprint.");
            }
            foreach (string file in new[] { sourceMain, sourceDependency })
            {
                using var access = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                Check(access.CanWrite, "Source remains writable after disposal without forced GC.");
            }
            File.WriteAllBytes(sourceDependency, new byte[] { 1, 2, 3 });
            Reject(() => { using var context = new TacticalCardAssemblyLoadContext(sourceMain, candidates); }, "Partial output cannot fall back to an old snapshot.");
            File.Copy(second, sourceDependency, true);
            using (var writer = new FileStream(sourceDependency, FileMode.Open, FileAccess.Write, FileShare.None))
                Reject(() => { using var context = new TacticalCardAssemblyLoadContext(sourceMain, candidates); }, "Busy compiler output must fail closed.");
            Reject(() => { using var context = new TacticalCardAssemblyLoadContext(sourceMain, new Dictionary<string, string>()); }, "Missing dependency must fail closed.");
            candidates["Fixture.Dependency"] = sourceMain;
            Reject(() => { using var context = new TacticalCardAssemblyLoadContext(sourceMain, candidates); }, "Wrong dependency identity must be rejected.");
            // The resolver also supports files whose actual assembly identity differs from the filename.
            candidates.Clear(); candidates["renamed"] = sourceDependency;
            using (var context = new TacticalCardAssemblyLoadContext(sourceMain, candidates))
                Check(Read(context) == "v2", "Metadata alias resolution must stay in memory.");
            Console.WriteLine("Tactical card assembly isolation tests passed: " + passed);
            return passed;
        }
        finally
        {
            string parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if (Path.GetDirectoryName(Path.GetFullPath(root)) == parent && Path.GetFileName(root).StartsWith("MxCardAssemblyIsolation-", StringComparison.Ordinal))
                Directory.Delete(root, recursive: true);
        }
    }

    private static string Read(TacticalCardAssemblyLoadContext context) =>
        (string)context.MainAssembly.GetType("Entry", true).GetMethod("Read").Invoke(null, null);

    private static string Build(string root, string folder, string name, string code, string dependency = null)
    {
        string directory = Path.Combine(root, folder); Directory.CreateDirectory(directory);
        string reference = dependency == null ? "" : "<ItemGroup><Reference Include=\"Fixture.Dependency\"><HintPath>" + System.Security.SecurityElement.Escape(dependency) + "</HintPath></Reference></ItemGroup>";
        File.WriteAllText(Path.Combine(directory, "Fixture.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net9.0</TargetFramework><AssemblyName>" + name + "</AssemblyName><AssemblyVersion>1.0.0.0</AssemblyVersion><NuGetAudit>false</NuGetAudit></PropertyGroup>" + reference + "</Project>");
        File.WriteAllText(Path.Combine(directory, "Fixture.cs"), code);
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { "build", "Fixture.csproj", "-c", "Release", "-o", "output", "--nologo", "-v:q" }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start);
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Isolated fixture build timed out."); }
        if (process.ExitCode != 0) throw new InvalidOperationException(output.Result + error.Result);
        return Path.Combine(directory, "output", name + ".dll");
    }
}
