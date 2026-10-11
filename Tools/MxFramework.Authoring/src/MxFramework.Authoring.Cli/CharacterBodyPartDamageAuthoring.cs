using System;
using System.IO;
using System.Net;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli
{
    internal static partial class CharacterPackageCommands
    {
        private static int DispatchBodyPartDamage(string[] args, JsonSerializerOptions options)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("error: body-part-damage requires read, validate, or save.");
                return Program.ExitToolError;
            }

            string operation = args[2];
            string packagePath = Program.RequireOption(args, "--package");
            if (operation == "read")
            {
                Console.WriteLine(JsonSerializer.Serialize(
                    EditorServer.ReadCharacterBodyPartDamageStateAtPath(packagePath, options),
                    options));
                return Program.ExitReady;
            }

            CharacterResourcePackage package = ReadPackage(packagePath, options);
            if (operation == "validate")
            {
                string input = Program.GetOption(args, "--input", string.Empty);
                if (!string.IsNullOrWhiteSpace(input))
                    ApplyBodyPartDamageLoadResult(package.Geometry, ReadBodyPartDamageFile(input, options));
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
                CharacterBodyPartDamageLoadResult proposed = ReadBodyPartDamageFile(input, options);
                try
                {
                    object result = EditorServer.SaveCharacterBodyPartDamageAtPath(
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

            Console.Error.WriteLine("error: unknown body-part-damage command.");
            return Program.ExitToolError;
        }

        private static void ReadBodyPartDamage(
            string packagePath,
            JsonSerializerOptions options,
            CharacterAuthoringGeometry geometry)
        {
            string path = Path.Combine(packagePath, "geometry", "body_part_damage.json");
            CharacterBodyPartDamageLoadResult result = File.Exists(path)
                ? ReadBodyPartDamageFile(path, options)
                : CharacterBodyPartDamageDocumentCodec.Missing();
            ApplyBodyPartDamageLoadResult(geometry, result);
        }

        private static CharacterBodyPartDamageLoadResult ReadBodyPartDamageFile(
            string path,
            JsonSerializerOptions options)
        {
            return CharacterBodyPartDamageDocumentCodec.Read(File.ReadAllText(path), options);
        }

        private static void ApplyBodyPartDamageLoadResult(
            CharacterAuthoringGeometry geometry,
            CharacterBodyPartDamageLoadResult result)
        {
            geometry.BodyPartDamage = result.Document;
            geometry.BodyPartDamageDocumentState = result.State;
            geometry.BodyPartDamageRawPayload = result.RawPayload;
        }
    }

    internal static partial class EditorServer
    {
        private static bool TryHandleCharacterBodyPartDamageApi(
            HttpListenerContext context,
            string rootPath,
            string defaultPackage,
            JsonSerializerOptions jsonOptions,
            string path)
        {
            if (path == "/api/character/body-part-damage"
                && context.Request.HttpMethod == "GET")
            {
                try
                {
                    string packageRelative = ResolveCharacterPackageRelative(
                        context,
                        rootPath,
                        defaultPackage);
                    string packagePath = ResolveSafePath(rootPath, packageRelative);
                    WriteJson(
                        context.Response,
                        ReadCharacterBodyPartDamageStateAtPath(packagePath, jsonOptions),
                        jsonOptions);
                }
                catch (ArgumentException)
                {
                    WriteBodyPartDamageError(context.Response, jsonOptions, saveFailure: false);
                }
                catch (DirectoryNotFoundException)
                {
                    WriteBodyPartDamageError(context.Response, jsonOptions, saveFailure: false);
                }
                return true;
            }

            if (path != "/api/character/body-part-damage/save"
                || context.Request.HttpMethod != "POST")
                return false;

            try
            {
                string relative = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
                string body = new StreamReader(
                    context.Request.InputStream,
                    context.Request.ContentEncoding).ReadToEnd();
                CharacterBodyPartDamageSaveRequest request =
                    JsonSerializer.Deserialize<CharacterBodyPartDamageSaveRequest>(body, jsonOptions)
                    ?? new CharacterBodyPartDamageSaveRequest();
                if (request.Document == null)
                    throw new ArgumentException("document is required.");
                var proposed = new CharacterBodyPartDamageLoadResult
                {
                    State = string.Equals(
                            request.Document.Format,
                            CharacterBodyPartDamageDocument.CurrentFormat,
                            StringComparison.Ordinal)
                        && string.Equals(
                            request.Document.SchemaVersion,
                            CharacterBodyPartDamageDocument.CurrentSchemaVersion,
                            StringComparison.Ordinal)
                        ? CharacterBodyPartDamageDocumentState.Current
                        : request.Document.Format != null
                            && request.Document.Format.StartsWith(
                                "mx.characterBodyPartDamage.v",
                                StringComparison.Ordinal)
                            ? CharacterBodyPartDamageDocumentState.Future
                            : CharacterBodyPartDamageDocumentState.Invalid,
                    Document = request.Document
                };
                string packagePath = ResolveSafePath(rootPath, relative);
                object result = SaveCharacterBodyPartDamageAtPath(
                    packagePath,
                    proposed,
                    request.ExpectedRevision,
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
                WriteBodyPartDamageError(context.Response, jsonOptions, saveFailure: false);
            }
            catch (ArgumentException)
            {
                WriteBodyPartDamageError(context.Response, jsonOptions, saveFailure: false);
            }
            catch (DirectoryNotFoundException)
            {
                WriteBodyPartDamageError(context.Response, jsonOptions, saveFailure: false);
            }
            catch (IOException)
            {
                WriteBodyPartDamageError(context.Response, jsonOptions, saveFailure: true);
            }
            catch (UnauthorizedAccessException)
            {
                WriteBodyPartDamageError(context.Response, jsonOptions, saveFailure: true);
            }
            return true;
        }

        internal static object ReadCharacterBodyPartDamageStateAtPath(
            string packagePath,
            JsonSerializerOptions jsonOptions)
        {
            CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(
                packagePath,
                jsonOptions);
            CharacterBodyPartDamageDocument document = package.Geometry.BodyPartDamage;
            return new
            {
                state = package.Geometry.BodyPartDamageDocumentState,
                writeEligible = package.Geometry.BodyPartDamageDocumentState
                    == CharacterBodyPartDamageDocumentState.Current,
                rawPayload = package.Geometry.BodyPartDamageRawPayload,
                document,
                contentHash = package.Geometry.BodyPartDamageDocumentState
                    == CharacterBodyPartDamageDocumentState.Current
                    ? CharacterBodyPartDamageDocumentCodec.ComputeContentHash(document)
                    : string.Empty,
                packageRevision = ComputeCharacterPackageRevision(packagePath)
            };
        }

        internal static object SaveCharacterBodyPartDamageAtPath(
            string packagePath,
            CharacterBodyPartDamageLoadResult proposed,
            string expectedRevision,
            JsonSerializerOptions jsonOptions)
        {
            if (string.IsNullOrWhiteSpace(expectedRevision))
                throw new ArgumentException("expectedRevision is required.");
            if (!Directory.Exists(packagePath))
                throw new DirectoryNotFoundException(
                    "Character package directory was not found: " + packagePath);

            EnsureCharacterPackageRevision(packagePath, expectedRevision, force: false);
            CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(
                packagePath,
                jsonOptions);
            if (package.Geometry.BodyPartDamageDocumentState == CharacterBodyPartDamageDocumentState.Future
                || package.Geometry.BodyPartDamageDocumentState == CharacterBodyPartDamageDocumentState.Invalid)
            {
                throw new CharacterPackageValidationException(
                    CharacterResourcePackageValidator.Validate(package));
            }

            package.Geometry.BodyPartDamage = proposed?.Document;
            package.Geometry.BodyPartDamageDocumentState = proposed?.State
                ?? CharacterBodyPartDamageDocumentState.Invalid;
            package.Geometry.BodyPartDamageRawPayload = proposed?.RawPayload ?? string.Empty;
            CharacterAuthoringValidationReport validation = CharacterResourcePackageValidator.Validate(
                package,
                new CharacterResourcePackageValidationOptions { PackageRootPath = packagePath });
            if (package.Geometry.BodyPartDamageDocumentState
                    != CharacterBodyPartDamageDocumentState.Current
                || HasBodyPartDamageExportBlocker(validation))
                throw new CharacterPackageValidationException(validation);

            CharacterBodyPartDamageDocument canonical =
                CharacterBodyPartDamageDocumentCodec.CreateCanonicalDocument(
                    package.Geometry.BodyPartDamage);
            WriteBodyPartDamageJsonAtomic(
                Path.Combine(packagePath, "geometry", "body_part_damage.json"),
                canonical,
                jsonOptions);
            return ReadCharacterBodyPartDamageStateAtPath(packagePath, jsonOptions);
        }

        private static void PreservePersistedBodyPartDamage(
            CharacterResourcePackage persisted,
            CharacterResourcePackage proposed)
        {
            if (persisted?.Geometry == null || proposed?.Geometry == null)
                return;
            proposed.Geometry.BodyPartDamage = persisted.Geometry.BodyPartDamage;
            proposed.Geometry.BodyPartDamageDocumentState =
                persisted.Geometry.BodyPartDamageDocumentState;
            proposed.Geometry.BodyPartDamageRawPayload = persisted.Geometry.BodyPartDamageRawPayload;
        }

        private static void WriteBodyPartDamageJsonAtomic(
            string path,
            CharacterBodyPartDamageDocument value,
            JsonSerializerOptions jsonOptions)
        {
            string directory = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(directory);
            string temp = path + ".tmp";
            bool tempWritten = false;
            try
            {
                File.WriteAllText(
                    temp,
                    JsonSerializer.Serialize(value, jsonOptions) + Environment.NewLine);
                tempWritten = true;
                ReplaceFileAtomic(temp, path);
            }
            finally
            {
                if (tempWritten && File.Exists(temp))
                    File.Delete(temp);
            }
        }

        private static bool HasBodyPartDamageExportBlocker(
            CharacterAuthoringValidationReport report)
        {
            for (int i = 0; report?.Issues != null && i < report.Issues.Count; i++)
            {
                CharacterAuthoringValidationIssue issue = report.Issues[i];
                if (issue != null
                    && issue.Gate == CharacterAuthoringValidationGate.ExportBlocked
                    && string.Equals(
                        issue.SourcePath,
                        "geometry/body_part_damage.json",
                        StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static void WriteBodyPartDamageError(
            HttpListenerResponse response,
            JsonSerializerOptions jsonOptions,
            bool saveFailure)
        {
            WriteJson(response, new
            {
                success = false,
                code = saveFailure
                    ? "CHARACTER_BODY_PART_DAMAGE_SAVE_FAILED"
                    : "CHARACTER_BODY_PART_DAMAGE_REQUEST_INVALID",
                message = saveFailure
                    ? "Body-part-damage could not be saved; the current document was preserved."
                    : "Body-part-damage request or package selection is invalid."
            }, jsonOptions, saveFailure ? 500 : 400);
        }

        private sealed class CharacterBodyPartDamageSaveRequest
        {
            public string ExpectedRevision { get; set; } = string.Empty;
            public CharacterBodyPartDamageDocument Document { get; set; }
        }
    }
}
