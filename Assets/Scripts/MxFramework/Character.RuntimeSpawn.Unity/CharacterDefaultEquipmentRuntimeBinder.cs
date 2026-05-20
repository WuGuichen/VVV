using System;
using System.Collections.Generic;
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
        [SerializeField] private Transform _authoringPreviewWeaponsRoot;
        [SerializeField] private Animator _animator;
        [SerializeField] private bool _instantiateDefaultWeaponsOnAwake = true;
        [SerializeField] private bool _playFirstAnimationOnStart;
        [SerializeField] private CharacterDefaultWeaponPrefabBinding[] _defaultWeapons = Array.Empty<CharacterDefaultWeaponPrefabBinding>();
        [SerializeField] private AnimationClip[] _animationClips = Array.Empty<AnimationClip>();

        private readonly List<GameObject> _spawnedWeapons = new List<GameObject>();
        private PlayableGraph _animationGraph;
        private AnimationClipPlayable _activeClipPlayable;
        private bool _loopAnimation;

        public string PackageId => _packageId;
        public string CharacterId => _characterId;
        public string LoadoutId => _loadoutId;
        public IReadOnlyList<CharacterDefaultWeaponPrefabBinding> DefaultWeapons => _defaultWeapons;
        public IReadOnlyList<AnimationClip> AnimationClips => _animationClips;
        public IReadOnlyList<GameObject> SpawnedWeapons => _spawnedWeapons;

        private void Awake()
        {
            DestroyAuthoringPreviewWeapons();
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

        public void InstantiateDefaultWeapons()
        {
            ClearSpawnedWeapons();
            for (int i = 0; i < _defaultWeapons.Length; i++)
            {
                CharacterDefaultWeaponPrefabBinding binding = _defaultWeapons[i];
                if (binding == null || binding.Prefab == null)
                    continue;

                Transform socket = binding.SocketTransform != null
                    ? binding.SocketTransform
                    : FindSocket(binding.SocketId);
                if (socket == null)
                {
                    Debug.LogWarning("MxFramework Character: default weapon socket was not found. weapon="
                        + binding.WeaponId + " socket=" + binding.SocketId, this);
                    continue;
                }

                GameObject instance = Instantiate(binding.Prefab, socket);
                instance.name = "DefaultWeapon_" + SafeName(binding.EquipSlot) + "_" + SafeName(binding.WeaponId);
                instance.transform.localPosition = binding.LocalPosition;
                instance.transform.localRotation = binding.LocalRotation;
                instance.transform.localScale = binding.LocalScale == Vector3.zero ? Vector3.one : binding.LocalScale;
                _spawnedWeapons.Add(instance);
            }
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
        }

        public void DestroyAuthoringPreviewWeapons()
        {
            if (_authoringPreviewWeaponsRoot == null)
                return;

            GameObject preview = _authoringPreviewWeaponsRoot.gameObject;
            _authoringPreviewWeaponsRoot = null;
            if (Application.isPlaying)
                Destroy(preview);
            else
                DestroyImmediate(preview);
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

        private Transform FindSocket(string socketId)
        {
            if (string.IsNullOrWhiteSpace(socketId))
                return null;

            Transform root = _socketsRoot != null ? _socketsRoot : transform;
            return FindChild(root, "Socket_" + socketId) ?? FindChild(root, socketId);
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
        [SerializeField] private string _weaponId = string.Empty;
        [SerializeField] private string _equipSlot = string.Empty;
        [SerializeField] private string _socketId = string.Empty;
        [SerializeField] private string _traceId = string.Empty;
        [SerializeField] private Transform _socketTransform;
        [SerializeField] private GameObject _prefab;
        [SerializeField] private Vector3 _localPosition;
        [SerializeField] private Quaternion _localRotation = Quaternion.identity;
        [SerializeField] private Vector3 _localScale = Vector3.one;

        public string WeaponId => _weaponId;
        public string EquipSlot => _equipSlot;
        public string SocketId => _socketId;
        public string TraceId => _traceId;
        public Transform SocketTransform => _socketTransform;
        public GameObject Prefab => _prefab;
        public Vector3 LocalPosition => _localPosition;
        public Quaternion LocalRotation => _localRotation;
        public Vector3 LocalScale => _localScale;
    }
}
