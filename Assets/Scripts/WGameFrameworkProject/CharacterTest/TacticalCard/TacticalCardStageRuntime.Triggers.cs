using System;
using MxFramework.Config.Levels;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.Config;
using MxFramework.Gameplay.Triggers.StoryBridge;
using MxFramework.Runtime;
using MxFramework.Story.Runtime;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed partial class TacticalCardStageRuntime
    {
        private TacticalStageEndTriggerBridge _end;
        private GameplayTriggerReliableOutboxModule _outbox;
        private GameplaySystemPipeline _triggerPipeline;
        private readonly GameplayWorld _triggerWorld = new GameplayWorld();
        private readonly GameplayCommandExecutionState _triggerCommandState = new GameplayCommandExecutionState();
        internal GameplayTriggerRuntime TriggerRuntime { get; private set; }
        internal TacticalStageEndTriggerBridge EndTriggers => _end;

        // Cold setup uses the completed Spawn consumer's actual generation, never guessed entity IDs.
        private void BindOutcomeTriggers()
        {
            var published = new CharacterTestLevelGameplayTriggerAuthoringDocument();
            published.AddTacticalBattleOutcomeTemplates();
            var document = CharacterTestLevelGameplayTriggerAuthoringAdapter.BindBattleRosterOwner(published, _player.GameplayEntityId);
            var report = CharacterTestLevelGameplayTriggerAuthoringAdapter.ValidateAndCompile(document,
                new LevelWorldObjectAuthoringDocument(), out var compiled);
            if (report.HasErrors || compiled?.Succeeded != true)
                throw new InvalidOperationException("Tactical battle Trigger compilation failed.");
            var plan = compiled.CompiledCatalog;
            if (!CharacterTestGameplayTriggerBattleVitalsCatalog.TryCreate(plan, out var catalog, out string issue))
                throw new InvalidOperationException(issue);
            var actual = new[]
            {
                GameplayTriggerActionBootstrapBinding.FromSink(new GameplayTriggerStoryReliableCommandActionSink(GameplayTriggerStoryCommandActionKinds.RaiseTrigger)),
                GameplayTriggerActionBootstrapBinding.FromInternal(new GameplayTriggerInternalDeferredActionDescriptor(
                    new GameplayTriggerActionKindId(CharacterTestLevelGameplayTriggerAuthoringDocument.TacticalSetBindingEnabledActionId), GameplayTriggerInternalDeferredOperationKind.SetEnabled))
            };
            var bootstrap = GameplayTriggerCatalogBootstrap.TryCreateCatalog(plan, actual,
                new GameplayTriggerRuntimeCapacityProfile(2, 4, 4, 4, 4, 4, 4, 4, 12, 12, 8, 8, 4));
            if (!bootstrap.Succeeded) throw new InvalidOperationException("Tactical battle Trigger bootstrap failed.");
            var events = new GameplayTriggerRuntimeEventSource(64);
            TriggerRuntime = new GameplayTriggerRuntime(bootstrap.Catalog, bootstrap.RuntimeOptions.Value, events);
            _end = new TacticalStageEndTriggerBridge(TriggerRuntime, catalog, _modules.CharacterRuntime.World.GameplayComponentWorld,
                Stage, 77, new GameplayTriggerStoryCommandResultRelay(events));
            var sources = new RuntimeCommandSourceRegistry(1);
            if (!sources.TryRegister(StoryRuntimeCommandSources.GameplayBridge, RuntimeCommandSourceKind.Derived,
                    new[] { StoryRuntimeCommandIds.RaiseTrigger }, out _)) throw new InvalidOperationException("Story source registration failed.");
            var transfer = new GameplayTriggerStoryReliableCommandTransfer(GameplayTriggerStoryCommandActionKinds.RaiseTrigger,
                StoryRuntimeCommandSources.GameplayBridge, _modules.Host, _modules.StoryModule,
                new RuntimeCommandSubmissionGate(sources, _modules.StoryModule.CommandBuffer, RuntimeCommandIngressKind.TrustedDerived));
            _outbox = new GameplayTriggerReliableOutboxModule(TriggerRuntime, new IGameplayTriggerReliableTargetTransfer[] { transfer });
            _outbox.Initialize(_hostContext);
            _triggerPipeline = new GameplaySystemPipeline();
            _triggerPipeline.Add(new GameplayTriggerDeferredSystem(TriggerRuntime));
            _triggerPipeline.Add(_end.Vitals); _triggerPipeline.Add(_end.Evaluation);
            _triggerPipeline.Add(new GameplayTriggerEventRouterSystem(events));
        }
    }
}
