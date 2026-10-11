using System;
using System.Collections.Generic;
using MxFramework.Audio;
using MxFramework.Audio.Physical;
using MxFramework.CharacterAction;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestAudioPresentationKind
    {
        None = 0,
        KatanaAirDisplacementFirst = 1,
        KatanaAirDisplacementSecond = 2,
        KatanaAirDisplacementThird = 3,
    }

    /// <summary>
    /// Matches authored numeric identities to a compact project presentation kind when a request enters the entity
    /// buffer, before any high-frequency presentation Tick reads it.
    /// </summary>
    internal static class CharacterTestAudioCueRequestClassifier
    {
        /// <remarks>NoAlloc. Classification uses numeric equality only.</remarks>
        public static CharacterTestAudioPresentationKind Classify(in CharacterActionAudioCueRequest request)
        {
            if (!(request.CueId == CharacterTestAudioCueCatalog.LightAttackAirDisplacementCueId))
            {
                return CharacterTestAudioPresentationKind.None;
            }

            if ((request.Metadata.StableEventId == CharacterTestKatanaAirDisplacementProfile.FirstAttackStableEventId))
            {
                return CharacterTestAudioPresentationKind.KatanaAirDisplacementFirst;
            }
            if ((request.Metadata.StableEventId == CharacterTestKatanaAirDisplacementProfile.SecondAttackStableEventId))
            {
                return CharacterTestAudioPresentationKind.KatanaAirDisplacementSecond;
            }
            if ((request.Metadata.StableEventId == CharacterTestKatanaAirDisplacementProfile.ThirdAttackStableEventId))
            {
                return CharacterTestAudioPresentationKind.KatanaAirDisplacementThird;
            }

            return CharacterTestAudioPresentationKind.None;
        }
    }

    /// <summary>CharacterTest project constants for the default katana air-displacement profile.</summary>
    /// <remarks>NoAlloc. These constants are composition data, not framework-wide acoustic defaults.</remarks>
    internal static class CharacterTestKatanaAirDisplacementProfile
    {
        public const int FirstAttackStableEventId = CharacterTestAuthoredContentIds.D401_Id139;
        public const string FirstAttackStableEventLabel = "sfx.light_attack.air_peak";
        public const int SecondAttackStableEventId = CharacterTestAuthoredContentIds.D401_Id140;
        public const string SecondAttackStableEventLabel = "sfx.light_attack_2.air_peak";
        public const int ThirdAttackStableEventId = CharacterTestAuthoredContentIds.D401_Id141;
        public const string ThirdAttackStableEventLabel = "sfx.light_attack_3.air_peak";

        public const float KatanaMassKilograms = 1.1f;
        public const float ReferenceWeaponMassKilograms = 2.2f;
        public const float KatanaNormalizedMass = KatanaMassKilograms / ReferenceWeaponMassKilograms;

        public const float KatanaBladeLengthMeters = 1.05f;
        public const float KatanaBladeWidthMeters = 0.032f;
        public const float ReferenceSweptAreaSquareMeters = 0.096f;
        public const float KatanaNormalizedSweptArea =
            KatanaBladeLengthMeters * KatanaBladeWidthMeters / ReferenceSweptAreaSquareMeters;

        public const float ReferenceAirSpanMeters = 1.4f;
        public const float AirNormalizedContactArea = KatanaBladeLengthMeters / ReferenceAirSpanMeters;
        public const float SweepAngleDegrees = 45f;
        public const int ActiveWindowStartFrame = 18;
        public const int ActiveWindowEndFrame = 34;

        private const float FirstAttackPeakSpeedMetersPerSecond = 18f;
        private const float SecondAttackPeakSpeedMetersPerSecond = 20f;
        private const float ThirdAttackPeakSpeedMetersPerSecond = 22f;
        private const float ReferencePeakSpeedMetersPerSecond = 32f;

        /// <remarks>NoAlloc.</remarks>
        public static bool TryResolvePeakSpeed(
            CharacterTestAudioPresentationKind presentationKind,
            out float speedMetersPerSecond)
        {
            switch (presentationKind)
            {
                case CharacterTestAudioPresentationKind.KatanaAirDisplacementFirst:
                    speedMetersPerSecond = FirstAttackPeakSpeedMetersPerSecond;
                    return true;
                case CharacterTestAudioPresentationKind.KatanaAirDisplacementSecond:
                    speedMetersPerSecond = SecondAttackPeakSpeedMetersPerSecond;
                    return true;
                case CharacterTestAudioPresentationKind.KatanaAirDisplacementThird:
                    speedMetersPerSecond = ThirdAttackPeakSpeedMetersPerSecond;
                    return true;
                default:
                    speedMetersPerSecond = 0f;
                    return false;
            }
        }

        /// <remarks>NoAlloc.</remarks>
        public static float NormalizeIntensity(float speedMetersPerSecond)
        {
            if (speedMetersPerSecond <= 0f || float.IsNaN(speedMetersPerSecond))
                return 0f;
            return speedMetersPerSecond >= ReferencePeakSpeedMetersPerSecond
                ? 1f
                : speedMetersPerSecond / ReferencePeakSpeedMetersPerSecond;
        }
    }

    /// <summary>Converts an authored light-attack presentation peak into one air-displacement fact.</summary>
    internal static class CharacterTestAirDisplacementFactBuilder
    {
        /// <remarks>NoAlloc.</remarks>
        public static bool IsLightAttackPresentation(CharacterTestAudioPresentationKind presentationKind)
        {
            return CharacterTestKatanaAirDisplacementProfile.TryResolvePeakSpeed(
                presentationKind,
                out _);
        }

        /// <remarks>NoAlloc.</remarks>
        public static bool TryCreate(
            CharacterTestCharacterEntity entity,
            in CharacterActionAudioCueRequest request,
            CharacterTestAudioPresentationKind presentationKind,
            RuntimeFrame frame,
            out PhysicalAudioInteractionFact fact)
        {
            return TryCreate(entity, request, presentationKind, frame, null, out fact);
        }

        /// <remarks>NoAlloc.</remarks>
        public static bool TryCreate(
            CharacterTestCharacterEntity entity,
            in CharacterActionAudioCueRequest request,
            CharacterTestAudioPresentationKind presentationKind,
            RuntimeFrame frame,
            IAcousticBodyPropertyProvider propertyProvider,
            out PhysicalAudioInteractionFact fact)
        {
            if (entity == null
                || !IsLightAttackPresentation(presentationKind)
                || request.Metadata.LocalFrame < CharacterTestKatanaAirDisplacementProfile.ActiveWindowStartFrame
                || request.Metadata.LocalFrame > CharacterTestKatanaAirDisplacementProfile.ActiveWindowEndFrame
                || !CharacterTestKatanaAirDisplacementProfile.TryResolvePeakSpeed(
                    presentationKind,
                    out float speedMetersPerSecond))
            {
                fact = default;
                return false;
            }

            var katana = new AcousticBodyProperties(
                CharacterTestAcousticMaterialMapper.Metal,
                CharacterTestKatanaAirDisplacementProfile.KatanaNormalizedMass,
                CharacterTestKatanaAirDisplacementProfile.KatanaNormalizedSweptArea,
                AcousticSurfaceKind.Smooth,
                AcousticConstructionKind.Solid);
            int weaponSubjectId = CharacterTestAcousticSubjectId.ForCharacter(
                entity.Id.Value,
                CharacterTestAcousticSubjectRole.Weapon);
            if (propertyProvider != null
                && propertyProvider.TryGetProperties(weaponSubjectId, out AcousticBodyProperties weapon))
            {
                katana = weapon;
            }
            var air = new AcousticBodyProperties(
                CharacterTestAcousticMaterialMapper.Air,
                mass: 0f,
                CharacterTestKatanaAirDisplacementProfile.AirNormalizedContactArea,
                AcousticSurfaceKind.Any,
                AcousticConstructionKind.Gas);
            float intensity = CharacterTestKatanaAirDisplacementProfile.NormalizeIntensity(
                speedMetersPerSecond);
            AudioTransform transform = CreateAudioTransform(entity);

            fact = new PhysicalAudioInteractionFact(
                PhysicalAudioInteractionKind.AirDisplacement,
                PhysicalAudioInteractionMode.Sweep,
                PhysicalAudioInteractionPhase.Started,
                katana,
                air,
                relativeSpeed: speedMetersPerSecond,
                normalSpeed: 0f,
                tangentialSpeed: speedMetersPerSecond,
                pressure: 0f,
                impulse: intensity,
                angleDegrees: CharacterTestKatanaAirDisplacementProfile.SweepAngleDegrees,
                intensity,
                new PhysicalAudioDirection(
                    transform.ForwardX,
                    transform.ForwardY,
                    transform.ForwardZ),
                transform,
                emitterId: entity.Id.Value,
                frame: frame.Value,
                sequence: request.Metadata.InstanceId);
            return true;
        }

        private static AudioTransform CreateAudioTransform(CharacterTestCharacterEntity entity)
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

    /// <summary>
    /// Replaces authored light-attack presentation Cue requests with resolver-driven AirDisplacement playback
    /// when and only when the complete generated Physical Audio catalog is active.
    /// </summary>
    internal sealed class CharacterTestAirDisplacementAudioSystem
    {
        private static readonly AudioTraceIdentity ResolvedTraceId = new AudioTraceIdentity(AudioTraceKindIds.AirDisplacement);

        private readonly ICharacterTestAudioCueCatalog _audioCueCatalog;
        private readonly PhysicalAudioResolver _resolver;
        private readonly CharacterTestAudioIntentQueue _audioIntentQueue;
        private readonly IAcousticBodyPropertyProvider _propertyProvider;
        private readonly AudioParameterValue[] _parameterValues =
            new AudioParameterValue[CharacterTestAudioIntent.MaxParameterCount];

        public CharacterTestAirDisplacementAudioSystem(
            CharacterTestAudioCuePlaybackSystem audioCuePlaybackSystem,
            CharacterTestAudioIntentQueue audioIntentQueue,
            IAcousticBodyPropertyProvider propertyProvider = null)
        {
            _audioCueCatalog = audioCuePlaybackSystem?.Catalog;
            if (_audioCueCatalog?.PhysicalAudioDefinitions != null)
                _resolver = new PhysicalAudioResolver(_audioCueCatalog.PhysicalAudioDefinitions);
            _audioIntentQueue = audioIntentQueue;
            _propertyProvider = propertyProvider;
            LastSummary = CharacterTestAirDisplacementAudioSummary.Empty;
        }

        public CharacterTestAirDisplacementAudioSummary LastSummary { get; private set; }

        /// <remarks>
        /// NoAlloc after initialization. A missing Physical Audio catalog leaves the presentation requests untouched
        /// so the existing generic Cue path remains the complete legacy fallback.
        /// </remarks>
        public void Tick(CharacterTestCharacterRuntimeWorld world, RuntimeFrame frame)
        {
            var state = new FrameState(frame, _resolver != null);
            if (_resolver == null || world == null)
            {
                LastSummary = state.ToSummary();
                return;
            }

            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
            {
                CharacterTestCharacterEntity entity = entities[entityIndex];
                int requestIndex = 0;
                while (requestIndex < entity.PendingAudioCueRequestCount)
                {
                    CharacterActionAudioCueRequest request = entity.AudioCueRequests[requestIndex];
                    CharacterTestAudioPresentationKind presentationKind =
                        entity.GetAudioCuePresentationKindAt(requestIndex);
                    if (!CharacterTestAirDisplacementFactBuilder.IsLightAttackPresentation(presentationKind))
                    {
                        requestIndex++;
                        continue;
                    }

                    // A complete generated catalog is authoritative. Consume its presentation fact even when
                    // validation or resolution fails, rather than mixing it with the legacy generic Cue path.
                    entity.RemoveAudioCueRequestAt(requestIndex);
                    Resolve(entity, request, presentationKind, frame, ref state);
                }
            }

            LastSummary = state.ToSummary();
        }

        private void Resolve(
            CharacterTestCharacterEntity entity,
            in CharacterActionAudioCueRequest request,
            CharacterTestAudioPresentationKind presentationKind,
            RuntimeFrame frame,
            ref FrameState state)
        {
            state.ProcessedCount++;
            if (!CharacterTestAirDisplacementFactBuilder.TryCreate(
                    entity,
                    request,
                    presentationKind,
                    frame,
                    _propertyProvider,
                    out PhysicalAudioInteractionFact fact))
            {
                state.InvalidPresentationSkipCount++;
                return;
            }
            if (!_resolver.TryResolve(fact, _parameterValues.AsSpan(), out PhysicalAudioResolvedPlayback resolved))
            {
                state.ResolveFailedSkipCount++;
                return;
            }
            if (_audioCueCatalog == null
                || !_audioCueCatalog.TryGetPhysicalAudioBinding(
                    resolved.CueId,
                    out CharacterTestPhysicalAudioPlaybackBinding playbackBinding))
            {
                state.MissingCueSkipCount++;
                return;
            }
            if (_audioIntentQueue == null
                || !_audioIntentQueue.Enqueue(CharacterTestAudioIntent.CreateEvent(
                    CharacterTestAudioIntentSource.PhysicalAudio,
                    playbackBinding.EventId,
                    fact.Transform,
                    fact.EmitterId,
                    frame,
                    ResolvedTraceId,
                    _parameterValues,
                    resolved.Priority,
                    playbackBinding.Is3D,
                    resolved.ParameterCount)))
            {
                state.QueueFullSkipCount++;
                return;
            }

            PhysicalAudioLatestFactCapture.Publish(
                fact,
                resolved,
                _parameterValues.AsSpan(0, resolved.ParameterCount));
            state.PlayedCount++;
            state.LastBasisId = resolved.BasisId.Value;
            state.LastEventId = playbackBinding.EventId;
            state.LastEmitterId = fact.EmitterId;
        }

        private struct FrameState
        {
            public FrameState(RuntimeFrame frame, bool physicalCatalogActive)
            {
                Frame = frame;
                PhysicalCatalogActive = physicalCatalogActive;
                ProcessedCount = 0;
                PlayedCount = 0;
                InvalidPresentationSkipCount = 0;
                ResolveFailedSkipCount = 0;
                MissingCueSkipCount = 0;
                QueueFullSkipCount = 0;
                LastBasisId = 0;
                LastEventId = 0;
                LastEmitterId = 0;
            }

            public RuntimeFrame Frame;
            public bool PhysicalCatalogActive;
            public int ProcessedCount;
            public int PlayedCount;
            public int InvalidPresentationSkipCount;
            public int ResolveFailedSkipCount;
            public int MissingCueSkipCount;
            public int QueueFullSkipCount;
            public int LastBasisId;
            public int LastEventId;
            public int LastEmitterId;

            public CharacterTestAirDisplacementAudioSummary ToSummary()
            {
                return new CharacterTestAirDisplacementAudioSummary(
                    Frame,
                    PhysicalCatalogActive,
                    ProcessedCount,
                    PlayedCount,
                    InvalidPresentationSkipCount,
                    ResolveFailedSkipCount,
                    MissingCueSkipCount,
                    QueueFullSkipCount,
                    LastBasisId,
                    LastEventId,
                    LastEmitterId);
            }
        }
    }

    /// <summary>Allocation-free per-frame AirDisplacement bridge diagnostics.</summary>
    public readonly struct CharacterTestAirDisplacementAudioSummary
    {
        public static readonly CharacterTestAirDisplacementAudioSummary Empty = default;

        public CharacterTestAirDisplacementAudioSummary(
            RuntimeFrame frame,
            bool physicalCatalogActive,
            int processedCount,
            int playedCount,
            int invalidPresentationSkipCount,
            int resolveFailedSkipCount,
            int missingCueSkipCount,
            int queueFullSkipCount,
            int lastBasisId,
            int lastEventId,
            int lastEmitterId)
        {
            Frame = frame;
            PhysicalCatalogActive = physicalCatalogActive;
            ProcessedCount = processedCount;
            PlayedCount = playedCount;
            InvalidPresentationSkipCount = invalidPresentationSkipCount;
            ResolveFailedSkipCount = resolveFailedSkipCount;
            MissingCueSkipCount = missingCueSkipCount;
            QueueFullSkipCount = queueFullSkipCount;
            LastBasisId = lastBasisId;
            LastEventId = lastEventId;
            LastEmitterId = lastEmitterId;
        }

        public RuntimeFrame Frame { get; }
        public bool PhysicalCatalogActive { get; }
        public int ProcessedCount { get; }
        public int PlayedCount { get; }
        public int InvalidPresentationSkipCount { get; }
        public int ResolveFailedSkipCount { get; }
        public int MissingCueSkipCount { get; }
        public int QueueFullSkipCount { get; }
        public int LastBasisId { get; }
        public int LastEventId { get; }
        public int LastEmitterId { get; }
    }
}
