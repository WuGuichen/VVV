using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestWorldObjectDefinitionCatalogSourceKind
    {
        BuiltInFallback = 0,
        Generated = 1,
        PlayerMirror = 2
    }

    public sealed class CharacterTestWorldObjectDefinitionCatalogLoadResult
    {
        internal CharacterTestWorldObjectDefinitionCatalogLoadResult(
            CharacterTestWorldObjectDefinitionCatalog catalog,
            CharacterTestWorldObjectPresentationCatalog presentations,
            string sourceHash,
            string contentHash,
            CharacterTestWorldObjectDefinitionCatalogSourceKind sourceKind,
            string sourcePath,
            IReadOnlyList<CharacterTestWorldObjectDefinitionValidationIssue> diagnostics)
        {
            Catalog = catalog;
            Presentations = presentations;
            SourceHash = sourceHash ?? string.Empty;
            ContentHash = contentHash ?? string.Empty;
            SourceKind = sourceKind;
            SourcePath = sourcePath ?? string.Empty;
            Diagnostics = diagnostics ?? Array.Empty<CharacterTestWorldObjectDefinitionValidationIssue>();
        }

        public CharacterTestWorldObjectDefinitionCatalog Catalog { get; }
        public CharacterTestWorldObjectPresentationCatalog Presentations { get; }
        public string SourceHash { get; }
        public string ContentHash { get; }
        public CharacterTestWorldObjectDefinitionCatalogSourceKind SourceKind { get; }
        public string SourcePath { get; }
        public IReadOnlyList<CharacterTestWorldObjectDefinitionValidationIssue> Diagnostics { get; }
        public bool UsedFallback => SourceKind == CharacterTestWorldObjectDefinitionCatalogSourceKind.BuiltInFallback;
    }

    public static class CharacterTestWorldObjectDefinitionCatalogLoader
    {
        public const string DefaultAuthoringPath =
            "Assets/Config/MxFramework/Levels/CharacterTest/world_object_definitions.json";
        public const string DefaultGeneratedRuntimePath =
            "Assets/MxFrameworkGenerated/Levels/CharacterTest/world_object_definitions.runtime.json";
        public const string DefaultPlayerMirrorRelativePath =
            "MxFramework/CharacterTest/RuntimeCatalogs/Levels/world_object_definitions.runtime.json";

        /// <remarks>LowFreqAlloc: reads and validates catalogs once during CharacterTest bootstrap.</remarks>
        public static CharacterTestWorldObjectDefinitionCatalogLoadResult LoadDefaultOrBuiltIn()
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string generated = Resolve(projectRoot, DefaultGeneratedRuntimePath);
            string playerMirror = Resolve(Application.streamingAssetsPath, DefaultPlayerMirrorRelativePath);
            string authoringPath = Resolve(projectRoot, DefaultAuthoringPath);
            bool authoringExists = File.Exists(authoringPath);
            bool authoringValid = TryReadExpectedAuthoringHash(
                authoringPath,
                out string expectedSourceHash,
                out CharacterTestWorldObjectDefinitionValidationReport authoringReport);
            if (Application.isEditor)
            {
                if (authoringExists && !authoringValid)
                {
                    CharacterTestWorldObjectDefinitionCatalogLoadResult fallback =
                        LoadOrBuiltIn(
                            Array.Empty<string>(),
                            Array.Empty<CharacterTestWorldObjectDefinitionCatalogSourceKind>());
                    var diagnostics =
                        new List<CharacterTestWorldObjectDefinitionValidationIssue>(fallback.Diagnostics);
                    diagnostics.Insert(0, new CharacterTestWorldObjectDefinitionValidationIssue(
                        "WOD_AUTHORING_INVALID",
                        authoringPath,
                        "Current Authoring JSON is invalid; generated catalogs were not accepted."));
                    CopyIssues(authoringReport, diagnostics);
                    return new CharacterTestWorldObjectDefinitionCatalogLoadResult(
                        fallback.Catalog,
                        fallback.Presentations,
                        fallback.SourceHash,
                        fallback.ContentHash,
                        fallback.SourceKind,
                        fallback.SourcePath,
                        diagnostics);
                }

                return LoadOrBuiltIn(
                    new[] { generated, playerMirror },
                    new[]
                    {
                        CharacterTestWorldObjectDefinitionCatalogSourceKind.Generated,
                        CharacterTestWorldObjectDefinitionCatalogSourceKind.PlayerMirror
                    },
                    expectedSourceHash);
            }

            return LoadOrBuiltIn(
                new[] { playerMirror, generated },
                new[]
                {
                    CharacterTestWorldObjectDefinitionCatalogSourceKind.PlayerMirror,
                    CharacterTestWorldObjectDefinitionCatalogSourceKind.Generated
                },
                string.Empty);
        }

        /// <remarks>AllocByDesign: explicit startup/test loader with fallback diagnostics.</remarks>
        public static CharacterTestWorldObjectDefinitionCatalogLoadResult LoadOrBuiltIn(
            IReadOnlyList<string> candidatePaths,
            IReadOnlyList<CharacterTestWorldObjectDefinitionCatalogSourceKind> sourceKinds,
            string expectedSourceHash = "")
        {
            var diagnostics = new List<CharacterTestWorldObjectDefinitionValidationIssue>();
            int count = Math.Min(candidatePaths?.Count ?? 0, sourceKinds?.Count ?? 0);
            for (int i = 0; i < count; i++)
            {
                string path = candidatePaths[i] ?? string.Empty;
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    diagnostics.Add(new CharacterTestWorldObjectDefinitionValidationIssue(
                        "WOD_RUNTIME_FILE_MISSING",
                        path,
                        "Runtime catalog candidate does not exist."));
                    continue;
                }

                if (!CharacterTestWorldObjectDefinitionJson.TryLoadRuntime(
                        File.ReadAllText(path),
                        out CharacterTestWorldObjectDefinitionRuntimeDocument runtime,
                        out CharacterTestWorldObjectDefinitionValidationReport validation))
                {
                    CopyIssues(validation, diagnostics);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(expectedSourceHash)
                    && !string.Equals(expectedSourceHash, runtime.SourceHash, StringComparison.Ordinal))
                {
                    diagnostics.Add(new CharacterTestWorldObjectDefinitionValidationIssue(
                        "WOD_RUNTIME_STALE",
                        path,
                        "Runtime catalog source hash does not match current Authoring JSON."));
                    continue;
                }

                CharacterTestWorldObjectDefinitionCatalog catalog;
                CharacterTestWorldObjectPresentationCatalog presentations;
                try
                {
                    BuildRuntimeCatalogs(runtime, out catalog, out presentations);
                }
                catch (Exception exception)
                {
                    diagnostics.Add(new CharacterTestWorldObjectDefinitionValidationIssue(
                        "WOD_RUNTIME_COMPILE_FAILED",
                        path,
                        exception.Message));
                    continue;
                }

                return new CharacterTestWorldObjectDefinitionCatalogLoadResult(
                    catalog,
                    presentations,
                    runtime.SourceHash,
                    runtime.ContentHash,
                    sourceKinds[i],
                    path,
                    diagnostics);
            }

            CharacterTestWorldObjectDefinitionAuthoringDocument fallbackAuthoring =
                CharacterTestWorldObjectDefinitionMigration.ExportBuiltInCatalog();
            CharacterTestWorldObjectDefinitionCompileResult fallback =
                CharacterTestWorldObjectDefinitionCompiler.Compile(fallbackAuthoring);
            diagnostics.Add(new CharacterTestWorldObjectDefinitionValidationIssue(
                "WOD_BUILTIN_FALLBACK",
                "$",
                "No valid generated world object catalog was available; built-in definitions are active."));
            return new CharacterTestWorldObjectDefinitionCatalogLoadResult(
                fallback.Catalog,
                fallback.Presentations,
                fallback.SourceHash,
                fallback.ContentHash,
                CharacterTestWorldObjectDefinitionCatalogSourceKind.BuiltInFallback,
                string.Empty,
                diagnostics);
        }

        private static void BuildRuntimeCatalogs(
            CharacterTestWorldObjectDefinitionRuntimeDocument runtime,
            out CharacterTestWorldObjectDefinitionCatalog catalog,
            out CharacterTestWorldObjectPresentationCatalog presentations)
        {
            var definitions = new List<CharacterTestWorldObjectDefinition>(runtime.Definitions.Count);
            var bindings = new List<CharacterTestWorldObjectPresentationBinding>(runtime.Definitions.Count);
            for (int i = 0; i < runtime.Definitions.Count; i++)
            {
                CharacterTestWorldObjectDefinitionAuthoringEntry entry = runtime.Definitions[i];
                definitions.Add(CharacterTestWorldObjectDefinitionCompiler.BuildDefinition(entry));
                bindings.Add(CharacterTestWorldObjectDefinitionCompiler.BuildPresentation(entry));
            }

            catalog = new CharacterTestWorldObjectDefinitionCatalog(definitions);
            presentations = new CharacterTestWorldObjectPresentationCatalog(bindings);
        }

        private static bool TryReadExpectedAuthoringHash(
            string path,
            out string sourceHash,
            out CharacterTestWorldObjectDefinitionValidationReport report)
        {
            sourceHash = string.Empty;
            report = new CharacterTestWorldObjectDefinitionValidationReport();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;
            if (!CharacterTestWorldObjectDefinitionJson.TryLoadAuthoring(
                    File.ReadAllText(path),
                    out CharacterTestWorldObjectDefinitionAuthoringDocument document,
                    out report))
            {
                return false;
            }

            sourceHash = CharacterTestWorldObjectDefinitionJson.ComputeAuthoringHash(document);
            return true;
        }

        private static void CopyIssues(
            CharacterTestWorldObjectDefinitionValidationReport report,
            List<CharacterTestWorldObjectDefinitionValidationIssue> output)
        {
            if (report == null)
                return;
            for (int i = 0; i < report.Issues.Count; i++)
                output.Add(report.Issues[i]);
        }

        private static string Resolve(string root, string relative)
        {
            return string.IsNullOrWhiteSpace(root)
                ? relative
                : Path.GetFullPath(Path.Combine(root, relative));
        }
    }

    public static class CharacterTestWorldObjectDefinitionCatalogSource
    {
        private static readonly CharacterTestWorldObjectDefinitionCatalogLoadResult Loaded =
            CharacterTestWorldObjectDefinitionCatalogLoader.LoadDefaultOrBuiltIn();

        public static CharacterTestWorldObjectDefinitionCatalog Catalog => Loaded.Catalog;
        public static CharacterTestWorldObjectPresentationCatalog Presentations => Loaded.Presentations;
        public static CharacterTestWorldObjectDefinitionCatalogLoadResult LoadResult => Loaded;
    }
}
