using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Audio;
using MxFramework.CharacterAction;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestAudioCuePlaybackSystem
    {
        private const string LogCategory = "CharacterTest.Audio";
        private const int StandaloneIntentCapacity = 64;

        private readonly IAudioService _audioService;
        private readonly ICharacterTestAudioCueCatalog _catalog;
        private readonly IRuntimeLogger _logger;
        private readonly bool _logMissingAudioServiceWarnings;
        private readonly RuntimeLogBuffer _logBuffer = new RuntimeLogBuffer(192);
        private readonly CharacterTestAudioIntentQueue _standaloneIntentQueue =
            new CharacterTestAudioIntentQueue(StandaloneIntentCapacity);
        private readonly CharacterTestAudioIntentDrainSystem _standaloneIntentDrainSystem;

        public CharacterTestAudioCuePlaybackSystem(
            IAudioService audioService,
            ICharacterTestAudioCueCatalog catalog,
            IRuntimeLogger logger)
            : this(audioService, catalog, logger, logMissingAudioServiceWarnings: true)
        {
        }

        internal CharacterTestAudioCuePlaybackSystem(
            IAudioService audioService,
            ICharacterTestAudioCueCatalog catalog,
            IRuntimeLogger logger,
            bool logMissingAudioServiceWarnings)
        {
            _audioService = audioService;
            _catalog = catalog;
            _logger = logger ?? NullRuntimeLogger.Instance;
            _logMissingAudioServiceWarnings = logMissingAudioServiceWarnings;
            _standaloneIntentDrainSystem = new CharacterTestAudioIntentDrainSystem(this);
        }

        public int LastFrameRequestCount { get; private set; }
        public int LastFramePlayedCount { get; private set; }
        public int LastFrameFailureCount { get; private set; }
        public CharacterTestAudioIntentDrainSummary LastIntentDrainSummary =>
            _standaloneIntentDrainSystem.LastSummary;
        public IAudioService AudioService => _audioService;
        public ICharacterTestAudioCueCatalog Catalog => _catalog;

        public void BeginFrame()
        {
            LastFrameRequestCount = 0;
            LastFramePlayedCount = 0;
            LastFrameFailureCount = 0;
        }

        /// <remarks>NoAlloc after initialization when the standalone intent queue capacity is not exceeded.</remarks>
        public void Tick(CharacterTestCharacterRuntimeWorld world, RuntimeFrame frame)
        {
            BeginFrame();
            _standaloneIntentQueue.Clear();
            CollectIntents(world, frame, _standaloneIntentQueue);
            _standaloneIntentDrainSystem.Tick(_standaloneIntentQueue, frame);
        }

        /// <remarks>NoAlloc after initialization when the target queue capacity is not exceeded.</remarks>
        internal void CollectIntents(
            CharacterTestCharacterRuntimeWorld world,
            RuntimeFrame frame,
            CharacterTestAudioIntentQueue queue)
        {
            if (world == null)
                return;

            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int i = 0; i < entities.Count; i++)
                CollectEntityIntents(entities[i], frame, queue);
        }

        private void CollectEntityIntents(
            CharacterTestCharacterEntity entity,
            RuntimeFrame frame,
            CharacterTestAudioIntentQueue queue)
        {
            if (entity == null || entity.PendingAudioCueRequestCount == 0)
                return;

            IReadOnlyList<CharacterActionAudioCueRequest> requests = entity.AudioCueRequests;
            try
            {
                for (int i = 0; i < requests.Count; i++)
                    Enqueue(entity, requests[i], frame, queue);
            }
            finally
            {
                entity.ClearAudioCueRequests();
            }
        }

        private void Enqueue(
            CharacterTestCharacterEntity entity,
            CharacterActionAudioCueRequest request,
            RuntimeFrame frame,
            CharacterTestAudioIntentQueue queue)
        {
            if (queue == null)
            {
                LastFrameRequestCount++;
                LastFrameFailureCount++;
                LogSkipped(request, "audio intent queue is not configured");
                return;
            }

            queue.Enqueue(CharacterTestAudioIntent.CreateCue(
                CharacterTestAudioIntentSource.ActionCue,
                request.CueId,
                CreateAudioTransform(entity),
                entity.Id.Value,
                frame,
                new AudioTraceIdentity(request.Metadata.TraceId.KindId, request.Metadata.TraceId.SourceId,
                    request.Metadata.TraceId.ExecutionId, request.Metadata.TraceId.Sequence)));
        }

        public bool TryPlayCueAt(
            int cueId,
            AudioTransform transform,
            int emitterId,
            RuntimeFrame frame,
            AudioTraceIdentity traceId)
        {
            return TryPlayCueAt(cueId, transform, emitterId, frame, traceId, null);
        }

        public bool TryPlayCueAt(
            int cueId,
            AudioTransform transform,
            int emitterId,
            RuntimeFrame frame,
            AudioTraceIdentity traceId,
            AudioParameterValue[] parameters)
        {
            return TryPlayCueAt(
                cueId,
                transform,
                emitterId,
                frame,
                traceId,
                parameters,
                priority: 0);
        }

        private bool TryPlayCueAt(
            int cueId,
            AudioTransform transform,
            int emitterId,
            RuntimeFrame frame,
            AudioTraceIdentity traceId,
            IReadOnlyList<AudioParameterValue> parameters,
            int priority)
        {
            LastFrameRequestCount++;
            if (cueId <= 0)
            {
                LastFrameFailureCount++;
                _logger.Warning(LogCategory, "Audio cue request skipped: cue id is empty.");
                return false;
            }

            if (_audioService == null)
            {
                LastFrameFailureCount++;
                if (_logMissingAudioServiceWarnings)
                    LogSkipped(cueId, "audio service is not configured");
                return false;
            }

            if (_catalog == null || !_catalog.TryGetCue(cueId, out AudioCueDefinition cue))
            {
                LastFrameFailureCount++;
                LogSkipped(cueId, "cue is not registered");
                return false;
            }

            if (cue.PlayMode != AudioCuePlayMode.OneShot)
            {
                LastFrameFailureCount++;
                LogSkipped(cueId, "only one-shot cues are supported by this slice");
                return false;
            }

            if (!_catalog.TryGetEvent(cue.EventId, out AudioEventDefinition audioEvent))
            {
                LastFrameFailureCount++;
                LogSkipped(cueId, "event is not registered");
                return false;
            }

            AudioPlayRequest playRequest = audioEvent.Is3D
                ? AudioPlayRequest.Create3D(
                    cue.EventId,
                    transform,
                    emitterId: emitterId,
                    parameters ?? cue.DefaultParameters,
                    priority,
                    frame: frame.Value,
                    traceId: traceId)
                : AudioPlayRequest.Create2D(
                    cue.EventId,
                    parameters ?? cue.DefaultParameters,
                    priority,
                    frame: frame.Value,
                    traceId: traceId);
            AudioPlayResult result = _audioService.PlayOneShot(playRequest);
            if (result.Success)
            {
                LastFramePlayedCount++;
                return true;
            }

            LastFrameFailureCount++;
            _logBuffer.Clear()
                .Append("Audio cue play failed. cue=")
                .Append(cueId)
                .Append(", eventId=")
                .Append(cue.EventId)
                .Append(", code=")
                .Append(result.ErrorCode.ToString())
                .Append(", message=")
                .Append(result.Message);
            _logger.Warning(LogCategory, _logBuffer);
            return false;
        }

        internal bool TryPlayEventAt(
            int eventId,
            AudioTransform transform,
            int emitterId,
            RuntimeFrame frame,
            AudioTraceIdentity traceId,
            IReadOnlyList<AudioParameterValue> parameters,
            int priority,
            bool is3D)
        {
            LastFrameRequestCount++;
            if (eventId <= 0)
            {
                LastFrameFailureCount++;
                _logger.Warning(LogCategory, "Audio event request skipped: event id is invalid.");
                return false;
            }

            if (_audioService == null)
            {
                LastFrameFailureCount++;
                if (_logMissingAudioServiceWarnings)
                    LogSkippedEvent(eventId, "audio service is not configured");
                return false;
            }

            bool playAs3D = is3D;
            if (_catalog != null)
            {
                if (!_catalog.TryGetEvent(eventId, out AudioEventDefinition audioEvent))
                {
                    LastFrameFailureCount++;
                    LogSkippedEvent(eventId, "event is not registered");
                    return false;
                }

                playAs3D = audioEvent.Is3D;
            }

            AudioPlayRequest playRequest = playAs3D
                ? AudioPlayRequest.Create3D(
                    eventId,
                    transform,
                    emitterId: emitterId,
                    parameters,
                    priority,
                    frame.Value,
                    traceId)
                : AudioPlayRequest.Create2D(
                    eventId,
                    parameters,
                    priority,
                    frame.Value,
                    traceId);
            AudioPlayResult result = _audioService.PlayOneShot(playRequest);
            if (result.Success)
            {
                LastFramePlayedCount++;
                return true;
            }

            LastFrameFailureCount++;
            _logBuffer.Clear()
                .Append("Audio event play failed. eventId=")
                .Append(eventId)
                .Append(", code=")
                .Append(result.ErrorCode.ToString())
                .Append(", message=")
                .Append(result.Message);
            _logger.Warning(LogCategory, _logBuffer);
            return false;
        }

        internal bool TryPlayIntent(in CharacterTestAudioIntent intent, AudioParameterValue[] parameters)
        {
            if (intent.CueId > 0)
            {
                return TryPlayCueAt(
                    intent.CueId,
                    intent.Transform,
                    intent.EmitterId,
                    intent.Frame,
                    intent.TraceId,
                    parameters,
                    intent.Priority);
            }

            return TryPlayEventAt(
                intent.EventId,
                intent.Transform,
                intent.EmitterId,
                intent.Frame,
                intent.TraceId,
                parameters,
                intent.Priority,
                intent.Is3D);
        }

        /// <remarks>NoAlloc after initialization. The parameter view must remain stable until the command is drained.</remarks>
        internal bool TryCreateCommand(
            in CharacterTestAudioIntent intent,
            IReadOnlyList<AudioParameterValue> parameters,
            int parameterCount,
            int dedupeOrdinal,
            out AudioCommand command)
        {
            command = default;
            LastFrameRequestCount++;
            if (_audioService == null)
            {
                LastFrameFailureCount++;
                if (_logMissingAudioServiceWarnings)
                {
                    if (intent.CueId > 0)
                        LogSkipped(intent.CueId, "audio service is not configured");
                    else
                        LogSkippedEvent(intent.EventId, "audio service is not configured");
                }
                return false;
            }

            int eventId = intent.EventId;
            bool is3D = intent.Is3D;
            IReadOnlyList<AudioParameterValue> resolvedParameters = parameters;
            int resolvedParameterCount = parameterCount;
            if (intent.CueId > 0)
            {
                if (_catalog == null || !_catalog.TryGetCue(intent.CueId, out AudioCueDefinition cue))
                {
                    LastFrameFailureCount++;
                    LogSkipped(intent.CueId, "cue is not registered");
                    return false;
                }
                if (cue.PlayMode != AudioCuePlayMode.OneShot)
                {
                    LastFrameFailureCount++;
                    LogSkipped(intent.CueId, "only one-shot cues are supported by this slice");
                    return false;
                }
                if (!_catalog.TryGetEvent(cue.EventId, out AudioEventDefinition cueEvent))
                {
                    LastFrameFailureCount++;
                    LogSkipped(intent.CueId, "event is not registered");
                    return false;
                }

                eventId = cue.EventId;
                is3D = cueEvent.Is3D;
                if (resolvedParameterCount <= 0)
                {
                    resolvedParameters = cue.DefaultParameters;
                    resolvedParameterCount = cue.DefaultParameters.Count;
                }
            }
            else
            {
                if (eventId <= 0)
                {
                    LastFrameFailureCount++;
                    _logger.Warning(LogCategory, "Audio event request skipped: event id is invalid.");
                    return false;
                }
                if (_catalog != null)
                {
                    if (!_catalog.TryGetEvent(eventId, out AudioEventDefinition audioEvent))
                    {
                        LastFrameFailureCount++;
                        LogSkippedEvent(eventId, "event is not registered");
                        return false;
                    }
                    is3D = audioEvent.Is3D;
                }
            }

            AudioSpatialMode spatialMode = is3D
                ? AudioSpatialMode.ThreeDimensional
                : AudioSpatialMode.TwoDimensional;
            AudioTransform transform = is3D ? intent.Transform : AudioTransform.Origin;
            int emitterId = is3D ? intent.EmitterId : 0;
            var request = new AudioPlayRequest(
                eventId,
                transform,
                resolvedParameters,
                emitterId,
                intent.Priority,
                intent.Frame.Value,
                intent.TraceId,
                AudioPlayMode.OneShot,
                spatialMode,
                resolvedParameterCount);
            command = new AudioCommand(
                AudioCommandKind.PlayOneShot,
                request,
                emitterId == 0 ? AudioScope.Scene : AudioScope.Entity,
                ownerId: emitterId,
                categoryId: (int)intent.Source,
                dedupeOrdinal: dedupeOrdinal,
                predictionPolicy: AudioPredictionPolicy.SuppressDuringReplay);
            return true;
        }

        internal void ApplyCommandDrainSnapshot(in AudioPresentationDrainSnapshot snapshot)
        {
            LastFramePlayedCount += snapshot.Accepted;
            LastFrameFailureCount += snapshot.Failed + snapshot.BudgetRejected;
        }

        private void LogSkipped(CharacterActionAudioCueRequest request, string reason)
        {
            LogSkipped(request.CueId, reason);
        }

        private void LogSkipped(int cueId, string reason)
        {
            _logBuffer.Clear()
                .Append("Audio cue request skipped. cue=")
                .Append(cueId)
                .Append(", reason=")
                .Append(reason);
            _logger.Warning(LogCategory, _logBuffer);
        }

        private void LogSkippedEvent(int eventId, string reason)
        {
            _logBuffer.Clear()
                .Append("Audio event request skipped. eventId=")
                .Append(eventId)
                .Append(", reason=")
                .Append(reason);
            _logger.Warning(LogCategory, _logBuffer);
        }

        private static AudioTransform CreateAudioTransform(
            CharacterTestCharacterEntity entity)
        {
            CharacterTestCharacterTransformComponent transform = entity.Transform;
            double yawRadians = transform.YawDegrees * Math.PI / 180.0;
            return new AudioTransform(
                transform.X,
                transform.Y,
                transform.Z,
                forwardX: (float)Math.Sin(yawRadians),
                forwardY: 0f,
                forwardZ: (float)Math.Cos(yawRadians));
        }
    }
}
