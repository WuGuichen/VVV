using System.Net;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private static bool TryHandleCharacterBodyPresenceApi(
        HttpListenerContext context,
        string rootPath,
        string defaultPackage,
        JsonSerializerOptions jsonOptions,
        string path)
    {
        if (path == "/api/character/body-presence" && context.Request.HttpMethod == "GET")
        {
            try
            {
                string packageRelative = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
                string packagePath = ResolveSafePath(rootPath, packageRelative);
                WriteJson(context.Response, ReadCharacterBodyPresenceStateAtPath(packagePath, jsonOptions), jsonOptions);
            }
            catch (ArgumentException)
            {
                WriteBodyPresenceRequestError(context.Response, jsonOptions);
            }
            catch (DirectoryNotFoundException)
            {
                WriteBodyPresenceRequestError(context.Response, jsonOptions);
            }
            return true;
        }

        if (path != "/api/character/body-presence/save" || context.Request.HttpMethod != "POST")
            return false;

        try
        {
            string relative = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            CharacterBodyPresenceSaveRequest request =
                JsonSerializer.Deserialize<CharacterBodyPresenceSaveRequest>(body, jsonOptions)
                ?? new CharacterBodyPresenceSaveRequest();
            if (request.document == null)
                throw new ArgumentException("document is required.");
            CharacterBodyPresenceLoadResult proposed = new CharacterBodyPresenceLoadResult
            {
                State = IsCurrentBodyPresence(request.document)
                    ? CharacterBodyPresenceDocumentState.Current
                    : CharacterBodyPresenceDocumentState.UnsupportedSchema,
                Document = request.document
            };
            string packagePath = ResolveSafePath(rootPath, relative);
            object result = SaveCharacterBodyPresenceAtPath(
                packagePath,
                proposed,
                request.expectedRevision,
                jsonOptions);
            InvalidateAuthoringResourceCache(rootPath, relative);
            WriteJson(context.Response, result, jsonOptions);
        }
        catch (CharacterPackageWriteConflictException ex)
        {
            WriteCharacterPackageWriteConflict(context.Response, ex, jsonOptions);
        }
        catch (CharacterPackageValidationException ex)
        {
            WriteJson(context.Response, new
            {
                success = false,
                code = CharacterPackageValidationException.ErrorCode,
                message = ex.Message,
                validation = ex.Validation
            }, jsonOptions, 422);
        }
        catch (JsonException)
        {
            WriteBodyPresenceRequestError(context.Response, jsonOptions);
        }
        catch (ArgumentException)
        {
            WriteBodyPresenceRequestError(context.Response, jsonOptions);
        }
        catch (DirectoryNotFoundException)
        {
            WriteBodyPresenceRequestError(context.Response, jsonOptions);
        }
        catch (IOException)
        {
            WriteJson(context.Response, new
            {
                success = false,
                code = "CHARACTER_BODY_PRESENCE_SAVE_FAILED",
                message = "Body Presence could not be saved; the current document was preserved."
            }, jsonOptions, 500);
        }
        catch (UnauthorizedAccessException)
        {
            WriteJson(context.Response, new
            {
                success = false,
                code = "CHARACTER_BODY_PRESENCE_SAVE_FAILED",
                message = "Body Presence could not be saved; the current document was preserved."
            }, jsonOptions, 500);
        }
        return true;
    }

    private static void WriteBodyPresenceRequestError(
        HttpListenerResponse response,
        JsonSerializerOptions jsonOptions)
    {
        WriteJson(response, new
        {
            success = false,
            code = "CHARACTER_BODY_PRESENCE_REQUEST_INVALID",
            message = "Body Presence request or package selection is invalid."
        }, jsonOptions, 400);
    }

    internal static object ReadCharacterBodyPresenceStateAtPath(
        string packagePath,
        JsonSerializerOptions jsonOptions)
    {
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        CharacterBodyPresenceDocument document = package.Geometry.BodyPresence;
        return new
        {
            state = package.Geometry.BodyPresenceDocumentState,
            writeEligible = package.Geometry.BodyPresenceWriteEligible,
            rawPayload = package.Geometry.BodyPresenceRawPayload,
            document,
            canonicalContent = package.Geometry.BodyPresenceWriteEligible
                ? CharacterBodyPresenceDocumentCodec.SerializeCanonical(document)
                : string.Empty,
            contentHash = package.Geometry.BodyPresenceWriteEligible
                ? CharacterBodyPresenceDocumentCodec.ComputeContentHash(document)
                : string.Empty,
            packageRevision = ComputeCharacterPackageRevision(packagePath)
        };
    }

    internal static object SaveCharacterBodyPresenceAtPath(
        string packagePath,
        CharacterBodyPresenceLoadResult proposed,
        string expectedRevision,
        JsonSerializerOptions jsonOptions)
    {
        if (string.IsNullOrWhiteSpace(expectedRevision))
            throw new ArgumentException("expectedRevision is required.");
        if (!Directory.Exists(packagePath))
            throw new DirectoryNotFoundException("Character package directory was not found: " + packagePath);

        EnsureCharacterPackageRevision(packagePath, expectedRevision, force: false);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        if (package.Geometry.BodyPresenceDocumentState == CharacterBodyPresenceDocumentState.UnsupportedSchema
            || package.Geometry.BodyPresenceDocumentState == CharacterBodyPresenceDocumentState.Invalid)
        {
            throw new CharacterPackageValidationException(
                CharacterResourcePackageValidator.Validate(package));
        }

        package.Geometry.BodyPresence = proposed?.Document;
        package.Geometry.BodyPresenceDocumentState = proposed?.State
            ?? CharacterBodyPresenceDocumentState.Invalid;
        package.Geometry.BodyPresenceRawPayload = proposed?.RawPayload ?? string.Empty;
        CharacterAuthoringValidationReport validation = CharacterResourcePackageValidator.Validate(
            package,
            new CharacterResourcePackageValidationOptions { PackageRootPath = packagePath });
        if (!package.Geometry.BodyPresenceWriteEligible || HasBodyPresenceExportBlocker(validation))
            throw new CharacterPackageValidationException(validation);

        CharacterBodyPresenceDocument canonical =
            CharacterBodyPresenceDocumentCodec.CreateCanonicalDocument(package.Geometry.BodyPresence);
        WriteBodyPresenceJsonAtomic(
            Path.Combine(packagePath, "geometry", "body_presence.json"),
            canonical,
            jsonOptions);
        return ReadCharacterBodyPresenceStateAtPath(packagePath, jsonOptions);
    }

    private static void WriteBodyPresenceJsonAtomic(
        string path,
        CharacterBodyPresenceDocument value,
        JsonSerializerOptions jsonOptions)
    {
        string directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        string temp = path + ".tmp";
        bool tempWritten = false;
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(value, jsonOptions) + Environment.NewLine);
            tempWritten = true;
            ReplaceFileAtomic(temp, path);
        }
        finally
        {
            if (tempWritten && File.Exists(temp))
                File.Delete(temp);
        }
    }

    private static bool HasBodyPresenceExportBlocker(CharacterAuthoringValidationReport report)
    {
        for (int i = 0; report?.Issues != null && i < report.Issues.Count; i++)
        {
            CharacterAuthoringValidationIssue issue = report.Issues[i];
            if (issue != null
                && issue.Gate == CharacterAuthoringValidationGate.ExportBlocked
                && string.Equals(issue.SourcePath, "geometry/body_presence.json", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static bool IsCurrentBodyPresence(CharacterBodyPresenceDocument document)
    {
        return document != null
            && CharacterBodyPresenceDocumentCodec.IsSupportedSchema(document.Format, document.SchemaVersion);
    }

    private static void PreservePersistedBodyPresence(
        CharacterResourcePackage persisted,
        CharacterResourcePackage proposed)
    {
        if (persisted?.Geometry == null || proposed?.Geometry == null)
            return;
        proposed.Geometry.BodyPresence = persisted.Geometry.BodyPresence;
        proposed.Geometry.BodyPresenceDocumentState = persisted.Geometry.BodyPresenceDocumentState;
        proposed.Geometry.BodyPresenceRawPayload = persisted.Geometry.BodyPresenceRawPayload;
    }

    private sealed class CharacterBodyPresenceSaveRequest
    {
        public string expectedRevision { get; set; } = string.Empty;
        public CharacterBodyPresenceDocument document { get; set; }
    }
}
