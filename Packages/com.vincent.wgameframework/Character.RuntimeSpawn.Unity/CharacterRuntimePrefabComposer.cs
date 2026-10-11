using System;
using System.Collections.Generic;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Resources;
using UnityEngine;

namespace MxFramework.CharacterRuntimeSpawn.Unity
{
    public sealed class CharacterRuntimePrefabCompositionOptions
    {
        public CharacterRuntimePrefabCompositionOptions(
            CharacterImportedPackage package,
            CharacterRuntimeSpawnResult spawnResult)
        {
            Package = package;
            SpawnResult = spawnResult;
            AnimationClips = Array.Empty<AnimationClip>();
        }

        public CharacterImportedPackage Package { get; }
        public CharacterRuntimeSpawnResult SpawnResult { get; }
        public IResourceManager ResourceManager { get; set; }
        public AnimationClip[] AnimationClips { get; set; }
        public bool RequireDescriptor { get; set; } = true;
        public bool InstallDefaultEquipmentBinder { get; set; } = true;
        public bool InstantiateDefaultWeapons { get; set; } = true;
        public bool InstallControllerBinding { get; set; } = true;
        public bool InitializeControllerBinding { get; set; } = true;
        public bool InstallInputMotionController { get; set; }
        public bool EnableInputMotion { get; set; } = true;
        public bool InstallLocomotionController { get; set; } = true;
        public bool InstallBoneRuntimeSync { get; set; } = true;
        public bool UseExternalRuntimeTick { get; set; }
        public bool PlayFirstAnimationOnStart { get; set; }
    }

    public sealed class CharacterRuntimePrefabCompositionResult
    {
        internal CharacterRuntimePrefabCompositionResult(
            bool success,
            string[] issues,
            CharacterRuntimePrefabDescriptor descriptor,
            CharacterDefaultEquipmentRuntimeBinder defaultEquipmentBinder,
            CharacterRuntimeControllerBinding controllerBinding,
            CharacterRuntimeInputMotionController inputMotionController,
            CharacterRuntimeLocomotionBlendController locomotionController,
            CharacterBoneRuntimeSync boneSync)
        {
            Success = success;
            Issues = issues ?? Array.Empty<string>();
            Descriptor = descriptor;
            DefaultEquipmentBinder = defaultEquipmentBinder;
            ControllerBinding = controllerBinding;
            InputMotionController = inputMotionController;
            LocomotionController = locomotionController;
            BoneSync = boneSync;
        }

        public bool Success { get; }
        public string[] Issues { get; }
        public CharacterRuntimePrefabDescriptor Descriptor { get; }
        public CharacterDefaultEquipmentRuntimeBinder DefaultEquipmentBinder { get; }
        public CharacterRuntimeControllerBinding ControllerBinding { get; }
        public CharacterRuntimeInputMotionController InputMotionController { get; }
        public CharacterRuntimeLocomotionBlendController LocomotionController { get; }
        public CharacterBoneRuntimeSync BoneSync { get; }
    }

