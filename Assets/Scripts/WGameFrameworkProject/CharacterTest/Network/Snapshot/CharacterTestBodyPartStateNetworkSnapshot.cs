using System;
using System.Collections.Generic;
using MxFramework.Combat.Physics;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Formal schema-38 authority for one CharacterTest entity's numeric body-part state.</summary>
    /// <remarks>LowFreqAlloc: snapshot, rollback, replay, and late-join paths only.</remarks>
    public sealed class CharacterTestNetworkBodyPartStateSnapshot
    {
        public int EntityId { get; set; }
        public int InitializationStatus { get; set; }
        public long ComponentRevision { get; set; }
        public string DefinitionSourceFormat { get; set; } = string.Empty;
        public string DefinitionSourceSchemaVersion { get; set; } = string.Empty;
        public int DefinitionSourceRevision { get; set; }
        public string DefinitionSourceContentHash { get; set; } = string.Empty;
        public CharacterTestNetworkBodyPartStateEntrySnapshot[] Entries { get; set; } =
            Array.Empty<CharacterTestNetworkBodyPartStateEntrySnapshot>();

        internal void NormalizeCanonical()
        {
            DefinitionSourceFormat ??= string.Empty;
            DefinitionSourceSchemaVersion ??= string.Empty;
            DefinitionSourceContentHash ??= string.Empty;
            Entries ??= Array.Empty<CharacterTestNetworkBodyPartStateEntrySnapshot>();
            for (int i = 0; i < Entries.Length; i++)
                Entries[i]?.NormalizeCanonical();
            Array.Sort(Entries, CompareEntries);
        }

        internal CharacterTestNetworkBodyPartStateSnapshot CloneCanonical()
        {
            var result = new CharacterTestNetworkBodyPartStateSnapshot
            {
                EntityId = EntityId,
                InitializationStatus = InitializationStatus,
                ComponentRevision = ComponentRevision,
                DefinitionSourceFormat = DefinitionSourceFormat ?? string.Empty,
                DefinitionSourceSchemaVersion = DefinitionSourceSchemaVersion ?? string.Empty,
                DefinitionSourceRevision = DefinitionSourceRevision,
                DefinitionSourceContentHash = DefinitionSourceContentHash ?? string.Empty,
                Entries = CharacterTestNetworkBodyPartStateEntrySnapshot.CloneArray(Entries)
            };
            result.NormalizeCanonical();
            return result;
        }

        private static int CompareEntries(
            CharacterTestNetworkBodyPartStateEntrySnapshot left,
            CharacterTestNetworkBodyPartStateEntrySnapshot right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;
            int result = left.DefinitionIndex.CompareTo(right.DefinitionIndex);
            return result != 0
                ? result
                : left.PartStateId.CompareTo(right.PartStateId);
        }
    }

    /// <summary>Formal schema-38 authority values for one numeric body-part definition.</summary>
    /// <remarks>LowFreqAlloc: snapshot, rollback, replay, and late-join paths only.</remarks>
    public sealed class CharacterTestNetworkBodyPartStateEntrySnapshot
    {
        public int DefinitionIndex { get; set; }
        public int PartStateId { get; set; }
        public int WoundValue { get; set; }
        public int WoundState { get; set; }
        public int CurrentIntegrity { get; set; }
        public int StructureState { get; set; }
        public long LastAcceptedHitFrame { get; set; }
        public long Revision { get; set; }

        internal void NormalizeCanonical()
        {
        }

        internal static CharacterTestNetworkBodyPartStateEntrySnapshot[] CloneArray(
            CharacterTestNetworkBodyPartStateEntrySnapshot[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<CharacterTestNetworkBodyPartStateEntrySnapshot>();
            var result = new CharacterTestNetworkBodyPartStateEntrySnapshot[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                CharacterTestNetworkBodyPartStateEntrySnapshot value = source[i];
                if (value == null) continue;
                result[i] = new CharacterTestNetworkBodyPartStateEntrySnapshot
                {
                    DefinitionIndex = value.DefinitionIndex,
                    PartStateId = value.PartStateId,
                    WoundValue = value.WoundValue,
                    WoundState = value.WoundState,
                    CurrentIntegrity = value.CurrentIntegrity,
                    StructureState = value.StructureState,
                    LastAcceptedHitFrame = value.LastAcceptedHitFrame,
                    Revision = value.Revision
                };
            }
            return result;
        }
    }

    internal readonly struct CharacterTestBodyPartStateNetworkRestorePlan
    {
        internal CharacterTestBodyPartStateNetworkRestorePlan(
            CharacterTestBodyPartStateComponent component,
            CharacterTestBodyPartStateEntry[] entries,
            long componentRevision,
            bool resetToLegacyDefaults)
        {
            Component = component;
            Entries = entries;
            ComponentRevision = componentRevision;
            ResetToLegacyDefaults = resetToLegacyDefaults;
        }

        internal CharacterTestBodyPartStateComponent Component { get; }
        internal CharacterTestBodyPartStateEntry[] Entries { get; }
        internal long ComponentRevision { get; }
        internal bool ResetToLegacyDefaults { get; }

        internal void Commit()
        {
            if (ResetToLegacyDefaults)
                Component.CommitLegacyNetworkDefaults();
            else
                Component.CommitNetworkRestore(Entries, ComponentRevision);
        }
    }

    internal static class CharacterTestBodyPartStateNetworkSnapshotCodec
    {
        internal static CharacterTestNetworkBodyPartStateSnapshot[] Capture(
            IReadOnlyList<CharacterTestCharacterEntity> entities)
        {
            int count = entities?.Count ?? 0;
            if (count == 0)
                return Array.Empty<CharacterTestNetworkBodyPartStateSnapshot>();
            var result = new CharacterTestNetworkBodyPartStateSnapshot[count];
            for (int i = 0; i < count; i++)
                result[i] = entities[i].BodyPartState.CaptureNetworkSnapshot();
            Array.Sort(result, CompareEntitySnapshots);
            return result;
        }

        internal static CharacterTestNetworkBodyPartStateSnapshot[] NormalizeCanonicalArray(
            CharacterTestNetworkBodyPartStateSnapshot[] source)
        {
            source ??= Array.Empty<CharacterTestNetworkBodyPartStateSnapshot>();
            for (int i = 0; i < source.Length; i++)
                source[i]?.NormalizeCanonical();
            Array.Sort(source, CompareEntitySnapshots);
            return source;
        }

        internal static CharacterTestNetworkBodyPartStateSnapshot[] NormalizeCanonicalArray(
            CharacterTestNetworkBodyPartStateSnapshot[] source,
            int schemaVersion)
        {
            source = NormalizeCanonicalArray(source);
            if (schemaVersion >=
                CharacterTestNetworkSnapshotParticipant.SeveredBodyPartStateSchemaVersionValue)
            {
                return source;
            }

            // Schema 31 serialized the structure enum as an integer before Severed
            // became a formal network value. Normalize a raw value of 2 to the
            // nearest representable authority state instead of allowing
            // Enum.IsDefined to silently opt the legacy schema into schema 32.
            for (int snapshotIndex = 0; snapshotIndex < source.Length; snapshotIndex++)
            {
                CharacterTestNetworkBodyPartStateEntrySnapshot[] entries =
                    source[snapshotIndex]?.Entries;
                if (entries == null)
                    continue;
                for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                {
                    CharacterTestNetworkBodyPartStateEntrySnapshot entry = entries[entryIndex];
                    if (entry != null
                        && entry.StructureState ==
                            (int)CharacterTestBodyPartStructureState.Severed)
                    {
                        entry.StructureState = (int)CharacterTestBodyPartStructureState.Broken;
                    }
                }
            }
            return source;
        }

        internal static CharacterTestNetworkBodyPartStateSnapshot[] CloneCanonicalArray(
            CharacterTestNetworkBodyPartStateSnapshot[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<CharacterTestNetworkBodyPartStateSnapshot>();
            var result = new CharacterTestNetworkBodyPartStateSnapshot[source.Length];
            for (int i = 0; i < source.Length; i++)
                result[i] = source[i]?.CloneCanonical();
            return NormalizeCanonicalArray(result);
        }

        internal static bool TryValidateDocumentShape(
            int schemaVersion,
            long snapshotFrame,
            CharacterTestNetworkCharacterSnapshot[] characters,
            CharacterTestNetworkBodyPartStateSnapshot[] snapshots,
            out string failure)
        {
            failure = string.Empty;
            characters ??= Array.Empty<CharacterTestNetworkCharacterSnapshot>();
            snapshots ??= Array.Empty<CharacterTestNetworkBodyPartStateSnapshot>();
            if (snapshots.Length != characters.Length)
            {
                failure = "CharacterTest body-part snapshot entity count does not match characters.";
                return false;
            }

            int previousEntityId = 0;
            for (int i = 0; i < snapshots.Length; i++)
            {
                CharacterTestNetworkCharacterSnapshot character = characters[i];
                CharacterTestNetworkBodyPartStateSnapshot snapshot = snapshots[i];
                if (character == null
                    || snapshot == null
                    || character.EntityId <= previousEntityId
                    || snapshot.EntityId != character.EntityId
                    || !TryValidateSnapshotShape(
                        snapshotFrame,
                        snapshot,
                        allowSevered: schemaVersion >=
                            CharacterTestNetworkSnapshotParticipant
                                .SeveredBodyPartStateSchemaVersionValue))
                {
                    failure = "CharacterTest body-part snapshot entity/source/entry shape is invalid.";
                    return false;
                }
                previousEntityId = character.EntityId;
            }
            return true;
        }

        internal static bool TryPrepareCurrentAuthority(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestNetworkBodyPartStateSnapshot[] snapshots,
            bool legacyDefaults,
            bool allowSevered,
            out CharacterTestBodyPartStateNetworkRestorePlan[] plans,
            out string failure)
        {
            failure = string.Empty;
            int entityCount = entities?.Count ?? 0;
            snapshots ??= Array.Empty<CharacterTestNetworkBodyPartStateSnapshot>();
            if (!legacyDefaults && snapshots.Length != entityCount)
            {
                plans = Array.Empty<CharacterTestBodyPartStateNetworkRestorePlan>();
                failure = "CharacterTest body-part snapshot does not cover the restored entity set.";
                return false;
            }

            plans = new CharacterTestBodyPartStateNetworkRestorePlan[entityCount];
            for (int i = 0; i < entityCount; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                CharacterTestBodyPartStateComponent component = entity?.BodyPartState;
                if (entity == null || component == null)
                {
                    failure = "CharacterTest restored entity is missing body-part authority.";
                    plans = Array.Empty<CharacterTestBodyPartStateNetworkRestorePlan>();
                    return false;
                }
                if (legacyDefaults)
                {
                    plans[i] = component.PrepareLegacyNetworkDefaults();
                    continue;
                }

                CharacterTestNetworkBodyPartStateSnapshot snapshot = snapshots[i];
                if (snapshot == null
                    || snapshot.EntityId != entity.Id.Value
                    || !component.TryPrepareNetworkRestore(
                        snapshot,
                        out plans[i],
                        out failure,
                        allowSevered))
                {
                    if (string.IsNullOrEmpty(failure))
                        failure = "CharacterTest body-part snapshot entity order does not match runtime authority.";
                    plans = Array.Empty<CharacterTestBodyPartStateNetworkRestorePlan>();
                    return false;
                }
            }
            return true;
        }

        internal static bool TryValidateDamageZoneLocks(
            long snapshotFrame,
            CharacterTestNetworkCharacterSnapshot[] characters,
            CharacterTestDamageZoneLockStateSnapshot[] locks,
            bool requireLegacyZeroRevision,
            out string failure)
        {
            failure = string.Empty;
            characters ??= Array.Empty<CharacterTestNetworkCharacterSnapshot>();
            locks ??= Array.Empty<CharacterTestDamageZoneLockStateSnapshot>();
            int previousSourceEntityId = 0;
            for (int i = 0; i < locks.Length; i++)
            {
                CharacterTestDamageZoneLockStateSnapshot value = locks[i];
                if (value == null
                    || value.SourceEntityId <= previousSourceEntityId
                    || !ContainsCharacterEntityId(characters, value.SourceEntityId)
                    || value.TargetGameplayEntityIndex <= 0
                    || value.TargetGameplayEntityGeneration <= 0
                    || value.TargetCombatEntityId <= 0
                    || !ContainsCharacterEntityId(
                        characters,
                        value.TargetCombatEntityId
                            - CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart)
                    || value.ResolvedActionRevision <= 0
                    || value.LogicalHurtboxId <= 0
                    || !Enum.IsDefined(
                        typeof(CharacterTestAttackOpportunityLevel),
                        value.OpportunityLevel)
                    || !Enum.IsDefined(
                        typeof(CharacterTestDamageZoneSwitchReason),
                        value.SwitchReason)
                    || value.BodyPartStateRevision < 0L
                    || (requireLegacyZeroRevision && value.BodyPartStateRevision != 0L)
                    || value.SelectedFrame < 0L
                    || value.SelectedFrame > snapshotFrame)
                {
                    failure = "CharacterTest DamageZone lock snapshot is invalid or non-canonical.";
                    return false;
                }
                previousSourceEntityId = value.SourceEntityId;
            }
            return true;
        }

        internal static bool TryPreflightDamageZoneLockRuntimeTargets(
            IReadOnlyList<CharacterTestCharacterEntity> currentEntities,
            CharacterTestNetworkCharacterSnapshot[] characters,
            CharacterTestDamageZoneLockStateSnapshot[] locks,
            int gameplayEntityAllocatedCount,
            out string failure)
        {
            failure = string.Empty;
            characters ??= Array.Empty<CharacterTestNetworkCharacterSnapshot>();
            locks ??= Array.Empty<CharacterTestDamageZoneLockStateSnapshot>();
            if (locks.Length == 0)
                return true;

            int currentCount = currentEntities?.Count ?? 0;
            bool freshLateJoin = currentCount == 0 && gameplayEntityAllocatedCount == 0;
            bool appliesInPlace = currentCount == characters.Length && currentCount > 0;
            for (int i = 0; appliesInPlace && i < currentCount; i++)
            {
                CharacterTestCharacterEntity entity = currentEntities[i];
                CharacterTestNetworkCharacterSnapshot character = characters[i];
                appliesInPlace = entity != null
                    && character != null
                    && entity.Id.Value == character.EntityId
                    && entity.Identity.CharacterId == character.CharacterId
                    && entity.Team.TeamId == character.TeamId
                    && entity.Team.DefaultTeamId == character.DefaultTeamId;
            }
            if (!freshLateJoin && !appliesInPlace)
            {
                failure = "CharacterTest DamageZone locks require a fresh late-join world or in-place character topology.";
                return false;
            }

            for (int i = 0; i < locks.Length; i++)
            {
                CharacterTestDamageZoneLockStateSnapshot value = locks[i];
                int targetEntityId = value.TargetCombatEntityId
                    - CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart;
                bool targetMatches = freshLateJoin
                    && value.TargetGameplayEntityIndex == targetEntityId
                    && value.TargetGameplayEntityGeneration == 1;
                for (int entityIndex = 0;
                    appliesInPlace && entityIndex < currentEntities.Count;
                    entityIndex++)
                {
                    CharacterTestCharacterEntity entity = currentEntities[entityIndex];
                    if (entity == null) continue;
                    if (entity.Id.Value == targetEntityId
                        && entity.GameplayEntityId.Index == value.TargetGameplayEntityIndex
                        && entity.GameplayEntityId.Generation == value.TargetGameplayEntityGeneration
                        && CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + targetEntityId
                            == value.TargetCombatEntityId)
                    {
                        targetMatches = true;
                        break;
                    }
                }
                if (!targetMatches)
                {
                    failure = "CharacterTest DamageZone lock target cannot match the post-apply runtime identity.";
                    return false;
                }
            }
            return true;
        }

        internal static void Commit(CharacterTestBodyPartStateNetworkRestorePlan[] plans)
        {
            plans ??= Array.Empty<CharacterTestBodyPartStateNetworkRestorePlan>();
            for (int i = 0; i < plans.Length; i++)
                plans[i].Commit();
        }

        internal static void AddStateHash(
            RuntimeHashAccumulator accumulator,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            int ownedEntityId = 0,
            bool excludeOwnedEntity = false)
        {
            int count = entities?.Count ?? 0;
            int includedCount = 0;
            for (int i = 0; i < count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                bool isOwned = entity != null && entity.Id.Value == ownedEntityId;
                if (ownedEntityId <= 0 || isOwned != excludeOwnedEntity)
                    includedCount++;
            }

            accumulator.BeginArray(NetworkBodyPartStateSnapshotHashFields.Entities, includedCount);
            int outputIndex = 0;
            for (int i = 0; i < count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                bool isOwned = entity != null && entity.Id.Value == ownedEntityId;
                if (ownedEntityId > 0 && isOwned == excludeOwnedEntity)
                    continue;
                accumulator.BeginElement(outputIndex);
                accumulator.BeginObject(NetworkBodyPartStateSnapshotHashFields.Entity,
                    RuntimeHashSchemaIds.NetworkSnapshotBodyPart, 2, entity != null);
                if (entity != null)
                    entity.BodyPartState.ContributeNetworkStateHash(accumulator);
                accumulator.EndObject();
                accumulator.EndElement();
                outputIndex++;
            }
            accumulator.EndArray();
        }


        private static bool TryValidateSnapshotShape(
            long snapshotFrame,
            CharacterTestNetworkBodyPartStateSnapshot snapshot,
            bool allowSevered)
        {
            if (snapshot.EntityId <= 0
                || snapshot.ComponentRevision < 0L
                || !Enum.IsDefined(
                    typeof(CharacterTestBodyPartStateInitializationStatus),
                    snapshot.InitializationStatus))
            {
                return false;
            }
            var status = (CharacterTestBodyPartStateInitializationStatus)snapshot.InitializationStatus;
            CharacterTestNetworkBodyPartStateEntrySnapshot[] entries = snapshot.Entries
                ?? Array.Empty<CharacterTestNetworkBodyPartStateEntrySnapshot>();
            if (status == CharacterTestBodyPartStateInitializationStatus.InvalidBinding)
                return false;
            if (status == CharacterTestBodyPartStateInitializationStatus.MissingDefinitions)
            {
                return snapshot.ComponentRevision == 0L
                    && snapshot.DefinitionSourceRevision == 0
                    && string.IsNullOrEmpty(snapshot.DefinitionSourceFormat)
                    && string.IsNullOrEmpty(snapshot.DefinitionSourceSchemaVersion)
                    && string.IsNullOrEmpty(snapshot.DefinitionSourceContentHash)
                    && entries.Length == 0;
            }
            if (entries.Length == 0
                || entries.Length > CharacterTestBodyPartStateComponent.MaximumDefinitionCount
                || snapshot.DefinitionSourceRevision <= 0
                || string.IsNullOrWhiteSpace(snapshot.DefinitionSourceFormat)
                || string.IsNullOrWhiteSpace(snapshot.DefinitionSourceSchemaVersion)
                || string.IsNullOrWhiteSpace(snapshot.DefinitionSourceContentHash))
            {
                return false;
            }

            int previousPartStateId = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                CharacterTestNetworkBodyPartStateEntrySnapshot entry = entries[i];
                if (entry == null
                    || entry.DefinitionIndex != i
                    || (entry.PartStateId <= 0)
                    || (i > 0 && CharacterTestAuthoredContentIds.GetLegacyOrdinalRank(400, previousPartStateId).CompareTo(CharacterTestAuthoredContentIds.GetLegacyOrdinalRank(400, entry.PartStateId)) >= 0)
                    || entry.WoundValue < 0
                    || !Enum.IsDefined(typeof(CharacterTestBodyPartWoundState), entry.WoundState)
                    || entry.CurrentIntegrity < 0
                    || !Enum.IsDefined(typeof(CharacterTestBodyPartStructureState), entry.StructureState)
                    || (!allowSevered
                        && entry.StructureState ==
                            (int)CharacterTestBodyPartStructureState.Severed)
                    || entry.LastAcceptedHitFrame < -1L
                    || entry.LastAcceptedHitFrame > snapshotFrame
                    || entry.Revision < 0L)
                {
                    return false;
                }
                previousPartStateId = entry.PartStateId;
            }
            return true;
        }

        private static bool ContainsCharacterEntityId(
            CharacterTestNetworkCharacterSnapshot[] characters,
            int entityId)
        {
            for (int i = 0; i < characters.Length; i++)
            {
                if (characters[i] != null && characters[i].EntityId == entityId)
                    return true;
            }
            return false;
        }

        private static int CompareEntitySnapshots(
            CharacterTestNetworkBodyPartStateSnapshot left,
            CharacterTestNetworkBodyPartStateSnapshot right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;
            return left.EntityId.CompareTo(right.EntityId);
        }
    }

    internal sealed partial class CharacterTestBodyPartStateComponent
    {
        internal CharacterTestNetworkBodyPartStateSnapshot CaptureNetworkSnapshot()
        {
            var entries = new CharacterTestNetworkBodyPartStateEntrySnapshot[DefinitionCount];
            for (int i = 0; i < DefinitionCount; i++)
            {
                CharacterTestBodyPartStateDefinition definition = _definitions[i];
                CharacterTestBodyPartStateEntry entry = _entries[i];
                entries[i] = new CharacterTestNetworkBodyPartStateEntrySnapshot
                {
                    DefinitionIndex = definition.DefinitionIndex,
                    PartStateId = definition.PartStateId,
                    WoundValue = entry.WoundValue,
                    WoundState = (int)entry.WoundState,
                    CurrentIntegrity = entry.CurrentIntegrity,
                    StructureState = (int)entry.StructureState,
                    LastAcceptedHitFrame = entry.LastAcceptedHitFrame,
                    Revision = entry.Revision
                };
            }
            return new CharacterTestNetworkBodyPartStateSnapshot
            {
                EntityId = OwnerEntityId,
                InitializationStatus = (int)InitializationStatus,
                ComponentRevision = Revision,
                DefinitionSourceFormat = DefinitionSourceFormat,
                DefinitionSourceSchemaVersion = DefinitionSourceSchemaVersion,
                DefinitionSourceRevision = DefinitionSourceRevision,
                DefinitionSourceContentHash = DefinitionSourceContentHash,
                Entries = entries
            };
        }

        internal bool TryPrepareNetworkRestore(
            CharacterTestNetworkBodyPartStateSnapshot snapshot,
            out CharacterTestBodyPartStateNetworkRestorePlan plan,
            out string failure,
            bool allowSevered = true)
        {
            plan = default;
            failure = string.Empty;
            if (snapshot == null
                || snapshot.EntityId != OwnerEntityId
                || snapshot.InitializationStatus != (int)InitializationStatus
                || InitializationStatus == CharacterTestBodyPartStateInitializationStatus.InvalidBinding
                || snapshot.ComponentRevision < 0L
                || snapshot.DefinitionSourceRevision != DefinitionSourceRevision
                || !string.Equals(
                    snapshot.DefinitionSourceFormat,
                    DefinitionSourceFormat,
                    StringComparison.Ordinal)
                || !string.Equals(
                    snapshot.DefinitionSourceSchemaVersion,
                    DefinitionSourceSchemaVersion,
                    StringComparison.Ordinal)
                || !string.Equals(
                    snapshot.DefinitionSourceContentHash,
                    DefinitionSourceContentHash,
                    StringComparison.Ordinal))
            {
                failure = "CharacterTest body-part snapshot definition source identity mismatch.";
                return false;
            }

            CharacterTestNetworkBodyPartStateEntrySnapshot[] snapshots = snapshot.Entries
                ?? Array.Empty<CharacterTestNetworkBodyPartStateEntrySnapshot>();
            if (snapshots.Length != DefinitionCount)
            {
                failure = "CharacterTest body-part snapshot definition count mismatch.";
                return false;
            }
            var entries = new CharacterTestBodyPartStateEntry[DefinitionCount];
            for (int i = 0; i < DefinitionCount; i++)
            {
                CharacterTestBodyPartStateDefinition definition = _definitions[i];
                CharacterTestNetworkBodyPartStateEntrySnapshot value = snapshots[i];
                if (value == null
                    || value.DefinitionIndex != i
                    || value.PartStateId != definition.PartStateId
                    || !TryCreateNetworkEntry(
                        definition,
                        value,
                        allowSevered,
                        out entries[i]))
                {
                    failure = "CharacterTest body-part snapshot entry does not match current definitions.";
                    return false;
                }
            }
            plan = new CharacterTestBodyPartStateNetworkRestorePlan(
                this,
                entries,
                snapshot.ComponentRevision,
                resetToLegacyDefaults: false);
            return true;
        }

        internal CharacterTestBodyPartStateNetworkRestorePlan PrepareLegacyNetworkDefaults()
        {
            return new CharacterTestBodyPartStateNetworkRestorePlan(
                this,
                Array.Empty<CharacterTestBodyPartStateEntry>(),
                componentRevision: 0L,
                resetToLegacyDefaults: true);
        }

        internal void CommitNetworkRestore(
            CharacterTestBodyPartStateEntry[] entries,
            long componentRevision)
        {
            for (int i = 0; i < DefinitionCount; i++)
            {
                _entries[i] = entries[i];
                _frameStartEntries[i] = default;
                _workEntries[i] = default;
            }
            Revision = componentRevision;
            ResetReducerState();
        }

        internal void CommitLegacyNetworkDefaults()
        {
            for (int i = 0; i < DefinitionCount; i++)
            {
                CharacterTestBodyPartStateDefinition definition = _definitions[i];
                _entries[i] = new CharacterTestBodyPartStateEntry(
                    i,
                    woundValue: 0,
                    CharacterTestBodyPartWoundState.Closed,
                    definition.MaxIntegrity,
                    CharacterTestBodyPartStructureState.Intact,
                    lastAcceptedHitFrame: -1L,
                    revision: 0L);
                _frameStartEntries[i] = default;
                _workEntries[i] = default;
            }
            Revision = 0L;
            ResetReducerState();
        }

        internal void ContributeNetworkStateHash(RuntimeHashAccumulator accumulator)
        {
            accumulator.AddInt(NetworkBodyPartStateEntityEntryHashFields.EntityId, OwnerEntityId);
            accumulator.AddInt(NetworkBodyPartStateEntityEntryHashFields.InitializationStatus, (int)InitializationStatus);
            accumulator.AddLong(NetworkBodyPartStateEntityEntryHashFields.ComponentRevision, Revision);
            accumulator.AddDigest(
                NetworkBodyPartStateEntityEntryHashFields.DefinitionSourceFormat,
                _sourceFormatDigest);
            accumulator.AddDigest(
                NetworkBodyPartStateEntityEntryHashFields.DefinitionSourceSchemaVersion,
                _sourceVersionDigest);
            accumulator.AddInt(NetworkBodyPartStateEntityEntryHashFields.DefinitionSourceRevision, DefinitionSourceRevision);
            _sourceContentDigest.Contribute(accumulator,
                NetworkBodyPartStateEntityEntryHashFields.DefinitionSourceContentHash);

            accumulator.BeginArray(NetworkBodyPartStateEntityEntryHashFields.Entries, DefinitionCount);
            for (int i = 0; i < DefinitionCount; i++)
            {
                accumulator.BeginElement(i);
                CharacterTestBodyPartStateDefinition definition = _definitions[i];
                CharacterTestBodyPartStateEntry entry = _entries[i];
                accumulator.AddInt(NetworkBodyPartStateEntryHashFields.DefinitionIndex, entry.DefinitionIndex);
                accumulator.AddInt(NetworkBodyPartStateEntryHashFields.PartStateId, definition.PartStateId);
                accumulator.AddInt(NetworkBodyPartStateEntryHashFields.WoundValue, entry.WoundValue);
                accumulator.AddInt(NetworkBodyPartStateEntryHashFields.WoundState, (int)entry.WoundState);
                accumulator.AddInt(NetworkBodyPartStateEntryHashFields.CurrentIntegrity, entry.CurrentIntegrity);
                accumulator.AddInt(NetworkBodyPartStateEntryHashFields.StructureState, (int)entry.StructureState);
                accumulator.AddLong(NetworkBodyPartStateEntryHashFields.LastAcceptedHitFrame, entry.LastAcceptedHitFrame);
                accumulator.AddLong(NetworkBodyPartStateEntryHashFields.Revision, entry.Revision);
                accumulator.EndElement();
            }
            accumulator.EndArray();
        }


        private static bool TryCreateNetworkEntry(
            CharacterTestBodyPartStateDefinition definition,
            CharacterTestNetworkBodyPartStateEntrySnapshot value,
            bool allowSevered,
            out CharacterTestBodyPartStateEntry entry)
        {
            entry = default;
            if (value.WoundValue < 0
                || value.WoundValue > definition.WoundCapacity
                || !Enum.IsDefined(typeof(CharacterTestBodyPartWoundState), value.WoundState)
                || value.CurrentIntegrity < 0
                || value.CurrentIntegrity > definition.MaxIntegrity
                || !Enum.IsDefined(typeof(CharacterTestBodyPartStructureState), value.StructureState)
                || (!allowSevered
                    && value.StructureState ==
                        (int)CharacterTestBodyPartStructureState.Severed)
                || (value.StructureState ==
                        (int)CharacterTestBodyPartStructureState.Severed
                    && !definition.Severable)
                || value.LastAcceptedHitFrame < -1L
                || value.Revision < 0L)
            {
                return false;
            }

            var woundState = (CharacterTestBodyPartWoundState)value.WoundState;
            var structureState = (CharacterTestBodyPartStructureState)value.StructureState;
            bool validWound = definition.WoundCapacity == 0
                ? value.WoundValue == 0 && woundState == CharacterTestBodyPartWoundState.Closed
                : woundState == CharacterTestBodyPartWoundState.Closed
                    ? value.WoundValue < definition.WoundCapacity
                    : value.WoundValue == definition.WoundCapacity
                        && (woundState != CharacterTestBodyPartWoundState.Consumed
                            || !string.IsNullOrEmpty(definition.FocusConsumeTag));
            bool validStructure = definition.MaxIntegrity == 0
                ? value.CurrentIntegrity == 0
                    && structureState == CharacterTestBodyPartStructureState.Intact
                : structureState == CharacterTestBodyPartStructureState.Intact
                    ? value.CurrentIntegrity > 0
                    : value.CurrentIntegrity == 0;
            if (!validWound || !validStructure)
                return false;

            entry = new CharacterTestBodyPartStateEntry(
                definition.DefinitionIndex,
                value.WoundValue,
                woundState,
                value.CurrentIntegrity,
                structureState,
                value.LastAcceptedHitFrame,
                value.Revision);
            return true;
        }
    }

    internal static class NetworkBodyPartStateSnapshotHashFields
    {
        public const int Entity = 2;
        public const int Entities = 1;
    }

    internal static class NetworkBodyPartStateEntityEntryHashFields
    {
        public const int EntityId = 1;
        public const int InitializationStatus = 2;
        public const int ComponentRevision = 3;
        public const int DefinitionSourceFormat = 4;
        public const int DefinitionSourceSchemaVersion = 5;
        public const int DefinitionSourceRevision = 6;
        public const int DefinitionSourceContentHash = 7;
        public const int Entries = 8;
    }

    internal static class NetworkBodyPartStateEntryHashFields
    {
        public const int DefinitionIndex = 1;
        public const int PartStateId = 2;
        public const int WoundValue = 3;
        public const int WoundState = 4;
        public const int CurrentIntegrity = 5;
        public const int StructureState = 6;
        public const int LastAcceptedHitFrame = 7;
        public const int Revision = 8;
    }
}
