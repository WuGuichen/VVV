using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace MxFramework.Combat.Authoring
{
    public static class CombatAuthoringJsonExporter
    {
        public const string DefaultToolVersion = "M10G";
        public const string ManifestPath = "manifest.json";
        public const string ValidationReportTextPath = "reports/validation_report.txt";
        public const string ValidationReportJsonPath = "reports/validation_report.json";
        public const string SchemaPath = "schema/combat_authoring.schema.json";

        public static CombatAuthoringExportResult Export(
            CombatActionAuthoringAsset action,
            CombatSceneBindingAsset binding,
            string packageId,
            string sourceAssetGuid,
            string toolVersion)
        {
            string normalizedPackageId = NormalizePackageId(packageId, action, binding);
            string normalizedToolVersion = string.IsNullOrWhiteSpace(toolVersion) ? DefaultToolVersion : toolVersion;
            CombatAuthoringReport validationReport = CombatAuthoringValidator.Validate(action, binding);
            string authoringJson = BuildAuthoringHashJson(action, binding);
            string runtimeDataJson = BuildRuntimeDataJson(action, binding);
            string authoringHash = ComputeHash(authoringJson);
            string runtimeDataHash = ComputeHash(runtimeDataJson);

            var failedContext = new CombatAuthoringExportContext(
                normalizedPackageId,
                sourceAssetGuid,
                authoringHash,
                runtimeDataHash,
                jsonPackageHash: string.Empty,
                normalizedToolVersion);

            if (validationReport.HasErrors)
            {
                CombatAuthoringManifest failedManifest = CombatAuthoringManifest.CreateDraft(failedContext);
                string failedReport = BuildReportText(
                    false,
                    "Validation gate failed. JSON package was not generated.",
                    validationReport,
                    failedManifest,
                    failedContext,
                    Array.Empty<CombatAuthoringExportFile>());

                return new CombatAuthoringExportResult(
                    false,
                    validationReport,
                    failedManifest,
                    failedContext,
                    new CombatAuthoringExportPackage(null),
                    failedReport);
            }

            string actionPath = GetActionPath(action);
            string bindingPath = GetSceneBindingPath(binding);
            string actionJson = BuildActionJson(action);
            string bindingJson = BuildSceneBindingJson(binding);
            string schemaJson = BuildSchemaJson();
            var contentFiles = new[]
            {
                CreateFile(SchemaPath, schemaJson),
                CreateFile(actionPath, actionJson),
                CreateFile(bindingPath, bindingJson),
            };
            string jsonPackageHash = ComputePackageHash(contentFiles);
            var context = new CombatAuthoringExportContext(
                normalizedPackageId,
                sourceAssetGuid,
                authoringHash,
                runtimeDataHash,
                jsonPackageHash,
                normalizedToolVersion);
            var manifest = new CombatAuthoringManifest(
                normalizedPackageId,
                version: "0.1.0",
                schema: CombatAuthoringJsonSchema.FileName,
                schemaVersion: CombatActionAuthoringAsset.CurrentSchemaVersion,
                createdAt: DateTime.UtcNow.ToString("O"),
                toolVersion: normalizedToolVersion,
                sourceAssetGuid: sourceAssetGuid,
                contentHash: jsonPackageHash);

            string manifestJson = BuildManifestJson(manifest);
            string reportJson = BuildValidationReportJson(true, validationReport, manifest, context, contentFiles);
            var reportListedFiles = new[]
            {
                CreateFile(ManifestPath, manifestJson),
                contentFiles[0],
                contentFiles[1],
                contentFiles[2],
                CreateFile(ValidationReportJsonPath, reportJson),
            };
            string reportText = BuildReportText(
                true,
                "Validation gate passed. JSON package generated.",
                validationReport,
                manifest,
                context,
                reportListedFiles);

            var files = new[]
            {
                reportListedFiles[0],
                contentFiles[0],
                contentFiles[1],
                contentFiles[2],
                CreateFile(ValidationReportTextPath, reportText),
                reportListedFiles[4],
            };

            return new CombatAuthoringExportResult(
                true,
                validationReport,
                manifest,
                context,
                new CombatAuthoringExportPackage(files),
                reportText);
        }

        public static string BuildValidationReportText(CombatAuthoringExportResult result)
        {
            return result == null ? "Combat Authoring Export Report\nNo export result." : result.ReportText;
        }

        private static CombatAuthoringExportFile CreateFile(string path, string content)
        {
            return new CombatAuthoringExportFile(path, content, ComputeHash(content));
        }

        private static string BuildAuthoringHashJson(CombatActionAuthoringAsset action, CombatSceneBindingAsset binding)
        {
            var builder = new StringBuilder(4096);
            builder.Append('{');
            AppendProperty(builder, "schema", "combat_authoring_hash_v0", false);
            builder.Append(',');
            AppendRawProperty(builder, "action", action == null ? "null" : BuildActionJson(action));
            builder.Append(',');
            AppendRawProperty(builder, "sceneBinding", binding == null ? "null" : BuildSceneBindingJson(binding));
            builder.Append('}');
            return builder.ToString();
        }

        private static string BuildRuntimeDataJson(CombatActionAuthoringAsset action, CombatSceneBindingAsset binding)
        {
            var builder = new StringBuilder(4096);
            builder.Append('{');
            AppendProperty(builder, "schema", "combat_runtime_data_draft_v0", false);
            builder.Append(',');
            AppendRawProperty(builder, "action", action == null ? "null" : BuildActionJson(action));
            builder.Append(',');
            AppendRawProperty(builder, "sceneBinding", binding == null ? "null" : BuildSceneBindingJson(binding));
            builder.Append('}');
            return builder.ToString();
        }

        private static string BuildManifestJson(CombatAuthoringManifest manifest)
        {
            var builder = new StringBuilder(512);
            builder.Append('{');
            AppendProperty(builder, "packageId", manifest.PackageId, false);
            AppendProperty(builder, "version", manifest.Version);
            AppendProperty(builder, "schema", manifest.Schema);
            AppendProperty(builder, "schemaVersion", manifest.SchemaVersion);
            AppendProperty(builder, "createdAt", manifest.CreatedAt);
            AppendProperty(builder, "toolVersion", manifest.ToolVersion);
            AppendProperty(builder, "sourceAssetGuid", manifest.SourceAssetGuid);
            AppendProperty(builder, "contentHash", manifest.ContentHash);
            builder.Append('}');
            return builder.ToString();
        }

        private static string BuildActionJson(CombatActionAuthoringAsset action)
        {
            var builder = new StringBuilder(4096);
            builder.Append('{');
            AppendProperty(builder, "schema", "combat_authoring_action", false);
            AppendProperty(builder, "schemaVersion", SafeSchemaVersion(action == null ? null : action.SchemaVersion));
            AppendProperty(builder, "actionId", action == null ? 0 : action.ActionId);
            AppendProperty(builder, "totalFrames", action == null ? 0 : action.TotalFrames);
            builder.Append(',');
            AppendQuoted(builder, "phases");
            builder.Append(':');
            AppendPhases(builder, action);
            builder.Append(',');
            AppendQuoted(builder, "hitboxes");
            builder.Append(':');
            AppendShapes(builder, action == null ? null : action.Hitboxes);
            builder.Append(',');
            AppendQuoted(builder, "hurtboxes");
            builder.Append(':');
            AppendShapes(builder, action == null ? null : action.Hurtboxes);
            builder.Append(',');
            AppendQuoted(builder, "weaponTraces");
            builder.Append(':');
            AppendWeaponTraces(builder, action == null ? null : action.WeaponTraces);
            builder.Append('}');
            return builder.ToString();
        }

        private static string BuildSceneBindingJson(CombatSceneBindingAsset binding)
        {
            var builder = new StringBuilder(4096);
            builder.Append('{');
            AppendProperty(builder, "schema", "combat_authoring_scene_binding", false);
            AppendProperty(builder, "schemaVersion", CombatActionAuthoringAsset.CurrentSchemaVersion);
            AppendProperty(builder, "sceneGuid", binding == null ? string.Empty : binding.SceneGuid);
            AppendProperty(builder, "bindingProfileId", binding == null ? string.Empty : binding.BindingProfileId);
            builder.Append(',');
            AppendQuoted(builder, "actors");
            builder.Append(':');
            AppendActors(builder, binding == null ? null : binding.Actors);
            builder.Append(',');
            AppendQuoted(builder, "markers");
            builder.Append(':');
            AppendMarkers(builder, binding == null ? null : binding.Markers);
            builder.Append('}');
            return builder.ToString();
        }

        private static string BuildSchemaJson()
        {
            var builder = new StringBuilder(2048);
            builder.Append('{');
            AppendProperty(builder, "$schema", "https://json-schema.org/draft/2020-12/schema", false);
            AppendProperty(builder, "title", "Combat Authoring Package");
            AppendProperty(builder, "schemaVersion", CombatActionAuthoringAsset.CurrentSchemaVersion);
            AppendProperty(builder, "type", "object");
            builder.Append(',');
            AppendQuoted(builder, "required");
            builder.Append(':');
            AppendStringArray(builder, new[] { "manifest", "actions", "sceneBindings", "reports" });
            builder.Append(',');
            AppendQuoted(builder, "properties");
            builder.Append(':');
            builder.Append('{');
            AppendSchemaProperty(builder, "manifest", "object", false);
            AppendSchemaProperty(builder, "actions", "array");
            AppendSchemaProperty(builder, "sceneBindings", "array");
            AppendSchemaProperty(builder, "reports", "object");
            builder.Append('}');
            builder.Append('}');
            return builder.ToString();
        }

        private static string BuildValidationReportJson(
            bool success,
            CombatAuthoringReport report,
            CombatAuthoringManifest manifest,
            CombatAuthoringExportContext context,
            CombatAuthoringExportFile[] files)
        {
            var builder = new StringBuilder(4096);
            builder.Append('{');
            AppendProperty(builder, "schema", "combat_authoring_export_report", false);
            AppendProperty(builder, "schemaVersion", CombatActionAuthoringAsset.CurrentSchemaVersion);
            AppendProperty(builder, "success", success);
            AppendProperty(builder, "packageId", context.PackageId);
            AppendProperty(builder, "authoringHash", context.AuthoringHash);
            AppendProperty(builder, "runtimeDataHash", context.RuntimeDataHash);
            AppendProperty(builder, "jsonPackageHash", context.JsonPackageHash);
            AppendProperty(builder, "contentHash", manifest.ContentHash);
            builder.Append(',');
            AppendQuoted(builder, "validation");
            builder.Append(':');
            AppendValidationJson(builder, report);
            builder.Append(',');
            AppendQuoted(builder, "files");
            builder.Append(':');
            AppendFileSummaries(builder, files);
            builder.Append('}');
            return builder.ToString();
        }

        private static string BuildReportText(
            bool success,
            string status,
            CombatAuthoringReport report,
            CombatAuthoringManifest manifest,
            CombatAuthoringExportContext context,
            CombatAuthoringExportFile[] files)
        {
            var builder = new StringBuilder(4096);
            builder.AppendLine("Combat Authoring Export Report");
            builder.AppendLine("Success: " + success);
            builder.AppendLine("Status: " + status);
            builder.AppendLine("PackageId: " + context.PackageId);
            builder.AppendLine("SchemaVersion: " + manifest.SchemaVersion);
            builder.AppendLine("ToolVersion: " + manifest.ToolVersion);
            builder.AppendLine("SourceAssetGuid: " + manifest.SourceAssetGuid);
            builder.AppendLine("AuthoringHash: " + context.AuthoringHash);
            builder.AppendLine("RuntimeDataHash: " + context.RuntimeDataHash);
            builder.AppendLine("JsonPackageHash: " + context.JsonPackageHash);
            builder.AppendLine("ContentHash: " + manifest.ContentHash);
            builder.AppendLine("IssueCount: " + (report == null ? 0 : report.IssueCount));
            builder.AppendLine("HasErrors: " + (report != null && report.HasErrors));
            builder.AppendLine();
            builder.AppendLine("Files:");
            if (files == null || files.Length == 0)
            {
                builder.AppendLine("- none");
            }
            else
            {
                CombatAuthoringExportFile[] sorted = CloneAndSort(files);
                for (int i = 0; i < sorted.Length; i++)
                {
                    builder.Append("- ");
                    builder.Append(sorted[i].Path);
                    builder.Append(" hash=");
                    builder.AppendLine(sorted[i].Hash);
                }
            }

            builder.AppendLine();
            builder.AppendLine("Validation:");
            AppendIssuesText(builder, report);
            return builder.ToString();
        }

        private static void AppendValidationJson(StringBuilder builder, CombatAuthoringReport report)
        {
            builder.Append('{');
            AppendProperty(builder, "issueCount", report == null ? 0 : report.IssueCount, false);
            AppendProperty(builder, "hasErrors", report != null && report.HasErrors);
            builder.Append(',');
            AppendQuoted(builder, "issues");
            builder.Append(':');
            builder.Append('[');
            if (report != null)
            {
                for (int i = 0; i < report.IssueCount; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }

                    AppendIssueJson(builder, report.GetIssue(i));
                }
            }

            builder.Append(']');
            builder.Append('}');
        }

        private static void AppendIssueJson(StringBuilder builder, CombatAuthoringIssue issue)
        {
            builder.Append('{');
            AppendProperty(builder, "severity", issue.Severity.ToString(), false);
            AppendProperty(builder, "sourceAsset", issue.SourceAsset);
            AppendProperty(builder, "section", issue.Section);
            AppendProperty(builder, "trackId", issue.TrackId);
            builder.Append(',');
            AppendQuoted(builder, "frameRange");
            builder.Append(':');
            AppendFrameRange(builder, issue.FrameRange);
            AppendProperty(builder, "field", issue.Field);
            AppendProperty(builder, "message", issue.Message);
            AppendProperty(builder, "suggestedFix", issue.SuggestedFix);
            AppendProperty(builder, "quickAction", issue.QuickAction.ToString());
            AppendProperty(builder, "sourceOrder", issue.SourceOrder);
            builder.Append('}');
        }

        private static void AppendIssuesText(StringBuilder builder, CombatAuthoringReport report)
        {
            if (report == null || report.IssueCount == 0)
            {
                builder.AppendLine("- none");
                return;
            }

            for (int i = 0; i < report.IssueCount; i++)
            {
                CombatAuthoringIssue issue = report.GetIssue(i);
                builder.Append(i + 1);
                builder.Append(". [");
                builder.Append(issue.Severity);
                builder.Append("] ");
                builder.Append(issue.SourceAsset);
                builder.Append(" / ");
                builder.Append(issue.Section);
                builder.Append(" / track ");
                builder.Append(issue.TrackId);
                builder.Append(" / ");
                builder.Append(issue.Field);
                builder.Append(" / frame ");
                builder.AppendLine(FormatRange(issue.FrameRange));
                builder.Append("   Message: ");
                builder.AppendLine(issue.Message);
                builder.Append("   Fix: ");
                builder.AppendLine(issue.SuggestedFix);
                builder.Append("   QuickAction: ");
                builder.AppendLine(issue.QuickAction.ToString());
            }
        }

        private static void AppendPhases(StringBuilder builder, CombatActionAuthoringAsset action)
        {
            builder.Append('{');
            AppendRawProperty(builder, "startup", action == null ? "{}" : FrameRangeToJson(action.Startup), false);
            AppendRawProperty(builder, "active", action == null ? "{}" : FrameRangeToJson(action.Active));
            AppendRawProperty(builder, "recovery", action == null ? "{}" : FrameRangeToJson(action.Recovery));
            builder.Append('}');
        }

        private static void AppendShapes(StringBuilder builder, CombatShapeAuthoringData[] shapes)
        {
            CombatShapeAuthoringData[] sorted = CloneAndSort(shapes, CompareShape);
            builder.Append('[');
            for (int i = 0; i < sorted.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                CombatShapeAuthoringData shape = sorted[i];
                builder.Append('{');
                AppendProperty(builder, "trackId", shape.TrackId, false);
                AppendProperty(builder, "shapeKind", ShapeKindToJson(shape.ShapeKind));
                builder.Append(',');
                AppendQuoted(builder, "frameRange");
                builder.Append(':');
                AppendFrameRange(builder, shape.FrameRange);
                AppendProperty(builder, "markerId", shape.MarkerId);
                builder.Append(',');
                AppendQuoted(builder, "localCenter");
                builder.Append(':');
                AppendVector3(builder, shape.LocalCenter);
                AppendProperty(builder, "radiusRaw", shape.RadiusRaw);
                AppendProperty(builder, "heightRaw", shape.HeightRaw);
                AppendProperty(builder, "sourceOrder", shape.SourceOrder);
                builder.Append('}');
            }

            builder.Append(']');
        }

        private static void AppendWeaponTraces(StringBuilder builder, CombatWeaponTraceAuthoringData[] traces)
        {
            CombatWeaponTraceAuthoringData[] sorted = CloneAndSort(traces, CompareWeaponTrace);
            builder.Append('[');
            for (int i = 0; i < sorted.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                CombatWeaponTraceAuthoringData trace = sorted[i];
                builder.Append('{');
                AppendProperty(builder, "traceId", trace.TraceId, false);
                builder.Append(',');
                AppendQuoted(builder, "frameRange");
                builder.Append(':');
                AppendFrameRange(builder, trace.FrameRange);
                AppendProperty(builder, "rootMarkerId", trace.RootMarkerId);
                AppendProperty(builder, "tipMarkerId", trace.TipMarkerId);
                AppendProperty(builder, "radiusRaw", trace.RadiusRaw);
                AppendProperty(builder, "sourceOrder", trace.SourceOrder);
                builder.Append('}');
            }

            builder.Append(']');
        }

        private static void AppendActors(StringBuilder builder, CombatActorBindingData[] actors)
        {
            CombatActorBindingData[] sorted = CloneAndSort(actors, CompareActor);
            builder.Append('[');
            for (int i = 0; i < sorted.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                CombatActorBindingData actor = sorted[i];
                builder.Append('{');
                AppendProperty(builder, "entityId", actor.EntityId, false);
                AppendProperty(builder, "displayName", actor.DisplayName);
                AppendProperty(builder, "markerId", actor.MarkerId);
                AppendProperty(builder, "bodyId", actor.BodyId);
                builder.Append(',');
                AppendQuoted(builder, "colliders");
                builder.Append(':');
                AppendColliders(builder, actor.Colliders);
                builder.Append('}');
            }

            builder.Append(']');
        }

        private static void AppendColliders(StringBuilder builder, CombatColliderBindingData[] colliders)
        {
            CombatColliderBindingData[] sorted = CloneAndSort(colliders, CompareCollider);
            builder.Append('[');
            for (int i = 0; i < sorted.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                CombatColliderBindingData collider = sorted[i];
                builder.Append('{');
                AppendProperty(builder, "colliderId", collider.ColliderId, false);
                AppendProperty(builder, "markerId", collider.MarkerId);
                AppendProperty(builder, "sourceOrder", collider.SourceOrder);
                builder.Append('}');
            }

            builder.Append(']');
        }

        private static void AppendMarkers(StringBuilder builder, CombatMarkerBindingData[] markers)
        {
            CombatMarkerBindingData[] sorted = CloneAndSort(markers, CompareMarker);
            builder.Append('[');
            for (int i = 0; i < sorted.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                CombatMarkerBindingData marker = sorted[i];
                builder.Append('{');
                AppendProperty(builder, "markerId", marker.MarkerId, false);
                AppendProperty(builder, "targetPath", marker.TargetPath);
                AppendProperty(builder, "sourceOrder", marker.SourceOrder);
                builder.Append('}');
            }

            builder.Append(']');
        }

        private static void AppendFileSummaries(StringBuilder builder, CombatAuthoringExportFile[] files)
        {
            CombatAuthoringExportFile[] sorted = CloneAndSort(files);
            builder.Append('[');
            for (int i = 0; i < sorted.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append('{');
                AppendProperty(builder, "path", sorted[i].Path, false);
                AppendProperty(builder, "hash", sorted[i].Hash);
                builder.Append('}');
            }

            builder.Append(']');
        }

        private static void AppendSchemaProperty(StringBuilder builder, string propertyName, string type, bool prependComma = true)
        {
            if (prependComma)
            {
                builder.Append(',');
            }

            AppendQuoted(builder, propertyName);
            builder.Append(':');
            builder.Append('{');
            AppendProperty(builder, "type", type, false);
            builder.Append('}');
        }

        private static string FrameRangeToJson(CombatAuthoringFrameRange range)
        {
            var builder = new StringBuilder(64);
            AppendFrameRange(builder, range);
            return builder.ToString();
        }

        private static void AppendFrameRange(StringBuilder builder, CombatAuthoringFrameRange range)
        {
            builder.Append('{');
            AppendProperty(builder, "startFrame", range.StartFrame, false);
            AppendProperty(builder, "endFrame", range.EndFrame);
            AppendProperty(builder, "isEmpty", range.IsEmpty);
            builder.Append('}');
        }

        private static void AppendVector3(StringBuilder builder, Vector3 value)
        {
            builder.Append('{');
            AppendProperty(builder, "x", value.x, false);
            AppendProperty(builder, "y", value.y);
            AppendProperty(builder, "z", value.z);
            builder.Append('}');
        }

        private static void AppendStringArray(StringBuilder builder, string[] values)
        {
            builder.Append('[');
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                AppendQuoted(builder, values[i]);
            }

            builder.Append(']');
        }

        private static void AppendProperty(StringBuilder builder, string name, string value, bool prependComma = true)
        {
            if (prependComma)
            {
                builder.Append(',');
            }

            AppendQuoted(builder, name);
            builder.Append(':');
            AppendQuoted(builder, value ?? string.Empty);
        }

        private static void AppendProperty(StringBuilder builder, string name, int value, bool prependComma = true)
        {
            if (prependComma)
            {
                builder.Append(',');
            }

            AppendQuoted(builder, name);
            builder.Append(':');
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendProperty(StringBuilder builder, string name, float value, bool prependComma = true)
        {
            if (prependComma)
            {
                builder.Append(',');
            }

            AppendQuoted(builder, name);
            builder.Append(':');
            builder.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void AppendProperty(StringBuilder builder, string name, bool value, bool prependComma = true)
        {
            if (prependComma)
            {
                builder.Append(',');
            }

            AppendQuoted(builder, name);
            builder.Append(':');
            builder.Append(value ? "true" : "false");
        }

        private static void AppendRawProperty(StringBuilder builder, string name, string rawJson, bool prependComma = true)
        {
            if (prependComma)
            {
                builder.Append(',');
            }

            AppendQuoted(builder, name);
            builder.Append(':');
            builder.Append(rawJson ?? "null");
        }

        private static void AppendQuoted(StringBuilder builder, string value)
        {
            builder.Append('"');
            string text = value ?? string.Empty;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                switch (c)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (c < 32)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(c);
                        }

                        break;
                }
            }

            builder.Append('"');
        }

        private static string ComputePackageHash(CombatAuthoringExportFile[] files)
        {
            CombatAuthoringExportFile[] sorted = CloneAndSort(files);
            var builder = new StringBuilder(4096);
            for (int i = 0; i < sorted.Length; i++)
            {
                builder.Append(sorted[i].Path);
                builder.Append('\n');
                builder.Append(sorted[i].Content);
                builder.Append('\n');
            }

            return ComputeHash(builder.ToString());
        }

        private static string ComputeHash(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            SHA256 sha = SHA256.Create();
            try
            {
                byte[] hash = sha.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
            finally
            {
                sha.Dispose();
            }
        }

        private static string NormalizePackageId(string packageId, CombatActionAuthoringAsset action, CombatSceneBindingAsset binding)
        {
            if (!string.IsNullOrWhiteSpace(packageId))
            {
                return SanitizeFileSegment(packageId);
            }

            string baseId = action == null ? "combat_authoring_package" : "combat_action_" + action.ActionId.ToString(CultureInfo.InvariantCulture);
            if (binding != null && !string.IsNullOrWhiteSpace(binding.BindingProfileId))
            {
                baseId += "_" + binding.BindingProfileId;
            }

            return SanitizeFileSegment(baseId);
        }

        private static string GetActionPath(CombatActionAuthoringAsset action)
        {
            int actionId = action == null ? 0 : action.ActionId;
            return CombatAuthoringJsonSchema.ActionsDirectory + "/action_" + actionId.ToString(CultureInfo.InvariantCulture) + ".json";
        }

        private static string GetSceneBindingPath(CombatSceneBindingAsset binding)
        {
            string segment = "scene_binding";
            if (binding != null)
            {
                if (!string.IsNullOrWhiteSpace(binding.BindingProfileId))
                {
                    segment = binding.BindingProfileId;
                }
                else if (!string.IsNullOrWhiteSpace(binding.SceneGuid))
                {
                    segment = binding.SceneGuid;
                }
                else if (!string.IsNullOrWhiteSpace(binding.name))
                {
                    segment = binding.name;
                }
            }

            return CombatAuthoringJsonSchema.SceneBindingsDirectory + "/" + SanitizeFileSegment(segment) + ".json";
        }

        private static string SanitizeFileSegment(string value)
        {
            string text = string.IsNullOrWhiteSpace(value) ? "unnamed" : value.Trim();
            var builder = new StringBuilder(text.Length);
            bool previousUnderscore = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = char.ToLowerInvariant(text[i]);
                bool valid = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                if (valid)
                {
                    builder.Append(c);
                    previousUnderscore = false;
                }
                else if (!previousUnderscore)
                {
                    builder.Append('_');
                    previousUnderscore = true;
                }
            }

            string sanitized = builder.ToString().Trim('_');
            return string.IsNullOrEmpty(sanitized) ? "unnamed" : sanitized;
        }

        private static string SafeSchemaVersion(string schemaVersion)
        {
            return string.IsNullOrWhiteSpace(schemaVersion)
                ? CombatActionAuthoringAsset.CurrentSchemaVersion
                : schemaVersion;
        }

        private static string ShapeKindToJson(CombatAuthoringShapeKind shapeKind)
        {
            switch (shapeKind)
            {
                case CombatAuthoringShapeKind.Capsule:
                    return "capsule";
                case CombatAuthoringShapeKind.Aabb:
                    return "aabb";
                case CombatAuthoringShapeKind.Sector:
                    return "sector";
                default:
                    return "sphere";
            }
        }

        private static string FormatRange(CombatAuthoringFrameRange range)
        {
            return range.IsEmpty ? "empty" : range.StartFrame + "-" + range.EndFrame;
        }

        private static CombatAuthoringExportFile[] CloneAndSort(CombatAuthoringExportFile[] files)
        {
            if (files == null || files.Length == 0)
            {
                return Array.Empty<CombatAuthoringExportFile>();
            }

            CombatAuthoringExportFile[] sorted = (CombatAuthoringExportFile[])files.Clone();
            Array.Sort(sorted);
            return sorted;
        }

        private static T[] CloneAndSort<T>(T[] values, Comparison<T> comparison)
        {
            if (values == null || values.Length == 0)
            {
                return Array.Empty<T>();
            }

            T[] sorted = (T[])values.Clone();
            Array.Sort(sorted, comparison);
            return sorted;
        }

        private static int CompareShape(CombatShapeAuthoringData left, CombatShapeAuthoringData right)
        {
            int compare = left.TrackId.CompareTo(right.TrackId);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.FrameRange.StartFrame.CompareTo(right.FrameRange.StartFrame);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.FrameRange.EndFrame.CompareTo(right.FrameRange.EndFrame);
            return compare != 0 ? compare : left.SourceOrder.CompareTo(right.SourceOrder);
        }

        private static int CompareWeaponTrace(CombatWeaponTraceAuthoringData left, CombatWeaponTraceAuthoringData right)
        {
            int compare = left.TraceId.CompareTo(right.TraceId);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.FrameRange.StartFrame.CompareTo(right.FrameRange.StartFrame);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.FrameRange.EndFrame.CompareTo(right.FrameRange.EndFrame);
            return compare != 0 ? compare : left.SourceOrder.CompareTo(right.SourceOrder);
        }

        private static int CompareActor(CombatActorBindingData left, CombatActorBindingData right)
        {
            int compare = left.EntityId.CompareTo(right.EntityId);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.BodyId.CompareTo(right.BodyId);
            return compare != 0 ? compare : string.CompareOrdinal(left.MarkerId, right.MarkerId);
        }

        private static int CompareCollider(CombatColliderBindingData left, CombatColliderBindingData right)
        {
            int compare = left.ColliderId.CompareTo(right.ColliderId);
            return compare != 0 ? compare : left.SourceOrder.CompareTo(right.SourceOrder);
        }

        private static int CompareMarker(CombatMarkerBindingData left, CombatMarkerBindingData right)
        {
            int compare = string.CompareOrdinal(left.MarkerId, right.MarkerId);
            return compare != 0 ? compare : left.SourceOrder.CompareTo(right.SourceOrder);
        }
    }
}
