using System;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers.Config;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers.CombatBridge
{
    /// <summary>Immutable fixed-world-space Combat query bound to one Trigger occupancy source.</summary>
    public readonly struct GameplayTriggerCombatSensorDescriptor
    {
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerCombatSensorDescriptor(
            GameplayTriggerSourceBindingId sourceBindingId,
            GameplayTriggerSourceProviderId providerId,
            CombatPhysicsQuery query,
            int maxQueryHits,
            int maxMappedSubjects)
            : this(
                sourceBindingId,
                providerId,
                query,
                maxQueryHits,
                maxMappedSubjects,
                GameplayComponentTargetFilter.Unrestricted)
        {
        }

        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerCombatSensorDescriptor(
            GameplayTriggerSourceBindingId sourceBindingId,
            GameplayTriggerSourceProviderId providerId,
            CombatPhysicsQuery query,
            int maxQueryHits,
            int maxMappedSubjects,
            GameplayComponentTargetFilter subjectFilter)
        {
            SourceBindingId = sourceBindingId;
            ProviderId = providerId;
            Query = query;
            MaxQueryHits = maxQueryHits;
            MaxMappedSubjects = maxMappedSubjects;
            SubjectFilter = subjectFilter;
        }

        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerSourceBindingId SourceBindingId { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerSourceProviderId ProviderId { get; }
        /// <remarks>NoAlloc.</remarks>
        public CombatPhysicsQuery Query { get; }
        /// <summary>Hard bound for caller-owned query-result scratch; it is not a truncation limit.</summary>
        /// <remarks>NoAlloc.</remarks>
        public int MaxQueryHits { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxMappedSubjects { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayComponentTargetFilter SubjectFilter { get; }
    }

    /// <summary>Canonical immutable authority configuration for Combat-backed Trigger sensors.</summary>
    public sealed class GameplayTriggerCombatSensorCatalog
    {
        public const int CurrentHashSchemaVersion = 2;

        private readonly GameplayTriggerCombatSensorDescriptor[] _descriptors;

        private GameplayTriggerCombatSensorCatalog(
            GameplayTriggerCombatSensorDescriptor[] descriptors,
            int maxQueryHits,
            int maxMappedSubjects,
            long sensorCatalogHash,
            long gameplayTriggerCatalogHash,
            long combinedCatalogHash)
        {
            _descriptors = descriptors;
            MaxQueryHits = maxQueryHits;
            MaxMappedSubjects = maxMappedSubjects;
            SensorCatalogHash = sensorCatalogHash;
            GameplayTriggerCatalogHash = gameplayTriggerCatalogHash;
            CombinedCatalogHash = combinedCatalogHash;
        }

        /// <remarks>NoAlloc.</remarks>
        public int Count => _descriptors.Length;
        /// <remarks>NoAlloc.</remarks>
        public int MaxQueryHits { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxMappedSubjects { get; }
        /// <remarks>NoAlloc.</remarks>
        public long SensorCatalogHash { get; }
        /// <remarks>NoAlloc.</remarks>
        public long GameplayTriggerCatalogHash { get; }
        /// <remarks>NoAlloc.</remarks>
        public long CombinedCatalogHash { get; }

        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerCombatSensorDescriptor GetDescriptor(int index) => _descriptors[index];

        /// <remarks>AllocByDesign. Copies, canonicalizes, validates and hashes immutable descriptors.</remarks>
        public static bool TryCreate(
            GameplayTriggerCombatSensorDescriptor[] descriptors,
            GameplayTriggerCompiledCatalogPlan triggerPlan,
            out GameplayTriggerCombatSensorCatalog catalog,
            out GameplayTriggerCombatSensorCatalogResult result)
        {
            catalog = null;
            if (descriptors == null || descriptors.Length == 0 || triggerPlan == null)
            {
                result = Failure(GameplayTriggerCombatSensorCatalogErrorCode.InvalidArgument);
                return false;
            }

            var copy = (GameplayTriggerCombatSensorDescriptor[])descriptors.Clone();
            Array.Sort(copy, CompareDescriptors);
            int maxQueryHits = 0;
            int maxMappedSubjects = 0;
            for (int i = 0; i < copy.Length; i++)
            {
                GameplayTriggerCombatSensorDescriptor descriptor = copy[i];
                if (!descriptor.SourceBindingId.IsValid || !descriptor.ProviderId.IsValid)
                {
                    result = Failure(
                        GameplayTriggerCombatSensorCatalogErrorCode.InvalidArgument,
                        i,
                        descriptor.SourceBindingId.Value,
                        descriptor.ProviderId.Value);
                    return false;
                }
                if (i > 0 && copy[i - 1].SourceBindingId == descriptor.SourceBindingId)
                {
                    result = Failure(
                        GameplayTriggerCombatSensorCatalogErrorCode.DuplicateSourceBinding,
                        i,
                        descriptor.SourceBindingId.Value);
                    return false;
                }
                if (descriptor.MaxQueryHits <= 0 || descriptor.MaxMappedSubjects <= 0)
                {
                    result = Failure(
                        GameplayTriggerCombatSensorCatalogErrorCode.CapacityMismatch,
                        i,
                        descriptor.MaxQueryHits,
                        descriptor.MaxMappedSubjects);
                    return false;
                }
                if (!TryFindRequirement(
                        triggerPlan,
                        descriptor.SourceBindingId,
                        out GameplayTriggerCompiledSourceRequirement requirement))
                {
                    result = Failure(
                        GameplayTriggerCombatSensorCatalogErrorCode.SourceRequirementMissing,
                        i,
                        descriptor.SourceBindingId.Value);
                    return false;
                }
                if (requirement.ProviderId.Value != descriptor.ProviderId.Value)
                {
                    result = Failure(
                        GameplayTriggerCombatSensorCatalogErrorCode.ProviderMismatch,
                        i,
                        requirement.ProviderId.Value,
                        descriptor.ProviderId.Value);
                    return false;
                }
                if (requirement.SourceKind != GameplayTriggerSourceKind.Occupancy)
                {
                    result = Failure(
                        GameplayTriggerCombatSensorCatalogErrorCode.SourceKindMismatch,
                        i,
                        (int)requirement.SourceKind);
                    return false;
                }
                if (!requirement.CompleteBatchPerEvaluation)
                {
                    result = Failure(
                        GameplayTriggerCombatSensorCatalogErrorCode.IncompleteBatchContract,
                        i);
                    return false;
                }
                if (!HasSupportedEntityCapabilities(requirement.EntityCapabilities))
                {
                    result = Failure(
                        GameplayTriggerCombatSensorCatalogErrorCode.EntityCapabilityMismatch,
                        i,
                        (int)requirement.EntityCapabilities);
                    return false;
                }
                if (requirement.MaxInputItemsPerEvaluation != descriptor.MaxMappedSubjects)
                {
                    result = Failure(
                        GameplayTriggerCombatSensorCatalogErrorCode.CapacityMismatch,
                        i,
                        requirement.MaxInputItemsPerEvaluation,
                        descriptor.MaxMappedSubjects);
                    return false;
                }
                if (requirement.MaxActiveRelations < descriptor.MaxMappedSubjects)
                {
                    result = Failure(
                        GameplayTriggerCombatSensorCatalogErrorCode.CapacityMismatch,
                        i,
                        requirement.MaxActiveRelations,
                        descriptor.MaxMappedSubjects);
                    return false;
                }
                if (requirement.SignalEnvelopeSchemaVersion != 0 ||
                    requirement.SignalOrdering != GameplayTriggerSignalOrdering.UnorderedMultiset ||
                    requirement.PreservesOrderedSequence)
                {
                    result = Failure(
                        GameplayTriggerCombatSensorCatalogErrorCode.ReplayMetadataMismatch,
                        i,
                        requirement.SignalEnvelopeSchemaVersion,
                        (int)requirement.SignalOrdering);
                    return false;
                }
                if (!TryValidateQuery(descriptor.Query, out GameplayTriggerCombatSensorCatalogErrorCode queryError))
                {
                    result = Failure(queryError, i, (int)descriptor.Query.Header.Kind, (int)descriptor.Query.Shape.Kind);
                    return false;
                }

                if (descriptor.MaxQueryHits > maxQueryHits)
                    maxQueryHits = descriptor.MaxQueryHits;
                if (descriptor.MaxMappedSubjects > maxMappedSubjects)
                    maxMappedSubjects = descriptor.MaxMappedSubjects;
            }

            long sensorHash = ComputeSensorCatalogHash(copy);
            long combinedHash = ComputeCombinedCatalogHash(
                triggerPlan.GameplayTriggerCatalogHash,
                sensorHash);
            catalog = new GameplayTriggerCombatSensorCatalog(
                copy,
                maxQueryHits,
                maxMappedSubjects,
                sensorHash,
                triggerPlan.GameplayTriggerCatalogHash,
                combinedHash);
            result = GameplayTriggerCombatSensorCatalogResult.Succeeded;
            return true;
        }

        private static bool TryFindRequirement(
            GameplayTriggerCompiledCatalogPlan plan,
            GameplayTriggerSourceBindingId sourceBindingId,
            out GameplayTriggerCompiledSourceRequirement requirement)
        {
            for (int i = 0; i < plan.SourceRequirementCount; i++)
            {
                GameplayTriggerCompiledSourceRequirement candidate = plan.GetSourceRequirement(i);
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
            const GameplayTriggerSourceEntityCapability Rejected =
                GameplayTriggerSourceEntityCapability.InstigatorPossible |
                GameplayTriggerSourceEntityCapability.InstigatorGuaranteed;
            const GameplayTriggerSourceEntityCapability Allowed =
                Required |
                GameplayTriggerSourceEntityCapability.PresenceEpoch;
            return (capabilities & Required) == Required &&
                (capabilities & Rejected) == 0 &&
                (capabilities & ~Allowed) == 0;
        }

        private static bool TryValidateQuery(
            CombatPhysicsQuery query,
            out GameplayTriggerCombatSensorCatalogErrorCode errorCode)
        {
            CombatPhysicsShape shape = query.Shape;
            if (shape.Kind != CombatPhysicsShapeKind.Sphere &&
                shape.Kind != CombatPhysicsShapeKind.Capsule &&
                shape.Kind != CombatPhysicsShapeKind.Aabb &&
                shape.Kind != CombatPhysicsShapeKind.Sector)
            {
                errorCode = GameplayTriggerCombatSensorCatalogErrorCode.UnsupportedShape;
                return false;
            }
            if ((int)query.Header.Kind != (int)shape.Kind)
            {
                errorCode = GameplayTriggerCombatSensorCatalogErrorCode.QueryHeaderMismatch;
                return false;
            }
            if (query.Header.LayerMask.Value == 0)
            {
                errorCode = GameplayTriggerCombatSensorCatalogErrorCode.EmptyLayerMask;
                return false;
            }
            if (query.Filter.OwnerId != 0 ||
                query.Filter.TeamId != 0 ||
                query.Filter.TargetOwnerMask != 0 ||
                query.Filter.TargetTeamMask != 0)
            {
                errorCode = GameplayTriggerCombatSensorCatalogErrorCode.UnsupportedFilter;
                return false;
            }

            bool valid;
            switch (shape.Kind)
            {
                case CombatPhysicsShapeKind.Sphere:
                    valid = shape.Radius >= Fix64.Zero;
                    break;
                case CombatPhysicsShapeKind.Capsule:
                    valid = shape.Radius >= Fix64.Zero;
                    break;
                case CombatPhysicsShapeKind.Aabb:
                    valid = shape.Min.X <= shape.Max.X &&
                        shape.Min.Y <= shape.Max.Y &&
                        shape.Min.Z <= shape.Max.Z;
                    break;
                case CombatPhysicsShapeKind.Sector:
                    valid = !shape.Direction.IsZero &&
                        shape.Radius >= Fix64.Zero &&
                        shape.MinDot >= -Fix64.One &&
                        shape.MinDot <= Fix64.One;
                    break;
                default:
                    valid = false;
                    break;
            }

            errorCode = valid
                ? GameplayTriggerCombatSensorCatalogErrorCode.None
                : GameplayTriggerCombatSensorCatalogErrorCode.InvalidGeometry;
            return valid;
        }

        private static long ComputeSensorCatalogHash(
            GameplayTriggerCombatSensorDescriptor[] descriptors)
        {
            var accumulator = new RuntimeContentHashAccumulator();
            accumulator.AddInt("trigger.combatSensor.catalog.schema", CurrentHashSchemaVersion);
            accumulator.AddInt("trigger.combatSensor.catalog.count", descriptors.Length);
            for (int i = 0; i < descriptors.Length; i++)
            {
                GameplayTriggerCombatSensorDescriptor descriptor = descriptors[i];
                CombatPhysicsQuery query = descriptor.Query;
                CombatQueryHeader header = query.Header;
                CombatPhysicsQueryFilter filter = query.Filter;
                CombatPhysicsShape shape = query.Shape;
                accumulator.AddInt("trigger.combatSensor.sourceBindingId", descriptor.SourceBindingId.Value);
                accumulator.AddInt("trigger.combatSensor.providerId", descriptor.ProviderId.Value);
                accumulator.AddInt("trigger.combatSensor.maxQueryHits", descriptor.MaxQueryHits);
                accumulator.AddInt("trigger.combatSensor.maxMappedSubjects", descriptor.MaxMappedSubjects);
                GameplayComponentTargetFilter subjectFilter =
                    descriptor.SubjectFilter;
                accumulator.AddInt(
                    "trigger.combatSensor.subjectFilter.identityTypes",
                    (int)subjectFilter.AcceptedIdentityTypes);
                accumulator.AddInt(
                    "trigger.combatSensor.subjectFilter.capabilities",
                    (int)subjectFilter.RequiredCapabilities);
                accumulator.AddInt(
                    "trigger.combatSensor.subjectFilter.requireAlive",
                    subjectFilter.RequireAlive ? 1 : 0);
                accumulator.AddInt(
                    "trigger.combatSensor.subjectFilter.relation",
                    (int)subjectFilter.RelationFilter);
                accumulator.AddInt(
                    "trigger.combatSensor.subjectFilter.referenceEntity.index",
                    subjectFilter.ReferenceEntityId.Index);
                accumulator.AddInt(
                    "trigger.combatSensor.subjectFilter.referenceEntity.generation",
                    subjectFilter.ReferenceEntityId.Generation);
                accumulator.AddInt(
                    "trigger.combatSensor.subjectFilter.referenceTeamId",
                    subjectFilter.ReferenceTeamId);
                accumulator.AddInt("trigger.combatSensor.query.id", header.QueryId);
                accumulator.AddInt("trigger.combatSensor.query.kind", (int)header.Kind);
                accumulator.AddInt("trigger.combatSensor.query.sourceEntity", header.SourceEntityId.Value);
                accumulator.AddInt("trigger.combatSensor.query.traceId", header.TraceId);
                accumulator.AddInt("trigger.combatSensor.query.actionId", header.ActionId);
                accumulator.AddInt("trigger.combatSensor.query.sourceOrder", header.SourceOrder);
                accumulator.AddInt("trigger.combatSensor.query.layerMask", header.LayerMask.Value);
                accumulator.AddInt("trigger.combatSensor.query.includeSource", filter.IncludeSourceEntity ? 1 : 0);
                accumulator.AddInt("trigger.combatSensor.shape.kind", (int)shape.Kind);
                AddEffectiveShape(accumulator, shape);
            }
            return accumulator.ToHash();
        }

        private static void AddEffectiveShape(
            RuntimeContentHashAccumulator accumulator,
            CombatPhysicsShape shape)
        {
            switch (shape.Kind)
            {
                case CombatPhysicsShapeKind.Sphere:
                    AddVector(accumulator, "trigger.combatSensor.shape.center", shape.Center);
                    accumulator.AddLong("trigger.combatSensor.shape.radius", shape.Radius.RawValue);
                    break;
                case CombatPhysicsShapeKind.Capsule:
                    AddVector(accumulator, "trigger.combatSensor.shape.pointA", shape.PointA);
                    AddVector(accumulator, "trigger.combatSensor.shape.pointB", shape.PointB);
                    accumulator.AddLong("trigger.combatSensor.shape.radius", shape.Radius.RawValue);
                    break;
                case CombatPhysicsShapeKind.Aabb:
                    AddVector(accumulator, "trigger.combatSensor.shape.min", shape.Min);
                    AddVector(accumulator, "trigger.combatSensor.shape.max", shape.Max);
                    break;
                case CombatPhysicsShapeKind.Sector:
                    AddVector(accumulator, "trigger.combatSensor.shape.origin", shape.Origin);
                    AddVector(accumulator, "trigger.combatSensor.shape.direction", shape.Direction);
                    accumulator.AddLong("trigger.combatSensor.shape.radius", shape.Radius.RawValue);
                    accumulator.AddLong("trigger.combatSensor.shape.minDot", shape.MinDot.RawValue);
                    break;
            }
        }

        private static void AddVector(
            RuntimeContentHashAccumulator accumulator,
            string prefix,
            FixVector3 value)
        {
            accumulator.AddLong(prefix + ".x", value.X.RawValue);
            accumulator.AddLong(prefix + ".y", value.Y.RawValue);
            accumulator.AddLong(prefix + ".z", value.Z.RawValue);
        }

        private static long ComputeCombinedCatalogHash(long triggerHash, long sensorHash)
        {
            var accumulator = new RuntimeContentHashAccumulator();
            accumulator.AddInt("trigger.combatSensor.combined.schema", CurrentHashSchemaVersion);
            accumulator.AddLong("trigger.combatSensor.combined.triggerCatalog", triggerHash);
            accumulator.AddLong("trigger.combatSensor.combined.sensorCatalog", sensorHash);
            return accumulator.ToHash();
        }

        private static int CompareDescriptors(
            GameplayTriggerCombatSensorDescriptor left,
            GameplayTriggerCombatSensorDescriptor right) =>
            left.SourceBindingId.CompareTo(right.SourceBindingId);

        private static GameplayTriggerCombatSensorCatalogResult Failure(
            GameplayTriggerCombatSensorCatalogErrorCode errorCode,
            int descriptorIndex = -1,
            int detail0 = 0,
            int detail1 = 0) =>
            new GameplayTriggerCombatSensorCatalogResult(
                false,
                errorCode,
                descriptorIndex,
                detail0,
                detail1);
    }
}
