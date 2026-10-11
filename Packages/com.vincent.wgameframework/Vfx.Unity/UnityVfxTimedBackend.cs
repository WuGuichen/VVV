using UnityEngine;

namespace MxFramework.Vfx.Unity
{
    public enum UnityVfxParticleBudgetImportance
    {
        Unknown = 0,
        Required = 1,
        Decorative = 2
    }

    /// <summary>Optional explicit importance declaration for particle simulation budgeting.</summary>
    /// <remarks>NoAlloc. No declaration means full-frequency automatic simulation; do not infer importance from names.</remarks>
    public interface IUnityVfxParticleBudgetAdmissionSource
    {
        UnityVfxParticleBudgetImportance GetParticleBudgetImportance(VfxInstanceSnapshot instance);
    }

    /// <summary>Optional active-view cadence supplied by a shared presentation budget owner.</summary>
    /// <remarks>
    /// NoAlloc. The source must return one stable decision per presentation traversal and reuse
    /// existing observations. Unknown subjects should update. Lifecycle and retiring views bypass it.
    /// </remarks>
    public interface IUnityVfxPresentationBudgetSource
    {
        bool ShouldUpdatePresentation(VfxInstanceSnapshot instance);
    }

    /// <summary>Optional presenter-owned clock/exit. No camera callbacks and no self-managed Update.</summary>
    public interface IUnityVfxTimedBackend
    {
        bool IsPresentationComplete { get; }
        void TickPresentation(float deltaTime);
        void BeginPresentationExit(VfxStopMode mode);
    }

    /// <summary>Low-frequency binding of a cached backend to its owner, after anchor application and before play.</summary>
    public interface IUnityVfxBackendBindingSource
    {
        void BindBackend(GameObject view, VfxInstanceSnapshot instance);
    }
}
