using System;

namespace MxFramework.Audio
{
    public sealed class AudioPresentationDrainOptions
    {
        public int ControlledHandleCapacity { get; set; } = 64;
        public int DedupeHistoryCapacity { get; set; } = 256;
        public int GlobalVoiceLimit { get; set; } = 64;
        public int CategoryVoiceLimit { get; set; } = 24;
        public int EventVoiceLimit { get; set; } = 8;
        public int EmitterVoiceLimit { get; set; } = 4;
    }

    public readonly struct AudioPresentationDrainSnapshot
    {
        public AudioPresentationDrainSnapshot(
            int accepted,
            int failed,
            int suppressedReplay,
            int awaitingConfirmation,
            int deduped,
            int budgetRejected,
            int stolen,
            int controlledHandles,
            int sourceDropped,
            int transientFrameHandles = 0,
            int entityHandles = 0,
            int sceneHandles = 0,
            int appPersistentHandles = 0)
        {
            Accepted = accepted;
            Failed = failed;
            SuppressedReplay = suppressedReplay;
            AwaitingConfirmation = awaitingConfirmation;
            Deduped = deduped;
            BudgetRejected = budgetRejected;
            Stolen = stolen;
            ControlledHandles = controlledHandles;
            SourceDropped = sourceDropped;
            TransientFrameHandles = transientFrameHandles;
            EntityHandles = entityHandles;
            SceneHandles = sceneHandles;
            AppPersistentHandles = appPersistentHandles;
        }

        public int Accepted { get; }
        public int Failed { get; }
        public int SuppressedReplay { get; }
        public int AwaitingConfirmation { get; }
        public int Deduped { get; }
        public int BudgetRejected { get; }
        public int Stolen { get; }
        public int ControlledHandles { get; }
        public int SourceDropped { get; }
        public int TransientFrameHandles { get; }
        public int EntityHandles { get; }
        public int SceneHandles { get; }
        public int AppPersistentHandles { get; }
    }

    /// <summary>Owns replay policy, dedupe, scope tracking and bounded controlled voices.</summary>
    public sealed class AudioPresentationDrain
    {
        private readonly ScopedHandle[] _handles;
        private readonly DedupeKey[] _dedupeHistory;
        private readonly VoiceAdmission[] _admissions;
        private readonly int _globalLimit;
        private readonly int _categoryLimit;
        private readonly int _eventLimit;
        private readonly int _emitterLimit;
        private int _handleCount;
        private int _dedupeCount;
        private int _dedupeWriteIndex;
        private int _admissionCount;
        private AudioPresentationDrainSnapshot _snapshot;

        /// <remarks>AllocByDesign during initialization; drain and scope stops are NoAlloc on successful backend paths.</remarks>
        public AudioPresentationDrain(AudioPresentationDrainOptions options = null)
        {
            options = options ?? new AudioPresentationDrainOptions();
            if (options.ControlledHandleCapacity <= 0 || options.DedupeHistoryCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(options));

            _handles = new ScopedHandle[options.ControlledHandleCapacity];
            _dedupeHistory = new DedupeKey[options.DedupeHistoryCapacity];
            _admissions = new VoiceAdmission[options.DedupeHistoryCapacity];
            _globalLimit = Positive(options.GlobalVoiceLimit);
            _categoryLimit = Positive(options.CategoryVoiceLimit);
            _eventLimit = Positive(options.EventVoiceLimit);
            _emitterLimit = Positive(options.EmitterVoiceLimit);
        }

        public AudioPresentationDrainSnapshot Snapshot => _snapshot;

        /// <remarks>NoAlloc after initialization when service calls succeed and commands carry preallocated parameter storage.</remarks>
        public void Drain(FixedAudioCommandBuffer buffer, IAudioService service, bool isReplay, bool isConfirmed)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            int accepted = 0;
            int failed = 0;
            int suppressedReplay = 0;
            int awaitingConfirmation = 0;
            int deduped = 0;
            int budgetRejected = 0;
            int stolen = 0;
            _admissionCount = 0;
            PruneInactive(service);

