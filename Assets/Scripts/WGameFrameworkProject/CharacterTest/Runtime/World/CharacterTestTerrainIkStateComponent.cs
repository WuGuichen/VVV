using System;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Future-affecting history for one authored terrain IK limb.</summary>
    internal struct CharacterTestTerrainIkLimbState
    {
        internal int Ordinal;
        internal int ProbeId;
        internal int IkBindingId;
        internal int PartStateId;
        internal int MissCountCap;
        internal bool CurrentValid;
        internal CharacterTestAuthorityPoseFailure CurrentFailure;
        internal long LastValidFrame;
        internal int ConsecutiveMissCount;
        internal FixVector3 LastValidTargetWorld;
        internal FixVector3 LastValidNormalWorld;
        internal int SurfaceEntityId;
        internal int SurfaceBodyId;
        internal int SurfaceColliderId;
        internal int SurfaceLeafId;
        internal CombatPhysicsShapeKind SurfaceShapeKind;
        internal CombatSurfaceMaterial SurfaceMaterial;
        internal Fix64 CurrentWeight;

        internal bool HasRetainedTarget => LastValidFrame >= 0L
            && !LastValidNormalWorld.IsZero;

        internal static CharacterTestTerrainIkLimbState Create(
            CharacterAuthorityGroundProbeDefinition probe)
        {
            return new CharacterTestTerrainIkLimbState
            {
                Ordinal = probe?.Ordinal ?? -1,
                ProbeId = probe?.ProbeId ?? 0,
                IkBindingId = probe?.IkBindingId ?? 0,
                PartStateId = probe?.PartStateId ?? 0,
                MissCountCap = ResolveMissCountCap(probe),
                CurrentValid = false,
                CurrentFailure = CharacterTestAuthorityPoseFailure.SourceStale,
                LastValidFrame = -1L,
                ConsecutiveMissCount = 0,
                LastValidTargetWorld = FixVector3.Zero,
                LastValidNormalWorld = FixVector3.Zero,
                SurfaceEntityId = 0,
                SurfaceBodyId = 0,
                SurfaceColliderId = 0,
                SurfaceLeafId = 0,
                SurfaceShapeKind = default,
                SurfaceMaterial = CombatSurfaceMaterial.Default,
                CurrentWeight = Fix64.Zero
            };
        }

        internal void ResetRuntimeHistory()
        {
            CurrentValid = false;
            CurrentFailure = CharacterTestAuthorityPoseFailure.SourceStale;
            LastValidFrame = -1L;
            ConsecutiveMissCount = 0;
            LastValidTargetWorld = FixVector3.Zero;
            LastValidNormalWorld = FixVector3.Zero;
            SurfaceEntityId = 0;
            SurfaceBodyId = 0;
            SurfaceColliderId = 0;
            SurfaceLeafId = 0;
            SurfaceShapeKind = default;
            SurfaceMaterial = CombatSurfaceMaterial.Default;
            CurrentWeight = Fix64.Zero;
        }

        private static int ResolveMissCountCap(CharacterAuthorityGroundProbeDefinition probe)
        {
            long cap = (long)Math.Max(0, probe?.LostFrames ?? 0)
                + Math.Max(1, probe?.ExitFrames ?? 1)
                + 1L;
            return cap >= int.MaxValue ? int.MaxValue : (int)cap;
        }
    }

    /// <summary>
    /// CharacterTest-owned, fixed-capacity terrain IK authority history for one entity.
    /// Derived mid/tip samples live only in <see cref="ResolvedFrame"/> and are never snapshot state.
    /// </summary>
    /// <remarks>LowFreqAlloc during definition registration; NoAlloc during Tick.</remarks>
    internal sealed partial class CharacterTestTerrainIkStateComponent
    {
        internal const int MaximumLimbCount = 4;

        private readonly CharacterTestTerrainIkLimbState[] _limbs =
            new CharacterTestTerrainIkLimbState[MaximumLimbCount];

        internal CharacterTestTerrainIkStateComponent(int ownerEntityId)
        {
            OwnerEntityId = ownerEntityId;
            ResolvedFrame = new CharacterTestResolvedAuthorityPoseFrame();
        }

        internal int OwnerEntityId { get; }
        internal bool IsConfigured { get; private set; }
        internal int DefinitionRevision { get; private set; }
        internal string DefinitionContentHash { get; private set; } = string.Empty;
        internal RuntimePreparedDigest DefinitionDigest { get; private set; } = RuntimePreparedDigest.PrepareExisting(string.Empty);
        internal int BaseSourceRevision { get; private set; }
        internal string BaseSourceContentHash { get; private set; } = string.Empty;
        internal RuntimePreparedDigest BaseSourceDigest { get; private set; } = RuntimePreparedDigest.PrepareExisting(string.Empty);
        internal int LimbCount { get; private set; }
        internal long LastPublishedRevision { get; private set; }
        internal CharacterTestResolvedAuthorityPoseFrame ResolvedFrame { get; }

        internal bool Configure(
            CharacterAuthorityPoseRigDefinition definition,
            int baseSourceRevision,
            string baseSourceContentHash)
        {
            int limbCount = definition?.GroundProbes?.Count ?? 0;
            if (definition == null
                || definition.Channels.Count == 0
                || definition.Channels.Count > CharacterTestResolvedAuthorityPoseFrame.MaximumSampleCount
                || limbCount == 0
                || limbCount > MaximumLimbCount
                || definition.TwoBoneIkBindings.Count != limbCount
                || definition.SourceRevision <= 0
                || string.IsNullOrWhiteSpace(definition.ContentHash)
                || baseSourceRevision <= 0
                || string.IsNullOrWhiteSpace(baseSourceContentHash))
            {
                ClearConfiguration();
                return false;
            }

            DefinitionRevision = definition.SourceRevision;
            DefinitionContentHash = definition.ContentHash;
            DefinitionDigest = RuntimePreparedDigest.PrepareExisting(DefinitionContentHash);
            BaseSourceRevision = baseSourceRevision;
            BaseSourceContentHash = baseSourceContentHash;
            BaseSourceDigest = RuntimePreparedDigest.PrepareExisting(BaseSourceContentHash);
            LimbCount = limbCount;
            for (int i = 0; i < LimbCount; i++)
                _limbs[i] = CharacterTestTerrainIkLimbState.Create(definition.GroundProbes[i]);
            for (int i = LimbCount; i < _limbs.Length; i++)
                _limbs[i] = default;
            LastPublishedRevision = 0L;
            ResolvedFrame.ClearDerived(LastPublishedRevision);
            IsConfigured = true;
            return true;
        }

        internal CharacterTestTerrainIkLimbState GetLimb(int index)
        {
            if ((uint)index >= (uint)LimbCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _limbs[index];
        }

        internal void SetLimb(int index, in CharacterTestTerrainIkLimbState value)
        {
            if ((uint)index >= (uint)LimbCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            _limbs[index] = value;
        }

        internal long BeginResolvedFrame(long runtimeFrame)
        {
            LastPublishedRevision = LastPublishedRevision == long.MaxValue
                ? 1L
                : LastPublishedRevision + 1L;
            ResolvedFrame.Begin(
                runtimeFrame,
                LastPublishedRevision,
                DefinitionRevision,
                DefinitionContentHash,
                BaseSourceRevision,
                BaseSourceContentHash);
            return LastPublishedRevision;
        }

        internal void RestorePublishedRevision(long revision)
        {
            LastPublishedRevision = revision < 0L ? 0L : revision;
            ResolvedFrame.ClearDerived(LastPublishedRevision);
        }

        internal void ResetLegacyNetworkState()
        {
            for (int i = 0; i < LimbCount; i++)
            {
                CharacterTestTerrainIkLimbState state = _limbs[i];
                state.ResetRuntimeHistory();
                _limbs[i] = state;
            }
            LastPublishedRevision = 0L;
            ResolvedFrame.ClearDerived(LastPublishedRevision);
        }

        private void ClearConfiguration()
        {
            IsConfigured = false;
            DefinitionRevision = 0;
            DefinitionContentHash = string.Empty;
            DefinitionDigest = RuntimePreparedDigest.PrepareExisting(string.Empty);
            BaseSourceRevision = 0;
            BaseSourceContentHash = string.Empty;
            BaseSourceDigest = RuntimePreparedDigest.PrepareExisting(string.Empty);
            LimbCount = 0;
            LastPublishedRevision = 0L;
            Array.Clear(_limbs, 0, _limbs.Length);
            ResolvedFrame.ClearDerived(LastPublishedRevision);
        }
    }
}