    public static class CharacterRuntimePrefabComposer
    {
        /// <summary>
        /// LowFreqAlloc: installs runtime-only character components on an instantiated exported character prefab.
        /// </summary>
        public static CharacterRuntimePrefabCompositionResult Compose(
            GameObject instance,
            CharacterRuntimePrefabCompositionOptions options)
        {
            var issues = new List<string>();
            if (instance == null)
            {
                issues.Add("Character prefab instance is missing.");
                return CreateResult(false, issues, null, null, null, null, null, null);
            }

            if (options == null)
            {
                issues.Add("Composition options are missing.");
                return CreateResult(false, issues, null, null, null, null, null, null);
            }

            CharacterImportedPackage package = options.Package;
            if (package == null)
                issues.Add("Imported package is missing.");

            CharacterRuntimeSpawnResult spawnResult = options.SpawnResult;
            if ((spawnResult == null || !spawnResult.IsSuccess) && package != null)
                spawnResult = CharacterRuntimeSpawnResolver.Resolve(package, default);
            if (spawnResult == null || !spawnResult.IsSuccess)
                issues.Add("Runtime spawn result is missing or failed.");

            CharacterRuntimePrefabDescriptor descriptor = instance.GetComponent<CharacterRuntimePrefabDescriptor>();
            if (options.RequireDescriptor && descriptor == null)
                issues.Add("CharacterRuntimePrefabDescriptor is missing.");

            if (issues.Count > 0)
                return CreateResult(false, issues, descriptor, null, null, null, null, null);

            Transform modelRoot = ResolveModelRoot(instance.transform);
            Animator animator = instance.GetComponentInChildren<Animator>(includeInactive: true);
            Transform socketsRoot = FindOrCreateChild(instance.transform, "Sockets");

            CharacterRuntimeControllerBinding controllerBinding = null;
            if (options.InstallControllerBinding)
                controllerBinding = ConfigureControllerBinding(instance, package, spawnResult, options.InitializeControllerBinding);

            CharacterRuntimeInputMotionController inputMotion = null;
            if (options.InstallInputMotionController)
            {
                inputMotion = instance.GetComponent<CharacterRuntimeInputMotionController>();
                if (inputMotion == null)
                    inputMotion = instance.AddComponent<CharacterRuntimeInputMotionController>();
                inputMotion.EnableInputMotion = options.EnableInputMotion;
            }

            CharacterRuntimeLocomotionBlendController locomotion = null;
            if (options.InstallLocomotionController)
            {
                locomotion = instance.GetComponent<CharacterRuntimeLocomotionBlendController>();
                if (locomotion == null)
                    locomotion = instance.AddComponent<CharacterRuntimeLocomotionBlendController>();
                locomotion.UseExternalRuntimeTick = options.UseExternalRuntimeTick;
                locomotion.Configure(modelRoot, animator, options.AnimationClips ?? Array.Empty<AnimationClip>());
            }

            CharacterBoneRuntimeSync boneSync = null;
            if (options.InstallBoneRuntimeSync)
            {
                boneSync = instance.GetComponent<CharacterBoneRuntimeSync>();
                if (boneSync == null)
                    boneSync = instance.AddComponent<CharacterBoneRuntimeSync>();
                boneSync.Configure(
                    BuildBoneBindings(package, instance.transform, modelRoot, issues),
                    options.UseExternalRuntimeTick);
                boneSync.ApplyBindings();
            }

            CharacterDefaultEquipmentRuntimeBinder binder = null;
            if (options.InstallDefaultEquipmentBinder)
            {
                binder = instance.GetComponent<CharacterDefaultEquipmentRuntimeBinder>();
                if (binder == null)
                    binder = instance.AddComponent<CharacterDefaultEquipmentRuntimeBinder>();
                binder.ConfigureDefaultEquipment(
                    package.PackageId,
                    spawnResult.Binding.SpawnPlan.CharacterId.ToString(),
                    spawnResult.Binding.SpawnPlan.LoadoutId.ToString(),
                    socketsRoot,
                    animator,
                    BuildDefaultWeaponBindings(package, spawnResult, socketsRoot),
                    options.AnimationClips ?? Array.Empty<AnimationClip>(),
                    instantiateDefaultWeaponsOnAwake: false,
                    options.PlayFirstAnimationOnStart);
                binder.ConfigureResourceManager(options.ResourceManager, options.InstantiateDefaultWeapons);
            }

            return CreateResult(issues.Count == 0, issues, descriptor, binder, controllerBinding, inputMotion, locomotion, boneSync);
        }

        private static CharacterRuntimePrefabCompositionResult CreateResult(
            bool success,
            List<string> issues,
            CharacterRuntimePrefabDescriptor descriptor,
            CharacterDefaultEquipmentRuntimeBinder binder,
            CharacterRuntimeControllerBinding controllerBinding,
            CharacterRuntimeInputMotionController inputMotion,
            CharacterRuntimeLocomotionBlendController locomotion,
            CharacterBoneRuntimeSync boneSync)
        {
            return new CharacterRuntimePrefabCompositionResult(
                success,
                issues == null ? Array.Empty<string>() : issues.ToArray(),
                descriptor,
                binder,
                controllerBinding,
                inputMotion,
                locomotion,
                boneSync);
        }

