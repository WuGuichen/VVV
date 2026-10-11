using System;
using MxFramework.Gameplay.Triggers.Config;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers.Unity
{
    /// <summary>Immutable Unity occupancy provider descriptor for one Trigger source binding.</summary>
    public readonly struct GameplayTriggerUnityOccupancyDescriptor
    {
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerUnityOccupancyDescriptor(
            GameplayTriggerSourceBindingId sourceBindingId,
            GameplayTriggerSourceProviderId providerId,
            int maxSubjects)
        {
            SourceBindingId = sourceBindingId;
            ProviderId = providerId;
            MaxSubjects = maxSubjects;
        }

        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerSourceBindingId SourceBindingId { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerSourceProviderId ProviderId { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxSubjects { get; }
    }

    /// <summary>Canonical immutable provider catalog for Unity-backed Trigger occupancy.</summary>
    public sealed class GameplayTriggerUnityOccupancyCatalog
    {
        public const int CurrentHashSchemaVersion = 1;

        private readonly GameplayTriggerUnityOccupancyDescriptor[] _descriptors;

        private GameplayTriggerUnityOccupancyCatalog(
            GameplayTriggerUnityOccupancyDescriptor[] descriptors,
            int maxSubjects,
            int totalMaxSubjects,
            long gameplayTriggerCatalogHash,
            long unityOccupancyCatalogHash,
            long combinedCatalogHash)
        {
            _descriptors = descriptors;
            MaxSubjects = maxSubjects;
            TotalMaxSubjects = totalMaxSubjects;
            GameplayTriggerCatalogHash = gameplayTriggerCatalogHash;
            UnityOccupancyCatalogHash = unityOccupancyCatalogHash;
            CombinedCatalogHash = combinedCatalogHash;
        }

        /// <remarks>NoAlloc.</remarks>
        public int Count => _descriptors.Length;
        /// <remarks>NoAlloc.</remarks>
        public int MaxSubjects { get; }
        /// <remarks>NoAlloc.</remarks>
        public int TotalMaxSubjects { get; }
        /// <remarks>NoAlloc.</remarks>
        public long GameplayTriggerCatalogHash { get; }
        /// <remarks>NoAlloc.</remarks>
        public long UnityOccupancyCatalogHash { get; }
        /// <remarks>NoAlloc.</remarks>
        public long CombinedCatalogHash { get; }

        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerUnityOccupancyDescriptor GetDescriptor(int index) =>
            _descriptors[index];

        /// <summary>Copies, canonicalizes, validates and hashes Unity occupancy descriptors.</summary>
        /// <remarks>AllocByDesign during immutable catalog construction.</remarks>
        public static bool TryCreate(
            GameplayTriggerUnityOccupancyDescriptor[] descriptors,
            GameplayTriggerCompiledCatalogPlan triggerPlan,
            out GameplayTriggerUnityOccupancyCatalog catalog,
            out GameplayTriggerUnityCatalogResult result)
        {
            catalog = null;
            if (descriptors == null || descriptors.Length == 0 || triggerPlan == null)
            {
                result = Failure(GameplayTriggerUnityCatalogErrorCode.InvalidArgument);
                return false;
            }

            var copy = (GameplayTriggerUnityOccupancyDescriptor[])descriptors.Clone();
            Array.Sort(copy, CompareDescriptors);
            int maxSubjects = 0;
            long totalMaxSubjects = 0L;
            for (int i = 0; i < copy.Length; i++)
            {
                GameplayTriggerUnityOccupancyDescriptor descriptor = copy[i];
                if (!descriptor.SourceBindingId.IsValid ||
                    !descriptor.ProviderId.IsValid ||
                    descriptor.MaxSubjects <= 0)
                {
                    result = Failure(
                        GameplayTriggerUnityCatalogErrorCode.InvalidArgument,
                        i,
                        descriptor.SourceBindingId.Value,
                        descriptor.MaxSubjects);
                    return false;
                }
                if (i > 0 && copy[i - 1].SourceBindingId == descriptor.SourceBindingId)
                {
                    result = Failure(
                        GameplayTriggerUnityCatalogErrorCode.DuplicateSourceBinding,
                        i,
                        descriptor.SourceBindingId.Value);
                    return false;
                }
                if (!TryFindRequirement(
                        triggerPlan,
                        descriptor.SourceBindingId,
                        out GameplayTriggerCompiledSourceRequirement requirement))
                {
                    result = Failure(
                        GameplayTriggerUnityCatalogErrorCode.SourceRequirementMissing,
                        i,
                        descriptor.SourceBindingId.Value);
                    return false;
                }
                if (!requirement.ProviderId.Equals(descriptor.ProviderId))
                {
                    result = Failure(
                        GameplayTriggerUnityCatalogErrorCode.ProviderMismatch,
                        i,
                        requirement.ProviderId.Value,
                        descriptor.ProviderId.Value);
                    return false;
                }
                if (requirement.SourceKind != GameplayTriggerSourceKind.Occupancy)
                {
                    result = Failure(
                        GameplayTriggerUnityCatalogErrorCode.SourceKindMismatch,
                        i,
                        (int)GameplayTriggerSourceKind.Occupancy,
                        (int)requirement.SourceKind);
                    return false;
                }
                if (!requirement.CompleteBatchPerEvaluation)
                {
                    result = Failure(
                        GameplayTriggerUnityCatalogErrorCode.IncompleteBatchContract,
                        i);
                    return false;
                }
                if (!HasSupportedEntityCapabilities(requirement.EntityCapabilities))
                {
                    result = Failure(
                        GameplayTriggerUnityCatalogErrorCode.EntityCapabilityMismatch,
                        i,
                        0L,
                        (long)requirement.EntityCapabilities);
                    return false;
                }
                if (requirement.MaxInputItemsPerEvaluation != descriptor.MaxSubjects)
                {
                    result = Failure(
                        GameplayTriggerUnityCatalogErrorCode.InputCapacityMismatch,
                        i,
                        requirement.MaxInputItemsPerEvaluation,
                        descriptor.MaxSubjects);
                    return false;
                }
                if (requirement.MaxActiveRelations < descriptor.MaxSubjects)
                {
                    result = Failure(
                        GameplayTriggerUnityCatalogErrorCode.ActiveRelationCapacityMismatch,
                        i,
                        descriptor.MaxSubjects,
                        requirement.MaxActiveRelations);
                    return false;
                }
                if (requirement.SignalEnvelopeSchemaVersion != 0 ||
                    requirement.SignalOrdering != GameplayTriggerSignalOrdering.UnorderedMultiset ||
                    requirement.PreservesOrderedSequence)
                {
                    result = Failure(
                        GameplayTriggerUnityCatalogErrorCode.ReplayMetadataMismatch,
                        i,
                        0L,
                        requirement.SignalEnvelopeSchemaVersion);
                    return false;
                }

                if (descriptor.MaxSubjects > maxSubjects)
                    maxSubjects = descriptor.MaxSubjects;
                totalMaxSubjects += descriptor.MaxSubjects;
                if (totalMaxSubjects > int.MaxValue)
                {
                    result = Failure(
                        GameplayTriggerUnityCatalogErrorCode.CapacityArithmeticOverflow,
                        i,
                        int.MaxValue,
                        totalMaxSubjects);
                    return false;
                }
            }

            long unityHash = ComputeUnityCatalogHash(copy);
            long combinedHash = ComputeCombinedCatalogHash(
                triggerPlan.GameplayTriggerCatalogHash,
                unityHash);
            catalog = new GameplayTriggerUnityOccupancyCatalog(
                copy,
                maxSubjects,
                (int)totalMaxSubjects,
                triggerPlan.GameplayTriggerCatalogHash,
                unityHash,
                combinedHash);
            result = new GameplayTriggerUnityCatalogResult(
                true,
                GameplayTriggerUnityCatalogErrorCode.None);
            return true;
        }

        private static bool TryFindRequirement(
            GameplayTriggerCompiledCatalogPlan plan,
            GameplayTriggerSourceBindingId sourceBindingId,
            out GameplayTriggerCompiledSourceRequirement requirement)
        {
            for (int i = 0; i < plan.SourceRequirementCount; i++)
            {
                GameplayTriggerCompiledSourceRequirement candidate =
                    plan.GetSourceRequirement(i);
                if (candidate.SourceBindingId != sourceBindingId)
                    continue;

                requirement = candidate;
                return true;
            }

            requirement = default;
            return false;
        }

        private static bool HasSupportedEntityCapabilities(
            GameplayTriggerSourceEntityCapability capabilities)
        {
            const GameplayTriggerSourceEntityCapability Required =
                GameplayTriggerSourceEntityCapability.SubjectPossible |
                GameplayTriggerSourceEntityCapability.SubjectGuaranteed;
            const GameplayTriggerSourceEntityCapability Allowed =
                Required |
                GameplayTriggerSourceEntityCapability.PresenceEpoch;
            return (capabilities & Required) == Required &&
                (capabilities & ~Allowed) == 0;
        }

        private static long ComputeUnityCatalogHash(
            GameplayTriggerUnityOccupancyDescriptor[] descriptors)
        {
            var accumulator = new RuntimeContentHashAccumulator();
            accumulator.AddInt(
                "trigger.unityOccupancy.catalog.schema",
                CurrentHashSchemaVersion);
            accumulator.AddInt(
                "trigger.unityOccupancy.catalog.count",
                descriptors.Length);
            for (int i = 0; i < descriptors.Length; i++)
            {
                accumulator.AddInt(
                    "trigger.unityOccupancy.sourceBindingId",
                    descriptors[i].SourceBindingId.Value);
                accumulator.AddInt(
                    "trigger.unityOccupancy.providerId",
                    descriptors[i].ProviderId.Value);
                accumulator.AddInt(
                    "trigger.unityOccupancy.maxSubjects",
                    descriptors[i].MaxSubjects);
            }
            return accumulator.ToHash();
        }

        private static long ComputeCombinedCatalogHash(long triggerHash, long unityHash)
        {
            var accumulator = new RuntimeContentHashAccumulator();
            accumulator.AddInt(
                "trigger.unityOccupancy.combined.schema",
                CurrentHashSchemaVersion);
            accumulator.AddLong(
                "trigger.unityOccupancy.combined.triggerCatalog",
                triggerHash);
            accumulator.AddLong(
                "trigger.unityOccupancy.combined.unityCatalog",
                unityHash);
            return accumulator.ToHash();
        }

        private static int CompareDescriptors(
            GameplayTriggerUnityOccupancyDescriptor left,
            GameplayTriggerUnityOccupancyDescriptor right) =>
            left.SourceBindingId.CompareTo(right.SourceBindingId);

        private static GameplayTriggerUnityCatalogResult Failure(
            GameplayTriggerUnityCatalogErrorCode errorCode,
            int descriptorIndex = -1,
            long expected = 0L,
            long actual = 0L) =>
            new GameplayTriggerUnityCatalogResult(
                false,
                errorCode,
                descriptorIndex,
                expected,
                actual);
    }
}
