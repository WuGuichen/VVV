using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Resources;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestRuntimeCharacterLoader
    {
        private readonly IResourceManager _resourceManager;
        private readonly IRuntimeLogger _logger;
        private readonly bool _installDefaultProceduralHitFeedbackChain;
        private readonly bool _logSuccessfulSpawnDiagnostics;

        public CharacterTestRuntimeCharacterLoader(
            IResourceManager resourceManager,
            IRuntimeLogger logger,
            bool installDefaultProceduralHitFeedbackChain = true,
            bool logSuccessfulSpawnDiagnostics = false)
        {
            _resourceManager = resourceManager;
            _logger = logger ?? NullRuntimeLogger.Instance;
            _installDefaultProceduralHitFeedbackChain = installDefaultProceduralHitFeedbackChain;
            _logSuccessfulSpawnDiagnostics = logSuccessfulSpawnDiagnostics;
        }

        public bool Instantiate(
            CharacterTestCharacterEntityId entityId,
            ResourceHandle<GameObject> prefabHandle,
            Vector3 position,
            string runtimeName,
            CharacterRuntimeAnimationBinding animationBinding,
            string generatedPackageRoot,
            CharacterTestCharacterControlOwner controlOwner,
            out CharacterTestRuntimeCharacterView view)
        {
            view = null;
            if (prefabHandle == null || prefabHandle.Value == null)
            {
                _logger.Warning("CharacterSpawn", "Character prefab handle is empty. entityId=" + entityId);
                return false;
            }

            GameObject prefab = prefabHandle.Value;
            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            instance.name = string.IsNullOrWhiteSpace(runtimeName)
                ? prefab.name + "_Entity" + entityId.Value
                : runtimeName;

            CharacterImportedPackage package = CharacterTestGeneratedPackageSource.LoadPackage(generatedPackageRoot);
            if (package == null)
            {
                _logger.Warning("CharacterSpawn", "Generated character package could not be loaded. root=" + generatedPackageRoot);
                Object.Destroy(instance);
                return false;
            }

            CharacterRuntimeSpawnResult spawnResult = CharacterRuntimeSpawnResolver.Resolve(package, default);
            if (spawnResult == null || !spawnResult.IsSuccess)
            {
                _logger.Warning("CharacterSpawn", "Runtime spawn resolve failed. " + FormatSpawnIssues(spawnResult));
                Object.Destroy(instance);
                return false;
            }

            CharacterRuntimePrefabCompositionResult composition = CharacterRuntimePrefabComposer.Compose(
                instance,
                new CharacterRuntimePrefabCompositionOptions(package, spawnResult)
                {
                    ResourceManager = _resourceManager,
                    InstantiateDefaultWeapons = true,
                    InstallInputMotionController = false,
                    EnableInputMotion = false,
                    UseExternalRuntimeTick = true
                });
            if (!composition.Success)
            {
                _logger.Warning(
                    "CharacterSpawn",
                    "Runtime prefab composition failed. character=" + instance.name
                    + " issue=" + string.Join("; ", composition.Issues));
                Object.Destroy(instance);
                return false;
            }

            ConfigureDefaultEquipment(instance, package);
            CharacterBoneRuntimeSync boneSync = ConfigureBoneRuntimeSync(instance);
            CharacterProceduralBoneRig proceduralBoneRig =
                ConfigureProceduralBoneRig(instance, generatedPackageRoot, package);
            ConfigureControlOwnership(instance, controlOwner);
            CharacterRuntimeLocomotionBlendController locomotion = LogLocomotionStatus(instance, animationBinding);

            view = new CharacterTestRuntimeCharacterView(
                entityId,
                instance,
                prefabHandle,
                _resourceManager,
                locomotion,
                boneSync,
                proceduralBoneRig,
                package?.Geometry?.AuthorityPoseRig);
            return true;
        }

        private void ConfigureDefaultEquipment(GameObject instance, CharacterImportedPackage package)
        {
            CharacterDefaultEquipmentRuntimeBinder binder =
                instance.GetComponent<CharacterDefaultEquipmentRuntimeBinder>();
            if (binder == null)
            {
                if (_logSuccessfulSpawnDiagnostics)
                    _logger.Info("CharacterSpawn", "Character has no default equipment binder. name=" + instance.name);
                return;
            }

            binder.ConfigureResourceManager(_resourceManager, instantiateDefaultWeapons: true);
            EnsureSpawnedWeaponTraceLocators(package, binder);
            binder.DisableStandaloneAnimationPlayback();
            if (_logSuccessfulSpawnDiagnostics)
            {
                _logger.Info(
                    "CharacterSpawn",
                    "Default equipment configured. character=" + instance.name
                    + " weaponCount=" + binder.DefaultWeapons.Count);
            }
        }

        // LowFreqAlloc during character spawn. Materialize only endpoints declared by the
        // imported geometry, matching the action preview's weapon-local locator convention.
        private static void EnsureSpawnedWeaponTraceLocators(
            CharacterImportedPackage package, CharacterDefaultEquipmentRuntimeBinder binder)
        {
            CharacterWeaponTraceRuntimeBinding[] traces = package?.Geometry?.WeaponTraces;
            if (traces == null)
                return;

            foreach (CharacterDefaultWeaponPrefabBinding binding in binder.DefaultWeapons)
            {
                if (binding == null || !binder.TryGetSpawnedWeapon(binding.WeaponId, binding.EquipSlot, out GameObject weapon))
                    continue;

                foreach (CharacterWeaponTraceRuntimeBinding trace in traces)
                {
                    if (trace == null || (binding.TraceId > 0 && trace.TraceId != binding.TraceId)
                        || (trace.WeaponId > 0 && trace.WeaponId != binding.WeaponId)
                        || (trace.EquipSlot > 0 && trace.EquipSlot != binding.EquipSlot))
                        continue;

                    EnsureWeaponTraceLocator(weapon.transform, binding, trace.StartLocatorPath, trace.StartPose);
                    EnsureWeaponTraceLocator(weapon.transform, binding, trace.EndLocatorPath, trace.EndPose);
                }
            }
        }

        private static void EnsureWeaponTraceLocator(Transform weaponRoot,
            CharacterDefaultWeaponPrefabBinding binding, string path, CharacterRuntimePose pose)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            string normalized = path.Replace('\\', '/').Trim('/');
            if (normalized.Length == 0 || weaponRoot.Find(normalized) != null
                || (normalized.StartsWith("weapon/", System.StringComparison.Ordinal)
                    && weaponRoot.Find(normalized.Substring(7)) != null))
                return;

            Transform locator = weaponRoot;
            foreach (string segment in normalized.Split('/'))
            {
                if (string.IsNullOrWhiteSpace(segment))
                    continue;
                Transform child = locator.Find(segment);
                if (child == null)
                {
                    child = new GameObject(segment).transform;
                    child.SetParent(locator, false);
                }
                locator = child;
            }
            if (locator == weaponRoot)
                return;

            Vector3 position = new Vector3(pose.Position.X, pose.Position.Y, pose.Position.Z);
            Quaternion rotation = new Quaternion(pose.Rotation.X, pose.Rotation.Y, pose.Rotation.Z, pose.Rotation.W);
            Vector3 scale = new Vector3(pose.Scale.X, pose.Scale.Y, pose.Scale.Z);
            bool socketRelative = (string.Equals(pose.ParentKind, "Socket", System.StringComparison.Ordinal)
                    || string.Equals(pose.ParentKind, "Bone", System.StringComparison.Ordinal))
                && (string.Equals(pose.ParentPath, binding.SocketLabel, System.StringComparison.Ordinal)
                    || string.Equals(pose.ParentPath, binding.EquipmentSlotLabel, System.StringComparison.Ordinal));
            if (socketRelative && weaponRoot.parent != null)
            {
                Transform socket = weaponRoot.parent;
                position = weaponRoot.InverseTransformPoint(socket.TransformPoint(position));
                rotation = Quaternion.Inverse(weaponRoot.rotation) * socket.rotation * rotation;
                Vector3 divisor = weaponRoot.lossyScale;
                scale = new Vector3(
                    Mathf.Abs(divisor.x) > 0.00001f ? scale.x / divisor.x : scale.x,
                    Mathf.Abs(divisor.y) > 0.00001f ? scale.y / divisor.y : scale.y,
                    Mathf.Abs(divisor.z) > 0.00001f ? scale.z / divisor.z : scale.z);
            }
            locator.localPosition = position;
            locator.localRotation = rotation;
            locator.localScale = scale;
        }

        private CharacterBoneRuntimeSync ConfigureBoneRuntimeSync(GameObject instance)
        {
            CharacterBoneRuntimeSync sync = instance.GetComponent<CharacterBoneRuntimeSync>();
            if (sync == null)
                return null;

            sync.UseExternalRuntimeTick = true;
            if (_logSuccessfulSpawnDiagnostics)
            {
                _logger.Info(
                    "CharacterSpawn",
                    "Bone runtime sync switched to external tick. character=" + instance.name
                    + " bindingCount=" + sync.Bindings.Count);
            }
            return sync;
        }

        private CharacterProceduralBoneRig ConfigureProceduralBoneRig(
            GameObject instance,
            string generatedPackageRoot,
            CharacterImportedPackage package)
        {
            CharacterProceduralBoneRig rig = instance.GetComponent<CharacterProceduralBoneRig>();
            if (rig == null)
                rig = instance.AddComponent<CharacterProceduralBoneRig>();

            InstallProceduralHitFeedbackChains(instance, generatedPackageRoot, package);
            rig.UseExternalRuntimeTick = true;
            rig.RefreshChildren();
            if (_logSuccessfulSpawnDiagnostics)
            {
                CharacterProceduralBoneRigSnapshot snapshot = rig.CreateSnapshot();
                _logger.Info(
                    "CharacterSpawn",
                    "Procedural bone rig switched to external tick. character=" + instance.name
                    + " chains=" + snapshot.ActiveChainCount
                    + " regions=" + snapshot.KnownRegionCount
                    + " ikTargets=" + snapshot.ActiveIkTargetCount
                    + " ragdoll=" + snapshot.RagdollState);
            }
            return rig;
        }

        private void InstallProceduralHitFeedbackChains(
            GameObject instance,
            string generatedPackageRoot,
            CharacterImportedPackage package)
        {
            if (!_installDefaultProceduralHitFeedbackChain || instance == null)
                return;

            if (instance.GetComponentInChildren<CharacterSecondaryBoneChain>(true) != null)
                return;

            CharacterProceduralBodyRegionInstallResult regionResult =
                CharacterProceduralBodyRegionInstaller.InstallFromImportedPackage(instance, package);
            if (regionResult.InstalledAny)
            {
                if (_logSuccessfulSpawnDiagnostics)
                {
                    _logger.Info(
                        "CharacterSpawn",
                        "Procedural hit feedback body regions installed. character=" + instance.name
                        + " requestedRegions=" + regionResult.RequestedRegionCount
                        + " installedChains=" + regionResult.InstalledRegionCount
                        + " package=" + CharacterTestCombatTraceText.EmptyAsNone(generatedPackageRoot)
                        + " issue=" + CharacterTestCombatTraceText.EmptyAsNone(regionResult.LastIssue));
                }
                return;
            }

            if (_logSuccessfulSpawnDiagnostics)
            {
                _logger.Info(
                    "CharacterSpawn",
                    "Procedural hit feedback was disabled because no explicit imported body-region binding resolved. character="
                    + instance.name + " issue=" + CharacterTestCombatTraceText.EmptyAsNone(regionResult.LastIssue));
            }
        }

        private static string ResolveTransformPath(Transform root, Transform target)
        {
            if (root == null || target == null)
                return string.Empty;

            if (root == target)
                return root.name;

            string path = target.name;
            Transform current = target.parent;
            while (current != null && current != root)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return current == root ? root.name + "/" + path : path;
        }

        private void ConfigureControlOwnership(
            GameObject instance,
            CharacterTestCharacterControlOwner controlOwner)
        {
            CharacterRuntimeInputMotionController motion =
                instance.GetComponent<CharacterRuntimeInputMotionController>();
            if (motion == null)
            {
                if (_logSuccessfulSpawnDiagnostics)
                    _logger.Info("CharacterSpawn", "Character has no input motion controller. name=" + instance.name);
                return;
            }

            motion.EnableInputMotion = false;
            if (_logSuccessfulSpawnDiagnostics)
            {
                _logger.Info(
                    "CharacterSpawn",
                    "Prefab input motion disabled; runtime ECS motion is authoritative. character=" + instance.name
                    + " owner=" + controlOwner
                    + " inputMotion=" + motion.EnableInputMotion);
            }
        }

        private CharacterRuntimeLocomotionBlendController LogLocomotionStatus(
            GameObject instance,
            CharacterRuntimeAnimationBinding animationBinding)
        {
            CharacterRuntimeLocomotionBlendController locomotion =
                instance.GetComponent<CharacterRuntimeLocomotionBlendController>();
            if (locomotion == null)
                return null;

            locomotion.UseExternalRuntimeTick = true;
            if (_logSuccessfulSpawnDiagnostics)
            {
                _logger.Info(
                    "CharacterSpawn",
                    "Locomotion controller active. character=" + instance.name
                    + " blendId=" + locomotion.ActiveBlendId
                    + " blendKind=" + locomotion.ActiveBlendKind);
            }
            CharacterTestCharacterAnimationResources.TryConfigureLocomotion(
                locomotion,
                _resourceManager,
                animationBinding,
                instance.name,
                _logger,
                logResult: _logSuccessfulSpawnDiagnostics);
            return locomotion;
        }

        private static string FormatSpawnIssues(CharacterRuntimeSpawnResult result)
        {
            if (result == null || result.Issues.Length == 0)
                return "unknown error";

            var parts = new System.Collections.Generic.List<string>(result.Issues.Length);
            for (int i = 0; i < result.Issues.Length; i++)
                parts.Add(result.Issues[i].Code + ":" + result.Issues[i].Message);
            return string.Join("; ", parts);
        }
    }
}
