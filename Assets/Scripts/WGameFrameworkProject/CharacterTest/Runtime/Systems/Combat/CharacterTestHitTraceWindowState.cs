using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestHitTraceWindowState
    {
        private static readonly Comparison<KeyValuePair<long, int>> CompareInstanceIdsCached = CompareInstanceIds;
        private static readonly Comparison<CharacterTestActiveHitTrace> CompareActiveTracesCached = CompareActiveTraces;
        private static readonly Comparison<WeaponHitOnceKey> CompareHitOnceKeysCached = CompareHitOnceKeys;
        private static readonly Comparison<CombatEntityId> CompareCombatEntityIdsCached = CompareCombatEntityIds;
        private static readonly Comparison<DamageAttributeScale> CompareDamageScalesCached = CompareDamageScales;

        private readonly HitTargetStateContributionStore _targetStateContributions =
            new HitTargetStateContributionStore();

        internal HitTargetStateContributionStore TargetStateContributions =>
            _targetStateContributions;
        private readonly List<CombatEntityId> _targetStateEntityScratch = new List<CombatEntityId>(64);
        private readonly Dictionary<long, int> _instanceIds = new Dictionary<long, int>();
        private readonly HashSet<WeaponHitOnceKey> _hitOnceKeys = new HashSet<WeaponHitOnceKey>();
        private readonly List<WeaponHitOnceKey> _hitOnceKeyRemovals =
            new List<WeaponHitOnceKey>(16);
        private readonly CharacterTestHitResolutionPolicyState _hitResolutionPolicyState =
            new CharacterTestHitResolutionPolicyState();
        private readonly List<CharacterTestActiveHitTrace> _activeTraces =
            new List<CharacterTestActiveHitTrace>();
        private readonly List<KeyValuePair<long, int>> _instanceIdSnapshotScratch =
            new List<KeyValuePair<long, int>>(64);
        private readonly List<WeaponHitOnceKey> _hitOnceSnapshotScratch =
            new List<WeaponHitOnceKey>(64);
        private readonly List<CharacterTestActiveHitTrace> _activeTraceSnapshotScratch =
            new List<CharacterTestActiveHitTrace>(64);
        private int _nextInstanceId = 1;
        private int _nextHitWindowId = 1;

        public IReadOnlyList<CharacterTestActiveHitTrace> ActiveTraces => _activeTraces;
        public HashSet<WeaponHitOnceKey> HitOnceKeys => _hitOnceKeys;
        internal CharacterTestHitResolutionPolicyState HitResolutionPolicyState => _hitResolutionPolicyState;
        public int ActiveTraceCount => _activeTraces.Count;

        internal CharacterTestHitResolutionPolicyNetworkSnapshot CaptureNetworkSnapshot()
        {
            CharacterTestHitResolutionPolicyNetworkSnapshot snapshot =
                _hitResolutionPolicyState.CaptureNetworkSnapshot();
            snapshot.NextInstanceId = _nextInstanceId;
            snapshot.NextHitWindowId = _nextHitWindowId;

            _instanceIdSnapshotScratch.Clear();
            foreach (KeyValuePair<long, int> pair in _instanceIds)
                _instanceIdSnapshotScratch.Add(pair);
            _instanceIdSnapshotScratch.Sort(CompareInstanceIdsCached);
            var instanceIds = new CharacterTestHitTraceInstanceNetworkSnapshot[
                _instanceIdSnapshotScratch.Count];
            for (int i = 0; i < _instanceIdSnapshotScratch.Count; i++)
            {
                KeyValuePair<long, int> pair = _instanceIdSnapshotScratch[i];
                instanceIds[i] = new CharacterTestHitTraceInstanceNetworkSnapshot
                {
                    SourceInstanceId = pair.Key,
                    ActionInstanceId = pair.Value
                };
            }
            snapshot.InstanceIds = instanceIds;

            _hitOnceSnapshotScratch.Clear();
            foreach (WeaponHitOnceKey key in _hitOnceKeys)
                _hitOnceSnapshotScratch.Add(key);
            _hitOnceSnapshotScratch.Sort(CompareHitOnceKeysCached);
            var hitOnceKeys = new CharacterTestHitOnceKeyNetworkSnapshot[
                _hitOnceSnapshotScratch.Count];
            for (int i = 0; i < _hitOnceSnapshotScratch.Count; i++)
            {
                WeaponHitOnceKey key = _hitOnceSnapshotScratch[i];
                hitOnceKeys[i] = new CharacterTestHitOnceKeyNetworkSnapshot
                {
                    ActionInstanceId = key.ActionInstanceId,
                    TraceId = key.TraceId,
                    TargetEntityId = key.TargetEntityId.Value
                };
            }
            snapshot.HitOnceKeys = hitOnceKeys;

            _activeTraceSnapshotScratch.Clear();
            _activeTraceSnapshotScratch.AddRange(_activeTraces);
            _activeTraceSnapshotScratch.Sort(CompareActiveTracesCached);
            var activeTraces = new CharacterTestHitTraceNetworkSnapshot[
                _activeTraceSnapshotScratch.Count];
            for (int i = 0; i < _activeTraceSnapshotScratch.Count; i++)
                activeTraces[i] = CreateNetworkSnapshot(_activeTraceSnapshotScratch[i]);
            snapshot.ActiveTraces = activeTraces;
            return snapshot;
        }

        internal bool TryRestoreNetworkSnapshot(
            CharacterTestHitResolutionPolicyNetworkSnapshot snapshot,
            CharacterTestCombatEntityLookup lookup,
            out string failure)
        {
            failure = string.Empty;
            if (!TryValidateNetworkSnapshot(snapshot, lookup, out failure))
                return false;

            var restoredInstanceIds = new KeyValuePair<long, int>[snapshot.InstanceIds.Length];
            for (int i = 0; i < snapshot.InstanceIds.Length; i++)
            {
                CharacterTestHitTraceInstanceNetworkSnapshot entry = snapshot.InstanceIds[i];
                restoredInstanceIds[i] = new KeyValuePair<long, int>(
                    entry.SourceInstanceId,
                    entry.ActionInstanceId);
            }

            var restoredHitOnceKeys = new WeaponHitOnceKey[snapshot.HitOnceKeys.Length];
            for (int i = 0; i < snapshot.HitOnceKeys.Length; i++)
            {
                CharacterTestHitOnceKeyNetworkSnapshot entry = snapshot.HitOnceKeys[i];
                restoredHitOnceKeys[i] = new WeaponHitOnceKey(
                    entry.ActionInstanceId,
                    entry.TraceId,
                    new CombatEntityId(entry.TargetEntityId));
            }

            var restoredTraces = new CharacterTestActiveHitTrace[snapshot.ActiveTraces.Length];
            for (int i = 0; i < snapshot.ActiveTraces.Length; i++)
            {
                if (!TryCreateActiveTrace(snapshot.ActiveTraces[i], out restoredTraces[i], out failure))
                    return false;
            }

            _instanceIds.Clear();
            for (int i = 0; i < restoredInstanceIds.Length; i++)
                _instanceIds.Add(restoredInstanceIds[i].Key, restoredInstanceIds[i].Value);
            _hitOnceKeys.Clear();
            for (int i = 0; i < restoredHitOnceKeys.Length; i++)
                _hitOnceKeys.Add(restoredHitOnceKeys[i]);
            _activeTraces.Clear();
            _activeTraces.AddRange(restoredTraces);
            _targetStateContributions.Clear();
            _nextInstanceId = snapshot.NextInstanceId;
            _nextHitWindowId = snapshot.NextHitWindowId;
            if (!_hitResolutionPolicyState.TryRestoreNetworkSnapshot(snapshot, out failure))
            {
                ClearNetworkState();
                return false;
            }
            return true;
        }

        internal void AddNetworkStateHash(
            RuntimeHashAccumulator accumulator,
            int sourceEntityIdFilter = 0,
            bool excludeSourceEntity = false)
        {
            if (accumulator == null)
                throw new ArgumentNullException(nameof(accumulator));
            accumulator.BeginObject(CharacterTestHitTraceWindowStateHashFields.F65, CharacterTestHitTraceWindowStateHashFields.SchemaId, CharacterTestHitTraceWindowStateHashFields.SchemaVersion, true);

            bool filtered = sourceEntityIdFilter > 0;
            // These generators and the source-instance map are shared
            // authority: a new action on any attacker advances the id stream
            // that later direct-hit windows consume. Keep them in full,
            // owned, and non-owned hashes rather than silently making the
            // filtered hashes blind to a future-id divergence.
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F1, _nextInstanceId);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F2, _nextHitWindowId);

            _instanceIdSnapshotScratch.Clear();
            foreach (KeyValuePair<long, int> pair in _instanceIds)
                _instanceIdSnapshotScratch.Add(pair);
            _instanceIdSnapshotScratch.Sort(CompareInstanceIdsCached);
            accumulator.BeginArray(
                CharacterTestHitTraceWindowStateHashFields.F3,
                _instanceIdSnapshotScratch.Count);
            for (int i = 0; i < _instanceIdSnapshotScratch.Count; i++)
            {
                accumulator.BeginElement(i);
                KeyValuePair<long, int> pair = _instanceIdSnapshotScratch[i];
                accumulator.AddLong(CharacterTestHitTraceWindowStateHashFields.F4, pair.Key);
                accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F5, pair.Value);
                accumulator.EndElement();
            }
            accumulator.EndArray();

            _activeTraceSnapshotScratch.Clear();
            for (int i = 0; i < _activeTraces.Count; i++)
            {
                CharacterTestActiveHitTrace trace = _activeTraces[i];
                bool sourceMatches = trace.EntityId == sourceEntityIdFilter;
                if (!filtered || (excludeSourceEntity ? !sourceMatches : sourceMatches))
                    _activeTraceSnapshotScratch.Add(trace);
            }
            _activeTraceSnapshotScratch.Sort(CompareActiveTracesCached);
            accumulator.BeginArray(
                CharacterTestHitTraceWindowStateHashFields.F6,
                _activeTraceSnapshotScratch.Count);
            for (int i = 0; i < _activeTraceSnapshotScratch.Count; i++)
            {
                accumulator.BeginElement(i);
                AddActiveTraceHash(accumulator, i, _activeTraceSnapshotScratch[i]);
                accumulator.EndElement();
            }
            accumulator.EndArray();

            _hitOnceSnapshotScratch.Clear();
            foreach (WeaponHitOnceKey key in _hitOnceKeys)
            {
                bool include = !filtered;
                if (filtered)
                {
                    include = false;
                    for (int i = 0; i < _activeTraceSnapshotScratch.Count; i++)
                    {
                        if (_activeTraceSnapshotScratch[i].HitWindowId == key.ActionInstanceId)
                        {
                            include = true;
                            break;
                        }
                    }
                }
                if (include)
                    _hitOnceSnapshotScratch.Add(key);
            }
            _hitOnceSnapshotScratch.Sort(CompareHitOnceKeysCached);
            accumulator.BeginArray(
                CharacterTestHitTraceWindowStateHashFields.F7,
                _hitOnceSnapshotScratch.Count);
            for (int i = 0; i < _hitOnceSnapshotScratch.Count; i++)
            {
                accumulator.BeginElement(i);
                WeaponHitOnceKey key = _hitOnceSnapshotScratch[i];
                accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F8, key.ActionInstanceId);
                accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F9, key.TraceId);
                accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F10, key.TargetEntityId.Value);
                accumulator.EndElement();
            }
            accumulator.EndArray();

            _hitResolutionPolicyState.AddStateHash(
                accumulator,
                sourceEntityIdFilter,
                excludeSourceEntity);

            _targetStateEntityScratch.Clear();
            _targetStateContributions.CopyEntityIds(_targetStateEntityScratch);
            _targetStateEntityScratch.Sort(CompareCombatEntityIdsCached);
            for (int i = _targetStateEntityScratch.Count - 1; filtered && i >= 0; i--)
            {
                int characterEntityId = CharacterTestCombatEntityLookup.ResolveCharacterEntityId(_targetStateEntityScratch[i]);
                if (excludeSourceEntity ? characterEntityId == sourceEntityIdFilter : characterEntityId != sourceEntityIdFilter)
                    _targetStateEntityScratch.RemoveAt(i);
            }
            accumulator.BeginArray(CharacterTestHitTraceWindowStateHashFields.F70, _targetStateEntityScratch.Count);
            for (int i = 0; i < _targetStateEntityScratch.Count; i++)
            {
                CombatEntityId entityId = _targetStateEntityScratch[i];
                accumulator.BeginElement(i);
                accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F11, entityId.Value);
                accumulator.AddInt(
                    CharacterTestHitTraceWindowStateHashFields.F12,
                    (int)_targetStateContributions.ResolveFlags(entityId));
                accumulator.EndElement();
            }
            accumulator.EndArray();
            accumulator.EndObject();
        }

        private static int CompareCombatEntityIds(CombatEntityId left, CombatEntityId right) =>
            left.Value.CompareTo(right.Value);

        private void ClearNetworkState()
        {
            _targetStateContributions.Clear();
            _instanceIds.Clear();
            _hitOnceKeys.Clear();
            _activeTraces.Clear();
            _hitResolutionPolicyState.Clear();
            _nextInstanceId = 1;
            _nextHitWindowId = 1;
        }

        private static bool TryValidateNetworkSnapshot(
            CharacterTestHitResolutionPolicyNetworkSnapshot snapshot,
            CharacterTestCombatEntityLookup lookup,
            out string failure)
        {
            failure = string.Empty;
            if (snapshot == null)
            {
                failure = "CharacterTest hit trace window snapshot is null.";
                return false;
            }
            if (snapshot.InstanceIds == null
                || snapshot.ActiveTraces == null
                || snapshot.HitOnceKeys == null)
            {
                failure = "CharacterTest hit trace window snapshot arrays are missing.";
                return false;
            }
            if (snapshot.NextInstanceId <= 0 || snapshot.NextHitWindowId <= 0)
            {
                failure = "CharacterTest hit trace window snapshot contains invalid next ids.";
                return false;
            }

            CharacterTestHitTraceInstanceNetworkSnapshot[] instanceIds =
                snapshot.InstanceIds ?? Array.Empty<CharacterTestHitTraceInstanceNetworkSnapshot>();
            long previousSourceInstanceId = 0L;
            int maxActionInstanceId = 0;
            var mappedActionInstanceIds = new HashSet<int>();
            for (int i = 0; i < instanceIds.Length; i++)
            {
                CharacterTestHitTraceInstanceNetworkSnapshot entry = instanceIds[i];
                if (entry == null || entry.SourceInstanceId <= 0L || entry.ActionInstanceId <= 0)
                {
                    failure = "CharacterTest hit trace window snapshot contains an invalid instance id entry.";
                    return false;
                }
                if (i > 0 && entry.SourceInstanceId <= previousSourceInstanceId)
                {
                    failure = "CharacterTest hit trace window instance ids are not in stable order.";
                    return false;
                }
                previousSourceInstanceId = entry.SourceInstanceId;
                if (!mappedActionInstanceIds.Add(entry.ActionInstanceId))
                {
                    failure = "CharacterTest hit trace window maps multiple source instance ids to one action instance id.";
                    return false;
                }
                if (entry.ActionInstanceId > maxActionInstanceId)
                    maxActionInstanceId = entry.ActionInstanceId;
            }
            if (snapshot.NextInstanceId <= maxActionInstanceId)
            {
                failure = "CharacterTest hit trace window next instance id is not above the captured map.";
                return false;
            }

            CharacterTestHitTraceNetworkSnapshot[] traces =
                snapshot.ActiveTraces ?? Array.Empty<CharacterTestHitTraceNetworkSnapshot>();
            int maxHitWindowId = 0;
            var knownHitWindows = new HashSet<int>();
            for (int i = 0; i < traces.Length; i++)
            {
                CharacterTestHitTraceNetworkSnapshot entry = traces[i];
                if (!TryValidateTraceEntry(entry, lookup, out failure))
                    return false;
                if (i > 0 && CompareNetworkTraceEntries(traces[i - 1], entry) >= 0)
                {
                    failure = "CharacterTest active hit traces are not in stable order or contain duplicates.";
                    return false;
                }
                if (!knownHitWindows.Add(entry.HitWindowId))
                {
                    failure = "CharacterTest hit trace window contains duplicate hit window ids.";
                    return false;
                }
                if (entry.HitWindowId > maxHitWindowId)
                    maxHitWindowId = entry.HitWindowId;

                if (entry.SourceInstanceId > 0L)
                {
                    bool found = false;
                    for (int mapIndex = 0; mapIndex < instanceIds.Length; mapIndex++)
                    {
                        if (instanceIds[mapIndex].SourceInstanceId == entry.SourceInstanceId)
                        {
                            found = instanceIds[mapIndex].ActionInstanceId == entry.SourceActionInstanceId;
                            break;
                        }
                    }
                    if (!found)
                    {
                        failure = "CharacterTest active hit trace references an unknown action instance mapping.";
                        return false;
                    }
                }
                else if (entry.SourceActionInstanceId != 0)
                {
                    failure = "CharacterTest active hit trace has an action id without a source instance.";
                    return false;
                }
            }
            if (snapshot.NextHitWindowId <= maxHitWindowId)
            {
                failure = "CharacterTest hit trace window next hit-window id is not above active traces.";
                return false;
            }

            CharacterTestHitOnceKeyNetworkSnapshot[] hitOnceKeys =
                snapshot.HitOnceKeys ?? Array.Empty<CharacterTestHitOnceKeyNetworkSnapshot>();
            CharacterTestHitOnceKeyNetworkSnapshot previousHitOnceKey = null;
            for (int i = 0; i < hitOnceKeys.Length; i++)
            {
                CharacterTestHitOnceKeyNetworkSnapshot entry = hitOnceKeys[i];
                if (entry == null
                    || entry.ActionInstanceId <= 0
                    || entry.TraceId < 0
                    || entry.TargetEntityId <= 0
                    || !knownHitWindows.Contains(entry.ActionInstanceId))
                {
                    failure = "CharacterTest hit trace window contains an invalid legacy hit-once key.";
                    return false;
                }
                if (previousHitOnceKey != null && CompareNetworkHitOnceKeys(previousHitOnceKey, entry) >= 0)
                {
                    failure = "CharacterTest legacy hit-once keys are not in stable order or contain duplicates.";
                    return false;
                }
                previousHitOnceKey = entry;
            }

            if (!CharacterTestHitResolutionPolicyState.TryValidateNetworkSnapshot(
                    snapshot,
                    out failure))
                return false;
            return true;
        }

        private static bool TryValidateTraceEntry(
            CharacterTestHitTraceNetworkSnapshot entry,
            CharacterTestCombatEntityLookup lookup,
            out string failure)
        {
            failure = string.Empty;
            if (entry == null
                || entry.EntityId <= 0
                || entry.SourceInstanceId < 0L
                || entry.SourceActionInstanceId < 0
                || entry.HitWindowId <= 0
                || entry.StartFrame < 0
                || !entry.ActionId.IsValid
                || entry.EquipmentSlotId < 0
                || entry.TraceProfileId < 0
                || entry.StableEventId < 0
                || entry.DamagePayload == null
                || entry.HitEvent == null
                || entry.HitVolume == null)
            {
                failure = "CharacterTest active hit trace snapshot contains a missing or invalid field.";
                return false;
            }
            if (lookup != null && lookup.FindEntity(entry.EntityId) == null)
            {
                failure = "CharacterTest active hit trace references an unknown source entity: "
                    + entry.EntityId.ToString(CultureInfo.InvariantCulture);
                return false;
            }
            if (!TryValidateDamagePayload(entry.DamagePayload, out failure)
                || !TryValidateHitEvent(entry.HitEvent, out failure)
                || !TryValidateHitVolume(entry.HitVolume, out failure))
                return false;
            return true;
        }

        private static bool TryValidateDamagePayload(
            CharacterTestDamagePayloadNetworkSnapshot payload,
            out string failure)
        {
            failure = string.Empty;
            if (payload == null
                || payload.DamageTypeId < 0
                || payload.FormulaId < 0
                || payload.Tags == null
                || payload.AnalysisCategory < 0
                || payload.PresentationProfileId < 0
                || payload.SourceAttributeScales == null
                || payload.BaseDamage < 0
                || payload.ImpactPower < 0
                || payload.LaunchPower < 0)
            {
                failure = "CharacterTest active hit trace damage payload is invalid.";
                return false;
            }
            int previousAttributeId = -1;
            for (int i = 0; i < payload.SourceAttributeScales.Length; i++)
            {
                DamageAttributeScaleNetworkSnapshot scale = payload.SourceAttributeScales[i];
                if (scale == null
                    || scale.AttributeId < 0
                    || float.IsNaN(scale.Scale)
                    || float.IsInfinity(scale.Scale)
                    || (i > 0 && scale.AttributeId <= previousAttributeId))
                {
                    failure = "CharacterTest active hit trace damage scales are invalid or unstable.";
                    return false;
                }
                previousAttributeId = scale.AttributeId;
            }
            for (int i = 0; i < payload.Tags.Length; i++)
            {
                if (payload.Tags[i] <= 0
                    || (i > 0 && payload.Tags[i - 1].CompareTo(payload.Tags[i]) >= 0))
                {
                    failure = "CharacterTest active hit trace damage tags are invalid or unstable.";
                    return false;
                }
            }
            return true;
        }

        private static bool TryValidateHitEvent(
            CharacterTestHitEventNetworkSnapshot hitEvent,
            out string failure)
        {
            failure = string.Empty;
            const int knownTargetMask = (int)(CombatHitTargetTypes.HostileCharacter
                | CombatHitTargetTypes.FriendlyCharacter
                | CombatHitTargetTypes.Self
                | CombatHitTargetTypes.ThirdPartyCharacter
                | CombatHitTargetTypes.DamageableObject
                | CombatHitTargetTypes.WorldWall
                | CombatHitTargetTypes.WorldGround
                | CombatHitTargetTypes.WorldStatic);
            if (hitEvent == null
                || hitEvent.StableEventId < 0
                || hitEvent.Damage == null
                || hitEvent.OnHitBuffIds == null
                || (hitEvent.TargetTypes & ~knownTargetMask) != 0
                || hitEvent.EnvironmentHitPolicy < (int)CombatEnvironmentHitPolicy.None
                || hitEvent.EnvironmentHitPolicy > (int)CombatEnvironmentHitPolicy.BlockedPresentation
                || hitEvent.MaxAcceptedHits <= 0
                || hitEvent.HitSameTargetPolicy < (int)CombatHitSameTargetPolicy.AllowRepeat
                || hitEvent.HitSameTargetPolicy > (int)CombatHitSameTargetPolicy.OncePerEvent)
            {
                failure = "CharacterTest active hit trace hit-event policy is invalid.";
                return false;
            }
            if (hitEvent.Damage.DamageTypeId < 0
                || hitEvent.Damage.Tags == null
                || hitEvent.Damage.AnalysisCategory < 0
                || hitEvent.Damage.BaseDamage < 0
                || hitEvent.Damage.ImpactPower < 0
                || hitEvent.Damage.LaunchPower < 0)
            {
                failure = "CharacterTest active hit trace hit-event damage is invalid.";
                return false;
            }
            for (int i = 0; i < hitEvent.Damage.Tags.Length; i++)
            {
                if (hitEvent.Damage.Tags[i] <= 0
                    || (i > 0 && hitEvent.Damage.Tags[i - 1].CompareTo(hitEvent.Damage.Tags[i]) >= 0))
                {
                    failure = "CharacterTest active hit trace hit-event tags are invalid or unstable.";
                    return false;
                }
            }
            for (int i = 0; i < hitEvent.OnHitBuffIds.Length; i++)
            {
                if (hitEvent.OnHitBuffIds[i] <= 0
                    || (i > 0 && hitEvent.OnHitBuffIds[i - 1] >= hitEvent.OnHitBuffIds[i]))
                {
                    failure = "CharacterTest active hit trace on-hit buffs are invalid or unstable.";
                    return false;
                }
            }
            return true;
        }

        private static bool TryValidateHitVolume(
            CharacterTestHitVolumeNetworkSnapshot hitVolume,
            out string failure)
        {
            failure = string.Empty;
            if (hitVolume == null
                || !AreFinite(
                    hitVolume.LocalPointAX,
                    hitVolume.LocalPointAY,
                    hitVolume.LocalPointAZ,
                    hitVolume.LocalPointBX,
                    hitVolume.LocalPointBY,
                    hitVolume.LocalPointBZ,
                    hitVolume.Radius,
                    hitVolume.BodyOffsetX,
                    hitVolume.BodyOffsetY,
                    hitVolume.BodyOffsetZ,
                    hitVolume.BodyRadiusScale,
                    hitVolume.BodyRadiusAdd,
                    hitVolume.BodyHeightScale,
                    hitVolume.BodyHeightAdd)
                || hitVolume.StartSocketId < 0
                || hitVolume.EndSocketId < 0
                || !Enum.IsDefined(typeof(CharacterActionHitVolumeSourceKind), hitVolume.SourceKind)
                || !Enum.IsDefined(typeof(CharacterActionHitVolumeShapeKind), hitVolume.ShapeKind)
                || hitVolume.Radius < 0f)
            {
                failure = "CharacterTest active hit trace hit-volume is invalid.";
                return false;
            }

            CharacterActionHitVolumeSourceKind source =
                (CharacterActionHitVolumeSourceKind)hitVolume.SourceKind;
            CharacterActionHitVolumeShapeKind shape =
                (CharacterActionHitVolumeShapeKind)hitVolume.ShapeKind;
            if (source == CharacterActionHitVolumeSourceKind.CasterBody)
            {
                if (shape != CharacterActionHitVolumeShapeKind.Capsule
                    || hitVolume.Radius != 0f
                    || !(hitVolume.StartSocketId == 0)
                    || !(hitVolume.EndSocketId == 0)
                    || hitVolume.BodyRadiusScale <= 0f
                    || hitVolume.BodyHeightScale <= 0f)
                {
                    failure = "CharacterTest caster-body hit volume is not canonical.";
                    return false;
                }
            }
            else if (source == CharacterActionHitVolumeSourceKind.BakedSocketCapsule)
            {
                if (shape != CharacterActionHitVolumeShapeKind.Capsule
                    || hitVolume.Radius <= 0f
                    || (hitVolume.StartSocketId == 0)
                    || (hitVolume.EndSocketId == 0))
                {
                    failure = "CharacterTest baked-socket hit volume is not canonical.";
                    return false;
                }
            }
            else if (shape == CharacterActionHitVolumeShapeKind.None)
            {
                if (hitVolume.Radius != 0f)
                {
                    failure = "CharacterTest empty hit volume contains a radius.";
                    return false;
                }
            }
            else if (hitVolume.Radius <= 0f)
            {
                failure = "CharacterTest manual hit volume has no positive radius.";
                return false;
            }
            return true;
        }

        private static bool AreFinite(params float[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (float.IsNaN(values[i]) || float.IsInfinity(values[i]))
                    return false;
            }
            return true;
        }

        private static bool TryCreateActiveTrace(
            CharacterTestHitTraceNetworkSnapshot snapshot,
            out CharacterTestActiveHitTrace trace,
            out string failure)
        {
            trace = default(CharacterTestActiveHitTrace);
            failure = string.Empty;
            if (!TryCreateDamagePayload(snapshot.DamagePayload, out CharacterActionDamagePayload damagePayload)
                || !TryCreateHitEvent(snapshot.HitEvent, out CombatHitEventConfig hitEvent)
                || !TryCreateHitVolume(snapshot.HitVolume, out CharacterActionHitVolumeConfig hitVolume))
            {
                failure = "CharacterTest active hit trace snapshot could not reconstruct its authored payload.";
                return false;
            }

            trace = new CharacterTestActiveHitTrace(
                snapshot.EntityId,
                snapshot.ActionId,
                snapshot.SourceInstanceId,
                snapshot.SourceActionInstanceId,
                snapshot.HitWindowId,
                snapshot.EquipmentSlotId,
                snapshot.TraceProfileId,
                damagePayload,
                hitEvent,
                hitVolume,
                snapshot.StableEventId,
                snapshot.StartFrame);
            return true;
        }

        private static bool TryCreateDamagePayload(
            CharacterTestDamagePayloadNetworkSnapshot snapshot,
            out CharacterActionDamagePayload payload)
        {
            payload = CharacterActionDamagePayload.Empty;
            if (!TryValidateDamagePayload(snapshot, out _))
                return false;

            var scales = new DamageAttributeScale[snapshot.SourceAttributeScales.Length];
            for (int i = 0; i < scales.Length; i++)
            {
                DamageAttributeScaleNetworkSnapshot scale = snapshot.SourceAttributeScales[i];
                scales[i] = new DamageAttributeScale(scale.AttributeId, scale.Scale);
            }
            payload = new CharacterActionDamagePayload(
                snapshot.DamageTypeId,
                snapshot.FormulaId,
                snapshot.BaseDamage,
                scales,
                snapshot.ImpactPower,
                snapshot.Tags,
                snapshot.AnalysisCategory,
                snapshot.PresentationProfileId,
                snapshot.LaunchPower);
            return true;
        }

        private static bool TryCreateHitEvent(
            CharacterTestHitEventNetworkSnapshot snapshot,
            out CombatHitEventConfig hitEvent)
        {
            hitEvent = default(CombatHitEventConfig);
            if (!TryValidateHitEvent(snapshot, out _))
                return false;

            var buffs = new CombatHitBuffApplicationConfig[snapshot.OnHitBuffIds.Length];
            for (int i = 0; i < buffs.Length; i++)
                buffs[i] = new CombatHitBuffApplicationConfig(snapshot.OnHitBuffIds[i]);
            hitEvent = new CombatHitEventConfig(
                (CombatHitTargetTypes)snapshot.TargetTypes,
                (CombatEnvironmentHitPolicy)snapshot.EnvironmentHitPolicy,
                snapshot.StableEventId,
                snapshot.MaxAcceptedHits,
                (CombatHitSameTargetPolicy)snapshot.HitSameTargetPolicy,
                new CombatHitDamageConfig(
                    snapshot.Damage.DamageTypeId,
                    snapshot.Damage.BaseDamage,
                    snapshot.Damage.ImpactPower,
                    snapshot.Damage.Tags,
                    snapshot.Damage.AnalysisCategory,
                    snapshot.Damage.LaunchPower),
                buffs);
            return true;
        }

        private static bool TryCreateHitVolume(
            CharacterTestHitVolumeNetworkSnapshot snapshot,
            out CharacterActionHitVolumeConfig hitVolume)
        {
            hitVolume = default(CharacterActionHitVolumeConfig);
            if (!TryValidateHitVolume(snapshot, out _))
                return false;

            var source = (CharacterActionHitVolumeSourceKind)snapshot.SourceKind;
            var shape = (CharacterActionHitVolumeShapeKind)snapshot.ShapeKind;
            switch (source)
            {
                case CharacterActionHitVolumeSourceKind.CasterBody:
                    hitVolume = CharacterActionHitVolumeConfig.CasterBody(
                        snapshot.BodyOffsetX,
                        snapshot.BodyOffsetY,
                        snapshot.BodyOffsetZ,
                        snapshot.BodyRadiusScale,
                        snapshot.BodyRadiusAdd,
                        snapshot.BodyHeightScale,
                        snapshot.BodyHeightAdd);
                    return true;
                case CharacterActionHitVolumeSourceKind.BakedSocketCapsule:
                    hitVolume = CharacterActionHitVolumeConfig.BakedSocketCapsule(
                        snapshot.StartSocketId,
                        snapshot.EndSocketId,
                        snapshot.Radius);
                    return true;
                default:
                    hitVolume = new CharacterActionHitVolumeConfig(
                        source,
                        shape,
                        snapshot.LocalPointAX,
                        snapshot.LocalPointAY,
                        snapshot.LocalPointAZ,
                        snapshot.LocalPointBX,
                        snapshot.LocalPointBY,
                        snapshot.LocalPointBZ,
                        snapshot.Radius);
                    return true;
            }
        }

        private static CharacterTestHitTraceNetworkSnapshot CreateNetworkSnapshot(
            CharacterTestActiveHitTrace trace)
        {
            CharacterActionDamagePayload payload = trace.DamagePayload;
            DamageAttributeScale[] scales = payload.SourceAttributeScales ?? Array.Empty<DamageAttributeScale>();
            var scaleSnapshots = new DamageAttributeScaleNetworkSnapshot[scales.Length];
            for (int i = 0; i < scales.Length; i++)
            {
                scaleSnapshots[i] = new DamageAttributeScaleNetworkSnapshot
                {
                    AttributeId = scales[i].AttributeId,
                    Scale = scales[i].Scale
                };
            }
            Array.Sort(scaleSnapshots, CompareScaleSnapshots);
            int[] tags = payload.Tags ?? Array.Empty<int>();
            tags = (int[])tags.Clone();
            Array.Sort(tags);
            CharacterTestHitEventNetworkSnapshot hitEvent = CreateHitEventSnapshot(trace.HitEvent);
            CharacterActionHitVolumeConfig volume = trace.HitVolume;
            return new CharacterTestHitTraceNetworkSnapshot
            {
                EntityId = trace.EntityId,
                ActionId = trace.ActionId,
                SourceInstanceId = trace.SourceInstanceId,
                SourceActionInstanceId = trace.SourceActionInstanceId,
                HitWindowId = trace.HitWindowId,
                EquipmentSlotId = trace.EquipmentSlotId,
                TraceProfileId = trace.TraceProfileId,
                DamagePayload = new CharacterTestDamagePayloadNetworkSnapshot
                {
                    DamageTypeId = payload.DamageTypeId,
                    FormulaId = payload.FormulaId,
                    BaseDamage = payload.BaseDamage,
                    SourceAttributeScales = scaleSnapshots,
                    ImpactPower = payload.ImpactPower,
                    LaunchPower = payload.LaunchPower,
                    Tags = tags,
                    AnalysisCategory = payload.AnalysisCategory,
                    PresentationProfileId = payload.PresentationProfileId
                },
                HitEvent = hitEvent,
                HitVolume = CreateHitVolumeSnapshot(volume),
                StableEventId = trace.StableEventId,
                StartFrame = trace.StartFrame
            };
        }

        private static CharacterTestHitEventNetworkSnapshot CreateHitEventSnapshot(
            CombatHitEventConfig hitEvent)
        {
            CombatHitDamageConfig damage = hitEvent.Damage;
            int[] tags = damage.Tags ?? Array.Empty<int>();
            tags = (int[])tags.Clone();
            Array.Sort(tags);
            CombatHitBuffApplicationConfig[] buffs = hitEvent.OnHitBuffs ?? Array.Empty<CombatHitBuffApplicationConfig>();
            var buffIds = new int[buffs.Length];
            for (int i = 0; i < buffs.Length; i++)
                buffIds[i] = buffs[i].BuffId;
            Array.Sort(buffIds);
            return new CharacterTestHitEventNetworkSnapshot
            {
                TargetTypes = (int)hitEvent.TargetTypes,
                EnvironmentHitPolicy = (int)hitEvent.EnvironmentHitPolicy,
                StableEventId = hitEvent.StableEventId,
                MaxAcceptedHits = hitEvent.MaxAcceptedHits,
                HitSameTargetPolicy = (int)hitEvent.HitSameTargetPolicy,
                Damage = new CharacterTestHitDamageNetworkSnapshot
                {
                    DamageTypeId = damage.DamageTypeId,
                    BaseDamage = damage.BaseDamage,
                    ImpactPower = damage.ImpactPower,
                    LaunchPower = damage.LaunchPower,
                    Tags = tags,
                    AnalysisCategory = damage.AnalysisCategory
                },
                OnHitBuffIds = buffIds
            };
        }

        private static CharacterTestHitVolumeNetworkSnapshot CreateHitVolumeSnapshot(
            CharacterActionHitVolumeConfig volume)
        {
            return new CharacterTestHitVolumeNetworkSnapshot
            {
                SourceKind = (int)volume.SourceKind,
                ShapeKind = (int)volume.ShapeKind,
                LocalPointAX = volume.LocalPointAX,
                LocalPointAY = volume.LocalPointAY,
                LocalPointAZ = volume.LocalPointAZ,
                LocalPointBX = volume.LocalPointBX,
                LocalPointBY = volume.LocalPointBY,
                LocalPointBZ = volume.LocalPointBZ,
                Radius = volume.Radius,
                BodyOffsetX = volume.BodyOffsetX,
                BodyOffsetY = volume.BodyOffsetY,
                BodyOffsetZ = volume.BodyOffsetZ,
                BodyRadiusScale = volume.BodyRadiusScale,
                BodyRadiusAdd = volume.BodyRadiusAdd,
                BodyHeightScale = volume.BodyHeightScale,
                BodyHeightAdd = volume.BodyHeightAdd,
                StartSocketId = volume.StartSocketId,
                EndSocketId = volume.EndSocketId
            };
        }

        private static int CompareInstanceIds(
            KeyValuePair<long, int> left,
            KeyValuePair<long, int> right)
        {
            int compare = left.Key.CompareTo(right.Key);
            return compare != 0 ? compare : left.Value.CompareTo(right.Value);
        }

        private static int CompareHitOnceKeys(WeaponHitOnceKey left, WeaponHitOnceKey right)
        {
            int compare = left.ActionInstanceId.CompareTo(right.ActionInstanceId);
            if (compare != 0)
                return compare;
            compare = left.TraceId.CompareTo(right.TraceId);
            return compare != 0 ? compare : left.TargetEntityId.Value.CompareTo(right.TargetEntityId.Value);
        }

        private static int CompareActiveTraces(
            CharacterTestActiveHitTrace left,
            CharacterTestActiveHitTrace right)
        {
            int compare = left.EntityId.CompareTo(right.EntityId);
            if (compare != 0)
                return compare;
            compare = left.HitWindowId.CompareTo(right.HitWindowId);
            if (compare != 0)
                return compare;
            compare = left.SourceActionInstanceId.CompareTo(right.SourceActionInstanceId);
            if (compare != 0)
                return compare;
            compare = left.SourceInstanceId.CompareTo(right.SourceInstanceId);
            if (compare != 0)
                return compare;
            return CharacterTestAuthoredContentIds.GetEventOrder(left.StableEventId).CompareTo(CharacterTestAuthoredContentIds.GetEventOrder(right.StableEventId));
        }

        private static int CompareNetworkTraceEntries(
            CharacterTestHitTraceNetworkSnapshot left,
            CharacterTestHitTraceNetworkSnapshot right)
        {
            int compare = left.EntityId.CompareTo(right.EntityId);
            if (compare != 0)
                return compare;
            compare = left.HitWindowId.CompareTo(right.HitWindowId);
            if (compare != 0)
                return compare;
            compare = left.SourceActionInstanceId.CompareTo(right.SourceActionInstanceId);
            if (compare != 0)
                return compare;
            compare = left.SourceInstanceId.CompareTo(right.SourceInstanceId);
            return compare != 0 ? compare : CharacterTestAuthoredContentIds.GetEventOrder(left.StableEventId).CompareTo(CharacterTestAuthoredContentIds.GetEventOrder(right.StableEventId));
        }

        private static int CompareNetworkHitOnceKeys(
            CharacterTestHitOnceKeyNetworkSnapshot left,
            CharacterTestHitOnceKeyNetworkSnapshot right)
        {
            int compare = left.ActionInstanceId.CompareTo(right.ActionInstanceId);
            if (compare != 0)
                return compare;
            compare = left.TraceId.CompareTo(right.TraceId);
            return compare != 0 ? compare : left.TargetEntityId.CompareTo(right.TargetEntityId);
        }

        private static int CompareScaleSnapshots(
            DamageAttributeScaleNetworkSnapshot left,
            DamageAttributeScaleNetworkSnapshot right)
        {
            return left.AttributeId.CompareTo(right.AttributeId);
        }

        private readonly List<DamageAttributeScale> _damageScalesHashScratch = new List<DamageAttributeScale>(16);
        private readonly List<int> _tagsHashScratch = new List<int>(16);
        private static readonly Comparison<int> CompareTagsCached = (left, right) => left.CompareTo(right);

        private void AddActiveTraceHash(
            RuntimeHashAccumulator accumulator,
            int index,
            CharacterTestActiveHitTrace trace)
        {
            accumulator.BeginObject(CharacterTestHitTraceWindowStateHashFields.F66, CharacterTestHitTraceWindowStateHashFields.SchemaId, CharacterTestHitTraceWindowStateHashFields.SchemaVersion, true);

            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F13, trace.EntityId);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F14, trace.ActionId.Value);
            accumulator.AddLong(CharacterTestHitTraceWindowStateHashFields.F15, trace.SourceInstanceId);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F16, trace.SourceActionInstanceId);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F17, trace.HitWindowId);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F18, trace.EquipmentSlotId);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F19, trace.TraceProfileId);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F20, trace.StableEventId);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F21, trace.StartFrame);
            AddDamagePayloadHash(accumulator, CharacterTestHitTraceWindowStateHashFields.F67, trace.DamagePayload);
            AddHitEventHash(accumulator, CharacterTestHitTraceWindowStateHashFields.F68, trace.HitEvent);
            AddHitVolumeHash(accumulator, CharacterTestHitTraceWindowStateHashFields.F69, trace.HitVolume);
            accumulator.EndObject();
        }

        private void AddDamagePayloadHash(
            RuntimeHashAccumulator accumulator,
            int key,
            CharacterActionDamagePayload payload)
        {
            accumulator.BeginObject(key, CharacterTestHitTraceWindowStateHashFields.SchemaId, CharacterTestHitTraceWindowStateHashFields.SchemaVersion, true);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F22, payload.DamageTypeId);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F23, payload.FormulaId);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F24, payload.BaseDamage);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F25, payload.ImpactPower);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F26, payload.LaunchPower);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F27, payload.AnalysisCategory);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F28, payload.PresentationProfileId);
            DamageAttributeScale[] scales = payload.SourceAttributeScales ?? Array.Empty<DamageAttributeScale>();
            var sortedScales = _damageScalesHashScratch;
            sortedScales.Clear();
            for (int i = 0; i < scales.Length; i++) sortedScales.Add(scales[i]);
            sortedScales.Sort(CompareDamageScalesCached);
            accumulator.BeginArray(CharacterTestHitTraceWindowStateHashFields.F29, sortedScales.Count);
            for (int i = 0; i < sortedScales.Count; i++)
            {
                accumulator.BeginElement(i);
                accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F30, sortedScales[i].AttributeId);
                accumulator.AddDoubleQuantized(CharacterTestHitTraceWindowStateHashFields.F31, sortedScales[i].Scale, 1000000d);
                accumulator.EndElement();
            }
            accumulator.EndArray();
            int[] tags = payload.Tags ?? Array.Empty<int>();
            var sortedTags = _tagsHashScratch;
            sortedTags.Clear();
            for (int i = 0; i < tags.Length; i++) sortedTags.Add(tags[i]);
            sortedTags.Sort(CompareTagsCached);
            accumulator.BeginArray(CharacterTestHitTraceWindowStateHashFields.F32, sortedTags.Count);
            for (int i = 0; i < sortedTags.Count; i++)
            {
                accumulator.BeginElement(i);
                accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F33, sortedTags[i]);
                accumulator.EndElement();
            }
            accumulator.EndArray();
            accumulator.EndObject();
        }

        private static void AddHitEventHash(
            RuntimeHashAccumulator accumulator,
            int key,
            CombatHitEventConfig hitEvent)
        {
            accumulator.BeginObject(key, CharacterTestHitTraceWindowStateHashFields.SchemaId, CharacterTestHitTraceWindowStateHashFields.SchemaVersion, true);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F34, (int)hitEvent.TargetTypes);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F35, (int)hitEvent.EnvironmentHitPolicy);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F20, hitEvent.StableEventId);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F36, hitEvent.MaxAcceptedHits);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F37, (int)hitEvent.HitSameTargetPolicy);
            CombatHitDamageConfig damage = hitEvent.Damage;
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F38, damage.DamageTypeId);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F39, damage.BaseDamage);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F40, damage.ImpactPower);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F41, damage.LaunchPower);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F42, damage.AnalysisCategory);
            int[] tags = damage.Tags ?? Array.Empty<int>();
            accumulator.BeginArray(CharacterTestHitTraceWindowStateHashFields.F43, tags.Length);
            for (int i = 0; i < tags.Length; i++)
            {
                accumulator.BeginElement(i);
                accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F44, tags[i]);
                accumulator.EndElement();
            }
            accumulator.EndArray();
            CombatHitBuffApplicationConfig[] buffs = hitEvent.OnHitBuffs ?? Array.Empty<CombatHitBuffApplicationConfig>();
            accumulator.BeginArray(CharacterTestHitTraceWindowStateHashFields.F45, buffs.Length);
            for (int i = 0; i < buffs.Length; i++)
            {
                accumulator.BeginElement(i);
                accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F46, buffs[i].BuffId);
                accumulator.EndElement();
            }
            accumulator.EndArray();
            accumulator.EndObject();
        }

        private static void AddHitVolumeHash(
            RuntimeHashAccumulator accumulator,
            int key,
            CharacterActionHitVolumeConfig volume)
        {
            accumulator.BeginObject(key, CharacterTestHitTraceWindowStateHashFields.SchemaId, CharacterTestHitTraceWindowStateHashFields.SchemaVersion, true);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F47, (int)volume.SourceKind);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F48, (int)volume.ShapeKind);
            accumulator.AddDoubleQuantized(CharacterTestHitTraceWindowStateHashFields.F49, volume.LocalPointAX, 1000000d);
            accumulator.AddDoubleQuantized(CharacterTestHitTraceWindowStateHashFields.F50, volume.LocalPointAY, 1000000d);
            accumulator.AddDoubleQuantized(CharacterTestHitTraceWindowStateHashFields.F51, volume.LocalPointAZ, 1000000d);
            accumulator.AddDoubleQuantized(CharacterTestHitTraceWindowStateHashFields.F52, volume.LocalPointBX, 1000000d);
            accumulator.AddDoubleQuantized(CharacterTestHitTraceWindowStateHashFields.F53, volume.LocalPointBY, 1000000d);
            accumulator.AddDoubleQuantized(CharacterTestHitTraceWindowStateHashFields.F54, volume.LocalPointBZ, 1000000d);
            accumulator.AddDoubleQuantized(CharacterTestHitTraceWindowStateHashFields.F55, volume.Radius, 1000000d);
            accumulator.AddDoubleQuantized(CharacterTestHitTraceWindowStateHashFields.F56, volume.BodyOffsetX, 1000000d);
            accumulator.AddDoubleQuantized(CharacterTestHitTraceWindowStateHashFields.F57, volume.BodyOffsetY, 1000000d);
            accumulator.AddDoubleQuantized(CharacterTestHitTraceWindowStateHashFields.F58, volume.BodyOffsetZ, 1000000d);
            accumulator.AddDoubleQuantized(CharacterTestHitTraceWindowStateHashFields.F59, volume.BodyRadiusScale, 1000000d);
            accumulator.AddDoubleQuantized(CharacterTestHitTraceWindowStateHashFields.F60, volume.BodyRadiusAdd, 1000000d);
            accumulator.AddDoubleQuantized(CharacterTestHitTraceWindowStateHashFields.F61, volume.BodyHeightScale, 1000000d);
            accumulator.AddDoubleQuantized(CharacterTestHitTraceWindowStateHashFields.F62, volume.BodyHeightAdd, 1000000d);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F63, volume.StartSocketId);
            accumulator.AddInt(CharacterTestHitTraceWindowStateHashFields.F64, volume.EndSocketId);
            accumulator.EndObject();
        }

        private static int CompareDamageScales(DamageAttributeScale left, DamageAttributeScale right)
        {
            return left.AttributeId.CompareTo(right.AttributeId);
        }

        public CharacterTestHitTraceWindowReplayState CaptureReplayState()
        {
            return new CharacterTestHitTraceWindowReplayState(
                _instanceIds,
                _hitOnceKeys,
                _activeTraces,
                _nextInstanceId,
                _nextHitWindowId,
                _hitResolutionPolicyState.CaptureSnapshot());
        }

        public void RestoreReplayState(CharacterTestHitTraceWindowReplayState state)
        {
            _targetStateContributions.Clear();
            _instanceIds.Clear();
            _hitOnceKeys.Clear();
            _hitResolutionPolicyState.Clear();
            _activeTraces.Clear();
            if (state == null)
            {
                _nextInstanceId = 1;
                _nextHitWindowId = 1;
                return;
            }
            state.CopyTo(_instanceIds, _hitOnceKeys, _activeTraces);
            _hitResolutionPolicyState.RestoreSnapshot(state.PolicyStateSnapshot);
            _nextInstanceId = state.NextInstanceId;
            _nextHitWindowId = state.NextHitWindowId;
        }

        public void ProcessStartCombatRequests(
            CharacterTestCharacterRuntimeWorld world,
            RuntimeFrame frame,
            CharacterTestTraceRecorder traceRecorder)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
            {
                CharacterTestCharacterEntity entity = entities[entityIndex];
                IReadOnlyList<CharacterActionCombatRequest> requests = entity.CombatRequests;
                for (int requestIndex = 0; requestIndex < requests.Count; requestIndex++)
                {
                    CharacterActionCombatRequest request = requests[requestIndex];
                    if (request.EventKind == CharacterActionTrackEventKind.SetHitTargetState)
                    {
                        if (request.Metadata.LifecyclePhase == CharacterActionTrackLifecyclePhase.Enter
                            || request.Metadata.LifecyclePhase == CharacterActionTrackLifecyclePhase.Active)
                        {
                            _targetStateContributions.Upsert(
                                request.CombatEntityId,
                                request.Metadata.InstanceId,
                                request.Metadata.StableEventId,
                                request.TargetStateFlags);
                        }
                        else if (request.Metadata.LifecyclePhase == CharacterActionTrackLifecyclePhase.Exit
                            && request.Metadata.LifecycleBoundary == CharacterActionTrackLifecycleBoundary.BeforeFrame)
                        {
                            _targetStateContributions.Remove(
                                request.CombatEntityId,
                                request.Metadata.InstanceId,
                                request.Metadata.StableEventId);
                        }
                    }
                    else if (request.EventKind == CharacterActionTrackEventKind.HitTraceWindow
                        && request.Metadata.LifecyclePhase == CharacterActionTrackLifecyclePhase.Enter)
                        StartTrace(entity, request, frame, traceRecorder);
                    else if (request.EventKind == CharacterActionTrackEventKind.HitTraceWindow
                        && request.Metadata.LifecyclePhase == CharacterActionTrackLifecyclePhase.Exit
                        && request.Metadata.LifecycleBoundary == CharacterActionTrackLifecycleBoundary.BeforeFrame)
                        StopTrace(entity, request, frame, traceRecorder);
                }
            }
        }

        public void ProcessStopCombatRequestsAndClear(
            CharacterTestCharacterRuntimeWorld world,
            RuntimeFrame frame,
            CharacterTestTraceRecorder traceRecorder)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
            {
                CharacterTestCharacterEntity entity = entities[entityIndex];
                IReadOnlyList<CharacterActionCombatRequest> requests = entity.CombatRequests;
                for (int requestIndex = 0; requestIndex < requests.Count; requestIndex++)
                {
                    CharacterActionCombatRequest request = requests[requestIndex];
                    if (request.EventKind == CharacterActionTrackEventKind.SetHitTargetState
                        && request.Metadata.LifecyclePhase == CharacterActionTrackLifecyclePhase.Exit)
                    {
                        _targetStateContributions.Remove(
                            request.CombatEntityId,
                            request.Metadata.InstanceId,
                            request.Metadata.StableEventId);
                    }
                    else if (request.EventKind == CharacterActionTrackEventKind.HitTraceWindow
                        && request.Metadata.LifecyclePhase == CharacterActionTrackLifecyclePhase.Exit)
                    {
                        if (request.Metadata.LifecycleBoundary == CharacterActionTrackLifecycleBoundary.AfterFrame)
                            StopTrace(entity, request, frame, traceRecorder);
                        else if (request.Metadata.LifecycleBoundary == CharacterActionTrackLifecycleBoundary.BeforeFrame)
                            StopTrace(entity, request, frame, traceRecorder, traceWhenMissing: false);
                    }
                }

                entity.ClearCombatRequests();
            }
        }

        public void RemoveEndedTraces(CharacterTestCombatEntityLookup lookup)
        {
            _targetStateEntityScratch.Clear();
            _targetStateContributions.CopyEntityIds(_targetStateEntityScratch);
            for (int i = 0; i < _targetStateEntityScratch.Count; i++)
            {
                CombatEntityId combatEntityId = _targetStateEntityScratch[i];
                if (lookup.FindEntityByCombatId(combatEntityId) == null)
                    _targetStateContributions.ClearEntity(combatEntityId);
            }
            for (int i = _activeTraces.Count - 1; i >= 0; i--)
            {
                CharacterTestActiveHitTrace trace = _activeTraces[i];
                CharacterTestCharacterEntity entity = lookup.FindEntity(trace.EntityId);
                if (entity == null || !entity.BattleState.CountsAsAlive)
                {
                    _activeTraces.RemoveAt(i);
                    ClearHitOnceKeys(trace.HitWindowId);
                }
            }
        }

        /// <summary>
        /// Removes dependent trace windows before this authority frame generates traces.
        /// A hit already accepted by a previous frame is never revisited.
        /// </summary>
        /// <remarks>NoAlloc when verbose trace recording is disabled.</remarks>
        internal void PruneIneligiblePartDependentTraces(
            CharacterTestCombatEntityLookup lookup,
            CharacterTestCharacterActionRuntime actionRuntime,
            RuntimeFrame frame,
            CharacterTestTraceRecorder traceRecorder)
        {
            for (int i = _activeTraces.Count - 1; i >= 0; i--)
            {
                CharacterTestActiveHitTrace trace = _activeTraces[i];
                CharacterTestCharacterEntity entity = lookup.FindEntity(trace.EntityId);
                CharacterTestActionPartStateEligibility eligibility = actionRuntime != null
                    ? actionRuntime.EvaluatePartStateEligibility(
                        entity,
                        trace.ActionId,
                        trace.StableEventId)
                    : new CharacterTestActionPartStateEligibility(
                        CharacterTestActionPartStateEligibilityStatus.ActionUnavailable,
                        0,
                        CharacterActionPartStructureStateMask.None,
                        CharacterActionPartStructureStateMask.None,
                        0L);
                if (eligibility.IsEligible)
                    continue;

                _activeTraces.RemoveAt(i);
                ClearHitOnceKeys(trace.HitWindowId);
                traceRecorder?.AttackCombat(
                    frame,
                    trace.EntityId,
                    "hitTrace dependency gate reject=" + eligibility.Status
                    + " code=" + eligibility.RejectCode
                    + " action=" + CharacterTestCombatTraceText.EmptyAsNone(trace.ActionId.ToString())
                    + " event=" + CharacterTestCombatTraceText.EmptyAsNone(trace.StableEventId)
                    + " partState=" + CharacterTestCombatTraceText.EmptyAsNone(
                        eligibility.RequiredPartStateId)
                    + " allowed=" + ((int)eligibility.AllowedStates).ToString(
                        CultureInfo.InvariantCulture)
                    + " observed=" + eligibility.ObservedState
                    + " revision=" + eligibility.BodyPartStateRevision.ToString(
                        CultureInfo.InvariantCulture));
            }
        }

        private void StartTrace(
            CharacterTestCharacterEntity entity,
            CharacterActionCombatRequest request,
            RuntimeFrame frame,
            CharacterTestTraceRecorder traceRecorder)
        {
            int sourceActionInstanceId = ResolveActionInstanceId(request.Metadata.InstanceId);
            RemoveMatchingTrace(entity, request.Metadata.StableEventId, sourceActionInstanceId);
            int hitWindowId = _nextHitWindowId++;
            _activeTraces.Add(new CharacterTestActiveHitTrace(
                entity.Id.Value,
                request.Metadata.ActionId,
                request.Metadata.InstanceId,
                sourceActionInstanceId,
                hitWindowId,
                request.EquipmentSlotId,
                request.TraceProfileId,
                request.DamagePayload,
                request.HitEvent,
                request.HitVolume,
                request.Metadata.StableEventId,
                request.Metadata.LocalFrame));
            traceRecorder?.AttackCombat(
                frame,
                entity.Id.Value,
                "hitTrace start slot=" + CharacterTestCombatTraceText.EmptyAsNone(request.EquipmentSlotId)
                + " trace=" + CharacterTestCombatTraceText.EmptyAsNone(request.TraceProfileId)
                + " volume=" + (request.HitVolume.HasVolume
                    ? request.HitVolume.SourceKind.ToString()
                    : "none")
                + " damage=" + (request.DamagePayload.HasInlineData
                    ? "hitEvent.damage"
                    : "missing")
                + " action=" + request.Metadata.ActionId
                + " window=" + hitWindowId.ToString(CultureInfo.InvariantCulture));
        }

        private void StopTrace(
            CharacterTestCharacterEntity entity,
            CharacterActionCombatRequest request,
            RuntimeFrame frame,
            CharacterTestTraceRecorder traceRecorder,
            bool traceWhenMissing = true)
        {
            int removed = RemoveMatchingTrace(
                entity,
                request.Metadata.StableEventId,
                ResolveActionInstanceId(request.Metadata.InstanceId));
            if (removed <= 0 && !traceWhenMissing)
                return;

            traceRecorder?.AttackCombat(
                frame,
                entity.Id.Value,
                "hitTrace stop slot=" + CharacterTestCombatTraceText.EmptyAsNone(request.EquipmentSlotId)
                + " trace=" + CharacterTestCombatTraceText.EmptyAsNone(request.TraceProfileId)
                + " removed=" + removed.ToString(CultureInfo.InvariantCulture));
        }

        private int RemoveMatchingTrace(
            CharacterTestCharacterEntity entity,
            int stableEventId,
            int sourceActionInstanceId)
        {
            int removed = 0;
            for (int i = _activeTraces.Count - 1; i >= 0; i--)
            {
                CharacterTestActiveHitTrace trace = _activeTraces[i];
                if (trace.EntityId == entity.Id.Value
                    && trace.SourceActionInstanceId == sourceActionInstanceId
                    && trace.StableEventId == stableEventId)
                {
                    _activeTraces.RemoveAt(i);
                    ClearHitOnceKeys(trace.HitWindowId);
                    removed++;
                }
            }

            return removed;
        }

        private int ResolveActionInstanceId(long instanceId)
        {
            if (instanceId <= 0L)
                return 0;

            if (_instanceIds.TryGetValue(instanceId, out int id))
                return id;

            id = _nextInstanceId++;
            _instanceIds.Add(instanceId, id);
            return id;
        }

        private void ClearHitOnceKeys(int hitWindowId)
        {
            if (hitWindowId <= 0)
                return;

            if (_hitOnceKeys.Count > 0)
            {
                _hitOnceKeyRemovals.Clear();
                foreach (WeaponHitOnceKey key in _hitOnceKeys)
                {
                    if (key.ActionInstanceId == hitWindowId)
                        _hitOnceKeyRemovals.Add(key);
                }

                for (int i = 0; i < _hitOnceKeyRemovals.Count; i++)
                    _hitOnceKeys.Remove(_hitOnceKeyRemovals[i]);
            }
            _hitResolutionPolicyState.ClearActionInstance(hitWindowId);
        }
    }

    internal sealed class CharacterTestHitTraceWindowReplayState
    {
        private readonly KeyValuePair<long, int>[] _instanceIds;
        private readonly WeaponHitOnceKey[] _hitOnceKeys;
        private readonly CharacterTestActiveHitTrace[] _activeTraces;
        private readonly CharacterTestHitResolutionPolicyStateSnapshot _policyStateSnapshot;

        public CharacterTestHitTraceWindowReplayState(
            Dictionary<long, int> instanceIds,
            HashSet<WeaponHitOnceKey> hitOnceKeys,
            List<CharacterTestActiveHitTrace> activeTraces,
            int nextInstanceId,
            int nextHitWindowId,
            CharacterTestHitResolutionPolicyStateSnapshot policyStateSnapshot)
        {
            _instanceIds = new KeyValuePair<long, int>[instanceIds.Count];
            int index = 0;
            foreach (KeyValuePair<long, int> pair in instanceIds)
                _instanceIds[index++] = pair;
            Array.Sort(_instanceIds, CompareInstanceIds);
            _hitOnceKeys = new WeaponHitOnceKey[hitOnceKeys.Count];
            hitOnceKeys.CopyTo(_hitOnceKeys);
            Array.Sort(_hitOnceKeys, CompareHitOnceKeys);
            _activeTraces = activeTraces.ToArray();
            Array.Sort(_activeTraces, CompareActiveTraces);
            _policyStateSnapshot = policyStateSnapshot;
            NextInstanceId = nextInstanceId;
            NextHitWindowId = nextHitWindowId;
        }

        private static int CompareInstanceIds(
            KeyValuePair<long, int> left,
            KeyValuePair<long, int> right)
        {
            int compare = left.Key.CompareTo(right.Key);
            return compare != 0 ? compare : left.Value.CompareTo(right.Value);
        }

        private static int CompareHitOnceKeys(WeaponHitOnceKey left, WeaponHitOnceKey right)
        {
            int compare = left.ActionInstanceId.CompareTo(right.ActionInstanceId);
            if (compare != 0)
                return compare;
            compare = left.TraceId.CompareTo(right.TraceId);
            return compare != 0 ? compare : left.TargetEntityId.Value.CompareTo(right.TargetEntityId.Value);
        }

        private static int CompareActiveTraces(
            CharacterTestActiveHitTrace left,
            CharacterTestActiveHitTrace right)
        {
            int compare = left.EntityId.CompareTo(right.EntityId);
            if (compare != 0)
                return compare;
            compare = left.HitWindowId.CompareTo(right.HitWindowId);
            if (compare != 0)
                return compare;
            compare = left.SourceActionInstanceId.CompareTo(right.SourceActionInstanceId);
            if (compare != 0)
                return compare;
            compare = left.SourceInstanceId.CompareTo(right.SourceInstanceId);
            if (compare != 0)
                return compare;
            return CharacterTestAuthoredContentIds.GetEventOrder(left.StableEventId).CompareTo(CharacterTestAuthoredContentIds.GetEventOrder(right.StableEventId));
        }

        public int NextInstanceId { get; }
        public int NextHitWindowId { get; }
        internal CharacterTestHitResolutionPolicyStateSnapshot PolicyStateSnapshot => _policyStateSnapshot;
        public void CopyTo(
            Dictionary<long, int> instanceIds,
            HashSet<WeaponHitOnceKey> hitOnceKeys,
            List<CharacterTestActiveHitTrace> activeTraces)
        {
            for (int i = 0; i < _instanceIds.Length; i++)
                instanceIds.Add(_instanceIds[i].Key, _instanceIds[i].Value);
            for (int i = 0; i < _hitOnceKeys.Length; i++)
                hitOnceKeys.Add(_hitOnceKeys[i]);
            activeTraces.AddRange(_activeTraces);
        }
    }

}

