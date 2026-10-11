using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

/// <summary>
/// CLI command: mod diagnose
/// Generates a diagnostic snapshot of mod package loading without Unity Runtime.
///
/// Pipeline: container directories → catalog → loadout → load plan → merge → snapshot → JSON
/// </summary>
internal static class ModDiagnoseCommand
{
    public static int Run(string[] args)
    {
        // --- Parse arguments ---
        string[] containers = GetOptionAll(args, "--container", "-c");
        string containersCsv = GetOption(args, "--containers", string.Empty);
        string loadoutPath = GetOption(args, "--loadout", string.Empty);
        string outputPath = GetOption(args, "--output", string.Empty);
        bool pretty = HasFlag(args, "--pretty");
        bool includeAbsolutePaths = HasFlag(args, "--include-absolute-paths");
        bool failOnWarning = HasFlag(args, "--fail-on-warning");

        // Merge --containers CSV into containers list
        if (!string.IsNullOrWhiteSpace(containersCsv))
        {
            foreach (string part in containersCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    var list = new List<string>(containers) { trimmed };
                    containers = list.ToArray();
                }
            }
        }

        // Default container
        if (containers.Length == 0)
        {
            containers = new[] { "Assets/StreamingAssets/MxFramework/Demo" };
        }

        ModDiagnosticSnapshotDto snapshot;
        try
        {
            snapshot = BuildSnapshot(containers, loadoutPath, includeAbsolutePaths);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("error: " + ex.Message);
            return AuthoringExitCodes.ToolError;
        }

        // --- Serialize ---
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = pretty,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        jsonOptions.Converters.Add(new JsonStringEnumConverter());

        string json = JsonSerializer.Serialize(snapshot, jsonOptions);

        // --- Output routing ---
        if (string.IsNullOrEmpty(outputPath))
        {
            // No --output: full snapshot → stdout, human summary → stderr
            Console.Write(json);
            Console.Error.WriteLine();
            Console.Error.WriteLine(FormatHumanSummary(snapshot));
            return ComputeExitCode(snapshot, failOnWarning);
        }

        // --output specified: full snapshot → file, machine summary → stdout, human summary → stderr
        string dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(outputPath, json, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        // Machine-readable one-line summary to stdout
        var summaryObj = new
        {
            success = snapshot.Success,
            errors = snapshot.Errors?.Count ?? 0,
            warnings = snapshot.Warnings?.Count ?? 0,
            output = outputPath
        };
        Console.WriteLine(JsonSerializer.Serialize(summaryObj, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));

        Console.Error.WriteLine(FormatHumanSummary(snapshot));
        return ComputeExitCode(snapshot, failOnWarning);
    }

    // ---------------------------------------------------------------
    // Pipeline: build snapshot from filesystem data
    // ---------------------------------------------------------------

    internal static ModDiagnosticSnapshotDto BuildSnapshot(string[] containers, string loadoutPath, bool includeAbsolutePaths)
    {
        return ModDiagnosticService.BuildSnapshot(containers, loadoutPath, includeAbsolutePaths);
    }

    // ---------------------------------------------------------------
    // Output helpers
    // ---------------------------------------------------------------

    private static string FormatHumanSummary(ModDiagnosticSnapshotDto snapshot)
    {
        var lines = new List<string>();
        lines.Add($"Mod Diagnostic Snapshot: {(snapshot.Success ? "SUCCESS" : "FAILED")}");
        lines.Add($"  Format: {snapshot.Format}");
        lines.Add($"  Generated: {snapshot.GeneratedUtc}");
        lines.Add($"  Discovered: {snapshot.Summary.Discovered} | Valid: {snapshot.Summary.Valid} | Invalid: {snapshot.Summary.Invalid}");
        lines.Add($"  Ordered: {snapshot.Summary.Ordered} | Skipped: {snapshot.Summary.Skipped}");
        lines.Add($"  Overrides: {snapshot.Summary.Overrides}");
        if (snapshot.Errors != null && snapshot.Errors.Count > 0)
        {
            lines.Add($"  Errors ({snapshot.Errors.Count}):");
            foreach (var err in snapshot.Errors)
                lines.Add($"    [{err.Code}] {err.Message}");
        }
        if (snapshot.Warnings != null && snapshot.Warnings.Count > 0)
        {
            lines.Add($"  Warnings ({snapshot.Warnings.Count}):");
            foreach (var warn in snapshot.Warnings)
                lines.Add($"    [{warn.Code}] {warn.Message}");
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static int ComputeExitCode(ModDiagnosticSnapshotDto snapshot, bool failOnWarning)
    {
        if (!snapshot.Success)
            return AuthoringExitCodes.ValidationBlocked;
        if (failOnWarning && snapshot.Warnings != null && snapshot.Warnings.Count > 0)
            return AuthoringExitCodes.Warning;
        return AuthoringExitCodes.Ready;
    }

    // ---------------------------------------------------------------
    // Argument parsing (reuse Program helpers)
    // ---------------------------------------------------------------

    private static string GetOption(string[] args, string name, string defaultValue)
        => Program.GetOption(args, name, defaultValue);

    private static string[] GetOptionAll(string[] args, string longName, string shortName = null)
    {
        var list = new List<string>();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == longName || (shortName != null && args[i] == shortName))
                list.Add(args[i + 1]);
        }
        return list.ToArray();
    }

    private static bool HasFlag(string[] args, string name)
        => Program.HasFlag(args, name);
}
