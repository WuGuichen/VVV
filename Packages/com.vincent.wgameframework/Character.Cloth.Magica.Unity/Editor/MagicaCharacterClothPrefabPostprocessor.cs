using System;
using System.Collections.Generic;
using System.IO;
using MagicaCloth2;
using MxFramework.Editor.CharacterImport;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.CharacterCloth.Magica.Unity.Editor
{
    /// <summary>
    /// Projects optional package-local Magica Cloth declarations onto generated character prefabs.
    /// </summary>
    public sealed class MagicaCharacterClothPrefabPostprocessor :
        ICharacterImportedPackagePrefabPostprocessor
    {
        internal const string ConfigRelativePath = MagicaCharacterClothAuthoringRepository.ConfigRelativePath;
        private static readonly HashSet<string> PendingAutomaticReimports =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public int Order => 200;

        /// <remarks>AllocByDesign: Editor-only imported character prefab construction.</remarks>
        public void Process(CharacterImportedPackagePrefabPostprocessContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            string configPath = NormalizeProjectPath(
                context.ImportedPackageRoot.TrimEnd('/', '\\') + "/" + ConfigRelativePath);
            if (!File.Exists(ProjectPath(configPath)))
                return;

            MagicaCharacterClothAuthoringDocument config =
                MagicaCharacterClothAuthoringRepository.Load(context.ImportedPackageRoot).Document;
            if (!config.enabled)
                return;
            MagicaCharacterClothValidationResult validation =
                MagicaCharacterClothAuthoringValidator.Validate(config, context.BodyModel);
            if (RequiresFullModelReset(validation))
                config = ResetFromCurrentModel(context, validation);
            ApplyDocument(
                context.CharacterRoot,
                context.BodyModel,
                config,
                context.Package?.PackageId);
        }

        internal static bool RequiresFullModelReset(MagicaCharacterClothValidationResult validation)
        {
            if (validation == null) return false;
            for (int diagnosticIndex = 0; diagnosticIndex < validation.Diagnostics.Count; diagnosticIndex++)
            {
                string code = validation.Diagnostics[diagnosticIndex]?.Code ?? string.Empty;
                if (string.Equals(code, "MAGCLOTH004", StringComparison.Ordinal)
                    || string.Equals(code, "MAGCLOTH006", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static MagicaCharacterClothAuthoringDocument ResetFromCurrentModel(
            CharacterImportedPackagePrefabPostprocessContext context,
            MagicaCharacterClothValidationResult invalid)
        {
            MagicaCharacterClothModelInitializationResult initialized =
                MagicaCharacterClothModelInitializer.Create(context.BodyModel);
            if (!initialized.Succeeded)
                throw new InvalidDataException(
                    "Character Cloth bone paths changed, but automatic full reset failed: "
                    + initialized.Message);

            var reset = new MagicaCharacterClothAuthoringDocument
            {
                schemaVersion = "1.1",
                backend = "MagicaCloth2",
                enabled = true,
                cloths = initialized.Cloths,
            };
            string packageId = context.Package?.PackageId ?? string.Empty;
            string authoringPackagePath = ResolveAuthoringPackagePath(packageId);
            if (string.IsNullOrWhiteSpace(authoringPackagePath))
                throw new DirectoryNotFoundException(
                    "Character Cloth automatic reset could not resolve the Authoring package for "
                    + packageId + ".");

            SaveResetDocument(authoringPackagePath, reset, "Authoring");
            SaveResetDocument(context.ImportedPackageRoot, reset, "Generated");
            ScheduleAutomaticReimport(authoringPackagePath);
            Debug.LogWarning(
                "MxFramework Character Cloth: model bone paths changed; reset all Cloth authoring data from the current model. "
                + "package=" + packageId
                + " previousErrors=" + invalid.ErrorCount
                + " cloths=" + reset.cloths.Length
                + " colliders=" + initialized.CreatedColliderCount);
            return reset;
        }

        private static void SaveResetDocument(
            string packagePath,
            MagicaCharacterClothAuthoringDocument document,
            string label)
        {
            MagicaCharacterClothAuthoringLoadResult load =
                MagicaCharacterClothAuthoringRepository.Load(packagePath);
            MagicaCharacterClothAuthoringSaveResult save =
                MagicaCharacterClothAuthoringRepository.Save(packagePath, document, load.Hash);
            if (!save.Succeeded)
                throw new InvalidDataException(
                    "Character Cloth automatic full reset could not save " + label
                    + " config: " + save.Message);
        }

        private static string ResolveAuthoringPackagePath(string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId)) return string.Empty;
            string candidate = Path.GetFullPath(Path.Combine(
                "Authoring",
                "CharacterPackages",
                "character-" + packageId));
            string manifestPath = Path.Combine(candidate, "manifest.json");
            if (!Directory.Exists(candidate) || !File.Exists(manifestPath)) return string.Empty;
            try
            {
                JObject manifest = JObject.Parse(File.ReadAllText(manifestPath));
                return string.Equals(
                    (string)manifest["packageId"] ?? string.Empty,
                    packageId,
                    StringComparison.Ordinal)
                    ? candidate
                    : string.Empty;
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return string.Empty;
            }
        }

        private static void ScheduleAutomaticReimport(string authoringPackagePath)
        {
            string fullPath = Path.GetFullPath(authoringPackagePath);
            if (!PendingAutomaticReimports.Add(fullPath)) return;
            EditorApplication.update -= ProcessPendingAutomaticReimports;
            EditorApplication.update += ProcessPendingAutomaticReimports;
        }

        private static void ProcessPendingAutomaticReimports()
        {
            if (EditorApplication.isCompiling
                || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            string fullPath = null;
            foreach (string pending in PendingAutomaticReimports)
            {
                fullPath = pending;
                break;
            }
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                EditorApplication.update -= ProcessPendingAutomaticReimports;
                return;
            }
            PendingAutomaticReimports.Remove(fullPath);
            if (PendingAutomaticReimports.Count == 0)
                EditorApplication.update -= ProcessPendingAutomaticReimports;
            try
            {
                int exitCode = CharacterPackageImportCommand.ImportPackage(fullPath);
                if (exitCode != 0)
                    Debug.LogError(
                        "MxFramework Character Cloth: automatic reimport after full model reset returned exit code "
                        + exitCode + ". package=" + fullPath);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "MxFramework Character Cloth: automatic reimport after full model reset failed. "
                    + exception);
            }
        }

        internal static void ApplyDocument(
            GameObject characterRoot,
            Transform bodyModel,
            MagicaCharacterClothAuthoringDocument config,
            string sourceLabel,
            bool logResult = true)
        {
            if (characterRoot == null)
                throw new ArgumentNullException(nameof(characterRoot));
            if (bodyModel == null)
                throw new ArgumentNullException(nameof(bodyModel));
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (!config.enabled)
                return;

            MagicaCharacterClothValidationResult validation =
                MagicaCharacterClothAuthoringValidator.Validate(config, bodyModel);
            if (validation.HasErrors)
                throw new InvalidDataException(validation.Summary + " " + FormatDiagnostics(validation));
            if (config.cloths == null || config.cloths.Length == 0)
                throw new InvalidDataException("Enabled Magica character cloth config requires at least one cloth entry.");

            for (int i = 0; i < config.cloths.Length; i++)
                BuildCloth(bodyModel, config.cloths[i], i);

            MagicaCharacterClothController controller =
                EnsureComponent<MagicaCharacterClothController>(characterRoot);
            MagicaCharacterClothSystem system =
                EnsureComponent<MagicaCharacterClothSystem>(characterRoot);
            if (Application.isPlaying)
            {
                controller.RefreshClothComponents();
                system.RefreshControllers();
            }
            if (logResult)
            {
                Debug.Log(
                    "MxFramework Character Cloth: generated Magica Cloth components. source="
                    + sourceLabel
                    + " cloths=" + config.cloths.Length);
            }
        }

        private static void BuildCloth(
            Transform bodyModel,
            MagicaCharacterClothAuthoringEntry entry,
            int index)
        {
            if (entry == null)
                throw new InvalidDataException("Magica character cloth entry is null at index " + index + ".");
            if (string.IsNullOrWhiteSpace(entry.id))
                throw new InvalidDataException("Magica character cloth entry id is required at index " + index + ".");
            if (!string.Equals(entry.clothType, "BoneCloth", StringComparison.Ordinal))
                throw new InvalidDataException("Only BoneCloth is supported by the current character cloth projection. id=" + entry.id);
            if (entry.rootBonePaths == null || entry.rootBonePaths.Length == 0)
                throw new InvalidDataException("BoneCloth requires at least one rootBonePath. id=" + entry.id);

            var clothObject = new GameObject("MagicaCloth_" + SanitizeName(entry.id));
            bool activateAfterConfiguration = Application.isPlaying;
            if (activateAfterConfiguration) clothObject.SetActive(false);
            clothObject.transform.SetParent(bodyModel, false);
            MagicaCloth cloth = clothObject.AddComponent<MagicaCloth>();

            if (!string.IsNullOrWhiteSpace(entry.presetAssetPath))
            {
                string presetPath = NormalizeProjectPath(entry.presetAssetPath);
                string presetFullPath = ProjectPath(presetPath);
                if (!File.Exists(presetFullPath))
                    throw new FileNotFoundException("Magica Cloth preset was not found.", presetPath);
                string presetJson = File.ReadAllText(presetFullPath);
                if (!cloth.SerializeData.ImportJson(presetJson))
                    throw new InvalidDataException("Magica Cloth preset could not be imported: " + presetPath);
            }

            MagicaCharacterClothParameterOverrideUtility.Apply(
                cloth.SerializeData,
                entry.parameterOverrides);

            cloth.SerializeData.clothType = ClothProcess.ClothType.BoneCloth;
            cloth.SerializeData.rootBones = new List<Transform>(entry.rootBonePaths.Length);
            for (int i = 0; i < entry.rootBonePaths.Length; i++)
            {
                cloth.SerializeData.rootBones.Add(
                    FindRequiredTransform(bodyModel, entry.rootBonePaths[i], "root bone", entry.id));
            }

            if (!Enum.TryParse(
                    string.IsNullOrWhiteSpace(entry.connectionMode) ? "Line" : entry.connectionMode,
                    ignoreCase: false,
                    out RenderSetupData.BoneConnectionMode connectionMode))
            {
                throw new InvalidDataException(
                    "Unsupported Magica BoneCloth connectionMode=" + entry.connectionMode + " id=" + entry.id);
            }
            cloth.SerializeData.connectionMode = connectionMode;

            cloth.SerializeData.colliderCollisionConstraint.colliderList =
                new List<ColliderComponent>();
            MagicaCharacterClothAuthoringCollider[] colliders = entry.colliders ?? Array.Empty<MagicaCharacterClothAuthoringCollider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                ColliderComponent collider = BuildCollider(bodyModel, entry.id, colliders[i], i);
                cloth.SerializeData.colliderCollisionConstraint.colliderList.Add(collider);
            }

            EditorUtility.SetDirty(cloth);
            if (activateAfterConfiguration) clothObject.SetActive(true);
        }

        private static ColliderComponent BuildCollider(
            Transform bodyModel,
            string clothId,
            MagicaCharacterClothAuthoringCollider config,
            int index)
        {
            if (config == null)
                throw new InvalidDataException("Magica collider is null. cloth=" + clothId + " index=" + index);
            if (string.IsNullOrWhiteSpace(config.id))
                throw new InvalidDataException("Magica collider id is required. cloth=" + clothId + " index=" + index);

            Transform parent = FindRequiredTransform(bodyModel, config.parentBonePath, "collider parent", clothId);
            var colliderObject = new GameObject(
                "MagicaCollider_" + SanitizeName(clothId) + "_" + SanitizeName(config.id));
            colliderObject.transform.SetParent(parent, false);
            colliderObject.transform.localPosition = ToVector3(config.localPosition);
            colliderObject.transform.localRotation = Quaternion.Euler(ToVector3(config.localRotationEuler));
            colliderObject.transform.localScale = InverseScale(parent.lossyScale);

            if (string.Equals(config.type, "Sphere", StringComparison.Ordinal))
            {
                if (!IsFinitePositive(config.radius))
                    throw new InvalidDataException("Magica sphere collider radius must be positive. id=" + config.id);
                MagicaSphereCollider sphere = colliderObject.AddComponent<MagicaSphereCollider>();
                sphere.SetSize(config.radius);
                return sphere;
            }

            if (string.Equals(config.type, "Capsule", StringComparison.Ordinal))
            {
                float endRadius = IsFinitePositive(config.endRadius) ? config.endRadius : config.radius;
                if (!IsFinitePositive(config.radius) || !IsFinitePositive(endRadius) || !IsFinitePositive(config.length))
                    throw new InvalidDataException("Magica capsule collider dimensions must be positive. id=" + config.id);
                MagicaCapsuleCollider capsule = colliderObject.AddComponent<MagicaCapsuleCollider>();
                capsule.SetSize(config.radius, endRadius, config.length);
                if (!Enum.TryParse(
                        string.IsNullOrWhiteSpace(config.direction) ? "Y" : config.direction,
                        ignoreCase: false,
                        out MagicaCapsuleCollider.Direction direction))
                {
                    throw new InvalidDataException(
                        "Unsupported Magica capsule direction=" + config.direction + " id=" + config.id);
                }
                capsule.direction = direction;
                return capsule;
            }

            throw new InvalidDataException("Unsupported Magica collider type=" + config.type + " id=" + config.id);
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static Transform FindRequiredTransform(
            Transform root,
            string configuredPath,
            string role,
            string clothId)
        {
            string normalized = NormalizeTransformPath(configuredPath);
            if (root == null || string.IsNullOrWhiteSpace(normalized))
                throw new InvalidDataException("Magica " + role + " path is required. cloth=" + clothId);

            Transform match = null;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(includeInactive: true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                string candidatePath = NormalizeTransformPath(
                    AnimationUtility.CalculateTransformPath(candidate, root));
                bool matches = normalized.IndexOf('/') >= 0
                    ? string.Equals(candidatePath, normalized, StringComparison.Ordinal)
                    : string.Equals(candidate.name, normalized, StringComparison.Ordinal);
                if (!matches)
                    continue;
                if (match != null)
                    throw new InvalidDataException(
                        "Magica " + role + " path is ambiguous: " + configuredPath + " cloth=" + clothId);
                match = candidate;
            }

            if (match == null)
                throw new InvalidDataException(
                    "Magica " + role + " was not found: " + configuredPath + " cloth=" + clothId);
            return match;
        }

        private static Vector3 InverseScale(Vector3 scale)
        {
            if (!IsFinitePositive(Mathf.Abs(scale.x))
                || !IsFinitePositive(Mathf.Abs(scale.y))
                || !IsFinitePositive(Mathf.Abs(scale.z)))
            {
                throw new InvalidDataException("Magica collider parent has an unusable world scale: " + scale);
            }
            return new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
        }

        private static Vector3 ToVector3(MagicaCharacterClothAuthoringVector value)
        {
            return value != null ? new Vector3(value.x, value.y, value.z) : Vector3.zero;
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static string NormalizeTransformPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim().Trim('/');
        }

        private static string NormalizeProjectPath(string path)
        {
            string normalized = (path ?? string.Empty).Replace('\\', '/').Trim();
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
                .Replace('\\', '/')
                .TrimEnd('/') + "/";
            string fullPath = Path.GetFullPath(normalized).Replace('\\', '/');
            if (!fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Magica character cloth path is outside the Unity project: " + path);
            return fullPath.Substring(projectRoot.Length);
        }

        private static string ProjectPath(string assetPath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string SanitizeName(string value)
        {
            char[] chars = (value ?? string.Empty).ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char ch = chars[i];
                if (!char.IsLetterOrDigit(ch) && ch != '_' && ch != '-')
                    chars[i] = '_';
            }
            return new string(chars);
        }

        private static string FormatDiagnostics(MagicaCharacterClothValidationResult validation)
        {
            var messages = new List<string>();
            for (int i = 0; i < validation.Diagnostics.Count; i++)
            {
                MagicaCharacterClothDiagnostic diagnostic = validation.Diagnostics[i];
                if (diagnostic.Severity == MagicaCharacterClothDiagnosticSeverity.Error)
                    messages.Add(diagnostic.Code + ": " + diagnostic.Message);
            }
            return string.Join(" | ", messages);
        }
    }
}
