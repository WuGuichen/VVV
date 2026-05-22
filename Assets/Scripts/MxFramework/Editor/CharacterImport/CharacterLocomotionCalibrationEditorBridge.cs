using MxFramework.CharacterRuntimeSpawn.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.CharacterImport
{
    [InitializeOnLoad]
    internal static class CharacterLocomotionCalibrationEditorBridge
    {
        static CharacterLocomotionCalibrationEditorBridge()
        {
            CharacterLocomotionCalibrationRunner.LocateProjectObjectRequested -= LocateProjectObject;
            CharacterLocomotionCalibrationRunner.LocateProjectObjectRequested += LocateProjectObject;
            CharacterLocomotionCalibrationRunner.ApplyCalibrationDraftRequested -= ApplyCalibrationDraft;
            CharacterLocomotionCalibrationRunner.ApplyCalibrationDraftRequested += ApplyCalibrationDraft;
        }

        private static void LocateProjectObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

            string path = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogWarning("MxFramework Locomotion Calibration: selected clip is not a persistent Project asset. Stop Play Mode and select a Project animation clip before editing import/bake settings.");
                Selection.activeObject = target;
                EditorGUIUtility.PingObject(target);
                return;
            }

            UnityEngine.Object selection = ResolveInspectorSelection(target, path);
            Selection.activeObject = selection;
            EditorGUIUtility.PingObject(selection);

            string extension = Path.GetExtension(path);
            string saveTarget = string.Equals(extension, ".anim", StringComparison.OrdinalIgnoreCase)
                ? path
                : path + ".meta";
            Debug.Log("MxFramework Locomotion Calibration: selected animation source '" + selection.name + "' at " + path + ". Inspector changes should persist to " + saveTarget + ". Edit outside Play Mode, then use Apply when Unity shows importer changes or Assets > Save Project for standalone .anim clips.");
        }

        private static UnityEngine.Object ResolveInspectorSelection(UnityEngine.Object target, string path)
        {
            string extension = Path.GetExtension(path);
            if (string.Equals(extension, ".anim", StringComparison.OrdinalIgnoreCase))
                return target;

            UnityEngine.Object mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
            return mainAsset != null ? mainAsset : target;
        }

        private static void ApplyCalibrationDraft(string draftJson)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(draftJson))
                {
                    ReportApplyResult("Apply Config failed: draft is empty.");
                    return;
                }

                JObject draft = JObject.Parse(draftJson);
                string packageId = ReadString(draft, "packageId");
                string authoringPath = ResolveAnimationAuthoringPath(packageId);
                if (string.IsNullOrWhiteSpace(authoringPath) || !File.Exists(authoringPath))
                {
                    ReportApplyResult("Apply Config failed: animation_authoring.json was not found.");
                    return;
                }

                int clipChanges = 0;
                int pointChanges = 0;
                int savedFiles = 0;
                string savedTargets = string.Empty;
                ApplyDraftToDocument(
                    authoringPath,
                    draft,
                    ref clipChanges,
                    ref pointChanges,
                    ref savedFiles,
                    ref savedTargets);

                string runtimePath = ResolveGeneratedAnimationSetDefinitionPath(packageId);
                if (!string.IsNullOrWhiteSpace(runtimePath) && File.Exists(runtimePath))
                {
                    ApplyDraftToDocument(
                        runtimePath,
                        draft,
                        ref clipChanges,
                        ref pointChanges,
                        ref savedFiles,
                        ref savedTargets);
                }

                if (clipChanges == 0 && pointChanges == 0)
                {
                    ReportApplyResult("Apply Config skipped: no matching clip or blend point changes.");
                    return;
                }

                AssetDatabase.Refresh();
                ReportApplyResult("Apply Config saved " + clipChanges + " clip override(s), "
                    + pointChanges + " blend point(s) in " + savedFiles + " file(s): " + savedTargets);
            }
            catch (Exception ex)
            {
                ReportApplyResult("Apply Config failed: " + ex.Message);
            }
        }

        private static void ApplyDraftToDocument(
            string path,
            JObject draft,
            ref int clipChanges,
            ref int pointChanges,
            ref int savedFiles,
            ref string savedTargets)
        {
            JObject config = JObject.Parse(File.ReadAllText(path));
            int clipChangesInFile = ApplyClipOverrides(config, draft["clipOverrides"] as JArray);
            int pointChangesInFile = ApplyBlendPointOverrides(config, draft["blendPointOverrides"] as JArray);
            if (clipChangesInFile == 0 && pointChangesInFile == 0)
                return;

            File.WriteAllText(path, config.ToString(Formatting.Indented) + Environment.NewLine);
            clipChanges += clipChangesInFile;
            pointChanges += pointChangesInFile;
            savedFiles++;
            savedTargets = string.IsNullOrEmpty(savedTargets) ? path : savedTargets + "; " + path;
        }

        private static int ApplyClipOverrides(JObject config, JArray overrides)
        {
            int changes = 0;
            for (int i = 0; overrides != null && i < overrides.Count; i++)
            {
                JObject entry = overrides[i] as JObject;
                if (entry == null)
                    continue;

                string clipResourceId = ReadString(entry, "clipResourceId");
                if (string.IsNullOrWhiteSpace(clipResourceId))
                    continue;

                JObject clip = FindClipByResourceId(config, clipResourceId);
                if (clip == null)
                    continue;

                float playbackSpeed = ReadFloat(entry, "playbackSpeed", 1f);
                if (SetNumber(clip, "speed", playbackSpeed))
                    changes++;

                JObject calibration = clip["calibration"] as JObject;
                if (calibration == null)
                {
                    calibration = new JObject();
                    clip["calibration"] = calibration;
                }

                if (SetNumber(calibration, "playbackSpeed", playbackSpeed))
                    changes++;
            }

            return changes;
        }

        private static int ApplyBlendPointOverrides(JObject config, JArray overrides)
        {
            int changes = 0;
            for (int i = 0; overrides != null && i < overrides.Count; i++)
            {
                JObject entry = overrides[i] as JObject;
                if (entry == null)
                    continue;

                string clipResourceId = ReadString(entry, "clipResourceId");
                JObject clip = FindClipByResourceId(config, clipResourceId);
                string clipId = ReadString(clip, "clipId");
                if (string.IsNullOrWhiteSpace(clipId))
                    continue;

                JObject point = FindBlendPointByClipId(config, clipId);
                if (point == null)
                    continue;

                float x = ReadFloat(entry, "x", ReadFloat(point, "x", 0f));
                float y = ReadFloat(entry, "y", ReadFloat(point, "y", 0f));
                if (SetNumber(point, "x", x))
                    changes++;
                if (SetNumber(point, "y", y))
                    changes++;
            }

            return changes;
        }

        private static JObject FindClipByResourceId(JObject config, string clipResourceId)
        {
            JArray sets = config["sets"] as JArray;
            for (int setIndex = 0; sets != null && setIndex < sets.Count; setIndex++)
            {
                JArray groups = (sets[setIndex] as JObject)?["groups"] as JArray;
                for (int groupIndex = 0; groups != null && groupIndex < groups.Count; groupIndex++)
                {
                    JArray clips = (groups[groupIndex] as JObject)?["clips"] as JArray;
                    for (int clipIndex = 0; clips != null && clipIndex < clips.Count; clipIndex++)
                    {
                        JObject clip = clips[clipIndex] as JObject;
                        if (clip == null)
                            continue;

                        if (string.Equals(ReadString(clip, "runtimeResourceKey"), clipResourceId, StringComparison.Ordinal)
                            || string.Equals(ReadString(clip["sourceSelection"] as JObject, "runtimeResourceKey"), clipResourceId, StringComparison.Ordinal))
                        {
                            return clip;
                        }
                    }
                }
            }

            return null;
        }

        private static JObject FindBlendPointByClipId(JObject config, string clipId)
        {
            JArray sets = config["sets"] as JArray;
            for (int setIndex = 0; sets != null && setIndex < sets.Count; setIndex++)
            {
                JArray groups = (sets[setIndex] as JObject)?["groups"] as JArray;
                for (int groupIndex = 0; groups != null && groupIndex < groups.Count; groupIndex++)
                {
                    JArray blends = (groups[groupIndex] as JObject)?["blend2D"] as JArray;
                    for (int blendIndex = 0; blends != null && blendIndex < blends.Count; blendIndex++)
                    {
                        JArray points = (blends[blendIndex] as JObject)?["points"] as JArray;
                        for (int pointIndex = 0; points != null && pointIndex < points.Count; pointIndex++)
                        {
                            JObject point = points[pointIndex] as JObject;
                            if (string.Equals(ReadString(point, "clipId"), clipId, StringComparison.Ordinal))
                                return point;
                        }
                    }
                }
            }

            return null;
        }

        private static string ResolveAnimationAuthoringPath(string packageId)
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
            string samplesRoot = Path.Combine(root, "Tools", "MxFramework.Authoring", "samples");
            if (!string.IsNullOrWhiteSpace(packageId))
            {
                string direct = Path.Combine(samplesRoot, packageId, "config", "animation_authoring.json");
                if (File.Exists(direct))
                    return direct;
            }

            string[] candidates = Directory.Exists(samplesRoot)
                ? Directory.GetFiles(samplesRoot, "animation_authoring.json", SearchOption.AllDirectories)
                : Array.Empty<string>();
            if (candidates.Length == 1)
                return candidates[0];

            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = candidates[i];
                if (string.IsNullOrWhiteSpace(packageId)
                    || candidate.IndexOf(packageId, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return candidate;
                }
            }

            return candidates.Length > 0 ? candidates[0] : string.Empty;
        }

        private static string ResolveGeneratedAnimationSetDefinitionPath(string packageId)
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
            string packageSlug = NormalizePackageSlug(packageId);
            string generatedRoot = Path.Combine(root, "Assets", "MxFrameworkGenerated", "CharacterPackages");
            if (!string.IsNullOrWhiteSpace(packageSlug))
            {
                string direct = Path.Combine(generatedRoot, packageSlug, "config", "animation_set_definition.json");
                if (File.Exists(direct))
                    return direct;
            }

            if (!Directory.Exists(generatedRoot))
                return string.Empty;

            string[] candidates = Directory.GetFiles(generatedRoot, "animation_set_definition.json", SearchOption.AllDirectories);
            if (candidates.Length == 1)
                return candidates[0];

            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = candidates[i];
                if (string.IsNullOrWhiteSpace(packageSlug)
                    || candidate.IndexOf(packageSlug, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return candidate;
                }
            }

            return candidates.Length > 0 ? candidates[0] : string.Empty;
        }

        private static string NormalizePackageSlug(string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId))
                return string.Empty;

            string value = packageId.Trim();
            if (value.StartsWith("animation.", StringComparison.Ordinal))
                value = value.Substring("animation.".Length);
            if (value.StartsWith("character.", StringComparison.Ordinal))
                value = value.Substring("character.".Length);
            return value;
        }

        private static bool SetNumber(JObject obj, string key, float value)
        {
            if (obj == null)
                return false;

            float current = ReadFloat(obj, key, float.NaN);
            if (!float.IsNaN(current) && Math.Abs(current - value) <= 0.0001f)
                return false;

            obj[key] = value;
            return true;
        }

        private static string ReadString(JObject obj, string key)
        {
            return obj != null ? (string)obj[key] ?? string.Empty : string.Empty;
        }

        private static float ReadFloat(JObject obj, string key, float fallback)
        {
            if (obj == null || obj[key] == null)
                return fallback;

            return float.TryParse(obj[key].ToString(), out float value) ? value : fallback;
        }

        private static void ReportApplyResult(string message)
        {
            if (message.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0)
                Debug.LogWarning("MxFramework Locomotion Calibration: " + message);
            else
                Debug.Log("MxFramework Locomotion Calibration: " + message);
            CharacterLocomotionCalibrationRunner.ReportCalibrationDraftApplyResult(message);
        }
    }
}
