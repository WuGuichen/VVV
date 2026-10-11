using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Core.Math;
using MxFramework.Rendering;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestInteractionRenderingSystem : ICharacterTestInteractionConsumer
    {
        public const string ConsumerIdValue = "interaction.rendering";

        private IRenderDataPublisher _publisher;
        private IRenderSubjectMap<int> _sourceSubjects;

        public CharacterTestInteractionRenderingSystem(
            IRenderDataPublisher publisher = null,
            IRenderSubjectMap<int> sourceSubjects = null)
        {
            _publisher = publisher;
            _sourceSubjects = sourceSubjects;
        }

        public CharacterTestInteractionRenderingSummary LastSummary { get; private set; } =
            CharacterTestInteractionRenderingSummary.Empty;

        public string ConsumerId => ConsumerIdValue;
        public int ConsumerOrder => 300;

        public void SetPublisher(
            IRenderDataPublisher publisher,
            IRenderSubjectMap<int> sourceSubjects)
        {
            _publisher = publisher;
            _sourceSubjects = sourceSubjects;
        }

        public void Consume(CharacterTestInteractionConsumerContext context)
        {
            Tick(context.Interactions, context.Frame);
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(
            IReadOnlyList<CharacterTestInteractionEvent> interactions,
            RuntimeFrame frame)
        {
            var state = new RenderingState(frame);
            if (interactions == null || interactions.Count == 0)
            {
                LastSummary = state.ToSummary();
                return;
            }

            for (int i = 0; i < interactions.Count; i++)
            {
                CharacterTestInteractionEvent interaction = interactions[i];
                if (interaction.Kind != CharacterTestInteractionKind.PhysicalField)
                    continue;

                state.ProcessedFieldCount++;
                state.LastForceMode = interaction.PhysicalField.ForceMode;
                state.LastFieldShape = interaction.PhysicalField.Shape;
                if (_publisher == null || _sourceSubjects == null)
                {
                    state.SkippedCount++;
                    state.LastStableId = interaction.StableId;
                    continue;
                }

                if (!CharacterTestInteractionRenderChannels.TryResolve(
                        interaction.PhysicalField,
                        out int channelId))
                {
                    state.SkippedCount++;
                    state.LastStableId = interaction.StableId;
                    continue;
                }

                int subjectSourceId = ResolveSubjectSourceId(interaction);
                if (subjectSourceId <= 0)
                {
                    state.SkippedCount++;
                    state.LastStableId = interaction.StableId;
                    continue;
                }

                MxRenderSubjectId subject =
                    _sourceSubjects.GetOrCreate(subjectSourceId, MxRenderSubjectRole.Tracked);
                float radius = ToFloat(interaction.Radius);
                float intensity = ResolveRenderIntensity(interaction.PhysicalField, interaction.Intensity);
                _publisher.PublishFieldImpulse(
                    subject,
                    new MxRenderFieldImpulseEvent(
                        ToVector3(interaction.AnchorPosition),
                        radius,
                        intensity,
                        channelId));

                state.PublishedFieldImpulseCount++;
                state.LastStableId = interaction.StableId;
                state.LastChannelId = channelId;
                state.LastSubjectSourceId = subjectSourceId;
                state.LastRadius = radius;
                state.LastIntensity = intensity;
            }

            LastSummary = state.ToSummary();
        }

        private static int ResolveSubjectSourceId(CharacterTestInteractionEvent interaction)
        {
            if (interaction.SourceEntityId > 0)
                return interaction.SourceEntityId;
            if (interaction.TraceId > 0)
                return interaction.TraceId;
            return 0;
        }

        private static float ResolveRenderIntensity(
            CharacterTestInteractionPhysicalField field,
            int fallbackIntensity)
        {
            if (field.ForceMode == CharacterTestInteractionForceMode.RadialImpulse
                && field.ImpulseSpeedMillimetersPerSecond > 0)
            {
                return field.ImpulseSpeedMillimetersPerSecond / 1000f;
            }

            if (field.ForceMode == CharacterTestInteractionForceMode.AttractionAcceleration
                && field.AccelerationMillimetersPerSecondSquared > 0)
            {
                return field.AccelerationMillimetersPerSecondSquared / 1000f;
            }

            return fallbackIntensity > 0 ? fallbackIntensity : 0f;
        }

        private static Vector3 ToVector3(FixVector3 value)
        {
            return new Vector3(ToFloat(value.X), ToFloat(value.Y), ToFloat(value.Z));
        }

        private static float ToFloat(Fix64 value)
        {
            return (float)(value.RawValue / (double)Fix64.Scale);
        }

        private struct RenderingState
        {
            public RenderingState(RuntimeFrame frame)
            {
                Frame = frame;
                ProcessedFieldCount = 0;
                PublishedFieldImpulseCount = 0;
                SkippedCount = 0;
                LastStableId = 0;
                LastChannelId = 0;
                LastForceMode = 0;
                LastFieldShape = 0;
                LastSubjectSourceId = 0;
                LastRadius = 0f;
                LastIntensity = 0f;
            }

            public RuntimeFrame Frame { get; }
            public int ProcessedFieldCount;
            public int PublishedFieldImpulseCount;
            public int SkippedCount;
            public int LastStableId;
            public int LastChannelId;
            public CharacterTestInteractionForceMode LastForceMode;
            public CharacterTestInteractionFieldShape LastFieldShape;
            public int LastSubjectSourceId;
            public float LastRadius;
            public float LastIntensity;

            public CharacterTestInteractionRenderingSummary ToSummary()
            {
                return new CharacterTestInteractionRenderingSummary(
                    Frame,
                    ProcessedFieldCount,
                    PublishedFieldImpulseCount,
                    SkippedCount,
                    LastStableId,
                    LastChannelId,
                    LastForceMode,
                    LastFieldShape,
                    LastSubjectSourceId,
                    LastRadius,
                    LastIntensity);
            }
        }
    }

    public readonly struct CharacterTestInteractionRenderingSummary
    {
        public static readonly CharacterTestInteractionRenderingSummary Empty =
            new CharacterTestInteractionRenderingSummary(
                RuntimeFrame.Zero,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0f,
                0f);

        public CharacterTestInteractionRenderingSummary(
            RuntimeFrame frame,
            int processedFieldCount,
            int publishedFieldImpulseCount,
            int skippedCount,
            int lastStableId,
            int lastChannelId,
            CharacterTestInteractionForceMode lastForceMode,
            CharacterTestInteractionFieldShape lastFieldShape,
            int lastSubjectSourceId,
            float lastRadius,
            float lastIntensity)
        {
            Frame = frame;
            ProcessedFieldCount = processedFieldCount < 0 ? 0 : processedFieldCount;
            PublishedFieldImpulseCount = publishedFieldImpulseCount < 0 ? 0 : publishedFieldImpulseCount;
            SkippedCount = skippedCount < 0 ? 0 : skippedCount;
            LastStableId = lastStableId;
            LastChannelId = lastChannelId < 0 ? 0 : lastChannelId;
            LastForceMode = lastForceMode;
            LastFieldShape = lastFieldShape;
            LastSubjectSourceId = lastSubjectSourceId < 0 ? 0 : lastSubjectSourceId;
            LastRadius = lastRadius < 0f ? 0f : lastRadius;
            LastIntensity = lastIntensity < 0f ? 0f : lastIntensity;
        }

        public RuntimeFrame Frame { get; }
        public int ProcessedFieldCount { get; }
        public int PublishedFieldImpulseCount { get; }
        public int SkippedCount { get; }
        public int LastStableId { get; }
        public int LastChannelId { get; }
        public CharacterTestInteractionForceMode LastForceMode { get; }
        public CharacterTestInteractionFieldShape LastFieldShape { get; }
        public int LastSubjectSourceId { get; }
        public float LastRadius { get; }
        public float LastIntensity { get; }

        public string ToSummaryLine()
        {
            return "frame=" + Frame.Value.ToString(CultureInfo.InvariantCulture)
                + " fields=" + ProcessedFieldCount.ToString(CultureInfo.InvariantCulture)
                + " published=" + PublishedFieldImpulseCount.ToString(CultureInfo.InvariantCulture)
                + " skipped=" + SkippedCount.ToString(CultureInfo.InvariantCulture)
                + " channel=" + CharacterTestInteractionRenderChannels.ToSummaryName(LastChannelId)
                + " lastMode=" + ((int)LastForceMode == 0 ? "none" : LastForceMode.ToString())
                + " lastShape=" + ((int)LastFieldShape == 0 ? "none" : LastFieldShape.ToString())
                + " source=" + LastSubjectSourceId.ToString(CultureInfo.InvariantCulture)
                + " radius=" + LastRadius.ToString("0.###", CultureInfo.InvariantCulture)
                + " intensity=" + LastIntensity.ToString("0.###", CultureInfo.InvariantCulture)
                + " stable=" + (LastStableId == 0 ? "none" : LastStableId.ToString(CultureInfo.InvariantCulture));
        }
    }

    public static class CharacterTestInteractionRenderChannels
    {
        public const int RadialImpulseSphere = 1;
        public const int AttractionCapsule = 2;

        public static bool TryResolve(
            CharacterTestInteractionPhysicalField field,
            out int channelId)
        {
            if (field.ForceMode == CharacterTestInteractionForceMode.RadialImpulse
                && field.Shape == CharacterTestInteractionFieldShape.Sphere)
            {
                channelId = RadialImpulseSphere;
                return true;
            }

            if (field.ForceMode == CharacterTestInteractionForceMode.AttractionAcceleration
                && field.Shape == CharacterTestInteractionFieldShape.Capsule)
            {
                channelId = AttractionCapsule;
                return true;
            }

            channelId = 0;
            return false;
        }

        public static string ToSummaryName(int channelId)
        {
            switch (channelId)
            {
                case RadialImpulseSphere:
                    return "radial_impulse_sphere";
                case AttractionCapsule:
                    return "attraction_capsule";
                default:
                    return "none";
            }
        }
    }
}


