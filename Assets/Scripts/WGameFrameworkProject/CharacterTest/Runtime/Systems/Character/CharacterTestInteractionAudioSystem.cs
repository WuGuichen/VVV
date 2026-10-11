using System.Collections.Generic;
using System.Globalization;
using MxFramework.Audio;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestInteractionAudioSystem : ICharacterTestInteractionConsumer
    {
        public const string ConsumerIdValue = "interaction.audio";
        private const int StandaloneAudioIntentCapacity = 32;

        private readonly CharacterTestAudioIntentQueue _audioIntentQueue;
        private readonly CharacterTestAudioIntentDrainSystem _standaloneAudioIntentDrainSystem;
        private readonly bool _useStandaloneAudioIntentQueue;

        public CharacterTestInteractionAudioSystem(CharacterTestAudioCuePlaybackSystem audioCuePlaybackSystem)
            : this(audioCuePlaybackSystem, null)
        {
        }

        internal CharacterTestInteractionAudioSystem(
            CharacterTestAudioCuePlaybackSystem audioCuePlaybackSystem,
            CharacterTestAudioIntentQueue audioIntentQueue)
        {
            _audioIntentQueue = audioIntentQueue ?? new CharacterTestAudioIntentQueue(StandaloneAudioIntentCapacity);
            _useStandaloneAudioIntentQueue = audioIntentQueue == null;
            _standaloneAudioIntentDrainSystem = _useStandaloneAudioIntentQueue
                ? new CharacterTestAudioIntentDrainSystem(audioCuePlaybackSystem)
                : null;
        }

        public CharacterTestInteractionAudioSummary LastSummary { get; private set; } =
            CharacterTestInteractionAudioSummary.Empty;
        public CharacterTestAudioIntentDrainSummary LastIntentDrainSummary =>
            _standaloneAudioIntentDrainSystem == null
                ? CharacterTestAudioIntentDrainSummary.Empty
                : _standaloneAudioIntentDrainSystem.LastSummary;

        public string ConsumerId => ConsumerIdValue;
        public int ConsumerOrder => 210;

        public void Consume(CharacterTestInteractionConsumerContext context)
        {
            Tick(context.Interactions, context.Frame);
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(
            IReadOnlyList<CharacterTestInteractionEvent> interactions,
            RuntimeFrame frame)
        {
            var state = new AudioState(frame);
            if (_useStandaloneAudioIntentQueue)
                _audioIntentQueue.Clear();
            if (interactions == null || interactions.Count == 0)
            {
                if (_useStandaloneAudioIntentQueue)
                    _standaloneAudioIntentDrainSystem.Tick(_audioIntentQueue, frame);
                LastSummary = state.ToSummary();
                return;
            }

            for (int i = 0; i < interactions.Count; i++)
            {
                CharacterTestInteractionEvent interaction = interactions[i];
                if (!CharacterTestInteractionPresentationRules.IsFireballExplosion(interaction))
                    continue;

                state.ProcessedInteractionCount++;
                PresentFireballExplosion(interaction, frame, ref state);
            }

            if (_useStandaloneAudioIntentQueue)
                _standaloneAudioIntentDrainSystem.Tick(_audioIntentQueue, frame);
            LastSummary = state.ToSummary();
        }

        private void PresentFireballExplosion(
            CharacterTestInteractionEvent interaction,
            RuntimeFrame frame,
            ref AudioState state)
        {
            if (_audioIntentQueue != null
                && _audioIntentQueue.Enqueue(CharacterTestAudioIntent.CreateCue(
                    CharacterTestAudioIntentSource.Interaction,
                    CharacterTestAudioCueCatalog.FireballExplosionCueId,
                    CreateAudioTransform(interaction),
                    interaction.SourceEntityId,
                    frame,
                    new AudioTraceIdentity(AudioTraceKindIds.GameplayInteraction, interaction.SourceEntityId, frame.Value, interaction.StableId))))
            {
                state.AudioPlayedCount++;
            }
            else
            {
                state.SkippedCount++;
            }

            state.LastStableId = interaction.StableId;
            state.LastForceMode = interaction.PhysicalField.ForceMode;
            state.LastFieldShape = interaction.PhysicalField.Shape;
            state.LastAudioCueKey = CharacterTestAudioCueCatalog.FireballExplosionCueId;
        }

        private static AudioTransform CreateAudioTransform(CharacterTestInteractionEvent interaction)
        {
            FixVector3 direction = interaction.Direction;
            return new AudioTransform(
                ToFloat(interaction.AnchorPosition.X),
                ToFloat(interaction.AnchorPosition.Y),
                ToFloat(interaction.AnchorPosition.Z),
                ToFloat(direction.X),
                ToFloat(direction.Y),
                ToFloat(direction.Z));
        }

        private static float ToFloat(Fix64 value)
        {
            return (float)(value.RawValue / (double)Fix64.Scale);
        }

        private struct AudioState
        {
            public AudioState(RuntimeFrame frame)
            {
                Frame = frame;
                ProcessedInteractionCount = 0;
                AudioPlayedCount = 0;
                SkippedCount = 0;
                LastStableId = 0;
                LastForceMode = 0;
                LastFieldShape = 0;
                LastAudioCueKey = 0;
            }

            public RuntimeFrame Frame { get; }
            public int ProcessedInteractionCount;
            public int AudioPlayedCount;
            public int SkippedCount;
            public int LastStableId;
            public CharacterTestInteractionForceMode LastForceMode;
            public CharacterTestInteractionFieldShape LastFieldShape;
            public int LastAudioCueKey;

            public CharacterTestInteractionAudioSummary ToSummary()
            {
                return new CharacterTestInteractionAudioSummary(
                    Frame,
                    ProcessedInteractionCount,
                    AudioPlayedCount,
                    SkippedCount,
                    LastStableId,
                    LastForceMode,
                    LastFieldShape,
                    LastAudioCueKey);
            }
        }
    }

    public readonly struct CharacterTestInteractionAudioSummary
    {
        public static readonly CharacterTestInteractionAudioSummary Empty =
            new CharacterTestInteractionAudioSummary(
                RuntimeFrame.Zero,
                0,
                0,
                0,
                0,
                0,
                0,
                0);

        public CharacterTestInteractionAudioSummary(
            RuntimeFrame frame,
            int processedInteractionCount,
            int audioPlayedCount,
            int skippedCount,
            int lastStableId,
            CharacterTestInteractionForceMode lastForceMode,
            CharacterTestInteractionFieldShape lastFieldShape,
            int lastAudioCueKey)
        {
            Frame = frame;
            ProcessedInteractionCount = processedInteractionCount < 0 ? 0 : processedInteractionCount;
            AudioPlayedCount = audioPlayedCount < 0 ? 0 : audioPlayedCount;
            SkippedCount = skippedCount < 0 ? 0 : skippedCount;
            LastStableId = lastStableId;
            LastForceMode = lastForceMode;
            LastFieldShape = lastFieldShape;
            LastAudioCueKey = lastAudioCueKey;
        }

        public RuntimeFrame Frame { get; }
        public int ProcessedInteractionCount { get; }
        public int AudioPlayedCount { get; }
        public int SkippedCount { get; }
        public int LastStableId { get; }
        public CharacterTestInteractionForceMode LastForceMode { get; }
        public CharacterTestInteractionFieldShape LastFieldShape { get; }
        public int LastAudioCueKey { get; }

        public string ToSummaryLine()
        {
            return "frame=" + Frame.Value.ToString(CultureInfo.InvariantCulture)
                + " interactions=" + ProcessedInteractionCount.ToString(CultureInfo.InvariantCulture)
                + " audio=" + AudioPlayedCount.ToString(CultureInfo.InvariantCulture)
                + " skipped=" + SkippedCount.ToString(CultureInfo.InvariantCulture)
                + " lastMode=" + ((int)LastForceMode == 0 ? "none" : LastForceMode.ToString())
                + " lastShape=" + ((int)LastFieldShape == 0 ? "none" : LastFieldShape.ToString())
                + " lastAudio=" + (LastAudioCueKey == 0 ? "none" : LastAudioCueKey.ToString(CultureInfo.InvariantCulture))
                + " stable=" + (LastStableId == 0 ? "none" : LastStableId.ToString(CultureInfo.InvariantCulture));
        }
    }
}

