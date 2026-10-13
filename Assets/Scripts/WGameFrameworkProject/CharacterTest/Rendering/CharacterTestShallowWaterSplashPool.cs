using System;
using MxFramework.Resources;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Bounded local particle views. Resource ownership and random clock never enter runtime hash/save.</summary>
    internal sealed class CharacterTestShallowWaterSplashPool : IDisposable
    {
        internal const string ResourceId = "character_test.environment.shallow_water.splash";
        private readonly ResourceManager _resources = new ResourceManager();
        private readonly MemoryResourceProvider _provider = new MemoryResourceProvider();
        private ResourceHandle<GameObject> _prefab;
        private View[] _views = Array.Empty<View>();
        private uint _random = 0xA178B539;
        private double _clock = -1;
        private bool _disposed, _disposing;
        private int _liveViews;
        internal bool Ready => !_disposed && !_disposing && _prefab != null && !_prefab.IsReleased && _liveViews > 0;
        internal int Capacity => _views.Length;
        internal int ActiveCount { get; private set; }
        internal int ParticleCount { get; private set; }
        internal int EmittedBursts { get; private set; }
        internal int ReusedBursts { get; private set; }
        internal int ResourceLoadCount => _provider.LoadCount;
        internal int ResourceReleaseCount => _provider.ReleaseCount;
        internal int CleanupFailures { get; private set; }
        internal bool IsDisposed => _disposed;
        internal string Issue { get; private set; } = string.Empty;
        private sealed class View
        {
            internal GameObject Root;
            internal ParticleSystem Particles;
            internal bool Active, Used, Lost;
            internal double Started;
        }

        /// <remarks>LowFreqAlloc. Uses the framework resource contract in a private presentation scope, without mutating global catalogs.</remarks>
        internal CharacterTestShallowWaterSplashPool(GameObject prefab, Scene scene, int capacity)
        {
            if (prefab == null || !scene.IsValid() || !scene.isLoaded)
            { Issue = "水花 Prefab 或所属 Scene 不可用。"; return; }
            var systems = prefab.GetComponentsInChildren<ParticleSystem>(true);
            if (systems.Length != 1 || prefab.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
            { Issue = "首版水花需要单个 ParticleSystem，且不包含行为脚本。"; return; }
            var renderer = systems[0].GetComponent<ParticleSystemRenderer>();
            if (renderer == null || renderer.sharedMaterial == null || renderer.sharedMaterial.shader == null
                || !renderer.sharedMaterial.shader.isSupported)
            { Issue = "水花粒子缺少可用材质或 Shader。"; return; }
            var main = systems[0].main;
            if (main.stopAction != ParticleSystemStopAction.None)
            { Issue = "水花粒子 stopAction 必须为 None，由所属粒子池统一回收。"; return; }
            if (main.loop || main.playOnAwake || main.simulationSpace != ParticleSystemSimulationSpace.World || main.maxParticles < 4
                || main.maxParticles > CharacterTestShallowWaterSplashPolicy.HardMaximumParticlesPerBurst
                || systems[0].emission.enabled || systems[0].collision.enabled || systems[0].subEmitters.enabled)
            { Issue = "水花资源需使用受限、非循环、World 粒子；关闭自动发射、碰撞和子发射器。"; return; }
            _provider.Register(ResourceId, prefab);
            _resources.RegisterProvider(_provider).AddCatalog(new ResourceCatalog(
                "character_test.shallow_water.presentation", "", new[] {
                    new ResourceCatalogEntry(ResourceId, ResourceTypeIds.GameObject, MemoryResourceProvider.Id, ResourceId)
                }));
            _resources.SealRuntimeConfiguration();
            var loaded = _resources.Load<GameObject>(new ResourceKey(ResourceId, ResourceTypeIds.GameObject));
            if (!loaded.Success) { Issue = loaded.Error.Message; return; }
            _prefab = loaded.Value;
            _views = new View[Mathf.Clamp(capacity, 1, CharacterTestShallowWaterSplashPolicy.HardMaximumBursts)];
            try
            {
                for (int i = 0; i < _views.Length; i++)
                {
                    var root = UnityEngine.Object.Instantiate(_prefab.Value);
                    // Record ownership before any further Unity call can fail.
                    var view = new View { Root = root }; _views[i] = view; _liveViews++;
                    root.name = "Shallow Water Splash " + i;
                    root.hideFlags = HideFlags.HideAndDontSave;
                    SceneManager.MoveGameObjectToScene(root, scene);
                    view.Particles = root.GetComponentInChildren<ParticleSystem>(true);
                    view.Particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    root.SetActive(false);
                }
            }
            catch { Dispose(); throw; }
        }

        /// <remarks>NoAlloc after initialization. Admission cannot grow the pool or load resources.</remarks>
        internal bool Emit(Vector3 point, float strength, float scale, int stepParticles, double clock)
        {
            if (!Ready || !Finite(point.x) || !Finite(point.y) || !Finite(point.z) || !Finite(strength) || !Finite(scale)) return false;
            View view = null;
            for (int i = 0; i < _views.Length; i++)
            {
                var candidate = _views[i]; if (candidate == null || candidate.Lost) continue;
                if (candidate.Root == null || candidate.Particles == null) { LoseView(candidate); continue; }
                if (!candidate.Active) { view = candidate; break; }
            }
            if (view == null) return false;
            if (_clock < 0) _clock = clock;
            view.Root.transform.SetPositionAndRotation(point, Quaternion.identity);
            view.Root.transform.localScale = Vector3.one;
            view.Root.SetActive(true);
            view.Particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            // Native particles must be initialized before a manual Emit on an inactive prewarmed view.
            view.Particles.Simulate(0, true, true, false);
            int count = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp(stepParticles, 4, 16) * strength), 4,
                CharacterTestShallowWaterSplashPolicy.HardMaximumParticlesPerBurst);
            float sizeScale = Mathf.Clamp(scale, .2f, 1.5f);
            float power = Mathf.Clamp(strength, .6f, 2);
            for (int i = 0; i < count; i++)
            {
                float angle = Next() * Mathf.PI * 2;
                float radial = (.25f + Next() * .7f) * power * sizeScale;
                float size = (.025f + Next() * .023f) * sizeScale;
                var emit = new ParticleSystem.EmitParams {
                    position = point + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (.015f + Next() * .05f),
                    velocity = new Vector3(Mathf.Cos(angle) * radial, (.95f + Next() * .65f) * power * sizeScale, Mathf.Sin(angle) * radial),
                    startLifetime = .28f + Next() * .22f,
                    startSize3D = new Vector3(size, size * (1.6f + Next() * 1.1f), size),
                    startColor = new Color(.69f, .86f, .96f, .8f),
                    randomSeed = NextSeed()
                };
                view.Particles.Emit(emit, 1);
            }
            view.Particles.Pause(true);
            view.Active = true; view.Started = clock;
            if (view.Used) ReusedBursts++;
            view.Used = true; ActiveCount++; ParticleCount += view.Particles.particleCount; EmittedBursts++;
            return true;
        }

        /// <remarks>NoAlloc. One manual presentation clock; pause produces zero delta and never emits.</remarks>
        internal void Tick(double clock)
        {
            if (!Ready || double.IsNaN(clock) || double.IsInfinity(clock)) return;
            if (_clock >= 0 && clock < _clock) { Clear(); _clock = clock; return; }
            float delta = _clock < 0 ? 0 : (float)(clock - _clock); _clock = clock;
            ParticleCount = 0;
            for (int i = 0; i < _views.Length; i++)
            {
                View view = _views[i]; if (view == null || view.Lost) continue;
                if (view.Root == null || view.Particles == null) { LoseView(view); continue; }
                if (!view.Active) continue;
                if (delta > 0) view.Particles.Simulate(Mathf.Min(delta, 1), true, false, false);
                if (view.Particles.particleCount == 0 || clock - view.Started > .85)
                { Return(view); continue; }
                ParticleCount += view.Particles.particleCount;
            }
        }
        internal void Clear()
        {
            try
            {
                for (int i = 0; i < _views.Length; i++)
                {
                    var view = _views[i]; if (view == null) continue;
                    if (!view.Lost && (view.Root == null || view.Particles == null)) { LoseView(view); continue; }
                    if (view.Active) TryReturn(view);
                }
            }
            finally { ActiveCount = ParticleCount = 0; _clock = -1; }
        }
        private void Return(View view)
        {
            if (view == null) return;
            bool active = view.Active;
            try
            {
                if (view.Particles != null)
                {
                    // Even an externally edited instance may not destroy/disable itself during Stop.
                    var main = view.Particles.main; main.stopAction = ParticleSystemStopAction.None;
                    view.Particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
            finally
            {
                try { if (view.Root != null) view.Root.SetActive(false); }
                finally { view.Active = false; if (active) ActiveCount = Math.Max(0, ActiveCount - 1); }
            }
        }
        private void TryReturn(View view)
        { try { Return(view); } catch (Exception error) { RecordCleanupFailure(error); } }
        private void LoseView(View view)
        {
            if (view.Lost) return;
            TryReturn(view); view.Lost = true; _liveViews = Math.Max(0, _liveViews - 1);
            if (CleanupFailures == 0) Issue = "水花池中的对象已被外部销毁；不会动态扩容或补发。";
        }
        public void Dispose()
        {
            if (_disposed || _disposing) return;
            _disposing = true;
            bool objectsReleased = false;
            try
            {
                Clear();
                bool allObjectsReleased = true;
                for (int i = 0; i < _views.Length; i++)
                {
                    var view = _views[i]; if (view == null) continue;
                    try
                    {
                        var root = view.Root;
                        if (root != null)
                        {
                            root.SetActive(false);
                            if (Application.isPlaying) UnityEngine.Object.Destroy(root);
                            else UnityEngine.Object.DestroyImmediate(root);
                        }
                        _views[i] = null;
                    }
                    catch (Exception error) { allObjectsReleased = false; RecordCleanupFailure(error); }
                }
                objectsReleased = allObjectsReleased;
            }
            finally
            {
                try
                {
                    if (_prefab != null)
                    {
                        if (!_prefab.IsReleased) _resources.Release(_prefab);
                        _prefab = null;
                    }
                }
                finally
                {
                    try { _provider.Dispose(); }
                    finally
                    {
                        if (objectsReleased) _views = Array.Empty<View>();
                        _liveViews = ActiveCount = ParticleCount = 0; _clock = -1;
                        _disposed = objectsReleased && (_prefab == null || _prefab.IsReleased);
                        _disposing = false;
                    }
                }
            }
        }
        /// <remarks>NoAlloc. Ownership diagnostics for native cleanup verification.</remarks>
        internal GameObject GetViewRoot(int index) => index >= 0 && index < _views.Length ? _views[index]?.Root : null;
        private void RecordCleanupFailure(Exception error)
        { CleanupFailures++; Issue = "水花清理失败：" + error.Message; }
        private uint NextSeed()
        { _random ^= _random << 13; _random ^= _random >> 17; _random ^= _random << 5; return _random; }
        private float Next() => (NextSeed() & 0xFFFFFF) / 16777216f;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
