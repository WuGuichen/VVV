using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Immutable warmed-session replay floor captured after Lab initialization.
    /// </summary>
    /// <remarks>LowFreqAlloc. This is a local Lab rollback origin, not network state.</remarks>
    internal sealed class RuntimeAiLabInitialReplayBaseline
    {
        public RuntimeAiLabInitialReplayBaseline(
            RuntimeFrame frame,
            double elapsedSeconds,
            CharacterTestAuthorityRollbackCheckpoint authority,
            RuntimeAiLabPresentationCheckpoint presentation,
            RuntimeCommandBufferSnapshot commandBuffer,
            RuntimeCommandBufferSnapshot? storyCommandBuffer,
            RuntimeCommandBufferSnapshot? vfxCommandBuffer,
            RuntimeAiLabGameplayTransientState gameplayTransientState,
            CharacterTestCharacterCombatTraceSystem.CharacterTestCombatTraceReplayState combatTrace,
            long authorityHash,
            int configurationRevision)
        {
            Frame = frame;
            ElapsedSeconds = elapsedSeconds;
            Authority = authority;
            Presentation = presentation;
            CommandBuffer = commandBuffer;
            StoryCommandBuffer = storyCommandBuffer;
            VfxCommandBuffer = vfxCommandBuffer;
            GameplayTransientState = gameplayTransientState;
            CombatTrace = combatTrace;
            AuthorityHash = authorityHash;
            ConfigurationRevision = configurationRevision;
        }

        public RuntimeFrame Frame { get; }
        public double ElapsedSeconds { get; }
        public CharacterTestAuthorityRollbackCheckpoint Authority { get; }
        public RuntimeAiLabPresentationCheckpoint Presentation { get; }
        public RuntimeCommandBufferSnapshot CommandBuffer { get; }
        public RuntimeCommandBufferSnapshot? StoryCommandBuffer { get; }
        public RuntimeCommandBufferSnapshot? VfxCommandBuffer { get; }
        public RuntimeAiLabGameplayTransientState GameplayTransientState { get; }
        public CharacterTestCharacterCombatTraceSystem.CharacterTestCombatTraceReplayState CombatTrace { get; }
        public long AuthorityHash { get; }
        public int ConfigurationRevision { get; }
        public bool Available => Authority != null
            && Presentation != null
            && Presentation.Animation != null
            && Presentation.Animation.FrameAligned
            && GameplayTransientState != null;
    }

    /// <summary>
    /// Lab-only gameplay state omitted by the network rollback document but required by deterministic replay.
    /// </summary>
    /// <remarks>LowFreqAlloc. Captured and restored only at the Warm Replay baseline.</remarks>
    internal sealed class RuntimeAiLabGameplayTransientState
    {
        private readonly GameplayComponentSnapshot<GameplayAttributeSetComponent>[] _attributes;
        private readonly GameplayComponentSnapshot<GameplayComponentModifierSetComponent>[] _modifiers;
        private readonly GameplayComponentSnapshot<GameplayComponentBuffSetComponent>[] _buffs;
        private readonly GameplayComponentSnapshot<GameplayComponentAuraSourceSetComponent>[] _auraSources;
        private readonly GameplayComponentSnapshot<GameplayStatusComponent>[] _statuses;
        private readonly GameplayComponentSnapshot<GameplayGuardPressureComponent>[] _guardPressure;
        private readonly GameplayComponentSnapshot<GameplayScatterPressureComponent>[] _scatterPressure;
        private readonly GameplayComponentSnapshot<GameplayAbilityCooldownComponent>[] _abilityCooldowns;
        private readonly GameplayComponentSnapshot<GameplayArmorIntegrityComponent>[] _armorIntegrity;

        private RuntimeAiLabGameplayTransientState(
            GameplayComponentSnapshot<GameplayAttributeSetComponent>[] attributes,
            GameplayComponentSnapshot<GameplayComponentModifierSetComponent>[] modifiers,
            GameplayComponentSnapshot<GameplayComponentBuffSetComponent>[] buffs,
            GameplayComponentSnapshot<GameplayComponentAuraSourceSetComponent>[] auraSources,
            GameplayComponentSnapshot<GameplayStatusComponent>[] statuses,
            GameplayComponentSnapshot<GameplayGuardPressureComponent>[] guardPressure,
            GameplayComponentSnapshot<GameplayScatterPressureComponent>[] scatterPressure,
            GameplayComponentSnapshot<GameplayAbilityCooldownComponent>[] abilityCooldowns,
            GameplayComponentSnapshot<GameplayArmorIntegrityComponent>[] armorIntegrity)
        {
            _attributes = attributes
                ?? Array.Empty<GameplayComponentSnapshot<GameplayAttributeSetComponent>>();
            _modifiers = modifiers
                ?? Array.Empty<GameplayComponentSnapshot<GameplayComponentModifierSetComponent>>();
            _buffs = buffs
                ?? Array.Empty<GameplayComponentSnapshot<GameplayComponentBuffSetComponent>>();
            _auraSources = auraSources
                ?? Array.Empty<GameplayComponentSnapshot<GameplayComponentAuraSourceSetComponent>>();
            _statuses = statuses
                ?? Array.Empty<GameplayComponentSnapshot<GameplayStatusComponent>>();
            _guardPressure = guardPressure
                ?? Array.Empty<GameplayComponentSnapshot<GameplayGuardPressureComponent>>();
            _scatterPressure = scatterPressure
                ?? Array.Empty<GameplayComponentSnapshot<GameplayScatterPressureComponent>>();
            _abilityCooldowns = abilityCooldowns
                ?? Array.Empty<GameplayComponentSnapshot<GameplayAbilityCooldownComponent>>();
            _armorIntegrity = armorIntegrity
                ?? Array.Empty<GameplayComponentSnapshot<GameplayArmorIntegrityComponent>>();
        }

        public static RuntimeAiLabGameplayTransientState Capture(GameplayComponentWorld world)
        {
            return new RuntimeAiLabGameplayTransientState(
                CaptureStore<GameplayAttributeSetComponent>(world),
                CaptureStore<GameplayComponentModifierSetComponent>(world),
                CaptureStore<GameplayComponentBuffSetComponent>(world),
                CaptureStore<GameplayComponentAuraSourceSetComponent>(world),
                CaptureStore<GameplayStatusComponent>(world),
                CaptureStore<GameplayGuardPressureComponent>(world),
                CaptureStore<GameplayScatterPressureComponent>(world),
                CaptureStore<GameplayAbilityCooldownComponent>(world),
                CaptureStore<GameplayArmorIntegrityComponent>(world));
        }

        public void Restore(GameplayComponentWorld world)
        {
            if (world == null)
                return;

            RestoreStore(world, _attributes);
            RestoreStore(world, _modifiers);
            RestoreStore(world, _buffs);
            RestoreStore(world, _auraSources);
            RestoreStore(world, _statuses);
            RestoreStore(world, _guardPressure);
            RestoreStore(world, _scatterPressure);
            RestoreStore(world, _abilityCooldowns);
            RestoreStore(world, _armorIntegrity);
        }

        private static GameplayComponentSnapshot<T>[] CaptureStore<T>(GameplayComponentWorld world)
            where T : struct, IGameplayComponent
        {
            return world != null && world.TryGetStore(out GameplayComponentStore<T> store)
                ? store.CreateSnapshot()
                : Array.Empty<GameplayComponentSnapshot<T>>();
        }

        private static void RestoreStore<T>(
            GameplayComponentWorld world,
            GameplayComponentSnapshot<T>[] entries)
            where T : struct, IGameplayComponent
        {
            if (!world.TryGetStore(out GameplayComponentStore<T> store))
            {
                if (entries == null || entries.Length == 0)
                    return;
                store = world.GetOrCreateStore<T>();
            }

            store.Clear();
            if (entries == null)
                return;
            for (int i = 0; i < entries.Length; i++)
            {
                GameplayComponentSnapshot<T> entry = entries[i];
                store.Set(entry.EntityId, entry.Component);
            }
        }
    }

    /// <summary>
    /// Optional full test checkpoint retained outside the Runtime AI Lab command timeline.
    /// </summary>
    /// <remarks>LowFreqAlloc. Captured only while the explicit Lab state-capture toggle is enabled.</remarks>
    internal sealed class RuntimeAiLabTestStateCapture
    {
        public RuntimeAiLabTestStateCapture(
            RuntimeFrame frame,
            double elapsedSeconds,
            CharacterTestAuthorityRollbackCheckpoint authority,
            RuntimeAiLabPresentationCheckpoint presentation,
            CharacterTestRuntimeAiLabPresentationDiagnosticSample presentationDiagnostics)
        {
            Frame = frame;
            ElapsedSeconds = elapsedSeconds;
            Authority = authority;
            Presentation = presentation;
            PresentationDiagnostics = presentationDiagnostics;
        }

        public RuntimeFrame Frame { get; }
        public double ElapsedSeconds { get; }
        public CharacterTestAuthorityRollbackCheckpoint Authority { get; }
        public RuntimeAiLabPresentationCheckpoint Presentation { get; }
        public CharacterTestRuntimeAiLabPresentationDiagnosticSample PresentationDiagnostics { get; }
    }

    internal sealed class RuntimeAiLabPresentationCheckpoint
    {
        public RuntimeAiLabPresentationCheckpoint(
            RuntimeFrame frame,
            CharacterTestAnimationPresentationCheckpoint animation,
            RuntimeSaveState vfxState,
            string diagnostic)
        {
            Frame = frame;
            Animation = animation;
            VfxState = vfxState;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public RuntimeFrame Frame { get; }
        public CharacterTestAnimationPresentationCheckpoint Animation { get; }
        public RuntimeSaveState VfxState { get; }
        public string Diagnostic { get; }
        public bool Available => Animation != null && Animation.FrameAligned && VfxState != null;
    }

    internal readonly struct RuntimeAiLabDeterministicReplayResult
    {
        public RuntimeAiLabDeterministicReplayResult(
            RuntimeFrame frame,
            CharacterTestRuntimeAiLabFrameSyncReport authority,
            CharacterTestRuntimeAiLabFrameSyncReport presentation)
        {
            Frame = frame;
            Authority = authority;
            Presentation = presentation;
        }

        public RuntimeFrame Frame { get; }
        public CharacterTestRuntimeAiLabFrameSyncReport Authority { get; }
        public CharacterTestRuntimeAiLabFrameSyncReport Presentation { get; }
    }

    internal sealed class RuntimeAiLabTestStateCaptureStore
    {
        private const int DefaultMaximumCaptureCount = 600;
        private readonly Dictionary<int, RuntimeAiLabTestStateCapture> _captures =
            new Dictionary<int, RuntimeAiLabTestStateCapture>();

        public bool Enabled { get; private set; }
        public bool CapacityReached { get; private set; }
        public int Count => _captures.Count;

        public void Clear()
        {
            _captures.Clear();
            Enabled = false;
            CapacityReached = false;
        }

        public bool Toggle()
        {
            Enabled = !Enabled;
            return Enabled;
        }

        public bool TryAdd(RuntimeAiLabTestStateCapture capture)
        {
            if (!Enabled || capture == null)
                return false;
            int frame = (int)capture.Frame.Value;
            if (!_captures.ContainsKey(frame) && _captures.Count >= DefaultMaximumCaptureCount)
            {
                Enabled = false;
                CapacityReached = true;
                return false;
            }

            _captures[frame] = capture;
            return true;
        }

        public bool TryGet(int frame, out RuntimeAiLabTestStateCapture capture)
        {
            return _captures.TryGetValue(frame, out capture);
        }

        internal RecoveryState CaptureRecoveryState()
        {
            var captures = new Dictionary<int, RuntimeAiLabTestStateCapture>(_captures);
            return new RecoveryState(Enabled, CapacityReached, captures, ComputeRecoveryHash());
        }

        internal void RestoreRecoveryState(RecoveryState state)
        {
            _captures.Clear();
            if (state != null)
            {
                foreach (KeyValuePair<int, RuntimeAiLabTestStateCapture> pair in state.Captures)
                    _captures.Add(pair.Key, pair.Value);
            }
            Enabled = state != null && state.Enabled;
            CapacityReached = state != null && state.CapacityReached;
        }

        internal long ComputeRecoveryHash()
        {
            int count = _captures.Count;
            var frames = new int[count];
            _captures.Keys.CopyTo(frames, 0);
            Array.Sort(frames);
            unchecked
            {
                ulong hash = 1469598103934665603UL;
                Mix(ref hash, Enabled ? 1 : 0);
                Mix(ref hash, CapacityReached ? 1 : 0);
                Mix(ref hash, count);
                for (int i = 0; i < frames.Length; i++)
                {
                    int frame = frames[i];
                    Mix(ref hash, frame);
                    RuntimeAiLabTestStateCapture capture = _captures[frame];
                    Mix(ref hash, capture?.Authority != null ? 1L : 0L);
                    Mix(ref hash, capture != null ? capture.Frame.Value : 0L);
                    Mix(ref hash, capture != null ? capture.ElapsedSeconds : 0d);
                    Mix(ref hash, capture?.Presentation?.Frame.Value ?? 0L);
                    Mix(ref hash, capture?.PresentationDiagnostics != null ? 1L : 0L);
                }
                return (long)hash;
            }
        }

        private static void Mix(ref ulong hash, long value)
        {
            unchecked
            {
                hash ^= (ulong)value;
                hash *= 1099511628211UL;
            }
        }

        private static void Mix(ref ulong hash, double value)
        {
            Mix(ref hash, BitConverter.DoubleToInt64Bits(value));
        }

        internal sealed class RecoveryState
        {
            internal RecoveryState(
                bool enabled,
                bool capacityReached,
                Dictionary<int, RuntimeAiLabTestStateCapture> captures,
                long contentHash)
            {
                Enabled = enabled;
                CapacityReached = capacityReached;
                Captures = captures ?? new Dictionary<int, RuntimeAiLabTestStateCapture>();
                ContentHash = contentHash;
            }

            internal bool Enabled { get; }
            internal bool CapacityReached { get; }
            internal Dictionary<int, RuntimeAiLabTestStateCapture> Captures { get; }
            internal long ContentHash { get; }
        }
    }
}
