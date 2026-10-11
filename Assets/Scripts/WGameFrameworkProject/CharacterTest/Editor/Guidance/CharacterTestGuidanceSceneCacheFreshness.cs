using System;
using System.IO;
using MxFramework.Config.Levels;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal readonly struct CharacterTestGuidanceSceneCacheFreshnessResult
    {
        internal CharacterTestGuidanceSceneCacheFreshnessResult(
            bool success,
            string code,
            string message,
            string authoredSourceHash,
            string cacheSourceHash)
        {
            Success = success;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            AuthoredSourceHash = authoredSourceHash ?? string.Empty;
            CacheSourceHash = cacheSourceHash ?? string.Empty;
        }

        internal bool Success { get; }
        internal string Code { get; }
        internal string Message { get; }
        internal string AuthoredSourceHash { get; }
        internal string CacheSourceHash { get; }
    }

    internal static class CharacterTestGuidanceSceneCacheFreshness
    {
        internal const string SuccessCode = "CHARACTER_TEST_GUIDANCE_CACHE_FRESH";
        internal const string StaleCode = "CHARACTER_TEST_GUIDANCE_CACHE_STALE";
        internal const string InvalidAuthoringCode = "CHARACTER_TEST_GUIDANCE_AUTHORING_INVALID";
        internal const string SceneMissingCode = "CHARACTER_TEST_GUIDANCE_SCENE_MISSING";
        internal const string SceneDirtyCode = "CHARACTER_TEST_GUIDANCE_SCENE_DIRTY";
        internal const string GameManagerMissingCode = "CHARACTER_TEST_GUIDANCE_GAME_MANAGER_MISSING";

        internal static CharacterTestGuidanceSceneCacheFreshnessResult ValidateFormalScene(
            bool requireSavedScene = true)
        {
            string scenePath = CharacterTestGuidanceAuthoringAdapter.DefaultScenePath;
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (openedForValidation)
                {
                    if (!File.Exists(ToFullPath(scenePath)))
                    {
                        return Failed(
                            SceneMissingCode,
                            "CharacterTest scene does not exist: " + scenePath);
                    }
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                }

                if (ShouldRejectDirtyScene(scene.isDirty, requireSavedScene))
                {
                    return Failed(
                        SceneDirtyCode,
                        "CharacterTest scene has unsaved changes. Save the coordinated Guidance payload/cache/Scene transaction before Play or build.");
                }

                GameManager manager = FindGameManager(scene);
                if (manager == null)
                {
                    return Failed(
                        GameManagerMissingCode,
                        "CharacterTest scene has no GameManager Guidance cache owner.");
                }
                return ValidateCache(
                    manager.GuidanceSceneCache,
                    CharacterTestGuidanceAuthoringAdapter.DefaultDocumentPath);
            }
            catch (Exception exception)
            {
                return Failed(
                    InvalidAuthoringCode,
                    "Guidance cache preflight failed: " + exception.Message);
            }
            finally
            {
                if (openedForValidation && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        internal static CharacterTestGuidanceSceneCacheFreshnessResult ValidateCache(
            CharacterTestGuidanceSceneCache cache,
            string documentPath)
        {
            CharacterTestGuidancePlan authoredPlan;
            try
            {
                string fullPath = ToFullPath(documentPath);
                if (!File.Exists(fullPath))
                {
                    return Failed(
                        InvalidAuthoringCode,
                        "Guidance level envelope does not exist: " + documentPath);
                }
                MxLevelAuthoringDocument envelope =
                    MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(fullPath));
                if (!CharacterTestGuidanceAuthoringAdapter.TryExtract(
                        envelope,
                        out CharacterTestGuidanceAuthoringDocument document,
                        out CharacterTestGuidanceRuntimeDiagnostic extractionDiagnostic))
                {
                    return Failed(
                        InvalidAuthoringCode,
                        extractionDiagnostic.Code + ": " + extractionDiagnostic.Message);
                }
                if (!CharacterTestGuidanceCompiler.TryCompile(
                        document,
                        out authoredPlan,
                        out CharacterTestGuidanceAuthoringReport report))
                {
                    return Failed(
                        InvalidAuthoringCode,
                        "Guidance authoring failed validation. Errors=" + report.ErrorCount + ".");
                }
            }
            catch (Exception exception)
            {
                return Failed(
                    InvalidAuthoringCode,
                    "Guidance level envelope cannot be read: " + exception.Message);
            }

            if (cache == null || !cache.HasSerializedInput)
            {
                return Failed(
                    StaleCode,
                    "GameManager Guidance scene cache is missing.",
                    authoredPlan.SourceHash,
                    cache?.GuidanceModuleSourceHash);
            }

            bool metadataMatches =
                cache.CacheVersion == CharacterTestGuidanceSceneCache.CurrentCacheVersion
                && cache.ModuleSchemaVersion == CharacterTestGuidanceAuthoringAdapter.ModuleSchemaVersion
                && cache.Enabled == authoredPlan.Enabled
                && cache.StepCount == authoredPlan.StepCount
                && string.Equals(
                    cache.GuidanceModuleSourceHash,
                    authoredPlan.SourceHash,
                    StringComparison.Ordinal)
                && string.Equals(
                    cache.CompletionCatalogHash,
                    authoredPlan.CompletionCatalogHash,
                    StringComparison.Ordinal)
                && string.Equals(
                    CharacterTestGuidanceCanonicalJson.ComputeHash(
                        cache.CanonicalGuidanceModuleJson),
                    authoredPlan.SourceHash,
                    StringComparison.Ordinal);
            if (!metadataMatches)
            {
                return Failed(
                    StaleCode,
                    "Guidance scene cache metadata does not match the latest characterTest.guidance module. Save the Teaching Guidance Level Editor module.",
                    authoredPlan.SourceHash,
                    cache.GuidanceModuleSourceHash);
            }

            if (authoredPlan.Enabled)
            {
                if (!cache.TryCompilePlan(
                        out CharacterTestGuidancePlan cachedPlan,
                        out CharacterTestGuidanceRuntimeDiagnostic diagnostic)
                    || !string.Equals(
                        cachedPlan.SourceHash,
                        authoredPlan.SourceHash,
                        StringComparison.Ordinal))
                {
                    return Failed(
                        StaleCode,
                        diagnostic.Code + ": " + diagnostic.Message,
                        authoredPlan.SourceHash,
                        cache.GuidanceModuleSourceHash);
                }
            }
            else if (cache.TryCompilePlan(out _, out CharacterTestGuidanceRuntimeDiagnostic disabledDiagnostic)
                || disabledDiagnostic.Code != CharacterTestGuidanceRuntimeDiagnosticCode.AuthorDisabled)
            {
                return Failed(
                    StaleCode,
                    "Disabled Guidance cache did not produce the expected typed AuthorDisabled diagnostic.",
                    authoredPlan.SourceHash,
                    cache.GuidanceModuleSourceHash);
            }

            return new CharacterTestGuidanceSceneCacheFreshnessResult(
                true,
                SuccessCode,
                "Guidance level authoring and CharacterTest scene cache are synchronized.",
                authoredPlan.SourceHash,
                cache.GuidanceModuleSourceHash);
        }

        internal static void ThrowIfFormalSceneInvalid()
        {
            CharacterTestGuidanceSceneCacheFreshnessResult result = ValidateFormalScene();
            if (!result.Success)
                throw new BuildFailedException(result.Code + ": " + result.Message);
        }

        internal static bool ShouldRejectDirtyScene(bool sceneIsDirty, bool requireSavedScene)
        {
            return sceneIsDirty && requireSavedScene;
        }

        private static GameManager FindGameManager(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameManager manager = roots[i].GetComponentInChildren<GameManager>(true);
                if (manager != null)
                    return manager;
            }
            return null;
        }

        private static string ToFullPath(string path)
        {
            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            return Path.GetFullPath(Path.Combine(projectRoot ?? string.Empty, path ?? string.Empty));
        }

        private static CharacterTestGuidanceSceneCacheFreshnessResult Failed(
            string code,
            string message,
            string authoredSourceHash = "",
            string cacheSourceHash = "")
        {
            return new CharacterTestGuidanceSceneCacheFreshnessResult(
                false,
                code,
                message,
                authoredSourceHash,
                cacheSourceHash);
        }
    }

    [InitializeOnLoad]
    internal static class CharacterTestGuidancePlayModePreflight
    {
        static CharacterTestGuidancePlayModePreflight()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode
                || !string.Equals(
                    SceneManager.GetActiveScene().path,
                    CharacterTestGuidanceAuthoringAdapter.DefaultScenePath,
                    StringComparison.Ordinal))
            {
                return;
            }

            CharacterTestGuidanceSceneCacheFreshnessResult result =
                CharacterTestGuidanceSceneCacheFreshness.ValidateFormalScene(
                    requireSavedScene: false);
            if (result.Success)
                return;

            EditorApplication.isPlaying = false;
            Debug.LogError(
                result.Code + ": " + result.Message
                + " CharacterTest Play Mode was blocked; no hardcoded Guidance fallback will be used.");
        }
    }

    internal sealed class CharacterTestGuidanceBuildPreflight : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (!scenes[i].enabled
                    || !string.Equals(
                        scenes[i].path,
                        CharacterTestGuidanceAuthoringAdapter.DefaultScenePath,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                CharacterTestGuidanceSceneCacheFreshness.ThrowIfFormalSceneInvalid();
                return;
            }
        }
    }
}