        private static CharacterRuntimeControllerBinding ConfigureControllerBinding(
            GameObject instance,
            CharacterImportedPackage package,
            CharacterRuntimeSpawnResult spawnResult,
            bool initialize)
        {
            CharacterRuntimeControllerBinding controllerBinding = instance.GetComponent<CharacterRuntimeControllerBinding>();
            if (controllerBinding == null)
                controllerBinding = instance.AddComponent<CharacterRuntimeControllerBinding>();

            controllerBinding.ConfigureFromRuntimeBinding(package.PackageId, spawnResult.Binding);
            if (initialize)
                controllerBinding.Initialize();
            return controllerBinding;
        }

        private static CharacterDefaultWeaponPrefabBinding[] BuildDefaultWeaponBindings(
            CharacterImportedPackage package,
            CharacterRuntimeSpawnResult spawnResult,
            Transform socketsRoot)
        {
            CharacterWeaponAttachmentRuntimeBinding[] attachments =
                spawnResult.Binding.WeaponAttachmentBindingPlan?.Attachments
                ?? package.Geometry?.WeaponAttachments
                ?? Array.Empty<CharacterWeaponAttachmentRuntimeBinding>();
            var bindings = new List<CharacterDefaultWeaponPrefabBinding>(attachments.Length);
            for (int i = 0; i < attachments.Length; i++)
            {
                CharacterWeaponAttachmentRuntimeBinding attachment = attachments[i];
                if (attachment == null || string.IsNullOrWhiteSpace(CharacterRuntimePrefabResourceCatalog.GetWeaponVisualResourceKey(attachment)))
                    continue;

                var binding = new CharacterDefaultWeaponPrefabBinding();
                ComposeWeaponPose(attachment, out Vector3 localPosition, out Quaternion localRotation, out Vector3 localScale);
                binding.Configure(
                    attachment.WeaponId,
                    attachment.EquipSlot,
                    attachment.AttachSocketId,
                    attachment.TraceId,
                    CharacterRuntimePrefabResourceCatalog.GetDefaultWeaponResourceId(package, attachment),
                    ResourceTypeIds.GameObject,
                    CharacterRuntimePrefabResourceCatalog.GetDefaultWeaponResourceVariant(attachment),
                    CharacterRuntimePrefabResourceCatalog.GetDefaultWeaponResourcePackageId(package, attachment),
                    FindSocket(socketsRoot, attachment.AttachSocketId, attachment.AttachSocketLabel),
                    prefab: null,
                    localPosition,
                    localRotation,
                    localScale,
                    socketLabel: attachment.AttachSocketLabel,
                    weaponLabel: attachment.WeaponLabel, equipmentSlotLabel: attachment.EquipmentSlotLabel, traceLabel: attachment.TraceLabel);
                bindings.Add(binding);
            }

            return bindings.ToArray();
        }

        private static void ComposeWeaponPose(
            CharacterWeaponAttachmentRuntimeBinding attachment,
            out Vector3 position,
            out Quaternion rotation,
            out Vector3 scale)
        {
            Vector3 slotPosition = ToVector3(attachment.SlotPose.Position, Vector3.zero);
            Quaternion slotRotation = ToQuaternion(attachment.SlotPose.Rotation);
            Vector3 slotScale = ToVector3(attachment.SlotPose.Scale, Vector3.one);
            Vector3 weaponPosition = ToVector3(attachment.WeaponModelPose.Position, Vector3.zero);
            Quaternion weaponRotation = ToQuaternion(attachment.WeaponModelPose.Rotation);
            Vector3 weaponScale = ToVector3(attachment.WeaponModelPose.Scale, Vector3.one);
            position = slotPosition + slotRotation * Vector3.Scale(slotScale, weaponPosition);
            rotation = slotRotation * weaponRotation;
            scale = Vector3.Scale(slotScale, weaponScale);
        }

