using System;
using System.Collections.Generic;
using MxFramework.CharacterApplication;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MxFramework.Editor.CharacterPreview
{
    /// <summary>Owns an editor-only visual instance. It never plays animation or modifies source assets.</summary>
    public sealed class CharacterModelPreviewSession : IDisposable
    {
        private readonly CharacterModelPreviewChoice _choice;
        private readonly ResourceManager _resources = new ResourceManager();
        private readonly Dictionary<int, Transform> _sockets = new Dictionary<int, Transform>();
        private List<GameObject> _weapons = new List<GameObject>();
        private List<ResourceHandle<GameObject>> _weaponHandles = new List<ResourceHandle<GameObject>>();
        private GameObject _body;
        public GameObject Instance { get; private set; }
        public Animator Animator { get; private set; }
        public string CharacterKey => _choice.Key;
        public string LoadoutKey { get; private set; }
        public bool WeaponsHidden { get; private set; }
        public int WeaponCount => _weapons.Count;

        private CharacterModelPreviewSession(CharacterModelPreviewChoice choice) { _choice = choice; }

        /// <summary>LowFreqAlloc: builds a visual instance in the supplied preview scene; failure releases all owned objects.</summary>
        public static bool TryCreate(CharacterModelPreviewChoice choice, string loadoutKey, bool hideWeapons,
            Scene scene, out CharacterModelPreviewSession session, out string diagnostic)
        {
            session = null;
            diagnostic = string.Empty;
            if (choice == null || !scene.IsValid() || !UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(scene))
            { diagnostic = "需要有效角色和独立预览场景。"; return false; }
            var candidate = new CharacterModelPreviewSession(choice);
            try
            {
                candidate.Build(scene);
                if (!candidate.TrySetEquipment(loadoutKey, hideWeapons, out diagnostic))
                { candidate.Dispose(); return false; }
                candidate.Instance.SetActive(true);
                session = candidate;
                return true;
            }
            catch (Exception ex)
            {
                diagnostic = ex.Message;
                candidate.Dispose();
                return false;
            }
        }

        private void Build(Scene scene)
        {
            var package = _choice.Package;
            string path = CharacterRuntimePrefabResourceCatalog.GetGeneratedCharacterPrefabPath(package.RootPath, package.PackageId);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("缺少角色预览 Prefab：" + path);
            Instance = new GameObject("Character Model Preview — " + _choice.Label);
            Instance.SetActive(false);
            SceneManager.MoveGameObjectToScene(Instance, scene);
            Instance.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            _body = Object.Instantiate(prefab, Instance.transform, false);
            DisableRuntimeBehaviours(_body);
            Animator = _body.GetComponentInChildren<Animator>(true);
            if (Animator == null || Animator.avatar == null || !Animator.avatar.isValid)
                throw new InvalidOperationException("角色缺少有效 Animator / Avatar：" + path);
            Animator.runtimeAnimatorController = null;
            Animator.applyRootMotion = true;
            Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Animator.enabled = true;
            var composition = CharacterRuntimePrefabComposer.Compose(_body,
                new CharacterRuntimePrefabCompositionOptions(package, null)
                {
                    InstallDefaultEquipmentBinder = false, InstantiateDefaultWeapons = false,
                    InstallControllerBinding = false, InitializeControllerBinding = false,
                    InstallInputMotionController = false, EnableInputMotion = false,
                    InstallLocomotionController = false, InstallBoneRuntimeSync = true, UseExternalRuntimeTick = true
                });
            if (!composition.Success || composition.BoneSync == null)
                throw new InvalidOperationException("角色骨骼装配失败：" + string.Join("; ", composition.Issues));
            foreach (var binding in composition.BoneSync.Bindings)
            {
                if (binding.SocketId <= 0 || binding.Target == null || binding.Bone == null) continue;
                // The animation editor can alter poses outside the project's tick. Parenting follows every sample immediately.
                binding.Target.SetParent(binding.Bone, true);
                _sockets.Add(binding.SocketId, binding.Target);
            }
            composition.BoneSync.enabled = false;
            _resources.RegisterProvider(new EditorAssetDatabaseResourceProvider());
            _resources.RegisterProvider(new ResourcesProvider());
            var local = new List<ResourceCatalogEntry>();
            CharacterRuntimePrefabResourceCatalog.AddWeaponPrefabEntries(package, package.RootPath, local);
            _resources.AddCatalog(new ResourceCatalog("character-model-preview-local", package.PackageId, local));
            var plan = GlobalPlayerResourceBuildProfileBuilder.CreateBuildPlan(GlobalPlayerResourceBuildProfileBuilder.ProfilePath);
            if (plan != null)
            {
                var catalog = GlobalPlayerResourceBuildProfileBuilder.CreateEditorAssetDatabaseCatalog(plan);
                if (catalog != null)
                {
                    var entries = new List<ResourceCatalogEntry>();
                    foreach (var entry in catalog.Entries)
                        if (entry.TypeId == ResourceTypeIds.GameObject)
                            entries.Add(new ResourceCatalogEntry(entry.Id, entry.TypeId, entry.ProviderId, entry.Address,
                                entry.Variant, entry.Id.StartsWith("weapon.", StringComparison.Ordinal) ? "common" : entry.PackageId,
                                entry.Dependencies, entry.Labels, entry.Hash, entry.Size, entry.AllowOverride, entry.ProviderData));
                    _resources.AddCatalog(new ResourceCatalog(catalog.CatalogId + ".modelPreview", catalog.PackageId, entries));
                }
            }
        }

        /// <summary>LowFreqAlloc: stages all weapons before replacing the active loadout; failure leaves the active equipment intact.</summary>
        public bool TrySetEquipment(string loadoutKey, bool hideWeapons, out string diagnostic)
        {
            diagnostic = string.Empty;
            if (Instance == null) { diagnostic = "临时角色已释放。"; return false; }
            if (LoadoutKey == loadoutKey)
            {
                foreach (var weapon in _weapons) weapon.SetActive(!hideWeapons);
                WeaponsHidden = hideWeapons;
                return true;
            }
            var objects = new List<GameObject>();
            var handles = new List<ResourceHandle<GameObject>>();
            GameObject staging = null;
            try
            {
                var package = _choice.Package;
                EquipmentLoadoutConfig loadout = null;
                foreach (var entry in package.Configs.Loadouts)
                    if (entry.StableId == loadoutKey && entry.EquipmentSchemaId.Equals(_choice.Character.EquipmentSchemaId))
                        loadout = entry;
                if (loadout == null) throw new InvalidOperationException("装备组合已失效：" + loadoutKey);
                var resolved = EquipmentStateResolver.Resolve(package.Configs.FindEquipmentSchema(_choice.Character.EquipmentSchemaId),
                    loadout, package.Configs.Weapons, package.Configs.EquipmentStates);
                if (!resolved.IsSuccess) throw new InvalidOperationException("装备组合无法解析：" + loadoutKey);
                staging = new GameObject("Equipment staging");
                staging.SetActive(false);
                SceneManager.MoveGameObjectToScene(staging, Instance.scene);
                var mounted = new HashSet<(int, int)>();
                var parents = new List<Transform>();
                foreach (EquippedWeaponSlot slot in resolved.EquippedWeapons)
                {
                    if (slot.Weapon == null) throw new InvalidOperationException("组合包含缺失武器：" + loadoutKey);
                    if (slot.Weapon.Category == WeaponCategory.Unarmed) continue;
                    int slotId = slot.MountedSlotId > 0 ? slot.MountedSlotId : slot.SlotId;
                    if (!mounted.Add((slotId, slot.Weapon.WeaponId.Value))) continue;
                    CharacterWeaponAttachmentRuntimeBinding attachment = null;
                    foreach (var binding in package.Geometry.WeaponAttachments)
                        if (binding.WeaponId == slot.Weapon.WeaponId.Value && binding.EquipSlot == slotId) { attachment = binding; break; }
                    if (attachment == null) throw new InvalidOperationException("缺少武器挂接配置：" + slot.Weapon.StableId + " / " + slotId);
                    if (!_sockets.TryGetValue(attachment.AttachSocketId, out Transform socket))
                        throw new InvalidOperationException("缺少武器挂点或对应骨骼：" + attachment.AttachSocketLabel);
                    var key = new ResourceKey(CharacterRuntimePrefabResourceCatalog.GetDefaultWeaponResourceId(package, attachment),
                        ResourceTypeIds.GameObject, CharacterRuntimePrefabResourceCatalog.GetDefaultWeaponResourceVariant(attachment),
                        CharacterRuntimePrefabResourceCatalog.GetDefaultWeaponResourcePackageId(package, attachment));
                    var loaded = _resources.Load<GameObject>(key);
                    if (!loaded.Success || loaded.Value?.Value == null)
                        throw new InvalidOperationException("武器资源加载失败：" + key + " — " + loaded.Error.Message);
                    handles.Add(loaded.Value);
                    GameObject weapon = Object.Instantiate(loaded.Value.Value, staging.transform, false);
                    objects.Add(weapon);
                    parents.Add(socket);
                    DisableRuntimeBehaviours(weapon);
                    Vector3 slotPosition = Vector(attachment.SlotPose.Position, Vector3.zero);
                    Quaternion slotRotation = Rotation(attachment.SlotPose.Rotation);
                    Vector3 slotScale = Vector(attachment.SlotPose.Scale, Vector3.one);
                    weapon.transform.localPosition = slotPosition + slotRotation * Vector3.Scale(slotScale, Vector(attachment.WeaponModelPose.Position, Vector3.zero));
                    weapon.transform.localRotation = slotRotation * Rotation(attachment.WeaponModelPose.Rotation);
                    weapon.transform.localScale = Vector3.Scale(slotScale, Vector(attachment.WeaponModelPose.Scale, Vector3.one));
                    weapon.SetActive(false);
                }
                for (int i = 0; i < objects.Count; i++) objects[i].transform.SetParent(parents[i], false);
                ReleaseWeapons(_weapons, _weaponHandles);
                _weapons = objects;
                _weaponHandles = handles;
                LoadoutKey = loadoutKey;
                WeaponsHidden = hideWeapons;
                foreach (var weapon in _weapons) weapon.SetActive(!hideWeapons);
                return true;
            }
            catch (Exception ex)
            {
                ReleaseWeapons(objects, handles);
                diagnostic = ex.Message;
                return false;
            }
            finally { if (staging != null) Object.DestroyImmediate(staging); }
        }

        private static void DisableRuntimeBehaviours(GameObject root)
        {
            foreach (Behaviour behaviour in root.GetComponentsInChildren<Behaviour>(true)) behaviour.enabled = false;
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.detectCollisions = false; }
        }

        private static Vector3 Vector(CharacterRuntimeVector3 value, Vector3 fallback)
        {
            var result = new Vector3(value.X, value.Y, value.Z);
            return result == Vector3.zero ? fallback : result;
        }
        private static Quaternion Rotation(CharacterRuntimeQuaternion value) => new Quaternion(value.X, value.Y, value.Z, value.W);
        private void ReleaseWeapons(List<GameObject> objects, List<ResourceHandle<GameObject>> handles)
        {
            foreach (var item in objects) if (item != null) Object.DestroyImmediate(item);
            foreach (var handle in handles) _resources.Release(handle);
            objects.Clear(); handles.Clear();
        }

        public void Dispose()
        {
            ReleaseWeapons(_weapons, _weaponHandles);
            if (Instance != null) Object.DestroyImmediate(Instance);
            Instance = null; Animator = null; _body = null;
        }
    }
}
