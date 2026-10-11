using System;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Explicit decoration admission. Unknown effects and critical combat cues retain full presentation.</summary>
    internal sealed class CharacterTestVfxPresentationBudgetSource : IUnityVfxPresentationBudgetSource, IUnityVfxParticleBudgetAdmissionSource
    {
        private readonly CharacterTestPresentationLod _lod;
        private readonly IUnityVfxParticleBudgetAdmissionSource _particleAdmission;
        internal CharacterTestVfxPresentationBudgetSource(CharacterTestPresentationLod lod) : this(lod, null) { }
        internal CharacterTestVfxPresentationBudgetSource(CharacterTestPresentationLod lod,
            IUnityVfxParticleBudgetAdmissionSource particleAdmission)
        { _lod = lod; _particleAdmission = particleAdmission; }

        public UnityVfxParticleBudgetImportance GetParticleBudgetImportance(VfxInstanceSnapshot instance)
        {
            return _particleAdmission?.GetParticleBudgetImportance(instance) ?? UnityVfxParticleBudgetImportance.Unknown;
        }

        public bool ShouldUpdatePresentation(VfxInstanceSnapshot instance)
        {
            // The existing blade-trail binding declares SourceId as its actual equipment/pose owner, even for a world anchor.
            if (_lod == null || instance == null || instance.SourceId <= 0
                || (!string.Equals(instance.PrefabResource.Id, CharacterTestBladeTrailVfxIds.ResourceId, StringComparison.Ordinal)
                    && GetParticleBudgetImportance(instance) != UnityVfxParticleBudgetImportance.Decorative)) return true;
            return _lod.ShouldUpdateVfx(instance.SourceId);
        }
    }
}
