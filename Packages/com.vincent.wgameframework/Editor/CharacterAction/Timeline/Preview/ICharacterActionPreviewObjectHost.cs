using System;
using UnityEngine;

namespace MxFramework.Editor.CharacterAction.Timeline.Preview
{
    internal enum CharacterActionPreviewSurfaceMode
    {
        Embedded = 0,
        SceneView = 1,
    }

    /// <summary>
    /// Owns the Unity objects created by an editor-only Character Action preview surface.
    /// </summary>
    /// <remarks>
    /// Allocation category: AllocByDesign. Instantiation and destruction happen only when the preview
    /// binding or surface changes; rendering and frame stepping do not call these methods.
    /// </remarks>
    internal interface ICharacterActionPreviewObjectHost : IDisposable
    {
        CharacterActionPreviewSurfaceMode SurfaceMode { get; }
        bool IsDisposed { get; }

        GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation);
        void Destroy(GameObject instance);
        void RequestRepaint();
    }
}
