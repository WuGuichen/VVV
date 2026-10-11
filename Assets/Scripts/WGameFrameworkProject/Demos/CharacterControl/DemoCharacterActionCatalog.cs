using System;
using System.Globalization;
using MxFramework.CharacterAction;
using MxFramework.CharacterControl;
using MxFramework.Combat.Animation;
using MxFramework.Runtime;

namespace MxFramework.Demo.CharacterControl
{
    internal static class DemoCharacterActionCatalog
    {
        public const int AttackActionId = 810001;
        public const int ReturnToMovementActionId = 810002;
        public const int AbilityActionId = 810003;
        public const int CohesionReactionActionId = 810004;
        public const int GuardReactionActionId = 810005;

        private const string ReactionProfileId = "demo.character_action.reactions";

        public static DemoCharacterActionRuntime Create(
            int combatActionId,
            CombatActionRegistry combatRegistry,
            RuntimeCommandBuffer gameplayCommands = null,
            int gameplayAbilityId = 0)
        {
            if (combatActionId <= 0)
                throw new ArgumentOutOfRangeException(nameof(combatActionId));

            var combatBridge = new CharacterActionCombatTrackBridge(combatRegistry);
            CharacterActionGameplayTrackBridge gameplayBridge = gameplayCommands != null
                ? new CharacterActionGameplayTrackBridge(gameplayCommands)
                : null;
            var trackAdapter = new CharacterActionTrackAdapter(
                combatSink: combatBridge,
                gameplaySink: gameplayBridge);
            CharacterActionConfig[] actions = gameplayAbilityId > 0
                ? new[]
                {
                    CreateAttack(combatActionId),
                    CreateReturnToMovement(),
                    CreateAbility(gameplayAbilityId),
                    CreateReaction(CohesionReactionActionId, "Cohesion Reaction"),
                    CreateReaction(GuardReactionActionId, "Guard Reaction")
                }
                : new[]
                {
                    CreateAttack(combatActionId),
                    CreateReturnToMovement(),
                    CreateReaction(CohesionReactionActionId, "Cohesion Reaction"),
                    CreateReaction(GuardReactionActionId, "Guard Reaction")
                };
            CharacterAbilityActionBinding[] abilityBindings = gameplayAbilityId > 0
                ? new[] { new CharacterAbilityActionBinding(gameplayAbilityId, new CharacterActionId(AbilityActionId)) }
                : Array.Empty<CharacterAbilityActionBinding>();
            var actionSet = new CharacterActionSetConfig(
                id: 810000,
                stableId: "demo.character_action.set",
                displayName: "Demo CharacterAction Set",
                characterStableId: string.Empty,
                equipmentStateStableId: string.Empty,
                commandBindings: new[]
                {
                    new CharacterActionBinding(CharacterActionRequest.AttackIntentId, new CharacterActionId(AttackActionId)),
                    new CharacterActionBinding(CharacterActionRequest.InteractIntentId, new CharacterActionId(ReturnToMovementActionId))
                },
                abilityBindings,
                reactionBindings: new[]
                {
                    new CharacterReactionBinding(ReactionProfileId, new CharacterActionId(CohesionReactionActionId))
                },
                movementProfileId: 0,
                reactionProfileId: ReactionProfileId,
                defaultActionId: new CharacterActionId(ReturnToMovementActionId));
            var reactionProfile = new CharacterReactionProfile(
                ReactionProfileId,
                new[]
                {
                    new CharacterReactionRule(
                        new CharacterActionId(CohesionReactionActionId),
                        CharacterReactionRuleTrigger.CohesionBreak,
                        priority: 20),
                    new CharacterReactionRule(
                        new CharacterActionId(GuardReactionActionId),
                        CharacterReactionRuleTrigger.GuardBreak,
                        priority: 30)
                },
                new CharacterActionId(CohesionReactionActionId));
            var options = new CharacterActionControllerOptions(
                actionSet,
                actions,
                reactionProfiles: new[] { reactionProfile },
                trackAdapter: trackAdapter,
                downstreamLifecycleSink: combatBridge);
            return new DemoCharacterActionRuntime(options, combatBridge, gameplayBridge);
        }