        private static CharacterBoneRuntimeSyncBinding[] BuildBoneBindings(
            CharacterImportedPackage package,
            Transform instanceRoot,
            Transform modelRoot,
            List<string> issues)
        {
            CharacterImportedGeometryBinding geometry = package.Geometry;
            Transform socketsRoot = FindOrCreateChild(instanceRoot, "Sockets");
            Transform collidersRoot = FindChild(instanceRoot, "AuthoringColliders");
            var bindings = new List<CharacterBoneRuntimeSyncBinding>();

            CharacterSocketRuntimeBinding[] sockets = geometry.Sockets ?? Array.Empty<CharacterSocketRuntimeBinding>();
            for (int i = 0; i < sockets.Length; i++)
            {
                CharacterSocketRuntimeBinding socket = sockets[i];
                if (socket == null)
                    continue;

                Transform bone = FindBodyChild(modelRoot, socket.BonePath, socket.LocalPose.ParentPath);
                Transform target = EnsureSocketTarget(socketsRoot, socket, bone);
                if (target == null || bone == null)
                    continue;

                bindings.Add(CharacterBoneRuntimeSyncBinding.FromWorld(
                    "socket:" + socket.SocketLabel,
                    socket.BonePath,
                    target,
                    bone,
                    socketId: socket.SocketId));
            }

            CharacterBodyColliderRuntimeBinding[] colliders = geometry.BodyColliders ?? Array.Empty<CharacterBodyColliderRuntimeBinding>();
            for (int i = 0; i < colliders.Length; i++)
            {
                CharacterBodyColliderRuntimeBinding collider = colliders[i];
                if (collider == null)
                    continue;

                Transform target = FindChild(collidersRoot, "Collider_" + collider.ColliderId);
                Transform bone = FindBodyChild(modelRoot, collider.LocalPose.ParentPath, collider.PartLabel);
                if (target == null || bone == null)
                    continue;

                bindings.Add(CharacterBoneRuntimeSyncBinding.FromWorld(
                    "collider:" + collider.ColliderId,
                    collider.LocalPose.ParentPath,
                    target,
                    bone));
            }

            return bindings.ToArray();
        }

        private static Transform ResolveModelRoot(Transform instanceRoot)
        {
            return FindChild(instanceRoot, "ModelRoot") ?? instanceRoot;
        }

