using System;
using System.Collections.Generic;
using MxFramework.CharacterRuntimeSpawn;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Read-only LCC-M3 Runtime Slice projection for developer diagnostics.</summary>
    /// <remarks>DebugProbe. It never writes body-part, topology, action, or presentation state.</remarks>
    internal sealed class CharacterTestSeverancePresentationDebugSnapshot
    {
        internal CharacterTestSeverancePresentationDebugSnapshot(
            int entityId,
            int partStateId,
            CharacterTestBodyPartStructureState authorityState,
            long authorityRevision,
            CharacterTestBodyPartDependencyStatus actionDependencyStatus,
            int expectedMemberHurtboxCount,
            int activeMemberHurtboxCount,
            int topologyRevision,
            bool hasViewProjection,
            CharacterTestSeverancePresentationSnapshot presentation)
        {
            EntityId = entityId;
            PartStateId = partStateId;
            AuthorityState = authorityState;
            AuthorityRevision = authorityRevision;
            ActionDependencyStatus = actionDependencyStatus;
            ExpectedMemberHurtboxCount = expectedMemberHurtboxCount;
            ActiveMemberHurtboxCount = activeMemberHurtboxCount;
            TopologyRevision = topologyRevision;
            HasViewProjection = hasViewProjection;
            Presentation = presentation;
        }

        internal int EntityId { get; }
        internal int PartStateId { get; }
        internal CharacterTestBodyPartStructureState AuthorityState { get; }
        internal long AuthorityRevision { get; }
        internal CharacterTestBodyPartDependencyStatus ActionDependencyStatus { get; }
        internal int ExpectedMemberHurtboxCount { get; }
        internal int ActiveMemberHurtboxCount { get; }
        internal int TopologyRevision { get; }
        internal bool HasViewProjection { get; }
        internal CharacterTestSeverancePresentationSnapshot Presentation { get; }

        internal bool AuthoritySevered =>
            AuthorityState == CharacterTestBodyPartStructureState.Severed;

        internal bool TopologyInvalidated => AuthoritySevered
            && ExpectedMemberHurtboxCount > 0
            && ActiveMemberHurtboxCount == 0;
    }

    internal static class CharacterTestSeverancePresentationDebug
    {
        internal const string DeliveryLevel = "Runtime Slice";
        internal const string ResourceCapability = "RuntimeSliceNoStableResource";
        internal const string ResourceLimitation =
            "single basemesh only; no stable tail renderer/mesh/resource key";
        internal const string SafetyBoundary =
            "whole renderer unchanged; no bone disable; no fake VFX/SFX/limb/Rigidbody/collider";

        /// <remarks>LowFreqAlloc DebugProbe.</remarks>
        internal static bool TryCapture(
            CharacterTestCharacterRuntimeModule runtime,
            CharacterTestCharacterEntity entity,
            out CharacterTestSeverancePresentationDebugSnapshot snapshot)
        {
            snapshot = null;
            if (runtime == null || entity == null)
                return false;

            CharacterTestBodyPartStateComponent state = entity.BodyPartState;
            if (state == null
                || state.InitializationStatus != CharacterTestBodyPartStateInitializationStatus.Ready)
            {
                return false;
            }

            int definitionIndex = -1;
            CharacterTestBodyPartStateDefinition definition = default;
            CharacterTestBodyPartStateEntry entry = default;
            for (int i = 0; i < state.DefinitionCount; i++)
            {
                if (!state.TryGetDefinition(i, out CharacterTestBodyPartStateDefinition candidate)
                    || !candidate.Severable
                    || !state.TryGetCurrentEntry(i, out CharacterTestBodyPartStateEntry candidateEntry))
                {
                    continue;
                }

                definitionIndex = i;
                definition = candidate;
                entry = candidateEntry;
                break;
            }

            if (definitionIndex < 0)
                return false;

            int expectedMemberHurtboxCount = 0;
            int activeMemberHurtboxCount = -1;
            int topologyRevision = 0;
            int[] memberPartIds = Array.Empty<int>();
            if (runtime.TryGetRuntimeGeometryBinding(
                    entity.Id.Value,
                    out CharacterImportedGeometryBinding binding)
                && binding != null)
            {
                CharacterBodyPartDamageRuntimeBinding[] definitions =
                    binding.BodyPartDamageDefinitions;
                for (int i = 0; definitions != null && i < definitions.Length; i++)
                {
                    CharacterBodyPartDamageRuntimeBinding candidate = definitions[i];
                    if (candidate != null
                        && candidate.PartStateId == definition.PartStateId)
                    {
                        memberPartIds = candidate.MemberPartIds ?? Array.Empty<int>();
                        break;
                    }
                }

                CharacterBodyColliderRuntimeBinding[] bodyColliders = binding.BodyColliders;
                for (int i = 0; bodyColliders != null && i < bodyColliders.Length; i++)
                {
                    CharacterBodyColliderRuntimeBinding collider = bodyColliders[i];
                    if (collider != null && ContainsOrdinal(memberPartIds, collider.PartId))
                        expectedMemberHurtboxCount++;
                }
            }

            if (runtime.TryGetDamageGeometrySnapshot(
                    entity.Id.Value,
                    out CharacterTestDamageGeometrySnapshot damageGeometry)
                && damageGeometry != null
                && damageGeometry.IsValid)
            {
                activeMemberHurtboxCount = 0;
                topologyRevision = damageGeometry.TopologyRevision;
                for (int i = 0; i < damageGeometry.ColliderCount; i++)
                {
                    CharacterTestDamageGeometryColliderMetadata collider =
                        damageGeometry.GetCollider(i);
                    if (ContainsOrdinal(memberPartIds, collider.PartId))
                        activeMemberHurtboxCount++;
                }
            }

            bool hasViewProjection = runtime.ViewBridge.TryGetSeverancePresentationSnapshot(
                entity.Id,
                definition.PartStateId,
                out CharacterTestSeverancePresentationSnapshot presentation);
            snapshot = new CharacterTestSeverancePresentationDebugSnapshot(
                entity.Id.Value,
                definition.PartStateId,
                entry.StructureState,
                entry.Revision,
                state.GetPartStateDependencyStatus(definition.PartStateId),
                expectedMemberHurtboxCount,
                activeMemberHurtboxCount,
                topologyRevision,
                hasViewProjection,
                presentation);
            return true;
        }

        /// <remarks>LowFreqAlloc DebugProbe.</remarks>
        internal static CharacterTestDeveloperDebugCard CreateCard(
            CharacterTestSeverancePresentationDebugSnapshot snapshot)
        {
            if (snapshot == null)
                return null;

            CharacterTestSeverancePresentationSnapshot presentation = snapshot.Presentation;
            string topology = snapshot.ActiveMemberHurtboxCount < 0
                ? "unavailable"
                : snapshot.ActiveMemberHurtboxCount
                    + "/"
                    + snapshot.ExpectedMemberHurtboxCount
                    + " member leaves active · topology rev "
                    + snapshot.TopologyRevision;
            string presentationStatus = snapshot.HasViewProjection
                ? presentation.Status.ToString()
                : "ViewUnavailable";
            var lines = new List<CharacterTestDeveloperDebugLine>
            {
                CharacterTestDeveloperDebugLine.StatusChip("Delivery", DeliveryLevel),
                CharacterTestDeveloperDebugLine.StatusChip(
                    "Authority",
                    snapshot.AuthorityState + " · " + snapshot.PartStateId
                        + " · revision " + snapshot.AuthorityRevision,
                    false),
                CharacterTestDeveloperDebugLine.StatusChip(
                    "Hurtbox Topology",
                    topology,
                    snapshot.AuthoritySevered && !snapshot.TopologyInvalidated),
                CharacterTestDeveloperDebugLine.StatusChip(
                    "Action Dependency",
                    snapshot.ActionDependencyStatus.ToString(),
                    snapshot.AuthoritySevered
                        && snapshot.ActionDependencyStatus
                            != CharacterTestBodyPartDependencyStatus.Severed),
                CharacterTestDeveloperDebugLine.StatusChip(
                    "Presentation",
                    presentationStatus,
                    snapshot.AuthoritySevered
                        && presentationStatus != ResourceCapability),
                CharacterTestDeveloperDebugLine.Row(
                    "Resource Capability",
                    ResourceCapability + " · " + ResourceLimitation,
                    snapshot.AuthoritySevered),
                CharacterTestDeveloperDebugLine.Row(
                    "Event Dedupe",
                    snapshot.HasViewProjection
                        ? "consumed=" + presentation.ConsumedAuthorityEventCount
                            + " duplicate=" + presentation.DuplicateAuthorityEventCount
                            + " stateSync=" + presentation.CurrentStateProjectionCount
                        : "view unavailable"),
                CharacterTestDeveloperDebugLine.Row(
                    "Emitted Presentation",
                    snapshot.HasViewProjection
                        ? "VFX=" + presentation.SpawnedVfxCount
                            + " SFX=" + presentation.PlayedSfxCount
                            + " limbs=" + presentation.DetachedViewCount
                        : "VFX=0 SFX=0 limbs=0"),
                CharacterTestDeveloperDebugLine.Row("Safety Boundary", SafetyBoundary),
                CharacterTestDeveloperDebugLine.Row(
                    "Projection",
                    "read-only; authority/topology/action remain upstream")
            };

            return CharacterTestDeveloperDebugCard.Entity(
                "Severance Runtime Slice · Entity #" + snapshot.EntityId,
                lines);
        }

        private static bool ContainsOrdinal(int[] values, int candidate)
        {
            for (int i = 0; values != null && i < values.Length; i++)
            {
                if (values[i] == candidate)
                    return true;
            }

            return false;
        }
    }
}
