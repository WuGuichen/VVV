using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static class CombatDesignCommands
{
    internal static int Dispatch(string[] args, JsonSerializerOptions options)
    {
        // Structured output must survive Windows code-page defaults when consumed by agents.
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        var gateway = new CombatDesignGateway(Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory()));
        object result = args[1] switch
        {
            "query" => gateway.Query(),
            "catalog" => new CombatDesignCatalogProjection(Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory())).Query(),
            "intent" => new CombatDesignCatalogProjection(Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory())).Query(),
            "mechanisms" => new CombatDesignMechanismProjection(Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory())).Query(),
            "plan" => gateway.Plan(ReadRequest(args)),
            "submit" => gateway.Submit(ReadRequest(args)),
            "status" => gateway.Status(Program.GetOption(args, "--operation-id", string.Empty)),
            _ => throw new ArgumentException("combat-design query|catalog|intent|mechanisms|plan|submit --request <json-file>|status --operation-id <returned-id>; --project-root <project>")
        };
        Console.WriteLine(JsonSerializer.Serialize(result, options));
        return Program.ExitReady;
    }

    private static string ReadRequest(string[] args)
    {
        string path = Program.GetOption(args, "--request", string.Empty);
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("--request <json-file> is required.");
        var info = new FileInfo(path);
        if (info.Length > 1024 * 1024) throw new ArgumentException("REQUEST_TOO_LARGE");
        return File.ReadAllText(path);
    }
}
