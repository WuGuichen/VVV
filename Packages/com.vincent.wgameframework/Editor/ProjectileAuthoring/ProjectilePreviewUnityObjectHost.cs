using System;
using MxFramework.Combat.Projectiles.Unity;
using MxFramework.Editor.Authoring;
using UnityEngine;

namespace MxFramework.Editor.ProjectileAuthoring
{
    /// <summary>Places projectile presentation instances directly in an isolated authoring preview scene.</summary>
    /// <remarks>Instantiation is LowFreqAlloc; Release is NoAlloc after scene-owner initialization.</remarks>
    internal sealed class ProjectilePreviewUnityObjectHost : ICombatProjectileUnityObjectHost, IDisposable
    {
        private AuthoringPreviewRenderScene _scene;
        private bool _disposed;

        internal ProjectilePreviewUnityObjectHost(AuthoringPreviewRenderScene scene)
        {
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        }

        internal int OwnedCount => _scene?.RootCount ?? 0;

        public GameObject Instantiate(GameObject prefab)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ProjectilePreviewUnityObjectHost));
            return _scene.Instantiate(prefab, Vector3.zero, Quaternion.identity);
        }

        public void Release(GameObject instance)
        {
            if (_disposed || instance == null)
                return;
            _scene.Destroy(instance);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _scene = null;
        }
    }
}
