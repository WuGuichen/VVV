using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class CharacterPackageCommands
{
    private static int DispatchBodyPresence(string[] args, JsonSerializerOptions options)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("error: body-presence requires read, validate, or save.");
            return Program.ExitToolError;
        }

        string operation = args[2];
        string packagePath = Program.RequireOption(args, "--package");
        if (operation == "read")
        {
            Console.WriteLine(JsonSerializer.Serialize(
                EditorServer.ReadCharacterBodyPresenceStateAtPath(packagePath, options),
                options));
            return Program.ExitReady;
        }

        CharacterResourcePackage package = ReadPackage(packagePath, options);
        if (operation == "validate")
        {
            string input = Program.GetOption(args, "--input", string.Empty);
            if (!string.IsNullOrWhiteSpace(input))
                ApplyBodyPresenceLoadResult(package.Geometry, ReadBodyPresenceFile(input, options));
            CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(
                package,
                new CharacterResourcePackageValidationOptions { PackageRootPath = packagePath });
            Console.Write(report.ToText());
            return report.HasBlockingIssues ? Program.ExitValidationBlocked : Program.ExitReady;
        }

        if (operation == "save")
        {
            string input = Program.RequireOption(args, "--input");
            string expectedRevision = Program.RequireOption(args, "--expected-revision");
            CharacterBodyPresenceLoadResult proposed = ReadBodyPresenceFile(input, options);
            try
            {
                object result = EditorServer.SaveCharacterBodyPresenceAtPath(
                    packagePath,
                    proposed,
                    expectedRevision,
                    options);
                Console.WriteLine(JsonSerializer.Serialize(result, options));
                return Program.ExitReady;
            }
            catch (EditorServer.CharacterPackageWriteConflictException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return Program.ExitValidationBlocked;
            }
            catch (EditorServer.CharacterPackageValidationException ex)
            {
                Console.Error.Write(ex.Validation.ToText());
                return Program.ExitValidationBlocked;
            }
        }

        Console.Error.WriteLine("error: unknown body-presence command.");
        return Program.ExitToolError;
    }

    private static void ReadBodyPresence(
        string packagePath,
        JsonSerializerOptions options,
        CharacterAuthoringGeometry geometry)
    {
        string path = Path.Combine(packagePath, "geometry", "body_presence.json");
        CharacterBodyPresenceLoadResult result = File.Exists(path)
            ? ReadBodyPresenceFile(path, options)
            : CharacterBodyPresenceDocumentCodec.Missing();
        ApplyBodyPresenceLoadResult(geometry, result);
    }

    private static CharacterBodyPresenceLoadResult ReadBodyPresenceFile(
        string path,
        JsonSerializerOptions options)
    {
        return CharacterBodyPresenceDocumentCodec.Read(File.ReadAllText(path), options);
    }

    private static void ApplyBodyPresenceLoadResult(
        CharacterAuthoringGeometry geometry,
        CharacterBodyPresenceLoadResult result)
    {
        geometry.BodyPresence = result.Document;
        geometry.BodyPresenceDocumentState = result.State;
        geometry.BodyPresenceRawPayload = result.RawPayload;
    }
}
