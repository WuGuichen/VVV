using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static class CharacterPackageCommands
{
    public static int Dispatch(string[] args, JsonSerializerOptions options)
    {
        if (args.Length >= 2 && args[1] == "inspect")
        {
            string packagePath = Program.RequireOption(args, "--package");
            CharacterResourcePackage package = ReadPackage(packagePath, options);
            var summary = new
            {
                package.Manifest.PackageId,
                package.Manifest.StableId,
                package.Manifest.Version,
                package.Manifest.Kind,
                package.Manifest.PackageSchemaVersion,
                package.Manifest.AuthoringSchemaVersion,
                resourceCount = package.ResourceCatalog.Entries.Count,
                bodyPartCount = package.Geometry.BodyParts.Count,
                colliderCount = package.Geometry.Colliders.Count,
                socketCount = package.Geometry.Sockets.Count,
                weaponAttachmentCount = package.Geometry.WeaponAttachments.Count,
                traceCount = package.Geometry.Traces.Count
            };
            Console.WriteLine(JsonSerializer.Serialize(summary, options));
            return Program.ExitReady;
        }

        if (args.Length >= 2 && args[1] == "validate")
        {
            string packagePath = Program.RequireOption(args, "--package");
            CharacterResourcePackage package = ReadPackage(packagePath, options);
            CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package);
            Console.Write(report.ToText());
            return report.HasBlockingIssues ? Program.ExitValidationBlocked : Program.ExitReady;
        }

        if (args.Length >= 2 && args[1] == "schema")
        {
            var schema = new
            {
                schemas = CharacterResourcePackageSchemas.CreateAll(),
                enums = CharacterResourcePackageSchemas.CreateEnumDomains()
            };
            Console.WriteLine(JsonSerializer.Serialize(schema, options));
            return Program.ExitReady;
        }

        Console.Error.WriteLine("error: unknown character command.");
        return Program.ExitToolError;
    }

    internal static CharacterResourcePackage ReadPackage(string packagePath, JsonSerializerOptions options)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
            throw new ArgumentException("--package is required.");
        if (!Directory.Exists(packagePath))
            throw new DirectoryNotFoundException("Character package directory was not found: " + packagePath);

        var package = new CharacterResourcePackage
        {
            Manifest = ReadRequired<CharacterPackageManifest>(packagePath, "manifest.json", options),
            ResourceCatalog = ReadOptional<CharacterPackageResourceCatalog>(packagePath, "resource_catalog.json", options) ?? new CharacterPackageResourceCatalog(),
            Geometry = ReadGeometry(packagePath, options),
            ValidationReport = ReadOptional<CharacterAuthoringValidationReport>(packagePath, Path.Combine("validation", "last_report.json"), options) ?? new CharacterAuthoringValidationReport()
        };

        return package;
    }

    private static CharacterAuthoringGeometry ReadGeometry(string packagePath, JsonSerializerOptions options)
    {
        CharacterAuthoringGeometry aggregate = ReadOptional<CharacterAuthoringGeometry>(packagePath, Path.Combine("geometry", "authoring_geometry.json"), options);
        if (aggregate != null)
            return aggregate;

        var geometry = new CharacterAuthoringGeometry
        {
            BodyProfile = ReadOptional<CharacterBodyGeometryProfile>(packagePath, Path.Combine("geometry", "body_geometry.json"), options) ?? new CharacterBodyGeometryProfile()
        };

        CharacterBodyPartsDocument bodyParts = ReadOptional<CharacterBodyPartsDocument>(packagePath, Path.Combine("geometry", "body_parts.json"), options);
        if (bodyParts != null && bodyParts.BodyParts != null)
            geometry.BodyParts.AddRange(bodyParts.BodyParts);

        CharacterBodyCollidersDocument colliders = ReadOptional<CharacterBodyCollidersDocument>(packagePath, Path.Combine("geometry", "body_colliders.json"), options);
        if (colliders != null && colliders.Colliders != null)
            geometry.Colliders.AddRange(colliders.Colliders);

        CharacterSocketsDocument sockets = ReadOptional<CharacterSocketsDocument>(packagePath, Path.Combine("geometry", "sockets.json"), options);
        if (sockets != null && sockets.Sockets != null)
            geometry.Sockets.AddRange(sockets.Sockets);

        WeaponAttachmentsDocument attachments = ReadOptional<WeaponAttachmentsDocument>(packagePath, Path.Combine("geometry", "weapon_attachments.json"), options);
        if (attachments != null && attachments.Attachments != null)
            geometry.WeaponAttachments.AddRange(attachments.Attachments);

        WeaponTracesDocument traces = ReadOptional<WeaponTracesDocument>(packagePath, Path.Combine("geometry", "traces.json"), options);
        if (traces != null && traces.Traces != null)
            geometry.Traces.AddRange(traces.Traces);

        return geometry;
    }

    private static T ReadRequired<T>(string packagePath, string relativePath, JsonSerializerOptions options) where T : new()
    {
        string path = Path.Combine(packagePath, relativePath);
        if (!File.Exists(path))
            throw new FileNotFoundException("Required character package file is missing: " + path);
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), options) ?? new T();
    }

    private static T ReadOptional<T>(string packagePath, string relativePath, JsonSerializerOptions options) where T : class, new()
    {
        string path = Path.Combine(packagePath, relativePath);
        if (!File.Exists(path))
            return null;
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), options) ?? new T();
    }

    private sealed class CharacterBodyPartsDocument
    {
        public string SchemaVersion { get; set; } = "1.0";
        public List<CharacterBodyPartAuthoring> BodyParts { get; set; } = new();
    }

    private sealed class CharacterBodyCollidersDocument
    {
        public string SchemaVersion { get; set; } = "1.0";
        public List<CharacterBodyColliderProfile> Colliders { get; set; } = new();
    }

    private sealed class CharacterSocketsDocument
    {
        public string SchemaVersion { get; set; } = "1.0";
        public List<CharacterSocketProfile> Sockets { get; set; } = new();
    }

    private sealed class WeaponAttachmentsDocument
    {
        public string SchemaVersion { get; set; } = "1.0";
        public List<WeaponAttachmentProfile> Attachments { get; set; } = new();
    }

    private sealed class WeaponTracesDocument
    {
        public string SchemaVersion { get; set; } = "1.0";
        public List<WeaponTraceProfile> Traces { get; set; } = new();
    }
}