namespace MxFramework.Game.CharacterTest
{
    // Frozen v3 protocol numbers. Retired entries remain reserved.
    internal static class CharacterTestHitTraceWindowStateHashFields
    {
        internal const int SchemaId = 10014;
        internal const int SchemaVersion = 1;
        internal const int F1 = 1; // characterTest.hitTrace.nextInstanceId
        internal const int F2 = 2; // characterTest.hitTrace.nextHitWindowId
        internal const int F3 = 3; // characterTest.hitTrace.instanceIds.count
        internal const int F4 = 4; // characterTest.hitTrace.instance.sourceId
        internal const int F5 = 5; // characterTest.hitTrace.instance.actionId
        internal const int F6 = 6; // characterTest.hitTrace.active.count
        internal const int F7 = 7; // characterTest.hitTrace.hitOnce.count
        internal const int F8 = 8; // characterTest.hitTrace.hitOnce.actionInstanceId
        internal const int F9 = 9; // characterTest.hitTrace.hitOnce.traceId
        internal const int F10 = 10; // characterTest.hitTrace.hitOnce.targetEntityId
        internal const int F11 = 11; // characterTest.hitTargetState.entityId
        internal const int F12 = 12; // characterTest.hitTargetState.flags
        internal const int F13 = 13; // .entityId
        internal const int F14 = 14; // .actionId
        internal const int F15 = 15; // .sourceInstanceId
        internal const int F16 = 16; // .sourceActionInstanceId
        internal const int F17 = 17; // .hitWindowId
        internal const int F18 = 18; // .equipmentSlotId
        internal const int F19 = 19; // .traceProfileId
        internal const int F20 = 20; // .stableEventId
        internal const int F21 = 21; // .startFrame
        internal const int F22 = 22; // .damageTypeId
        internal const int F23 = 23; // .formulaId
        internal const int F24 = 24; // .baseDamage
        internal const int F25 = 25; // .impactPower
        internal const int F26 = 26; // .launchPower
        internal const int F27 = 27; // .analysisCategory
        internal const int F28 = 28; // .presentationProfileId
        internal const int F29 = 29; // .sourceAttributeScales.count
        internal const int F30 = 30; // .sourceAttributeScales.attributeId
        internal const int F31 = 31; // .sourceAttributeScales.scale
        internal const int F32 = 32; // .tags.count
        internal const int F33 = 33; // .tags.value
        internal const int F34 = 34; // .targetTypes
        internal const int F35 = 35; // .environmentHitPolicy
        internal const int F36 = 36; // .maxAcceptedHits
        internal const int F37 = 37; // .hitSameTargetPolicy
        internal const int F38 = 38; // .damage.damageTypeId
        internal const int F39 = 39; // .damage.baseDamage
        internal const int F40 = 40; // .damage.impactPower
        internal const int F41 = 41; // .damage.launchPower
        internal const int F42 = 42; // .damage.analysisCategory
        internal const int F43 = 43; // .damage.tags.count
        internal const int F44 = 44; // .damage.tags.value
        internal const int F45 = 45; // .onHitBuffs.count
        internal const int F46 = 46; // .onHitBuffs.buffId
        internal const int F47 = 47; // .sourceKind
        internal const int F48 = 48; // .shapeKind
        internal const int F49 = 49; // .localPointAX
        internal const int F50 = 50; // .localPointAY
        internal const int F51 = 51; // .localPointAZ
        internal const int F52 = 52; // .localPointBX
        internal const int F53 = 53; // .localPointBY
        internal const int F54 = 54; // .localPointBZ
        internal const int F55 = 55; // .radius
        internal const int F56 = 56; // .bodyOffsetX
        internal const int F57 = 57; // .bodyOffsetY
        internal const int F58 = 58; // .bodyOffsetZ
        internal const int F59 = 59; // .bodyRadiusScale
        internal const int F60 = 60; // .bodyRadiusAdd
        internal const int F61 = 61; // .bodyHeightScale
        internal const int F62 = 62; // .bodyHeightAdd
        internal const int F63 = 63; // .startSocketId
        internal const int F64 = 64; // .endSocketId
        internal const int F65 = 65; // $scope.AddNetworkStateHash
        internal const int F66 = 66; // $scope.AddActiveTraceHash
        internal const int F67 = 67; // .damagePayload
        internal const int F68 = 68; // .hitEvent
        internal const int F69 = 69; // .hitVolume
        internal const int F70 = 70; // targetStateContributions
    }
}


