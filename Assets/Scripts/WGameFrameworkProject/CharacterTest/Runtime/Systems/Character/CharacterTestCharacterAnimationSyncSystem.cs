using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterControlPresentationAnimationDefinition
    {
        public CharacterControlPresentationAnimationDefinition(
            CharacterControlPresentationKind kind,
            string actionKey,
            float transitionSeconds,
            float holdSeconds)
        {
            Kind = kind;
            ActionKey = actionKey ?? string.Empty;
            TransitionSeconds = transitionSeconds < 0f ? 0f : transitionSeconds;
            HoldSeconds = holdSeconds < 0f ? 0f : holdSeconds;
        }

        public CharacterControlPresentationKind Kind { get; }
        public string ActionKey { get; }
        public float TransitionSeconds { get; }
        public float HoldSeconds { get; }
        public bool HasActionKey => !string.IsNullOrEmpty(ActionKey);

        public CharacterTestCharacterAnimationComponent CreatePresentationAnimation(bool emitActionKey)
        {
            if (!HasActionKey)
                return CharacterTestCharacterAnimationComponent.IdleGrounded;

            return new CharacterTestCharacterAnimationComponent(
                Vector2.zero,
                0f,
                grounded: true,
                airborne: false,
                actionActive: true,
                emitActionKey ? ActionKey : string.Empty,
                TransitionSeconds,
                HoldSeconds);
        }
    }

    internal sealed class CharacterControlPresentationAnimationRegistry
    {
        private static readonly CharacterControlPresentationAnimationDefinition[] DefaultDefinitions =
        {
            new CharacterControlPresentationAnimationDefinition(
                CharacterControlPresentationKind.Stunned,
                string.Empty,
                transitionSeconds: 0.08f,
                holdSeconds: 0.1f),
            new CharacterControlPresentationAnimationDefinition(
                CharacterControlPresentationKind.Paralyzed,
                string.Empty,
                transitionSeconds: 0.08f,
                holdSeconds: 0.1f),
            new CharacterControlPresentationAnimationDefinition(
                CharacterControlPresentationKind.Knockdown,
                string.Empty,
                transitionSeconds: 0.08f,
                holdSeconds: 0.1f),
            new CharacterControlPresentationAnimationDefinition(
                CharacterControlPresentationKind.Knockup,
                string.Empty,
                transitionSeconds: 0.08f,
                holdSeconds: 0.1f),
        };

        public static readonly CharacterControlPresentationAnimationRegistry Default =
            new CharacterControlPresentationAnimationRegistry(DefaultDefinitions);

        private readonly CharacterControlPresentationAnimationDefinition[] _definitions;

        public CharacterControlPresentationAnimationRegistry(
            params CharacterControlPresentationAnimationDefinition[] definitions)
        {
            _definitions = definitions ?? Array.Empty<CharacterControlPresentationAnimationDefinition>();
        }

        public bool TryGet(
            CharacterControlPresentationKind kind,
            out CharacterControlPresentationAnimationDefinition definition)
        {
            for (int i = 0; i < _definitions.Length; i++)
            {
                CharacterControlPresentationAnimationDefinition candidate = _definitions[i];
                if (candidate.Kind == kind)
                {
                    definition = candidate;
                    return true;
                }
            }

            definition = default;
            return false;
        }
    }

    internal sealed class CharacterTestCharacterAnimationSyncSystem
    {
        private readonly CharacterTestCharacterUnityViewBridge _viewBridge;
        private readonly CharacterControlPresentationAnimationRegistry _controlAnimationRegistry;
        private readonly Dictionary<int, ActiveControlPresentationAnimation> _activeControlAnimations =
            new Dictionary<int, ActiveControlPresentationAnimation>();
        private readonly HashSet<int> _presentEntityIds = new HashSet<int>();
        private readonly List<int> _removeEntityIds = new List<int>();

        public CharacterTestCharacterAnimationSyncSystem(
            CharacterTestCharacterUnityViewBridge viewBridge,
            CharacterControlPresentationAnimationRegistry controlAnimationRegistry = null)
        {
            _viewBridge = viewBridge;
            _controlAnimationRegistry = controlAnimationRegistry
                ?? CharacterControlPresentationAnimationRegistry.Default;
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(
            CharacterTestCharacterRuntimeWorld world,
            RuntimeFrame frame = default,
            ICharacterTestStatusControlProvider statusControl = null,
            CharacterTestCharacterActionRuntime actionRuntime = null)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            ICharacterTestControlPresentationProvider presentationProvider =
                statusControl as ICharacterTestControlPresentationProvider;
            _presentEntityIds.Clear();
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                CharacterTestCharacterAnimationComponent animation =
                    ResolvePresentationAnimation(
                        entity,
                        statusControl,
                        presentationProvider,
                        out bool presentationPaused);
                if (actionRuntime != null && actionRuntime.Registry.TryGetAction(entity.Action.ActiveActionId, out CharacterActionConfig action))
                    animation = ResolveOrdinaryGuardRecoveryPresentation(action, entity.Action.LocalFrame, animation);
                _viewBridge.SyncAnimation(
                    entity,
                    animation,
                    frame,
                    presentationPaused);
                _viewBridge.SyncActionPlaybackTime(entity.Id,
                    ResolveActionPlaybackTime(entity, animation, actionRuntime, frame));
            }

            PruneInactiveControlAnimations();
        }

        internal static double ResolveActionPlaybackTime(CharacterTestCharacterEntity entity,
            CharacterTestCharacterAnimationComponent animation, CharacterTestCharacterActionRuntime actionRuntime,
            RuntimeFrame frame)
        {
            if (entity == null || !animation.ActionActive || actionRuntime == null
                || !actionRuntime.Registry.TryGetAction(entity.Action.ActiveActionId, out CharacterActionConfig action))
                return -1d;
            AnimationTrackEvent[] events = action.AnimationTrack?.Events ?? Array.Empty<AnimationTrackEvent>();
            bool deathPlayback = entity.CombatState.DeathFrame >= 0
                && (entity.CombatState.IsDying || entity.CombatState.IsDead);
            long playbackFrame = deathPlayback
                ? Math.Max(entity.Action.LocalFrame, frame.Value - entity.CombatState.DeathFrame)
                : entity.Action.LocalFrame;
            for (int i = events.Length - 1; i >= 0; i--)
            {
                AnimationTrackEvent evt = events[i];
                long sampleFrame = playbackFrame;
                if (sampleFrame < evt.Frame || (!deathPlayback && sampleFrame > evt.EndFrame)
                    || string.IsNullOrEmpty(evt.EffectiveAnimationKey))
                    continue;
                if (deathPlayback && !string.Equals(entity.Action.LastAnimationActionKey,
                        evt.EffectiveAnimationKey, StringComparison.Ordinal))
                    continue;
                if (!string.IsNullOrEmpty(animation.ActionKey)
                    && !string.Equals(animation.ActionKey, evt.EffectiveAnimationKey, StringComparison.Ordinal))
                    return -1d;
                // The action clock keeps advancing while the active guard samples one authored pose.
                // Freezing presentation time must never freeze Blocking duration or reaction admission.
                long clipFrame = ResolveOrdinaryGuardClipFrame(action, sampleFrame);
                return (evt.AnimationStartOffsetFrames
                    + (clipFrame - evt.Frame) * (double)evt.PlaybackSpeed)
                    / CharacterTestDeterministicUnits.DefaultTickRate;
            }
            return -1d;
        }

        /// <remarks>NoAlloc. Ordinary guard enters at normal clip speed, then samples its Active-start pose.</remarks>
        internal static long ResolveOrdinaryGuardClipFrame(CharacterActionConfig action, long localFrame)
        {
            if (!IsOrdinaryGuard(action)) return localFrame;
            foreach (var phase in action.Phases)
                if (phase.Kind == CharacterActionPhaseKind.Active && localFrame >= phase.StartFrame)
                    return phase.StartFrame;
            return localFrame;
        }

        /// <remarks>NoAlloc. Recovery removes action suppression and uses the existing locomotion blend transition.</remarks>
        internal static CharacterTestCharacterAnimationComponent ResolveOrdinaryGuardRecoveryPresentation(
            CharacterActionConfig action, int localFrame, CharacterTestCharacterAnimationComponent animation)
        {
            if (IsOrdinaryGuardRecovery(action, localFrame))
                return new CharacterTestCharacterAnimationComponent(animation.LocomotionBlend,
                    animation.LocomotionSpeed01, animation.Grounded, animation.Airborne);
            return animation;
        }

        internal static bool IsOrdinaryGuardRecovery(CharacterActionConfig action, int localFrame)
        {
            if (!IsOrdinaryGuard(action)) return false;
            foreach (var phase in action.Phases)
                if (phase.Kind == CharacterActionPhaseKind.Recovery && phase.Contains(localFrame))
                    return true;
            return false;
        }

        private static bool IsOrdinaryGuard(CharacterActionConfig action)
            => action != null && action.Category == CharacterActionCategory.Guard
                && Array.IndexOf(action.Tags, "ordinary-guard") >= 0;

        internal CharacterTestCharacterAnimationComponent ResolvePresentationAnimationForValidation(
            CharacterTestCharacterEntity entity,
            ICharacterTestStatusControlProvider statusControl,
            out bool presentationPaused)
        {
            return ResolvePresentationAnimation(
                entity,
                statusControl,
                statusControl as ICharacterTestControlPresentationProvider,
                out presentationPaused);
        }

        private CharacterTestCharacterAnimationComponent ResolvePresentationAnimation(
            CharacterTestCharacterEntity entity,
            ICharacterTestStatusControlProvider statusControl,
            ICharacterTestControlPresentationProvider presentationProvider,
            out bool presentationPaused)
        {
            presentationPaused = statusControl != null
                && statusControl.ShouldPauseAnimationPresentation(entity);
            if (entity == null)
                return CharacterTestCharacterAnimationComponent.IdleGrounded;

            if (entity.BattleState.LifeState != CharacterTestCharacterLifeState.Alive
                && entity.Animation.ActionActive)
            {
                ClearControlAnimation(entity.Id.Value);
                presentationPaused = false;
                return entity.Animation;
            }

            if (presentationProvider == null
                || !presentationProvider.TryGetControlPresentationState(
                    entity,
                    out CharacterControlPresentationState state))
            {
                ClearControlAnimation(entity.Id.Value);
                return entity.Animation;
            }

            presentationPaused = state.ShouldPauseAnimationPresentation;
            if (presentationPaused)
            {
                ClearControlAnimation(entity.Id.Value);
                return entity.Animation;
            }

            if (!string.IsNullOrWhiteSpace(entity.Animation.ActionKey))
            {
                ClearControlAnimation(entity.Id.Value);
                return entity.Animation;
            }

            if (!_controlAnimationRegistry.TryGet(
                state.PrimaryKind,
                out CharacterControlPresentationAnimationDefinition definition))
            {
                ClearControlAnimation(entity.Id.Value);
                return entity.Animation;
            }

            int entityId = entity.Id.Value;
            _presentEntityIds.Add(entityId);
            bool emitActionKey = ShouldEmitControlActionKey(entityId, definition);
            _activeControlAnimations[entityId] = new ActiveControlPresentationAnimation(
                definition.Kind,
                definition.ActionKey);
            return definition.CreatePresentationAnimation(emitActionKey);
        }

        private bool ShouldEmitControlActionKey(
            int entityId,
            CharacterControlPresentationAnimationDefinition definition)
        {
            if (!definition.HasActionKey)
                return false;
            if (!_activeControlAnimations.TryGetValue(
                entityId,
                out ActiveControlPresentationAnimation active))
            {
                return true;
            }

            return active.Kind != definition.Kind
                || !string.Equals(active.ActionKey, definition.ActionKey, StringComparison.Ordinal);
        }

        private void ClearControlAnimation(int entityId)
        {
            _activeControlAnimations.Remove(entityId);
        }

        private void PruneInactiveControlAnimations()
        {
            _removeEntityIds.Clear();
            foreach (KeyValuePair<int, ActiveControlPresentationAnimation> item in _activeControlAnimations)
            {
                if (!_presentEntityIds.Contains(item.Key))
                    _removeEntityIds.Add(item.Key);
            }

            for (int i = 0; i < _removeEntityIds.Count; i++)
                _activeControlAnimations.Remove(_removeEntityIds[i]);
            _removeEntityIds.Clear();
        }

        private readonly struct ActiveControlPresentationAnimation
        {
            public ActiveControlPresentationAnimation(
                CharacterControlPresentationKind kind,
                string actionKey)
            {
                Kind = kind;
                ActionKey = actionKey ?? string.Empty;
            }

            public CharacterControlPresentationKind Kind { get; }
            public string ActionKey { get; }
        }
    }
}