        private static Transform FindSocket(Transform socketsRoot, int socketId, string socketLabel = null)
        {
            if (socketsRoot == null)
                return null;

            if (!string.IsNullOrWhiteSpace(socketLabel))
            {
                Transform byLabel = FindChild(socketsRoot, "Socket_" + socketLabel)
                    ?? FindChild(socketsRoot, socketLabel);
                if (byLabel != null)
                    return byLabel;
            }

            if (socketId <= 0)
                return null;

            return FindChild(socketsRoot, "Socket_" + socketId)
                ?? FindChild(socketsRoot, socketId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private static Transform EnsureSocketTarget(
            Transform socketsRoot,
            CharacterSocketRuntimeBinding socket,
            Transform bone)
        {
            if (socketsRoot == null || socket == null || bone == null || socket.SocketId <= 0)
                return null;

            Vector3 localPosition = ToVector3(socket.LocalPose.Position, Vector3.zero);
            Quaternion localRotation = ToQuaternion(socket.LocalPose.Rotation);
            Vector3 localScale = ToVector3(socket.LocalPose.Scale, Vector3.one);

            Transform target = FindSocket(socketsRoot, socket.SocketId, socket.SocketLabel);
            if (target != null)
            {
                target.SetPositionAndRotation(
                    bone.position + (bone.rotation * localPosition),
                    bone.rotation * localRotation);
                target.localScale = localScale;
                return target;
            }

            string markerName = !string.IsNullOrWhiteSpace(socket.SocketLabel)
                ? "Socket_" + socket.SocketLabel
                : "Socket_" + socket.SocketId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var marker = new GameObject(markerName).transform;
            marker.SetParent(socketsRoot, false);
            marker.SetPositionAndRotation(
                bone.position + (bone.rotation * localPosition),
                bone.rotation * localRotation);
            marker.localScale = localScale;
            return marker;
        }

        private static Transform FindBodyChild(Transform root, string path, string fallbackKey)
        {
            if (root == null)
                return null;

            string normalized = (path ?? string.Empty).StartsWith("bone.", StringComparison.Ordinal)
                ? path.Substring("bone.".Length)
                : path ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                Transform direct = root.Find(normalized);
                if (direct != null)
                    return direct;
            }

            string last = normalized.Contains("/")
                ? normalized.Substring(normalized.LastIndexOf("/", StringComparison.Ordinal) + 1)
                : normalized;
            foreach (string candidate in GetBoneNameCandidates(last, fallbackKey))
            {
                Transform match = FindChild(root, candidate);
                if (match != null)
                    return match;
            }

            return null;
        }

        private static IEnumerable<string> GetBoneNameCandidates(string name, string fallbackKey)
        {
            if (!string.IsNullOrWhiteSpace(name))
                yield return name;

            string key = (name + " " + fallbackKey).ToLowerInvariant();
            if (key.Contains("righthand") || key.Contains("right_hand") || key.Contains("hand.r"))
            {
                yield return "hand.R";
                yield return "RightHand";
                yield return "right_hand";
            }
            if (key.Contains("lefthand") || key.Contains("left_hand") || key.Contains("hand.l"))
            {
                yield return "hand.L";
                yield return "LeftHand";
                yield return "left_hand";
            }
            if (key.Contains("upper_arm.r") || key.Contains("upper_arm_r") || key.Contains("rightarm"))
            {
                yield return "upper_arm.R";
                yield return "RightArm";
                yield return "right_arm";
            }
            if (key.Contains("upper_arm.l") || key.Contains("upper_arm_l") || key.Contains("leftarm"))
            {
                yield return "upper_arm.L";
                yield return "LeftArm";
                yield return "left_arm";
            }
            if (key.Contains("forearm.r") || key.Contains("forearm_r") || key.Contains("rightforearm"))
            {
                yield return "forearm.R";
                yield return "RightForeArm";
                yield return "RightForearm";
                yield return "right_forearm";
            }
            if (key.Contains("forearm.l") || key.Contains("forearm_l") || key.Contains("leftforearm"))
            {
                yield return "forearm.L";
                yield return "LeftForeArm";
                yield return "LeftForearm";
                yield return "left_forearm";
            }
            if (key.Contains("thigh.r") || key.Contains("thigh_r") || key.Contains("rightupleg"))
            {
                yield return "thigh.R";
                yield return "RightUpLeg";
                yield return "right_up_leg";
            }
            if (key.Contains("thigh.l") || key.Contains("thigh_l") || key.Contains("leftupleg"))
            {
                yield return "thigh.L";
                yield return "LeftUpLeg";
                yield return "left_up_leg";
            }
            if (key.Contains("shin.r") || key.Contains("shin_r") || key.Contains("rightleg"))
            {
                yield return "shin.R";
                yield return "RightLeg";
                yield return "right_leg";
            }
            if (key.Contains("shin.l") || key.Contains("shin_l") || key.Contains("leftleg"))
            {
                yield return "shin.L";
                yield return "LeftLeg";
                yield return "left_leg";
            }
            if (key.Contains("foot.r") || key.Contains("foot_r") || key.Contains("rightfoot"))
            {
                yield return "foot.R";
                yield return "RightFoot";
                yield return "right_foot";
            }
            if (key.Contains("foot.l") || key.Contains("foot_l") || key.Contains("leftfoot"))
            {
                yield return "foot.L";
                yield return "LeftFoot";
                yield return "left_foot";
            }
            if (key.Contains("head"))
            {
                yield return "spine.006_end";
                yield return "spine.006";
                yield return "Head";
            }
            if (key.Contains("chest"))
            {
                yield return "spine.004";
                yield return "spine.003";
                yield return "spine.002";
            }
            if (key.Contains("spine") || key.Contains("torso"))
            {
                yield return "spine.003";
                yield return "spine.002";
                yield return "spine";
            }
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null || string.IsNullOrWhiteSpace(name))
                return null;
            if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase))
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindChild(root.GetChild(i), name);
                if (result != null)
                    return result;
            }

            return null;
        }

        private static Transform FindOrCreateChild(Transform root, string name)
        {
            if (root == null || string.IsNullOrWhiteSpace(name))
                return null;

            Transform child = FindChild(root, name);
            if (child != null)
                return child;

            child = new GameObject(name).transform;
            child.SetParent(root, false);
            return child;
        }

        private static Vector3 ToVector3(CharacterRuntimeVector3 value, Vector3 fallback)
        {
            Vector3 vector = new Vector3(value.X, value.Y, value.Z);
            return vector == Vector3.zero && fallback == Vector3.one ? fallback : vector;
        }

        private static Quaternion ToQuaternion(CharacterRuntimeQuaternion value)
        {
            Quaternion q = new Quaternion(value.X, value.Y, value.Z, value.W);
            return q == default ? Quaternion.identity : q;
        }
    }
}
