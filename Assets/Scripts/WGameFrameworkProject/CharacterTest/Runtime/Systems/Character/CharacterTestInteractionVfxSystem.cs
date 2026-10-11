using System.Collections.Generic;
using System.Globalization;
using MxFramework.Runtime;
using MxFramework.Vfx;
using MxFramework.Vfx.CombatBridge;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestInteractionVfxSystem : ICharacterTestInteractionConsumer
    {
        public const string ConsumerIdValue = "interaction.vfx";

        private VfxRuntimeModule _vfxRuntime;

        public CharacterTestInteractionVfxSystem(VfxRuntimeModule vfxRuntime)
        {
            _vfxRuntime = vfxRuntime;
        }

        public CharacterTestInteractionVfxSummary LastSummary { get; private set; } =
            CharacterTestInteractionVfxSummary.Empty;

        public string ConsumerId => ConsumerIdValue;
        public int ConsumerOrder => 200;

        public void SetVfxRuntime(VfxRuntimeModule vfxRuntime)
        {
            _vfxRuntime = vfxRuntime;
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
            var state = new VfxState(frame);
            if (interactions == null || interactions.Count == 0)
            {
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

            LastSummary = state.ToSummary();
        }

        private void PresentFireballExplosion(
            CharacterTestInteractionEvent interaction,
            RuntimeFrame frame,
            ref VfxState state)
        {
            if (_vfxRuntime == null)
            {
                state.SkippedCount++;
                state.LastStableId = interaction.StableId;
                state.LastForceMode = interaction.PhysicalField.ForceMode;
                state.LastFieldShape = interaction.PhysicalField.Shape;
                return;
            }

            int triggerId = interaction.StableId;
            VfxSpawnRequest request = _vfxRuntime.EnqueueSpawnRequest(new VfxSpawnRequest(
                frame,
                CharacterTestInteractionPresentationRules.VfxSourceDomain,
                interaction.Frame.Value,
                localFrame: 0,
                interaction.SourceOrder,
                triggerId,
                interaction.SourceEntityId,
                targetId: 0,
                new VfxDefinitionId(CharacterTestVfxIds.FireballExplosionImpactDefinitionId),
                VfxAnchor.World(new VfxPose(
                    CombatVfxPhysicsAdapter.ToVfxVector(interaction.AnchorPosition),
                    CombatVfxPhysicsAdapter.ToVfxVector(interaction.Direction))),
                new VfxParameterBlock(triggerId, interaction.Intensity),
                CharacterTestVfxIds.FireballExplosionImpactLifetimeFrames,
                new RuntimeTraceIdentity(CharacterTestFrameworkTraceKinds.ProjectileInteractionVfx, interaction.SourceEntityId, interaction.Frame.Value, interaction.SourceOrder)));
            if (request.DefinitionId.IsValid)
                state.VfxSpawnedCount++;
            else
                state.SkippedCount++;

            state.LastStableId = interaction.StableId;
            state.LastForceMode = interaction.PhysicalField.ForceMode;
            state.LastFieldShape = interaction.PhysicalField.Shape;
            state.LastVfxDefinitionId = CharacterTestVfxIds.FireballExplosionImpactDefinitionId;
        }

        private struct VfxState
        {
            public VfxState(RuntimeFrame frame)
            {
                Frame = frame;
                ProcessedInteractionCount = 0;
                VfxSpawnedCount = 0;
                SkippedCount = 0;
                LastStableId = 0;
                LastForceMode = 0;
                LastFieldShape = 0;
                LastVfxDefinitionId = 0;
            }

            public RuntimeFrame Frame { get; }
            public int ProcessedInteractionCount;
            public int VfxSpawnedCount;
            public int SkippedCount;
            public int LastStableId;
            public CharacterTestInteractionForceMode LastForceMode;
            public CharacterTestInteractionFieldShape LastFieldShape;
            public int LastVfxDefinitionId;

            public CharacterTestInteractionVfxSummary ToSummary()
            {
                return new CharacterTestInteractionVfxSummary(
                    Frame,
                    ProcessedInteractionCount,
                    VfxSpawnedCount,
                    SkippedCount,
                    LastStableId,
                    LastForceMode,
                    LastFieldShape,
                    LastVfxDefinitionId);
            }
        }
    }

    public readonly struct CharacterTestInteractionVfxSummary
    {
        public static readonly CharacterTestInteractionVfxSummary Empty =
            new CharacterTestInteractionVfxSummary(
                RuntimeFrame.Zero,
                0,
                0,
                0,
                0,
                0,
                0,
                0);

        public CharacterTestInteractionVfxSummary(
            RuntimeFrame frame,
            int processedInteractionCount,
            int vfxSpawnedCount,
            int skippedCount,
            int lastStableId,
            CharacterTestInteractionForceMode lastForceMode,
            CharacterTestInteractionFieldShape lastFieldShape,
            int lastVfxDefinitionId)
        {
            Frame = frame;
            ProcessedInteractionCount = processedInteractionCount < 0 ? 0 : processedInteractionCount;
            VfxSpawnedCount = vfxSpawnedCount < 0 ? 0 : vfxSpawnedCount;
            SkippedCount = skippedCount < 0 ? 0 : skippedCount;
            LastStableId = lastStableId;
            LastForceMode = lastForceMode;
            LastFieldShape = lastFieldShape;
            LastVfxDefinitionId = lastVfxDefinitionId < 0 ? 0 : lastVfxDefinitionId;
        }

        public RuntimeFrame Frame { get; }
        public int ProcessedInteractionCount { get; }
        public int VfxSpawnedCount { get; }
        public int SkippedCount { get; }
        public int LastStableId { get; }
        public CharacterTestInteractionForceMode LastForceMode { get; }
        public CharacterTestInteractionFieldShape LastFieldShape { get; }
        public int LastVfxDefinitionId { get; }

        public string ToSummaryLine()
        {
            return "frame=" + Frame.Value.ToString(CultureInfo.InvariantCulture)
                + " interactions=" + ProcessedInteractionCount.ToString(CultureInfo.InvariantCulture)
                + " vfx=" + VfxSpawnedCount.ToString(CultureInfo.InvariantCulture)
                + " skipped=" + SkippedCount.ToString(CultureInfo.InvariantCulture)
                + " lastMode=" + ((int)LastForceMode == 0 ? "none" : LastForceMode.ToString())
                + " lastShape=" + ((int)LastFieldShape == 0 ? "none" : LastFieldShape.ToString())
                + " lastVfx=" + (LastVfxDefinitionId > 0
                    ? LastVfxDefinitionId.ToString(CultureInfo.InvariantCulture)
                    : "none")
                + " stable=" + (LastStableId == 0 ? "none" : LastStableId.ToString(CultureInfo.InvariantCulture));
        }
    }
}