            for (int i = 0; i < buffer.Count; i++)
            {
                AudioCommand command = buffer[i];
                if (command.PredictionPolicy == AudioPredictionPolicy.ConfirmedOnly && !isConfirmed)
                {
                    awaitingConfirmation++;
                    continue;
                }
                if (command.PredictionPolicy == AudioPredictionPolicy.SuppressDuringReplay && isReplay)
                {
                    suppressedReplay++;
                    continue;
                }

                DedupeKey key = new DedupeKey(command.Request.Frame, command.Request.EventId, command.Request.EmitterId, command.DedupeOrdinal);
                if (ContainsDedupe(key))
                {
                    deduped++;
                    continue;
                }

                if (!HasBudget(command))
                {
                    if (command.Kind != AudioCommandKind.StartEvent || !TryStealLowerPriority(command, service))
                    {
                        budgetRejected++;
                        continue;
                    }
                    stolen++;
                }

                AudioPlayResult result;
                if (command.Kind == AudioCommandKind.StartEvent)
                {
                    result = service.StartEvent(command.Request, out AudioHandle handle);
                    if (result.Success && handle.IsValid)
                    {
                        if (!TryTrack(handle, command))
                        {
                            service.Stop(handle, AudioStopMode.Immediate);
                            budgetRejected++;
                            continue;
                        }
                    }
                }
                else
                {
                    result = service.PlayOneShot(command.Request);
                }

                if (result.Success)
                {
                    accepted++;
                    RememberAdmission(command);
                    RememberDedupe(key);
                }
                else
                {
                    failed++;
                }
            }

            CountScopes(out int transientFrame, out int entity, out int scene, out int appPersistent);
            _snapshot = new AudioPresentationDrainSnapshot(
                accepted,
                failed,
                suppressedReplay,
                awaitingConfirmation,
                deduped,
                budgetRejected,
                stolen,
                _handleCount,
                buffer.DroppedCount,
                transientFrame,
                entity,
                scene,
                appPersistent);
            buffer.Clear();
        }

