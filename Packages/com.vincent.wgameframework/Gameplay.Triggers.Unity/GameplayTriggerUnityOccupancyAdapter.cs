using System;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Gameplay.Triggers.Unity
{
    /// <summary>
    /// Owns fixed transient Unity contact rows and emits authorized complete occupancy observations.
    /// </summary>
    public sealed class GameplayTriggerUnityOccupancyAdapter
    {
        internal const long OccupancyBatchId = 1L;

        private readonly int _commandSourceId;
        private readonly GameplayTriggerUnitySubmissionMode _submissionMode;
        private readonly GameplayTriggerUnityOccupancyCatalog _catalog;
        private readonly GameplayTriggerRuntime _triggerRuntime;
        private readonly GameplayComponentWorld _componentWorld;
        private readonly RuntimeCommandSubmissionGate _submissionGate;
        private readonly ZoneSlot[] _zones;
        private readonly SubjectSlot[] _subjects;
        private readonly ColliderSlot[] _colliders;
        private readonly ContactSlot[] _contacts;
        private readonly GameplayEntityId[] _subjectScratch;
        private readonly RuntimeCommand[] _commandScratch;
        private readonly GameplayEntityId[] _lastSubmittedSubjects;
        private readonly GameplayTriggerUnityZoneFlushResult[] _lastFlushResults;
        private RuntimeFrame _lastFlushFrame;
        private bool _hasFlushed;

        internal GameplayTriggerUnityOccupancyAdapter(
            int commandSourceId,
            GameplayTriggerUnitySubmissionMode submissionMode,
            GameplayTriggerUnityOccupancyCatalog catalog,
            GameplayTriggerRuntime triggerRuntime,
            GameplayComponentWorld componentWorld,
            RuntimeCommandSubmissionGate submissionGate,
            in GameplayTriggerUnityOccupancyOptions options)
        {
            _commandSourceId = commandSourceId;
            _submissionMode = submissionMode;
            _catalog = catalog;
            _triggerRuntime = triggerRuntime;
            _componentWorld = componentWorld;
            _submissionGate = submissionGate;
            _zones = new ZoneSlot[catalog.Count];
            _subjects = new SubjectSlot[options.MaxSubjectBindings];
            _colliders = new ColliderSlot[options.MaxSubjectColliders];
            _contacts = new ContactSlot[options.MaxActiveContactPairs];
            _subjectScratch = new GameplayEntityId[catalog.MaxSubjects];
            _commandScratch = new RuntimeCommand[Math.Max(1, catalog.MaxSubjects)];
            _lastSubmittedSubjects = new GameplayEntityId[catalog.TotalMaxSubjects];
            _lastFlushResults = new GameplayTriggerUnityZoneFlushResult[catalog.Count];

            int subjectOffset = 0;
            for (int i = 0; i < _zones.Length; i++)
            {
                GameplayTriggerUnityOccupancyDescriptor descriptor = catalog.GetDescriptor(i);
                _zones[i].Generation = 1;
                _zones[i].LastSubjectOffset = subjectOffset;
                subjectOffset += descriptor.MaxSubjects;
            }
            for (int i = 0; i < _subjects.Length; i++)
                _subjects[i].Generation = 1;
        }

        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerUnityFlushSummary LastFlushSummary { get; private set; }

        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerUnityZoneFlushResult GetLastZoneFlushResult(int canonicalIndex) =>
            _lastFlushResults[canonicalIndex];

        /// <summary>Registers the exact authoring zone for one frozen source descriptor.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public bool TryRegisterZone(
            GameplayTriggerSourceBindingId sourceBindingId,
            Collider triggerCollider,
            ulong transientSceneHandle,
            out GameplayTriggerUnityZoneHandle handle,
            out GameplayTriggerUnityZoneRegistrationResult result)
        {
            handle = default;
            int zoneIndex = FindDescriptorIndex(sourceBindingId);
            if (zoneIndex < 0)
            {
                result = new GameplayTriggerUnityZoneRegistrationResult(
                    GameplayTriggerUnityZoneRegistrationStatus.UnknownSourceBinding,
                    sourceBindingId);
                return false;
            }
            if (triggerCollider == null || !triggerCollider.isTrigger)
            {
                result = new GameplayTriggerUnityZoneRegistrationResult(
                    GameplayTriggerUnityZoneRegistrationStatus.InvalidArgument,
                    sourceBindingId);
                return false;
            }

            ref ZoneSlot slot = ref _zones[zoneIndex];
            if (slot.Registered || slot.Retiring || slot.Generation <= 0)
            {
                result = new GameplayTriggerUnityZoneRegistrationResult(
                    GameplayTriggerUnityZoneRegistrationStatus.SourceBindingBusy,
                    sourceBindingId);
                return false;
            }

            slot.Registered = true;
            slot.Retiring = false;
            slot.TriggerCollider = triggerCollider;
            slot.SceneHandle = transientSceneHandle;
            slot.LastSubjectCount = 0;
            handle = new GameplayTriggerUnityZoneHandle(zoneIndex, slot.Generation);
            result = new GameplayTriggerUnityZoneRegistrationResult(
                GameplayTriggerUnityZoneRegistrationStatus.Registered,
                sourceBindingId);
            return true;
        }

        /// <summary>Schedules a generation-safe zone for final-empty retirement.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public bool TryRetireZone(
            GameplayTriggerUnityZoneHandle handle,
            out GameplayTriggerUnityLifecycleResult result)
        {
            if (!TryGetZone(handle, out int zoneIndex))
            {
                result = new GameplayTriggerUnityLifecycleResult(
                    GameplayTriggerUnityLifecycleStatus.StaleHandle);
                return false;
            }

            ref ZoneSlot slot = ref _zones[zoneIndex];
            if (slot.Retiring)
            {
                result = new GameplayTriggerUnityLifecycleResult(
                    GameplayTriggerUnityLifecycleStatus.AlreadyScheduled);
                return true;
            }

            slot.Retiring = true;
            slot.TriggerCollider = null;
            RemoveContactsForZone(zoneIndex);
            result = new GameplayTriggerUnityLifecycleResult(
                GameplayTriggerUnityLifecycleStatus.RetirementScheduled);
            return true;
        }

        /// <summary>Binds caller-owned Collider references to one live generation-safe Gameplay entity.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public bool TryBindSubject(
            GameplayEntityId entityId,
            ReadOnlySpan<Collider> colliders,
            ulong transientSceneHandle,
            out GameplayTriggerUnitySubjectHandle handle,
            out GameplayTriggerUnitySubjectBindingResult result)
        {
            handle = default;
            if (!entityId.IsValid || !_componentWorld.IsAlive(entityId) || colliders.Length == 0)
            {
                result = new GameplayTriggerUnitySubjectBindingResult(
                    GameplayTriggerUnitySubjectBindingStatus.InvalidEntity,
                    entityId);
                return false;
            }
            if (!ValidateNewColliders(colliders, out int colliderDetail))
            {
                result = new GameplayTriggerUnitySubjectBindingResult(
                    colliderDetail < 0
                        ? GameplayTriggerUnitySubjectBindingStatus.DuplicateCollider
                        : GameplayTriggerUnitySubjectBindingStatus.InvalidArgument,
                    entityId,
                    Math.Abs(colliderDetail));
                return false;
            }

            int subjectIndex = FindFreeSubject();
            if (subjectIndex < 0)
            {
                result = new GameplayTriggerUnitySubjectBindingResult(
                    GameplayTriggerUnitySubjectBindingStatus.SubjectCapacityExceeded,
                    entityId);
                return false;
            }
            if (CountFreeColliders() < colliders.Length)
            {
                result = new GameplayTriggerUnitySubjectBindingResult(
                    GameplayTriggerUnitySubjectBindingStatus.ColliderCapacityExceeded,
                    entityId,
                    colliders.Length);
                return false;
            }

            ref SubjectSlot subject = ref _subjects[subjectIndex];
            subject.Registered = true;
            subject.EntityId = entityId;
            subject.SceneHandle = transientSceneHandle;
            for (int i = 0; i < colliders.Length; i++)
            {
                int colliderIndex = FindFreeCollider();
                _colliders[colliderIndex] = new ColliderSlot
                {
                    Registered = true,
                    Collider = colliders[i],
                    SubjectIndex = subjectIndex
                };
            }

            handle = new GameplayTriggerUnitySubjectHandle(subjectIndex, subject.Generation);
            result = new GameplayTriggerUnitySubjectBindingResult(
                GameplayTriggerUnitySubjectBindingStatus.Bound,
                entityId,
                colliders.Length);
            return true;
        }

        /// <summary>Atomically changes the live Gameplay generation mapped by a subject binding.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public bool TryRebindSubject(
            GameplayTriggerUnitySubjectHandle handle,
            GameplayEntityId entityId,
            out GameplayTriggerUnitySubjectBindingResult result)
        {
            if (!TryGetSubject(handle, out int subjectIndex))
            {
                result = new GameplayTriggerUnitySubjectBindingResult(
                    GameplayTriggerUnitySubjectBindingStatus.StaleHandle,
                    entityId);
                return false;
            }
            if (!entityId.IsValid || !_componentWorld.IsAlive(entityId))
            {
                result = new GameplayTriggerUnitySubjectBindingResult(
                    GameplayTriggerUnitySubjectBindingStatus.InvalidEntity,
                    entityId);
                return false;
            }

            _subjects[subjectIndex].EntityId = entityId;
            result = new GameplayTriggerUnitySubjectBindingResult(
                GameplayTriggerUnitySubjectBindingStatus.Rebound,
                entityId);
            return true;
        }

        /// <summary>Removes all Collider contacts owned by a generation-safe subject binding.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public bool TryUnbindSubject(
            GameplayTriggerUnitySubjectHandle handle,
            out GameplayTriggerUnityLifecycleResult result)
        {
            if (!TryGetSubject(handle, out int subjectIndex))
            {
                result = new GameplayTriggerUnityLifecycleResult(
                    GameplayTriggerUnityLifecycleStatus.StaleHandle);
                return false;
            }

            UnbindSubjectAt(subjectIndex);
            result = new GameplayTriggerUnityLifecycleResult(
                GameplayTriggerUnityLifecycleStatus.SubjectUnbound);
            return true;
        }

        /// <summary>Applies one transient Unity contact level mutation.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public GameplayTriggerUnityContactResult SetContact(
            GameplayTriggerUnityZoneHandle zone,
            Collider subjectCollider,
            bool active)
        {
            if (!TryGetZone(zone, out int zoneIndex))
            {
                return new GameplayTriggerUnityContactResult(
                    GameplayTriggerUnityContactStatus.StaleZone);
            }
            if (_zones[zoneIndex].Retiring)
            {
                return new GameplayTriggerUnityContactResult(
                    GameplayTriggerUnityContactStatus.ZoneRetiring,
                    _catalog.GetDescriptor(zoneIndex).SourceBindingId);
            }

            int colliderIndex = FindCollider(subjectCollider);
            if (colliderIndex < 0)
            {
                return new GameplayTriggerUnityContactResult(
                    GameplayTriggerUnityContactStatus.UnknownCollider,
                    _catalog.GetDescriptor(zoneIndex).SourceBindingId);
            }

            int contactIndex = FindContact(zoneIndex, colliderIndex);
            if (active)
            {
                if (contactIndex >= 0)
                {
                    return new GameplayTriggerUnityContactResult(
                        GameplayTriggerUnityContactStatus.DuplicateEnter,
                        _catalog.GetDescriptor(zoneIndex).SourceBindingId);
                }

                int freeIndex = FindFreeContact();
                if (freeIndex < 0)
                {
                    return new GameplayTriggerUnityContactResult(
                        GameplayTriggerUnityContactStatus.ContactCapacityExceeded,
                        _catalog.GetDescriptor(zoneIndex).SourceBindingId);
                }
                _contacts[freeIndex] = new ContactSlot
                {
                    Active = true,
                    ZoneIndex = zoneIndex,
                    ColliderIndex = colliderIndex
                };
                return new GameplayTriggerUnityContactResult(
                    GameplayTriggerUnityContactStatus.Activated,
                    _catalog.GetDescriptor(zoneIndex).SourceBindingId);
            }

            if (contactIndex < 0)
            {
                return new GameplayTriggerUnityContactResult(
                    GameplayTriggerUnityContactStatus.UnknownExit,
                    _catalog.GetDescriptor(zoneIndex).SourceBindingId);
            }

            _contacts[contactIndex] = default;
            return new GameplayTriggerUnityContactResult(
                GameplayTriggerUnityContactStatus.Deactivated,
                _catalog.GetDescriptor(zoneIndex).SourceBindingId);
        }

        /// <remarks>NoAlloc. Called only by the persistent flush module.</remarks>
        internal GameplayTriggerUnityFlushSummary Flush(RuntimeFrame frame)
        {
            if (_hasFlushed && frame <= _lastFlushFrame)
            {
                for (int i = 0; i < _lastFlushResults.Length; i++)
                {
                    _lastFlushResults[i] = new GameplayTriggerUnityZoneFlushResult(
                        frame,
                        _catalog.GetDescriptor(i).SourceBindingId,
                        GameplayTriggerUnityZoneFlushStatus.DuplicateOrRegressedFrame);
                }
                LastFlushSummary = new GameplayTriggerUnityFlushSummary(
                    frame,
                    _catalog.Count,
                    submittedZoneCount: 0,
                    failedZoneCount: _catalog.Count,
                    duplicateOrRegressedFrame: true);
                return LastFlushSummary;
            }

            _hasFlushed = true;
            _lastFlushFrame = frame;
            int submitted = 0;
            int failed = 0;
            for (int i = 0; i < _zones.Length; i++)
            {
                GameplayTriggerUnityZoneFlushResult result = FlushZone(frame, i);
                _lastFlushResults[i] = result;
                if (result.Status == GameplayTriggerUnityZoneFlushStatus.Submitted)
                    submitted++;
                else if (IsFailure(result.Status))
                    failed++;
            }

            LastFlushSummary = new GameplayTriggerUnityFlushSummary(
                frame,
                _catalog.Count,
                submitted,
                failed,
                duplicateOrRegressedFrame: false);
            return LastFlushSummary;
        }

        /// <remarks>NoAlloc. Scene handle is transient adapter bookkeeping only.</remarks>
        internal void NotifySceneUnloaded(ulong transientSceneHandle)
        {
            for (int i = 0; i < _zones.Length; i++)
            {
                if (!_zones[i].Registered ||
                    _zones[i].SceneHandle != transientSceneHandle ||
                    _zones[i].Retiring)
                    continue;

                _zones[i].Retiring = true;
                _zones[i].TriggerCollider = null;
                RemoveContactsForZone(i);
            }
            for (int i = 0; i < _subjects.Length; i++)
            {
                if (_subjects[i].Registered &&
                    _subjects[i].SceneHandle == transientSceneHandle)
                    UnbindSubjectAt(i);
            }
        }

        private GameplayTriggerUnityZoneFlushResult FlushZone(
            RuntimeFrame frame,
            int zoneIndex)
        {
            GameplayTriggerUnityOccupancyDescriptor descriptor =
                _catalog.GetDescriptor(zoneIndex);
            ref ZoneSlot zone = ref _zones[zoneIndex];
            if (!zone.Registered)
            {
                return new GameplayTriggerUnityZoneFlushResult(
                    frame,
                    descriptor.SourceBindingId,
                    GameplayTriggerUnityZoneFlushStatus.ZoneNotRegistered);
            }

            if (!_triggerRuntime.TryGetSourceEnabled(
                    descriptor.SourceBindingId,
                    out bool sourceEnabled))
            {
                return new GameplayTriggerUnityZoneFlushResult(
                    frame,
                    descriptor.SourceBindingId,
                    GameplayTriggerUnityZoneFlushStatus.UnknownSourceBinding);
            }

            if (zone.Retiring)
            {
                if (_submissionMode == GameplayTriggerUnitySubmissionMode.ReplayDisabled)
                {
                    RecycleZone(zoneIndex);
                    return new GameplayTriggerUnityZoneFlushResult(
                        frame,
                        descriptor.SourceBindingId,
                        GameplayTriggerUnityZoneFlushStatus.ReplayRetiredWithoutSubmission);
                }
                if (!sourceEnabled)
                {
                    RecycleZone(zoneIndex);
                    return new GameplayTriggerUnityZoneFlushResult(
                        frame,
                        descriptor.SourceBindingId,
                        GameplayTriggerUnityZoneFlushStatus.RetiredWhileSourceDisabled);
                }

                return EncodeAndSubmit(frame, zoneIndex, subjectCount: 0, staleCount: 0);
            }

            if (_submissionMode == GameplayTriggerUnitySubmissionMode.ReplayDisabled)
            {
                return new GameplayTriggerUnityZoneFlushResult(
                    frame,
                    descriptor.SourceBindingId,
                    GameplayTriggerUnityZoneFlushStatus.ReplaySuppressed);
            }
            if (!sourceEnabled)
            {
                return new GameplayTriggerUnityZoneFlushResult(
                    frame,
                    descriptor.SourceBindingId,
                    GameplayTriggerUnityZoneFlushStatus.SourceDisabled);
            }

            int subjectCount = 0;
            int staleCount = 0;
            for (int i = 0; i < _contacts.Length; i++)
            {
                if (!_contacts[i].Active || _contacts[i].ZoneIndex != zoneIndex)
                    continue;

                int colliderIndex = _contacts[i].ColliderIndex;
                if ((uint)colliderIndex >= (uint)_colliders.Length ||
                    !_colliders[colliderIndex].Registered)
                    continue;
                int subjectIndex = _colliders[colliderIndex].SubjectIndex;
                if ((uint)subjectIndex >= (uint)_subjects.Length ||
                    !_subjects[subjectIndex].Registered)
                    continue;

                GameplayEntityId entityId = _subjects[subjectIndex].EntityId;
                if (!entityId.IsValid || !_componentWorld.IsAlive(entityId))
                {
                    staleCount++;
                    continue;
                }
                if (ContainsSubject(_subjectScratch, subjectCount, entityId))
                    continue;
                if (subjectCount >= descriptor.MaxSubjects)
                {
                    return new GameplayTriggerUnityZoneFlushResult(
                        frame,
                        descriptor.SourceBindingId,
                        GameplayTriggerUnityZoneFlushStatus.SubjectCapacityExceeded,
                        subjectCount,
                        commandCount: 0,
                        staleSubjectCount: staleCount);
                }

                _subjectScratch[subjectCount++] = entityId;
            }

            SortSubjects(_subjectScratch, subjectCount);
            return EncodeAndSubmit(frame, zoneIndex, subjectCount, staleCount);
        }

        private GameplayTriggerUnityZoneFlushResult EncodeAndSubmit(
            RuntimeFrame frame,
            int zoneIndex,
            int subjectCount,
            int staleCount)
        {
            GameplayTriggerUnityOccupancyDescriptor descriptor =
                _catalog.GetDescriptor(zoneIndex);
            if (!GameplayTriggerOccupancyCommandCodec.TryEncodeBatch(
                    frame,
                    _commandSourceId,
                    RuntimeCommandProvenanceKind.Exogenous,
                    descriptor.SourceBindingId,
                    OccupancyBatchId,
                    new ReadOnlySpan<GameplayEntityId>(_subjectScratch, 0, subjectCount),
                    _commandScratch,
                    out int written,
                    out GameplayTriggerOccupancyCommandCodecStatus codecStatus,
                    default))
            {
                return new GameplayTriggerUnityZoneFlushResult(
                    frame,
                    descriptor.SourceBindingId,
                    GameplayTriggerUnityZoneFlushStatus.CodecRejected,
                    subjectCount,
                    commandCount: 0,
                    staleSubjectCount: staleCount,
                    codecStatus);
            }

            RuntimeCommandBatchSubmissionResult submission =
                _submissionGate.SubmitBatch(
                    new ReadOnlySpan<RuntimeCommand>(_commandScratch, 0, written));
            if (!submission.Success)
            {
                return new GameplayTriggerUnityZoneFlushResult(
                    frame,
                    descriptor.SourceBindingId,
                    GameplayTriggerUnityZoneFlushStatus.AdmissionRejected,
                    subjectCount,
                    written,
                    staleCount,
                    codecStatus,
                    submission.Error.Code,
                    submission.FailedCommandIndex);
            }

            ref ZoneSlot zone = ref _zones[zoneIndex];
            for (int i = 0; i < subjectCount; i++)
                _lastSubmittedSubjects[zone.LastSubjectOffset + i] = _subjectScratch[i];
            for (int i = subjectCount; i < zone.LastSubjectCount; i++)
                _lastSubmittedSubjects[zone.LastSubjectOffset + i] = default;
            zone.LastSubjectCount = subjectCount;
            bool retiring = zone.Retiring;
            if (retiring)
                RecycleZone(zoneIndex);

            return new GameplayTriggerUnityZoneFlushResult(
                frame,
                descriptor.SourceBindingId,
                GameplayTriggerUnityZoneFlushStatus.Submitted,
                subjectCount,
                written,
                staleCount);
        }

        private int FindDescriptorIndex(GameplayTriggerSourceBindingId sourceBindingId)
        {
            int low = 0;
            int high = _catalog.Count - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                int comparison = _catalog.GetDescriptor(middle)
                    .SourceBindingId.CompareTo(sourceBindingId);
                if (comparison == 0)
                    return middle;
                if (comparison < 0)
                    low = middle + 1;
                else
                    high = middle - 1;
            }
            return -1;
        }

        private bool TryGetZone(GameplayTriggerUnityZoneHandle handle, out int index)
        {
            index = handle.Index;
            return handle.IsValid &&
                (uint)index < (uint)_zones.Length &&
                _zones[index].Registered &&
                _zones[index].Generation == handle.Generation;
        }

        private bool TryGetSubject(GameplayTriggerUnitySubjectHandle handle, out int index)
        {
            index = handle.Index;
            return handle.IsValid &&
                (uint)index < (uint)_subjects.Length &&
                _subjects[index].Registered &&
                _subjects[index].Generation == handle.Generation;
        }

        private int FindFreeSubject()
        {
            for (int i = 0; i < _subjects.Length; i++)
            {
                if (!_subjects[i].Registered && _subjects[i].Generation > 0)
                    return i;
            }
            return -1;
        }

        private int CountFreeColliders()
        {
            int count = 0;
            for (int i = 0; i < _colliders.Length; i++)
            {
                if (!_colliders[i].Registered)
                    count++;
            }
            return count;
        }

        private int FindFreeCollider()
        {
            for (int i = 0; i < _colliders.Length; i++)
            {
                if (!_colliders[i].Registered)
                    return i;
            }
            return -1;
        }

        private int FindCollider(Collider collider)
        {
            if (collider == null)
                return -1;
            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i].Registered &&
                    ReferenceEquals(_colliders[i].Collider, collider))
                    return i;
            }
            return -1;
        }

        private bool ValidateNewColliders(
            ReadOnlySpan<Collider> colliders,
            out int detail)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null)
                {
                    detail = i;
                    return false;
                }
                if (FindCollider(colliders[i]) >= 0)
                {
                    detail = -(i + 1);
                    return false;
                }
                for (int j = 0; j < i; j++)
                {
                    if (ReferenceEquals(colliders[i], colliders[j]))
                    {
                        detail = -(i + 1);
                        return false;
                    }
                }
            }
            detail = 0;
            return true;
        }

        private int FindContact(int zoneIndex, int colliderIndex)
        {
            for (int i = 0; i < _contacts.Length; i++)
            {
                if (_contacts[i].Active &&
                    _contacts[i].ZoneIndex == zoneIndex &&
                    _contacts[i].ColliderIndex == colliderIndex)
                    return i;
            }
            return -1;
        }

        private int FindFreeContact()
        {
            for (int i = 0; i < _contacts.Length; i++)
            {
                if (!_contacts[i].Active)
                    return i;
            }
            return -1;
        }

        private void RemoveContactsForZone(int zoneIndex)
        {
            for (int i = 0; i < _contacts.Length; i++)
            {
                if (_contacts[i].Active && _contacts[i].ZoneIndex == zoneIndex)
                    _contacts[i] = default;
            }
        }

        private void UnbindSubjectAt(int subjectIndex)
        {
            for (int colliderIndex = 0; colliderIndex < _colliders.Length; colliderIndex++)
            {
                if (!_colliders[colliderIndex].Registered ||
                    _colliders[colliderIndex].SubjectIndex != subjectIndex)
                    continue;
                for (int contactIndex = 0; contactIndex < _contacts.Length; contactIndex++)
                {
                    if (_contacts[contactIndex].Active &&
                        _contacts[contactIndex].ColliderIndex == colliderIndex)
                        _contacts[contactIndex] = default;
                }
                _colliders[colliderIndex] = default;
            }

            int generation = _subjects[subjectIndex].Generation;
            _subjects[subjectIndex] = default;
            _subjects[subjectIndex].Generation = NextGeneration(generation);
        }

        private void RecycleZone(int zoneIndex)
        {
            int generation = _zones[zoneIndex].Generation;
            int offset = _zones[zoneIndex].LastSubjectOffset;
            _zones[zoneIndex] = default;
            _zones[zoneIndex].Generation = NextGeneration(generation);
            _zones[zoneIndex].LastSubjectOffset = offset;
        }

        private static int NextGeneration(int generation) =>
            generation == int.MaxValue ? 0 : generation + 1;

        private static bool ContainsSubject(
            GameplayEntityId[] subjects,
            int count,
            GameplayEntityId subject)
        {
            for (int i = 0; i < count; i++)
            {
                if (subjects[i] == subject)
                    return true;
            }
            return false;
        }

        private static void SortSubjects(GameplayEntityId[] subjects, int count)
        {
            for (int i = 1; i < count; i++)
            {
                GameplayEntityId value = subjects[i];
                int j = i - 1;
                while (j >= 0 && subjects[j].CompareTo(value) > 0)
                {
                    subjects[j + 1] = subjects[j];
                    j--;
                }
                subjects[j + 1] = value;
            }
        }

        private static bool IsFailure(GameplayTriggerUnityZoneFlushStatus status) =>
            status == GameplayTriggerUnityZoneFlushStatus.ZoneNotRegistered ||
            status == GameplayTriggerUnityZoneFlushStatus.UnknownSourceBinding ||
            status == GameplayTriggerUnityZoneFlushStatus.SubjectCapacityExceeded ||
            status == GameplayTriggerUnityZoneFlushStatus.CodecRejected ||
            status == GameplayTriggerUnityZoneFlushStatus.AdmissionRejected ||
            status == GameplayTriggerUnityZoneFlushStatus.DuplicateOrRegressedFrame;

        private struct ZoneSlot
        {
            public int Generation;
            public bool Registered;
            public bool Retiring;
            public Collider TriggerCollider;
            public ulong SceneHandle;
            public int LastSubjectOffset;
            public int LastSubjectCount;
        }

        private struct SubjectSlot
        {
            public int Generation;
            public bool Registered;
            public GameplayEntityId EntityId;
            public ulong SceneHandle;
        }

        private struct ColliderSlot
        {
            public bool Registered;
            public Collider Collider;
            public int SubjectIndex;
        }

        private struct ContactSlot
        {
            public bool Active;
            public int ZoneIndex;
            public int ColliderIndex;
        }
    }
}
