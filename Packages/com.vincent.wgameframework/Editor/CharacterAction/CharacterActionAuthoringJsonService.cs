using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MxFramework.CharacterAction;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Editor.CharacterAction
{
    public sealed class CharacterActionAuthoringImportResult
    {
        public CharacterActionAuthoringImportResult(
            CharacterActionAuthoringModel model,
            CharacterActionAuthoringExportResult validation)
        {
            Model = model;
            Validation = validation;
        }

        public CharacterActionAuthoringModel Model { get; }
        public CharacterActionAuthoringExportResult Validation { get; }
        public bool Success => Model != null && Validation != null && Validation.ImportResult.Success;
    }

    public sealed class CharacterActionAuthoringExportResult
    {
        public CharacterActionAuthoringExportResult(
            bool success,
            string actionSetsJson,
            string actionsJson,
            string reactionProfilesJson,
            CharacterActionJsonImportResult importResult,
            CharacterActionConfigRegistry registry,
            CharacterActionValidationReport localValidationReport,
            CharacterActionValidationReport validationReport,
            CharacterActionWorkstationSnapshot[] workstationSnapshots,
            string diagnosticsText,
            string landingProfilesJson = null)
        {
            Success = success;
            ActionSetsJson = actionSetsJson ?? string.Empty;
            ActionsJson = actionsJson ?? string.Empty;
            ReactionProfilesJson = reactionProfilesJson ?? string.Empty;
            ImportResult = importResult;
            Registry = registry;
            LocalValidationReport = localValidationReport;
            ValidationReport = validationReport;
            WorkstationSnapshots = workstationSnapshots ?? Array.Empty<CharacterActionWorkstationSnapshot>();
            DiagnosticsText = diagnosticsText ?? string.Empty;
            LandingProfilesJson = landingProfilesJson
                ?? CharacterActionAuthoringJsonService.CreateEmptyLandingProfilesJson();
        }

        public bool Success { get; }
        public string ActionSetsJson { get; }
        public string ActionsJson { get; }
        public string ReactionProfilesJson { get; }
        public string LandingProfilesJson { get; }
        public CharacterActionJsonImportResult ImportResult { get; }
        public CharacterActionConfigRegistry Registry { get; }
        public CharacterActionValidationReport LocalValidationReport { get; }
        public CharacterActionValidationReport ValidationReport { get; }
        public CharacterActionWorkstationSnapshot[] WorkstationSnapshots { get; }
        public string DiagnosticsText { get; }
        public bool HasErrors => ImportResult == null
            || !ImportResult.Success
            || ValidationReport == null
            || ValidationReport.HasErrors;
    }

    public static class CharacterActionAuthoringJsonService
    {
        public static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Include,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters = { new StringEnumConverter() },
        };

        public static CharacterActionAuthoringImportResult Import(
            string actionSetsJson,
            string actionsJson,
            string reactionProfilesJson = null,
            string landingProfilesJson = null)
        {
            CharacterActionAuthoringExportResult validation = ValidateJson(
                actionSetsJson,
                actionsJson,
                ResolveDefaultReactionProfilesJson(reactionProfilesJson),
                ResolveDefaultLandingProfilesJson(landingProfilesJson),
                CharacterActionAuthoringReferenceContext.CreateDefault(),
                buildWorkstationSnapshots: true);
            if (!validation.ImportResult.Success)
                return new CharacterActionAuthoringImportResult(null, validation);

            CharacterActionSetsJsonDocument actionSetsDocument =
                JsonConvert.DeserializeObject<CharacterActionSetsJsonDocument>(actionSetsJson, JsonSettings);
            CharacterActionsJsonDocument actionsDocument =
                JsonConvert.DeserializeObject<CharacterActionsJsonDocument>(actionsJson, JsonSettings);
            CharacterReactionProfilesJsonDocument reactionProfilesDocument =
                JsonConvert.DeserializeObject<CharacterReactionProfilesJsonDocument>(
                    ResolveDefaultReactionProfilesJson(reactionProfilesJson),
                    JsonSettings);
            CharacterLandingProfilesJsonDocument landingProfilesDocument =
                JsonConvert.DeserializeObject<CharacterLandingProfilesJsonDocument>(
                    ResolveDefaultLandingProfilesJson(landingProfilesJson),
                    JsonSettings);

            var model = new CharacterActionAuthoringModel
            {
                ActionSet = (actionSetsDocument?.ActionSets != null && actionSetsDocument.ActionSets.Length > 0)
                    ? actionSetsDocument.ActionSets[0]
                    : new CharacterActionSetJsonDto(),
            };
            if (actionsDocument?.Actions != null)
                model.Actions.AddRange(actionsDocument.Actions);
            if (reactionProfilesDocument?.ReactionProfiles != null)
                model.ReactionProfiles.AddRange(reactionProfilesDocument.ReactionProfiles);
            if (landingProfilesDocument?.LandingProfiles != null)
                model.LandingProfiles.AddRange(landingProfilesDocument.LandingProfiles);
            model.ReferenceContext = CharacterActionAuthoringReferenceContext.CreateFromModel(model);
            model.Normalize();
            return new CharacterActionAuthoringImportResult(model, validation);
        }

        public static CharacterActionAuthoringImportResult ImportProfileOnly(string profileJson)
        {
            CharacterActionProfilesJsonDocument profileDocument =
                JsonConvert.DeserializeObject<CharacterActionProfilesJsonDocument>(profileJson, JsonSettings);
            var model = new CharacterActionAuthoringModel
            {
                Profile = (profileDocument?.Profiles != null && profileDocument.Profiles.Length > 0)
                    ? profileDocument.Profiles[0]
                    : new CharacterActionProfileJsonDto(),
                ReferenceContext = CharacterActionAuthoringReferenceContext.CreateDefault(),
            };
            model.Normalize();
            string emptyActionSetsJson = CreateEmptyActionSetsJson();
            string emptyActionsJson = CreateEmptyActionsJson();
            string emptyReactionProfilesJson = CreateEmptyReactionProfilesJson();
            CharacterActionJsonImportResult importResult = CharacterActionJsonImporter.Import(
                emptyActionSetsJson,
                emptyActionsJson,
                emptyReactionProfilesJson,
                CharacterActionJsonImporter.DefaultActionSetsSourcePath,
                CharacterActionJsonImporter.DefaultActionsSourcePath,
                CharacterActionJsonImporter.DefaultReactionProfilesSourcePath);
            CharacterActionAuthoringExportResult validation = ValidateImportedJson(
                emptyActionSetsJson,
                emptyActionsJson,
                emptyReactionProfilesJson,
                importResult,
                model.ReferenceContext,
                buildWorkstationSnapshots: false);
            return new CharacterActionAuthoringImportResult(model, validation);
        }

        public static CharacterActionAuthoringImportResult ImportActionSetsOnly(string actionSetsJson)
        {
            return ImportActionSetsOnly(actionSetsJson, CreateEmptyReactionProfilesJson());
        }

        public static CharacterActionAuthoringImportResult ImportActionSetsOnly(
            string actionSetsJson,
            string reactionProfilesJson,
            string landingProfilesJson = null)
        {
            reactionProfilesJson = string.IsNullOrWhiteSpace(reactionProfilesJson)
                ? CreateEmptyReactionProfilesJson()
                : reactionProfilesJson;
            landingProfilesJson = string.IsNullOrWhiteSpace(landingProfilesJson)
                ? CreateEmptyLandingProfilesJson()
                : landingProfilesJson;
            string emptyActionsJson = CreateEmptyActionsJson();
            CharacterActionJsonImportResult importResult = ImportActionSetsAndReactionProfilesOnly(
                actionSetsJson,
                reactionProfilesJson,
                landingProfilesJson);
            var registry = new CharacterActionConfigRegistry(
                importResult.ActionSets,
                importResult.Actions,
                CharacterActionJsonImporter.DefaultActionSetsSourcePath,
                CharacterActionJsonImporter.DefaultActionsSourcePath,
                importResult.ReactionProfiles,
                CharacterActionJsonImporter.DefaultReactionProfilesSourcePath,
                importResult.LandingProfiles,
                CharacterActionJsonImporter.DefaultLandingProfilesSourcePath);
            var emptyReport = new CharacterActionValidationReport(Array.Empty<CharacterActionValidationDiagnostic>());
            CharacterActionAuthoringExportResult validation = new CharacterActionAuthoringExportResult(
                importResult.Success,
                actionSetsJson,
                emptyActionsJson,
                reactionProfilesJson,
                importResult,
                registry,
                emptyReport,
                emptyReport,
                Array.Empty<CharacterActionWorkstationSnapshot>(),
                FormatDiagnostics(importResult, emptyReport, emptyReport),
                landingProfilesJson);
            if (!validation.ImportResult.Success)
                return new CharacterActionAuthoringImportResult(null, validation);

            CharacterActionSetsJsonDocument actionSetsDocument =
                JsonConvert.DeserializeObject<CharacterActionSetsJsonDocument>(actionSetsJson, JsonSettings);
            CharacterReactionProfilesJsonDocument reactionProfilesDocument =
                JsonConvert.DeserializeObject<CharacterReactionProfilesJsonDocument>(reactionProfilesJson, JsonSettings);
            var model = new CharacterActionAuthoringModel
            {
                ActionSet = (actionSetsDocument?.ActionSets != null && actionSetsDocument.ActionSets.Length > 0)
                    ? actionSetsDocument.ActionSets[0]
                    : new CharacterActionSetJsonDto(),
                ReferenceContext = CharacterActionAuthoringReferenceContext.CreateDefault(),
            };
            if (reactionProfilesDocument?.ReactionProfiles != null)
                model.ReactionProfiles.AddRange(reactionProfilesDocument.ReactionProfiles);
            CharacterLandingProfilesJsonDocument landingProfilesDocument =
                JsonConvert.DeserializeObject<CharacterLandingProfilesJsonDocument>(landingProfilesJson, JsonSettings);
            if (landingProfilesDocument?.LandingProfiles != null)
                model.LandingProfiles.AddRange(landingProfilesDocument.LandingProfiles);
            model.Normalize();
            return new CharacterActionAuthoringImportResult(model, validation);
        }

        private static CharacterActionJsonImportResult ImportActionSetsAndReactionProfilesOnly(
            string actionSetsJson,
            string reactionProfilesJson,
            string landingProfilesJson)
        {
            CharacterActionJsonImportResult actionSetsImport = CharacterActionJsonImporter.ImportActionSets(
                actionSetsJson,
                CharacterActionJsonImporter.DefaultActionSetsSourcePath);
            CharacterActionJsonImportResult reactionProfilesImport = CharacterActionJsonImporter.ImportReactionProfiles(
                reactionProfilesJson,
                CharacterActionJsonImporter.DefaultReactionProfilesSourcePath);
            CharacterActionJsonImportResult landingProfilesImport = CharacterActionJsonImporter.ImportLandingProfiles(
                landingProfilesJson,
                CharacterActionJsonImporter.DefaultLandingProfilesSourcePath);
            CharacterActionJsonDiagnostic[] diagnostics = MergeImportDiagnostics(
                actionSetsImport.Diagnostics,
                reactionProfilesImport.Diagnostics);
            diagnostics = MergeImportDiagnostics(diagnostics, landingProfilesImport.Diagnostics);
            return new CharacterActionJsonImportResult(
                actionSetsImport.ActionSets,
                Array.Empty<CharacterActionConfig>(),
                reactionProfilesImport.ReactionProfiles,
                landingProfilesImport.LandingProfiles,
                diagnostics);
        }

        private static CharacterActionJsonDiagnostic[] MergeImportDiagnostics(
            CharacterActionJsonDiagnostic[] left,
            CharacterActionJsonDiagnostic[] right)
        {
            int leftLength = left?.Length ?? 0;
            int rightLength = right?.Length ?? 0;
            if (leftLength == 0 && rightLength == 0)
                return Array.Empty<CharacterActionJsonDiagnostic>();

            CharacterActionJsonDiagnostic[] result = new CharacterActionJsonDiagnostic[leftLength + rightLength];
            if (leftLength > 0)
                Array.Copy(left, 0, result, 0, leftLength);
            if (rightLength > 0)
                Array.Copy(right, 0, result, leftLength, rightLength);
            Array.Sort(result, CompareImportDiagnostics);
            return result;
        }

        private static int CompareImportDiagnostics(
            CharacterActionJsonDiagnostic left,
            CharacterActionJsonDiagnostic right)
        {
            int compare = string.CompareOrdinal(left.SourcePath, right.SourcePath);
            if (compare != 0)
                return compare;
            compare = string.CompareOrdinal(left.JsonPath, right.JsonPath);
            if (compare != 0)
                return compare;
            compare = string.CompareOrdinal(left.Code, right.Code);
            if (compare != 0)
                return compare;
            return string.CompareOrdinal(left.Message, right.Message);
        }

        public static CharacterActionAuthoringImportResult ImportActionsOnly(string actionsJson)
        {
            CharacterActionJsonImportResult importResult = CharacterActionJsonImporter.ImportActions(
                actionsJson,
                CharacterActionJsonImporter.DefaultActionsSourcePath);
            CharacterActionAuthoringExportResult validation = ValidateImportedJson(
                CreateEmptyActionSetsJson(),
                actionsJson,
                CreateEmptyReactionProfilesJson(),
                importResult,
                CharacterActionAuthoringReferenceContext.CreateDefault(),
                buildWorkstationSnapshots: false);
            if (!validation.ImportResult.Success)
                return new CharacterActionAuthoringImportResult(null, validation);

            CharacterActionsJsonDocument actionsDocument =
                JsonConvert.DeserializeObject<CharacterActionsJsonDocument>(actionsJson, JsonSettings);
            var model = new CharacterActionAuthoringModel
            {
                ActionSet = new CharacterActionSetJsonDto(),
                ReferenceContext = CharacterActionAuthoringReferenceContext.CreateDefault(),
            };
            if (actionsDocument?.Actions != null)
                model.Actions.AddRange(actionsDocument.Actions);
            model.ReferenceContext = CharacterActionAuthoringReferenceContext.CreateFromModel(model);
            model.Normalize();
            return new CharacterActionAuthoringImportResult(model, validation);
        }

        public static CharacterActionAuthoringExportResult Validate(CharacterActionAuthoringModel model)
        {
            return Validate(model, model?.ReferenceContext, buildWorkstationSnapshots: true);
        }

        public static CharacterActionAuthoringExportResult Validate(
            CharacterActionAuthoringModel model,
            CharacterActionAuthoringReferenceContext referenceContext)
        {
            return Validate(model, referenceContext, buildWorkstationSnapshots: true);
        }

        public static CharacterActionAuthoringExportResult Validate(
            CharacterActionAuthoringModel model,
            CharacterActionAuthoringReferenceContext referenceContext,
            bool buildWorkstationSnapshots)
        {
            string actionSetsJson;
            string actionsJson;
            string reactionProfilesJson;
            string landingProfilesJson;
            ExportJsonUnchecked(model, out actionSetsJson, out actionsJson, out reactionProfilesJson, out landingProfilesJson);
            return ValidateJson(actionSetsJson, actionsJson, reactionProfilesJson, landingProfilesJson, referenceContext, buildWorkstationSnapshots);
        }

        public static CharacterActionAuthoringExportResult Export(CharacterActionAuthoringModel model)
        {
            return Export(model, model?.ReferenceContext, buildWorkstationSnapshots: true);
        }

        public static CharacterActionAuthoringExportResult Export(
            CharacterActionAuthoringModel model,
            CharacterActionAuthoringReferenceContext referenceContext)
        {
            return Export(model, referenceContext, buildWorkstationSnapshots: true);
        }

        public static CharacterActionAuthoringExportResult Export(
            CharacterActionAuthoringModel model,
            CharacterActionAuthoringReferenceContext referenceContext,
            bool buildWorkstationSnapshots)
        {
            CharacterActionAuthoringExportResult result = Validate(model, referenceContext, buildWorkstationSnapshots);
            return new CharacterActionAuthoringExportResult(
                !result.HasErrors,
                result.ActionSetsJson,
                result.ActionsJson,
                result.ReactionProfilesJson,
                result.ImportResult,
                result.Registry,
                result.LocalValidationReport,
                result.ValidationReport,
                result.WorkstationSnapshots,
                result.DiagnosticsText,
                result.LandingProfilesJson);
        }

        public static void WriteExportFiles(string directory, CharacterActionAuthoringExportResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (!result.Success)
                throw new InvalidOperationException("Cannot write Character Action JSON while validation has blocking errors.");
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Export directory is empty.", nameof(directory));

            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, CharacterActionJsonImporter.DefaultActionSetsSourcePath), result.ActionSetsJson);
            File.WriteAllText(Path.Combine(directory, CharacterActionJsonImporter.DefaultActionsSourcePath), result.ActionsJson);
            File.WriteAllText(Path.Combine(directory, CharacterActionJsonImporter.DefaultReactionProfilesSourcePath), result.ReactionProfilesJson);
            File.WriteAllText(Path.Combine(directory, CharacterActionJsonImporter.DefaultLandingProfilesSourcePath), result.LandingProfilesJson);
        }

        public static void WriteActionSetsFile(string directory, CharacterActionAuthoringExportResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (!result.Success)
                throw new InvalidOperationException("Cannot write Character ActionSet JSON while validation has blocking errors.");
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Export directory is empty.", nameof(directory));

            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, CharacterActionJsonImporter.DefaultActionSetsSourcePath), result.ActionSetsJson);
        }

        public static void WriteReactionProfilesFile(string directory, CharacterActionAuthoringExportResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (!result.Success)
                throw new InvalidOperationException("Cannot write Character ReactionProfile JSON while validation has blocking errors.");
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Export directory is empty.", nameof(directory));

            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, CharacterActionJsonImporter.DefaultReactionProfilesSourcePath),
                result.ReactionProfilesJson);
        }

        public static void WriteLandingProfilesFile(string directory, CharacterActionAuthoringExportResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (!result.Success)
                throw new InvalidOperationException("Cannot write Character LandingProfile JSON while validation has blocking errors.");
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, CharacterActionJsonImporter.DefaultLandingProfilesSourcePath),
                result.LandingProfilesJson);
        }

        public static void WriteActionsFile(string directory, CharacterActionAuthoringExportResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (!result.Success)
                throw new InvalidOperationException("Cannot write Character Actions JSON while validation has blocking errors.");
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Export directory is empty.", nameof(directory));

            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, CharacterActionJsonImporter.DefaultActionsSourcePath), result.ActionsJson);
        }

        public static void WriteProfileFile(string directory, CharacterActionAuthoringModel model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Export directory is empty.", nameof(directory));

            model.Normalize();
            var document = new CharacterActionProfilesJsonDocument
            {
                SchemaVersion = CharacterActionJsonImporter.CurrentProjectionSchemaVersion,
                Profiles = new[] { model.Profile },
            };

            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, CharacterActionJsonImporter.DefaultProfilesSourcePath),
                JsonConvert.SerializeObject(document, JsonSettings));
        }

        public static void ExportJsonUnchecked(
            CharacterActionAuthoringModel model,
            out string actionSetsJson,
            out string actionsJson)
        {
            string reactionProfilesJson;
            ExportJsonUnchecked(model, out actionSetsJson, out actionsJson, out reactionProfilesJson);
        }

        public static void ExportJsonUnchecked(
            CharacterActionAuthoringModel model,
            out string actionSetsJson,
            out string actionsJson,
            out string reactionProfilesJson)
        {
            string landingProfilesJson;
            ExportJsonUnchecked(model, out actionSetsJson, out actionsJson, out reactionProfilesJson, out landingProfilesJson);
        }

        public static void ExportJsonUnchecked(
            CharacterActionAuthoringModel model,
            out string actionSetsJson,
            out string actionsJson,
            out string reactionProfilesJson,
            out string landingProfilesJson)
        {
            model = model ?? CharacterActionAuthoringModel.CreateDefault();
            model.Normalize();

            var actionSetsDocument = new CharacterActionSetsJsonDocument
            {
                SchemaVersion = CharacterActionJsonImporter.CurrentProjectionSchemaVersion,
                ActionSets = new[] { model.ActionSet },
            };
            var actionsDocument = new CharacterActionsJsonDocument
            {
                SchemaVersion = CharacterActionJsonImporter.CurrentActionLibrarySchemaVersion,
                Actions = model.Actions.ToArray(),
            };
            var reactionProfilesDocument = new CharacterReactionProfilesJsonDocument
            {
                SchemaVersion = CharacterActionJsonImporter.CurrentProjectionSchemaVersion,
                ReactionProfiles = model.ReactionProfiles.ToArray(),
            };
            var landingProfilesDocument = new CharacterLandingProfilesJsonDocument
            {
                SchemaVersion = CharacterActionJsonImporter.CurrentProjectionSchemaVersion,
                LandingProfiles = model.LandingProfiles.ToArray(),
            };

            actionSetsJson = JsonConvert.SerializeObject(actionSetsDocument, JsonSettings);
            foreach (var action in model.Actions) CharacterActionControlScaleAuthoring.Prepare(action);
            actionsJson = CharacterActionControlScaleAuthoring.Publish(JsonConvert.SerializeObject(actionsDocument, JsonSettings));
            reactionProfilesJson = JsonConvert.SerializeObject(reactionProfilesDocument, JsonSettings);
            landingProfilesJson = JsonConvert.SerializeObject(landingProfilesDocument, JsonSettings);
        }

        public static CharacterActionAuthoringExportResult ValidateJson(string actionSetsJson, string actionsJson)
        {
            return ValidateJson(
                actionSetsJson,
                actionsJson,
                ResolveDefaultReactionProfilesJson(null),
                CharacterActionAuthoringReferenceContext.CreateDefault(),
                buildWorkstationSnapshots: true);
        }

        public static CharacterActionAuthoringExportResult ValidateJson(
            string actionSetsJson,
            string actionsJson,
            CharacterActionAuthoringReferenceContext referenceContext)
        {
            return ValidateJson(
                actionSetsJson,
                actionsJson,
                ResolveDefaultReactionProfilesJson(null),
                referenceContext,
                buildWorkstationSnapshots: true);
        }

        public static CharacterActionAuthoringExportResult ValidateJson(
            string actionSetsJson,
            string actionsJson,
            CharacterActionAuthoringReferenceContext referenceContext,
            bool buildWorkstationSnapshots)
        {
            return ValidateJson(
                actionSetsJson,
                actionsJson,
                ResolveDefaultReactionProfilesJson(null),
                referenceContext,
                buildWorkstationSnapshots);
        }

        public static CharacterActionAuthoringExportResult ValidateJson(
            string actionSetsJson,
            string actionsJson,
            string reactionProfilesJson,
            CharacterActionAuthoringReferenceContext referenceContext,
            bool buildWorkstationSnapshots)
        {
            return ValidateJson(
                actionSetsJson,
                actionsJson,
                reactionProfilesJson,
                CreateEmptyLandingProfilesJson(),
                referenceContext,
                buildWorkstationSnapshots);
        }

        public static CharacterActionAuthoringExportResult ValidateJson(
            string actionSetsJson,
            string actionsJson,
            string reactionProfilesJson,
            string landingProfilesJson,
            CharacterActionAuthoringReferenceContext referenceContext,
            bool buildWorkstationSnapshots)
        {
            CharacterActionJsonImportResult import = CharacterActionJsonImporter.Import(
                actionSetsJson,
                actionsJson,
                ResolveDefaultReactionProfilesJson(reactionProfilesJson),
                ResolveDefaultLandingProfilesJson(landingProfilesJson),
                CharacterActionJsonImporter.DefaultActionSetsSourcePath,
                CharacterActionJsonImporter.DefaultActionsSourcePath,
                CharacterActionJsonImporter.DefaultReactionProfilesSourcePath,
                CharacterActionJsonImporter.DefaultLandingProfilesSourcePath);
            return ValidateImportedJson(
                actionSetsJson,
                actionsJson,
                ResolveDefaultReactionProfilesJson(reactionProfilesJson),
                import,
                referenceContext,
                buildWorkstationSnapshots,
                landingProfilesJson);
        }

        private static CharacterActionAuthoringExportResult ValidateImportedJson(
            string actionSetsJson,
            string actionsJson,
            string reactionProfilesJson,
            CharacterActionJsonImportResult import,
            CharacterActionAuthoringReferenceContext referenceContext,
            bool buildWorkstationSnapshots,
            string landingProfilesJson = null)
        {
            var registry = new CharacterActionConfigRegistry(
                import.ActionSets,
                import.Actions,
                CharacterActionJsonImporter.DefaultActionSetsSourcePath,
                CharacterActionJsonImporter.DefaultActionsSourcePath,
                import.ReactionProfiles,
                CharacterActionJsonImporter.DefaultReactionProfilesSourcePath,
                import.LandingProfiles,
                CharacterActionJsonImporter.DefaultLandingProfilesSourcePath);
            CharacterActionValidationReport localReport = CharacterActionValidationReport.Validate(registry);
            localReport = MergeLocalValidationDiagnostics(
                localReport,
                CharacterActionAuthoringLocalValidationRegistry.Validate(
                    new CharacterActionAuthoringLocalValidationContext(
                        null,
                        actionSetsJson,
                        actionsJson)));
            CharacterActionValidationOptions options = (referenceContext ?? CharacterActionAuthoringReferenceContext.CreateDefault()).CreateValidationOptions();
            CharacterActionValidationReport report = CharacterActionValidationReport.Validate(registry, options);
            CharacterActionWorkstationSnapshot[] snapshots = buildWorkstationSnapshots
                ? BuildSnapshots(import.Actions)
                : Array.Empty<CharacterActionWorkstationSnapshot>();
            string diagnosticsText = FormatDiagnostics(import, localReport, report);
            return new CharacterActionAuthoringExportResult(
                !HasErrors(import, report),
                actionSetsJson,
                actionsJson,
                reactionProfilesJson,
                import,
                registry,
                localReport,
                report,
                snapshots,
                diagnosticsText,
                JsonConvert.SerializeObject(new CharacterLandingProfilesJsonDocument
                {
                    SchemaVersion = CharacterActionJsonImporter.CurrentProjectionSchemaVersion,
                    LandingProfiles = import.LandingProfiles == null
                        ? Array.Empty<CharacterLandingProfileJsonDto>()
                        : JsonConvert.DeserializeObject<CharacterLandingProfilesJsonDocument>(
                            ResolveDefaultLandingProfilesJson(landingProfilesJson), JsonSettings)?.LandingProfiles
                            ?? Array.Empty<CharacterLandingProfileJsonDto>(),
                }, JsonSettings));
        }

        public static string FormatDiagnostics(
            CharacterActionJsonImportResult import,
            CharacterActionValidationReport report)
        {
            return FormatDiagnostics(import, null, report);
        }

        public static string FormatDiagnostics(
            CharacterActionJsonImportResult import,
            CharacterActionValidationReport localReport,
            CharacterActionValidationReport report)
        {
            var builder = new StringBuilder();
            CharacterActionJsonDiagnostic[] importDiagnostics = import?.Diagnostics ?? Array.Empty<CharacterActionJsonDiagnostic>();
            for (int i = 0; i < importDiagnostics.Length; i++)
            {
                CharacterActionJsonDiagnostic diagnostic = importDiagnostics[i];
                builder.Append("[Import] ")
                    .Append(diagnostic.Severity)
                    .Append(' ')
                    .Append(diagnostic.Code)
                    .Append(' ')
                    .Append(diagnostic.SourcePath)
                    .Append(' ')
                    .Append(diagnostic.JsonPath)
                    .Append(" - ")
                    .Append(diagnostic.Message)
                    .AppendLine();
            }

            CharacterActionValidationDiagnostic[] localDiagnostics =
                localReport?.Diagnostics ?? Array.Empty<CharacterActionValidationDiagnostic>();
            for (int i = 0; i < localDiagnostics.Length; i++)
            {
                CharacterActionValidationDiagnostic diagnostic = localDiagnostics[i];
                builder.Append("[Local Validation] ")
                    .Append(diagnostic.Severity)
                    .Append(' ')
                    .Append(diagnostic.Code)
                    .Append(' ')
                    .Append(diagnostic.SourcePath)
                    .Append(' ')
                    .Append(diagnostic.StablePath)
                    .Append(" - ")
                    .Append(FormatLocalValidationMessage(diagnostic))
                    .AppendLine();
            }

            CharacterActionValidationDiagnostic[] validationDiagnostics =
                report?.Diagnostics ?? Array.Empty<CharacterActionValidationDiagnostic>();
            for (int i = 0; i < validationDiagnostics.Length; i++)
            {
                CharacterActionValidationDiagnostic diagnostic = validationDiagnostics[i];
                if (ContainsDiagnostic(localDiagnostics, diagnostic))
                    continue;

                builder.Append("[Cross-module Reference] ")
                    .Append(diagnostic.Severity)
                    .Append(' ')
                    .Append(diagnostic.Code)
                    .Append(' ')
                    .Append(diagnostic.SourcePath)
                    .Append(' ')
                    .Append(diagnostic.StablePath)
                    .Append(" - ")
                    .Append(diagnostic.Message)
                    .AppendLine();
            }

            if (builder.Length == 0)
                builder.Append("No Character Action import, local validation, or cross-module reference diagnostics.");

            return builder.ToString();
        }

        private static string FormatLocalValidationMessage(CharacterActionValidationDiagnostic diagnostic)
        {
            if (string.Equals(diagnostic.Code, CharacterActionDiagnosticCodes.MissingReactionProfile, StringComparison.Ordinal))
            {
                return diagnostic.Message
                    + " Load or regenerate character_reaction_profiles.json together with character_action_sets.json; legacy reaction.default bindings should be migrated to reaction.<package>.default.";
            }

            return diagnostic.Message;
        }

        private static bool ContainsDiagnostic(CharacterActionValidationDiagnostic[] diagnostics, CharacterActionValidationDiagnostic diagnostic)
        {
            for (int i = 0; i < diagnostics.Length; i++)
            {
                if (diagnostics[i].Equals(diagnostic))
                    return true;
            }

            return false;
        }

        private static CharacterActionValidationReport MergeLocalValidationDiagnostics(
            CharacterActionValidationReport localReport,
            CharacterActionValidationDiagnostic[] additionalDiagnostics)
        {
            additionalDiagnostics = additionalDiagnostics ?? Array.Empty<CharacterActionValidationDiagnostic>();
            if (additionalDiagnostics.Length == 0)
                return localReport;

            CharacterActionValidationDiagnostic[] localDiagnostics =
                localReport?.Diagnostics ?? Array.Empty<CharacterActionValidationDiagnostic>();
            var merged = new List<CharacterActionValidationDiagnostic>(
                localDiagnostics.Length + additionalDiagnostics.Length);
            merged.AddRange(localDiagnostics);
            for (int i = 0; i < additionalDiagnostics.Length; i++)
            {
                CharacterActionValidationDiagnostic diagnostic = additionalDiagnostics[i];
                if (!ContainsDiagnostic(localDiagnostics, diagnostic))
                    merged.Add(diagnostic);
            }

            return new CharacterActionValidationReport(merged.ToArray());
        }

        private static bool HasErrors(
            CharacterActionJsonImportResult import,
            CharacterActionValidationReport report)
        {
            return import == null || !import.Success || report == null || report.HasErrors;
        }

        private static CharacterActionWorkstationSnapshot[] BuildSnapshots(CharacterActionConfig[] actions)
        {
            actions = actions ?? Array.Empty<CharacterActionConfig>();
            var snapshots = new List<CharacterActionWorkstationSnapshot>(actions.Length);
            for (int i = 0; i < actions.Length; i++)
                snapshots.Add(CharacterActionWorkstation.BuildSnapshot(actions[i]));
            return snapshots.ToArray();
        }

        private static string CreateEmptyActionSetsJson()
        {
            var document = new CharacterActionSetsJsonDocument
            {
                SchemaVersion = CharacterActionJsonImporter.CurrentProjectionSchemaVersion,
                ActionSets = Array.Empty<CharacterActionSetJsonDto>(),
            };
            return JsonConvert.SerializeObject(document, JsonSettings);
        }

        private static string CreateEmptyActionsJson()
        {
            var document = new CharacterActionsJsonDocument
            {
                SchemaVersion = CharacterActionJsonImporter.CurrentActionLibrarySchemaVersion,
                Actions = Array.Empty<CharacterActionJsonDto>(),
            };
            return JsonConvert.SerializeObject(document, JsonSettings);
        }

        public static string CreateEmptyReactionProfilesJson()
        {
            var document = new CharacterReactionProfilesJsonDocument
            {
                SchemaVersion = CharacterActionJsonImporter.CurrentProjectionSchemaVersion,
                ReactionProfiles = Array.Empty<CharacterReactionProfileJsonDto>(),
            };
            return JsonConvert.SerializeObject(document, JsonSettings);
        }

        public static string CreateEmptyLandingProfilesJson()
        {
            return JsonConvert.SerializeObject(
                new CharacterLandingProfilesJsonDocument
                {
                    SchemaVersion = CharacterActionJsonImporter.CurrentProjectionSchemaVersion,
                    LandingProfiles = Array.Empty<CharacterLandingProfileJsonDto>(),
                },
                JsonSettings);
        }

        private static string ResolveDefaultLandingProfilesJson(string landingProfilesJson)
        {
            return string.IsNullOrWhiteSpace(landingProfilesJson)
                ? CreateEmptyLandingProfilesJson()
                : landingProfilesJson;
        }

        private static string ResolveDefaultReactionProfilesJson(string reactionProfilesJson)
        {
            if (!string.IsNullOrWhiteSpace(reactionProfilesJson))
                return reactionProfilesJson;

            var document = new CharacterReactionProfilesJsonDocument
            {
                SchemaVersion = CharacterActionJsonImporter.CurrentProjectionSchemaVersion,
                ReactionProfiles = new[]
                {
                    CharacterActionAuthoringModel.CreateDefaultReactionProfile(),
                },
            };
            return JsonConvert.SerializeObject(document, JsonSettings);
        }
    }

    public sealed class CharacterActionAuthoringLocalValidationContext
    {
        public CharacterActionAuthoringLocalValidationContext(
            CharacterActionAuthoringModel model,
            string actionSetsJson,
            string actionsJson)
        {
            Model = model;
            ActionSetsJson = actionSetsJson ?? string.Empty;
            ActionsJson = actionsJson ?? string.Empty;
        }

        public CharacterActionAuthoringModel Model { get; }
        public string ActionSetsJson { get; }
        public string ActionsJson { get; }
    }

    public interface ICharacterActionAuthoringLocalValidationProvider
    {
        /// <remarks>AllocByDesign: editor-only validation extension point.</remarks>
        CharacterActionValidationDiagnostic[] Validate(CharacterActionAuthoringLocalValidationContext context);
    }

    public static class CharacterActionAuthoringLocalValidationRegistry
    {
        private static readonly List<ICharacterActionAuthoringLocalValidationProvider> Providers =
            new List<ICharacterActionAuthoringLocalValidationProvider>();

        /// <remarks>LowFreqAlloc: editor provider registration during domain load.</remarks>
        public static void Register(ICharacterActionAuthoringLocalValidationProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            Type providerType = provider.GetType();
            for (int i = 0; i < Providers.Count; i++)
            {
                ICharacterActionAuthoringLocalValidationProvider registered = Providers[i];
                if (registered != null && registered.GetType() == providerType)
                    return;
            }

            Providers.Add(provider);
        }

        /// <remarks>AllocByDesign: editor-only validation gathers provider diagnostics.</remarks>
        public static CharacterActionValidationDiagnostic[] Validate(CharacterActionAuthoringLocalValidationContext context)
        {
            if (Providers.Count == 0)
                return Array.Empty<CharacterActionValidationDiagnostic>();

            var diagnostics = new List<CharacterActionValidationDiagnostic>();
            for (int i = 0; i < Providers.Count; i++)
            {
                ICharacterActionAuthoringLocalValidationProvider provider = Providers[i];
                if (provider == null)
                    continue;

                try
                {
                    CharacterActionValidationDiagnostic[] providerDiagnostics = provider.Validate(context)
                        ?? Array.Empty<CharacterActionValidationDiagnostic>();
                    diagnostics.AddRange(providerDiagnostics);
                }
                catch (Exception ex)
                {
                    diagnostics.Add(new CharacterActionValidationDiagnostic(
                        "CharacterActionAuthoring",
                        "$.localValidationProviders",
                        "CA_AUTHORING_LOCAL_VALIDATOR_FAILED",
                        CharacterActionDiagnosticSeverity.Warning,
                        provider.GetType().Name + " failed: " + ex.Message));
                }
            }

            return diagnostics.ToArray();
        }
    }
}