        private static CharacterActionConfig CreateAttack(int combatActionId)
        {
            return new CharacterActionConfig(
                new CharacterActionId(AttackActionId),
                "Light Attack",
                CharacterActionCategory.BasicAttack,
                CharacterActionTimelineAuthority.CharacterAuthored,
                tags: null,
                priority: 10,
                durationFrames: 4,
                requirements: null,
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 0),
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 1, 1),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 2, 3)
                },
                cancelRules: new[]
                {
                    new CharacterCancelRule(2, 3),
                    new CharacterCancelRule(2, 3)
                },
                interruptRules: new[]
                {
                    new CharacterInterruptRule(CharacterActionSourceKind.CohesionBreak, targetActionId: new CharacterActionId(CohesionReactionActionId)),
                    new CharacterInterruptRule(CharacterActionSourceKind.GuardBreak, targetActionId: new CharacterActionId(GuardReactionActionId))
                },
                combatTrack: new CombatTrackConfig(
                    combatActionId,
                    new[]
                    {
                        new CombatTrackEvent(
                            0,
                            CharacterActionTrackEventKind.StartCombatAction,
                            combatActionId,
                            stableEventId: 810101)
                    }));
        }

        private static CharacterActionConfig CreateReturnToMovement()
        {
            return new CharacterActionConfig(
                new CharacterActionId(ReturnToMovementActionId),
                "Return To Movement",
                CharacterActionCategory.Movement,
                CharacterActionTimelineAuthority.CharacterAuthored,
                tags: null,
                priority: 0,
                durationFrames: 1,
                requirements: null,
                phases: new[] { new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 0, 0) },
                cancelRules: null,
                interruptRules: null);
        }

        private static CharacterActionConfig CreateAbility(int gameplayAbilityId)
        {
            return new CharacterActionConfig(
                new CharacterActionId(AbilityActionId),
                "Demo Ability",
                CharacterActionCategory.Skill,
                CharacterActionTimelineAuthority.CharacterAuthored,
                tags: null,
                priority: 20,
                durationFrames: 2,
                requirements: null,
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 0, 0),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 1, 1)
                },
                cancelRules: null,
                interruptRules: null,
                gameplayTrack: new GameplayTrackConfig(new[]
                {
                    new GameplayTrackEvent(
                        0,
                        CharacterActionTrackEventKind.CastAbility,
                        requestId: gameplayAbilityId,
                        abilityStableId: gameplayAbilityId,
                        stableEventId: 810102)
                }));
        }

        private static CharacterActionConfig CreateReaction(int actionId, string displayName)
        {
            return new CharacterActionConfig(
                new CharacterActionId(actionId),
                displayName,
                CharacterActionCategory.Reaction,
                CharacterActionTimelineAuthority.CharacterAuthored,
                tags: null,
                priority: 100,
                durationFrames: 4,
                requirements: null,
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 0),
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 1, 2),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 3, 3)
                },
                cancelRules: null,
                interruptRules: null);
        }
    }

    internal sealed class DemoCharacterActionRuntime : IDisposable
    {
        public DemoCharacterActionRuntime(
            CharacterActionControllerOptions options,
            CharacterActionCombatTrackBridge combatBridge,
            CharacterActionGameplayTrackBridge gameplayBridge)
        {
            Options = options;
            CombatBridge = combatBridge;
            GameplayBridge = gameplayBridge;
        }

        public CharacterActionControllerOptions Options { get; }
        public CharacterActionCombatTrackBridge CombatBridge { get; }
        public CharacterActionGameplayTrackBridge GameplayBridge { get; }

        public void Dispose()
        {
            CombatBridge.Dispose();
        }
    }
}
