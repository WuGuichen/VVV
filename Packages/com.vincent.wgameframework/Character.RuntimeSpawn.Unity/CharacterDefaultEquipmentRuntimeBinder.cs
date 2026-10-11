using System;
using System.Collections.Generic;
using MxFramework.Resources;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MxFramework.CharacterRuntimeSpawn.Unity
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Character/Default Equipment Runtime Binder")]
    public sealed class CharacterDefaultEquipmentRuntimeBinder : MonoBehaviour
    {
        [SerializeField] private string _packageId = string.Empty;
        [SerializeField] private string _characterId = string.Empty;
        [SerializeField] private string _loadoutId = string.Empty;
        [SerializeField] private Transform _socketsRoot;
        [SerializeField] private Animator _animator;
        [SerializeField] private bool _instantiateDefaultWeaponsOnAwake;
        [SerializeField] private bool _playFirstAnimationOnStart;
        [SerializeField] private CharacterDefaultWeaponPrefabBinding[] _defaultWeapons = Array.Empty<CharacterDefaultWeaponPrefabBinding>();
        [SerializeField] private AnimationClip[] _animationClips = Array.Empty<AnimationClip>();

        private readonly List<GameObject> _spawnedWeapons = new List<GameObject>();
        private readonly List<CharacterDefaultWeaponPrefabBinding> _spawnedWeaponBindings = new List<CharacterDefaultWeaponPrefabBinding>();
        private int _equipmentRevision;
        private readonly List<ResourceHandle<GameObject>> _weaponHandles = new List<ResourceHandle<GameObject>>();
        private PlayableGraph _animationGraph;
        private AnimationClipPlayable _activeClipPlayable;
        private bool _loopAnimation;
        private IResourceManager _resourceManager;

        public string PackageId => _packageId;
        public string CharacterId => _characterId;
        public string LoadoutId => _loadoutId;
        public IReadOnlyList<CharacterDefaultWeaponPrefabBinding> DefaultWeapons => _defaultWeapons;
        public IReadOnlyList<AnimationClip> AnimationClips => _animationClips;
        public IReadOnlyList<GameObject> SpawnedWeapons => _spawnedWeapons;
        public int EquipmentRevision => _equipmentRevision;
        /// <summary>NoAlloc. Successful spawn mapping remains correct when earlier bindings fail to load.</summary>
        public bool TryGetSpawnedWeapon(int weaponId, int equipmentSlotId, out GameObject weapon)
        {
            weapon = null;
            for (int i = 0; i < _spawnedWeapons.Count; i++)
            {
                CharacterDefaultWeaponPrefabBinding binding = _spawnedWeaponBindings[i];
                if (binding.WeaponId == weaponId && binding.EquipSlot == equipmentSlotId)
                { weapon = _spawnedWeapons[i]; return weapon != null; }
            }
            return false;
        }

        private void Awake()
        {
            if (_instantiateDefaultWeaponsOnAwake)
                InstantiateDefaultWeapons();
        }

        private void Start()
        {
            if (_playFirstAnimationOnStart && _animationClips.Length > 0)
                TryPlayAnimation(0, loop: true);
        }

        private void Update()
        {
            if (!_loopAnimation || !_animationGraph.IsValid() || !_activeClipPlayable.IsValid())
                return;

            double duration = _activeClipPlayable.GetDuration();
            if (duration <= 0)
                return;

            double time = _activeClipPlayable.GetTime();
            if (time >= duration)
                _activeClipPlayable.SetTime(time % duration);
        }

        private void OnDestroy()
        {
            StopAnimation();
        }

        public void ConfigureResourceManager(IResourceManager resourceManager, bool instantiateDefaultWeapons)
        {
            _resourceManager = resourceManager;
            if (instantiateDefaultWeapons)
                InstantiateDefaultWeapons(resourceManager);
        }

        /// <summary>
        /// LowFreqAlloc: configures generated package equipment bindings after a character prefab is instantiated.
        /// </summary>
        public void ConfigureDefaultEquipment(
            string packageId,
            string characterId,
            string loadoutId,
            Transform socketsRoot,
            Animator animator,
            CharacterDefaultWeaponPrefabBinding[] defaultWeapons,
            AnimationClip[] animationClips,
            bool instantiateDefaultWeaponsOnAwake,
            bool playFirstAnimationOnStart)
        {
            _equipmentRevision++;
            _packageId = packageId ?? string.Empty;
            _characterId = characterId ?? string.Empty;
            _loadoutId = loadoutId ?? string.Empty;
            _socketsRoot = socketsRoot;
            _animator = animator;
            _defaultWeapons = defaultWeapons ?? Array.Empty<CharacterDefaultWeaponPrefabBinding>();
            _animationClips = animationClips ?? Array.Empty<AnimationClip>();
            _instantiateDefaultWeaponsOnAwake = instantiateDefaultWeaponsOnAwake;
            _playFirstAnimationOnStart = playFirstAnimationOnStart;
        }

        public void InstantiateDefaultWeapons()
        {
            InstantiateDefaultWeapons(_resourceManager);
        }

        public void InstantiateDefaultWeapons(IResourceManager resourceManager)
        {
            ClearSpawnedWeapons();
            for (int i = 0; i < _defaultWeapons.Length; i++)
            {
                CharacterDefaultWeaponPrefabBinding binding = _defaultWeapons[i];
                if (binding == null)
                    continue;

                Transform socket = binding.SocketTransform != null
                    ? binding.SocketTransform
                    : FindSocket(binding.SocketId, binding.SocketLabel);
                if (socket == null)
                {
                    Debug.LogWarning("MxFramework Character: default weapon socket was not found. weapon="
                        + binding.WeaponId + " socket=" + binding.SocketId, this);
                    continue;
                }

                GameObject prefab = LoadWeaponPrefab(resourceManager, binding, out ResourceHandle<GameObject> handle);
                if (prefab == null)
                {
                    Debug.LogWarning("MxFramework Character: default weapon prefab could not be loaded. weapon="
                        + binding.WeaponId + " resource=" + binding.ResourceId, this);
                    continue;
                }

                if (handle != null)
                    _weaponHandles.Add(handle);

                GameObject instance = Instantiate(prefab, socket);
                instance.name = "DefaultWeapon_" + SafeName(binding.EquipmentSlotLabel) + "_" + SafeName(binding.WeaponLabel);
                Vector3 prefabLocalPosition = instance.transform.localPosition;
                Quaternion prefabLocalRotation = instance.transform.localRotation;
                Vector3 prefabLocalScale = instance.transform.localScale;
                Vector3 bindingLocalScale = binding.LocalScale == Vector3.zero ? Vector3.one : binding.LocalScale;
                instance.transform.localPosition = binding.LocalPosition
                    + binding.LocalRotation * Vector3.Scale(prefabLocalPosition, bindingLocalScale);
                instance.transform.localRotation = binding.LocalRotation * prefabLocalRotation;
                instance.transform.localScale = new Vector3(
                    prefabLocalScale.x * bindingLocalScale.x,
                    prefabLocalScale.y * bindingLocalScale.y,
                    prefabLocalScale.z * bindingLocalScale.z);
                _spawnedWeapons.Add(instance);
                _spawnedWeaponBindings.Add(binding);
            }
        }

        public void DisableStandaloneAnimationPlayback()
        {
            _playFirstAnimationOnStart = false;
            StopAnimation();
        }

        public void ClearSpawnedWeapons()
        {
            for (int i = _spawnedWeapons.Count - 1; i >= 0; i--)
            {
                GameObject instance = _spawnedWeapons[i];
                if (instance == null)
                    continue;
                if (Application.isPlaying)
                    Destroy(instance);
                else
                    DestroyImmediate(instance);
            }

            _spawnedWeapons.Clear();
            _spawnedWeaponBindings.Clear();
            _equipmentRevision++;
            ReleaseWeaponHandles();
        }

        public bool TryPlayAnimation(int clipIndex, bool loop)
        {
            if (clipIndex < 0 || clipIndex >= _animationClips.Length)
                return false;

            AnimationClip clip = _animationClips[clipIndex];
            if (clip == null)
                return false;

            Animator animator = ResolveAnimator();
            if (animator == null)
                return false;

            StopAnimation();
            _animationGraph = PlayableGraph.Create("CharacterDefaultEquipmentRuntimeBinder." + gameObject.name);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(_animationGraph, "Animation", animator);
            AnimationClipPlayable playable = AnimationClipPlayable.Create(_animationGraph, clip);
            playable.SetApplyFootIK(false);
            playable.SetDuration(clip.length);
            output.SetSourcePlayable(playable);
            _activeClipPlayable = playable;
            _loopAnimation = loop;
            _animationGraph.Play();
            return true;
        }

        public void StopAnimation()
        {
            if (_animationGraph.IsValid())
                _animationGraph.Destroy();
            _activeClipPlayable = default;
            _loopAnimation = false;
        }

        private Animator ResolveAnimator()
        {
            if (_animator != null)
                return _animator;

            _animator = GetComponentInChildren<Animator>();
            if (_animator != null)
                return _animator;

            GameObject model = transform.Find("ModelRoot")?.gameObject ?? gameObject;
            _animator = model.GetComponent<Animator>();
            if (_animator == null)
                _animator = model.AddComponent<Animator>();
            return _animator;
        }

        private Transform FindSocket(int socketId, string socketLabel = null)
        {
            Transform root = _socketsRoot != null ? _socketsRoot : transform;
            if (root == null)
                return null;

            if (!string.IsNullOrWhiteSpace(socketLabel))
            {
                Transform byLabel = FindChild(root, "Socket_" + socketLabel)
                    ?? FindChild(root, socketLabel);
                if (byLabel != null)
                    return byLabel;
            }

            if (socketId <= 0)
                return null;

            return FindChild(root, "Socket_" + socketId)
                ?? FindChild(root, socketId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private GameObject LoadWeaponPrefab(IResourceManager resourceManager, CharacterDefaultWeaponPrefabBinding binding, out ResourceHandle<GameObject> handle)
        {
            handle = null;
            if (resourceManager != null && binding.HasResourceKey)
            {
                ResourceLoadResult<ResourceHandle<GameObject>> result = resourceManager.Load<GameObject>(binding.CreateResourceKey());
                if (result.Success)
                {
                    handle = result.Value;
                    return result.Value.Value;
                }

                Debug.LogWarning("MxFramework Character: resource load failed for default weapon. key="
                    + binding.CreateResourceKey() + " error=" + result.Error.Message, this);
            }

            return binding.Prefab;
        }

        private void ReleaseWeaponHandles()
        {
            if (_resourceManager == null)
            {
                _weaponHandles.Clear();
                return;
            }

            for (int i = 0; i < _weaponHandles.Count; i++)
                _resourceManager.Release(_weaponHandles[i]);
            _weaponHandles.Clear();
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null || string.IsNullOrWhiteSpace(name))
                return null;
            if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase))
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform match = FindChild(root.GetChild(i), name);
                if (match != null)
                    return match;
            }

            return null;
        }

        private static string SafeName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value.Replace(' ', '_').Replace('/', '_');
        }
    }

    [Serializable]
    public sealed class CharacterDefaultWeaponPrefabBinding
    {
        [SerializeField] private int _weaponId;
        [SerializeField] private int _equipSlot;
        [SerializeField] private int _socketId;
        [SerializeField] private string _socketLabel = string.Empty;
        [SerializeField] private string _weaponLabel = string.Empty;
        [SerializeField] private string _equipmentSlotLabel = string.Empty;
        [SerializeField] private string _traceLabel = string.Empty;
        [SerializeField] private int _traceId;
        [SerializeField] private string _resourceId = string.Empty;
        [SerializeField] private string _resourceTypeId = ResourceTypeIds.GameObject;
        [SerializeField] private string _resourceVariant = "default";
        [SerializeField] private string _resourcePackageId = string.Empty;
        [SerializeField] private Transform _socketTransform;
        [SerializeField] private GameObject _prefab;
        [SerializeField] private Vector3 _localPosition;
        [SerializeField] private Quaternion _localRotation = Quaternion.identity;
        [SerializeField] private Vector3 _localScale = Vector3.one;

        public int WeaponId => _weaponId;
        public int EquipSlot => _equipSlot;
        public int SocketId => _socketId;
        public string SocketLabel => _socketLabel;
        public string WeaponLabel => _weaponLabel;
        public string EquipmentSlotLabel => _equipmentSlotLabel;
        public string TraceLabel => _traceLabel;
        public int TraceId => _traceId;
        public string ResourceId => _resourceId;
        public string ResourceTypeId => _resourceTypeId;
        public string ResourceVariant => _resourceVariant;
        public string ResourcePackageId => _resourcePackageId;
        public bool HasResourceKey => !string.IsNullOrWhiteSpace(_resourceId);
        public Transform SocketTransform => _socketTransform;
        public GameObject Prefab => _prefab;
        public Vector3 LocalPosition => _localPosition;
        public Quaternion LocalRotation => _localRotation;
        public Vector3 LocalScale => _localScale;

        public ResourceKey CreateResourceKey()
        {
            return new ResourceKey(_resourceId, _resourceTypeId, _resourceVariant, _resourcePackageId);
        }

        /// <summary>
        /// LowFreqAlloc: fills a runtime-created default weapon binding.
        /// </summary>
        public void Configure(
            int weaponId,
            int equipSlot,
            int socketId,
            int traceId,
            string resourceId,
            string resourceTypeId,
            string resourceVariant,
            string resourcePackageId,
            Transform socketTransform,
            GameObject prefab,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            string socketLabel = "",
            string weaponLabel = "", string equipmentSlotLabel = "", string traceLabel = "")
        {
            _weaponId = weaponId;
            _equipSlot = equipSlot;
            _socketId = socketId;
            _socketLabel = socketLabel ?? string.Empty;
            _weaponLabel = weaponLabel ?? string.Empty;
            _equipmentSlotLabel = equipmentSlotLabel ?? string.Empty;
            _traceLabel = traceLabel ?? string.Empty;
            _traceId = traceId;
            _resourceId = resourceId ?? string.Empty;
            _resourceTypeId = string.IsNullOrWhiteSpace(resourceTypeId) ? ResourceTypeIds.GameObject : resourceTypeId;
            _resourceVariant = resourceVariant ?? string.Empty;
            _resourcePackageId = resourcePackageId ?? string.Empty;
            _socketTransform = socketTransform;
            _prefab = prefab;
            _localPosition = localPosition;
            _localRotation = localRotation;
            _localScale = localScale == Vector3.zero ? Vector3.one : localScale;
        }
    }
}