        /// <remarks>NoAlloc on successful backend paths.</remarks>
        public int StopAllByScope(IAudioService service, AudioScope scope, int ownerId = 0, AudioStopMode stopMode = AudioStopMode.AllowFadeout)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            int stopped = 0;
            for (int i = _handleCount - 1; i >= 0; i--)
            {
                ScopedHandle tracked = _handles[i];
                if (tracked.Scope != scope || (ownerId != 0 && tracked.OwnerId != ownerId))
                    continue;

                if (service.Stop(tracked.Handle, stopMode).Success)
                {
                    stopped++;
                    RemoveHandleAt(i);
                }
            }
            return stopped;
        }

        /// <remarks>NoAlloc on successful backend paths.</remarks>
        public int StopAllByEmitter(IAudioService service, int emitterId, AudioStopMode stopMode = AudioStopMode.Immediate)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));
            int stopped = 0;
            for (int i = _handleCount - 1; i >= 0; i--)
            {
                if (_handles[i].EmitterId != emitterId)
                    continue;
                if (service.Stop(_handles[i].Handle, stopMode).Success)
                {
                    stopped++;
                    RemoveHandleAt(i);
                }
            }
            return stopped;
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public int UpdateEmitters(IAudioService service, AudioEmitterRegistry emitters)
        {
            if (!(service is IAudioSpatialControl spatial) || emitters == null)
                return 0;
            int updated = 0;
            for (int i = 0; i < _handleCount; i++)
            {
                int emitterId = _handles[i].EmitterId;
                if (emitterId != 0
                    && emitters.TryGet(emitterId, out AudioTransform transform)
                    && spatial.Update3DAttributes(_handles[i].Handle, transform).Success)
                {
                    updated++;
                }
            }
            return updated;
        }

        /// <summary>Clears replay dedupe ownership at a session boundary.</summary>
        /// <remarks>NoAlloc.</remarks>
        public void ResetDedupeHistory()
        {
            Array.Clear(_dedupeHistory, 0, _dedupeHistory.Length);
            _dedupeCount = 0;
            _dedupeWriteIndex = 0;
        }

        private bool HasBudget(in AudioCommand command)
        {
            int global = _handleCount;
            int category = 0;
            int eventCount = 0;
            int emitter = 0;
            for (int i = 0; i < _handleCount; i++)
            {
                ScopedHandle active = _handles[i];
                if (active.CategoryId == command.CategoryId)
                    category++;
                if (active.EventId == command.Request.EventId)
                    eventCount++;
                if (command.Request.EmitterId != 0 && active.EmitterId == command.Request.EmitterId)
                    emitter++;
            }
            global += _admissionCount;
            for (int i = 0; i < _admissionCount; i++)
            {
                VoiceAdmission admission = _admissions[i];
                if (admission.CategoryId == command.CategoryId)
                    category++;
                if (admission.EventId == command.Request.EventId)
                    eventCount++;
                if (command.Request.EmitterId != 0 && admission.EmitterId == command.Request.EmitterId)
                    emitter++;
            }

            return global < _globalLimit
                && category < _categoryLimit
                && eventCount < _eventLimit
                && (command.Request.EmitterId == 0 || emitter < _emitterLimit);
        }

        private void CountScopes(out int transientFrame, out int entity, out int scene, out int appPersistent)
        {
            transientFrame = 0;
            entity = 0;
            scene = 0;
            appPersistent = 0;
            for (int i = 0; i < _handleCount; i++)
            {
                switch (_handles[i].Scope)
                {
                    case AudioScope.Entity: entity++; break;
                    case AudioScope.Scene: scene++; break;
                    case AudioScope.AppPersistent: appPersistent++; break;
                    default: transientFrame++; break;
                }
            }
        }

        private bool TryStealLowerPriority(in AudioCommand command, IAudioService service)
        {
            int candidate = -1;
            int priority = command.Request.Priority;
            for (int i = 0; i < _handleCount; i++)
            {
                if (_handles[i].Priority >= priority)
                    continue;
                if (candidate < 0 || _handles[i].Priority < _handles[candidate].Priority)
                    candidate = i;
            }
            if (candidate < 0)
                return false;

            if (service.Stop(_handles[candidate].Handle, AudioStopMode.Immediate).Failed)
                return false;
            RemoveHandleAt(candidate);
            return HasBudget(command);
        }

        private bool TryTrack(AudioHandle handle, in AudioCommand command)
        {
            if (_handleCount == _handles.Length)
                return false;
            _handles[_handleCount++] = new ScopedHandle(handle, command);
            return true;
        }

        private void PruneInactive(IAudioService service)
        {
            if (!(service is IAudioHandleStateProvider provider))
                return;

            for (int i = _handleCount - 1; i >= 0; i--)
            {
                if (!provider.IsHandleActive(_handles[i].Handle))
                    RemoveHandleAt(i);
            }
        }

        private void RememberAdmission(in AudioCommand command)
        {
            if (command.Kind == AudioCommandKind.PlayOneShot && _admissionCount < _admissions.Length)
                _admissions[_admissionCount++] = new VoiceAdmission(command.CategoryId, command.Request.EventId, command.Request.EmitterId);
        }

        private void RemoveHandleAt(int index)
        {
            int last = --_handleCount;
            if (index != last)
                _handles[index] = _handles[last];
            _handles[last] = default;
        }

        private bool ContainsDedupe(in DedupeKey key)
        {
            for (int i = 0; i < _dedupeCount; i++)
            {
                if (_dedupeHistory[i].Equals(key))
                    return true;
            }
            return false;
        }

        private void RememberDedupe(in DedupeKey key)
        {
            _dedupeHistory[_dedupeWriteIndex] = key;
            _dedupeWriteIndex = (_dedupeWriteIndex + 1) % _dedupeHistory.Length;
            if (_dedupeCount < _dedupeHistory.Length)
                _dedupeCount++;
        }

        private static int Positive(int value)
        {
            return value > 0 ? value : int.MaxValue;
        }

        private readonly struct DedupeKey
        {
            public DedupeKey(long frame, int eventId, int emitterId, int ordinal)
            {
                Frame = frame;
                EventId = eventId;
                EmitterId = emitterId;
                Ordinal = ordinal;
            }

            private long Frame { get; }
            private int EventId { get; }
            private int EmitterId { get; }
            private int Ordinal { get; }

            public bool Equals(in DedupeKey other)
            {
                return Frame == other.Frame && EventId == other.EventId && EmitterId == other.EmitterId && Ordinal == other.Ordinal;
            }
        }

        private readonly struct ScopedHandle
        {
            public ScopedHandle(AudioHandle handle, in AudioCommand command)
            {
                Handle = handle;
                Scope = command.Scope;
                OwnerId = command.OwnerId;
                CategoryId = command.CategoryId;
                EventId = command.Request.EventId;
                EmitterId = command.Request.EmitterId;
                Priority = command.Request.Priority;
            }

            public AudioHandle Handle { get; }
            public AudioScope Scope { get; }
            public int OwnerId { get; }
            public int CategoryId { get; }
            public int EventId { get; }
            public int EmitterId { get; }
            public int Priority { get; }
        }

        private readonly struct VoiceAdmission
        {
            public VoiceAdmission(int categoryId, int eventId, int emitterId)
            {
                CategoryId = categoryId;
                EventId = eventId;
                EmitterId = emitterId;
            }

            public int CategoryId { get; }
            public int EventId { get; }
            public int EmitterId { get; }
        }
    }
}
