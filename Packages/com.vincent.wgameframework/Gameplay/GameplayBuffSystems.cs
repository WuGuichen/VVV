using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public sealed class GameplayBuffCommandSystem : IGameplaySystem
    {
        public const string DefaultSystemId = "mxframework.gameplay.command.buff";

        private readonly GameplayBuffDefinitionRegistry _definitions;
        private readonly GameplayBuffRequestBuffer _requests;

        public GameplayBuffCommandSystem(
            GameplayBuffDefinitionRegistry definitions,
            GameplayBuffRequestBuffer requests,
            string systemId = DefaultSystemId,
            int priority = 55)
        {
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _requests = requests ?? throw new ArgumentNullException(nameof(requests));
            SystemId = systemId ?? string.Empty;
            Priority = priority;
        }

        public string SystemId { get; }
        public GameplaySystemPhase Phase => GameplaySystemPhase.Command;
        public int Priority { get; }
        public bool IsEnabled { get; private set; } = true;

        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(GameplaySystemContext context)
        {
            IReadOnlyList<RuntimeCommand> commands = context.Commands;
            for (int i = 0; i < commands.Count; i++)
            {
                RuntimeCommand command = commands[i];
                if (command.CommandId == GameplayRuntimeCommandIds.ApplyComponentBuff)
                {
                    ExecuteApply(context, command);
                    context.CommandState.MarkHandled(command);
                    continue;
                }

                if (command.CommandId == GameplayRuntimeCommandIds.RemoveComponentBuff)
                {
                    ExecuteRemove(context, command);
                    context.CommandState.MarkHandled(command);
                }
            }
        }

        private void ExecuteApply(GameplaySystemContext context, RuntimeCommand command)
        {
            if (!TryValidateCommand(context, command, requireDefinition: true, out GameplayEntityId entityId))
                return;

            _requests.EnqueueApply(
                entityId,
                command.Payload1,
                sourceId: command.SourceId,
                traceId: command.TraceId,
                commandId: command.CommandId);
        }

        private void ExecuteRemove(GameplaySystemContext context, RuntimeCommand command)
        {
            if (command.Payload2 != 0)
            {
                EnqueueRejected(context, command, default, GameplayComponentBuffEvents.InvalidCommandPayloadReason);
                return;
            }

            if (!TryValidateCommand(context, command, requireDefinition: false, out GameplayEntityId entityId))
                return;

            _requests.EnqueueRemove(
                entityId,
                command.Payload1,
                sourceId: command.SourceId,
                traceId: command.TraceId,
                commandId: command.CommandId);
        }

        private bool TryValidateCommand(
            GameplaySystemContext context,
            RuntimeCommand command,
            bool requireDefinition,
            out GameplayEntityId entityId)
        {
            entityId = default;
            GameplayComponentWorld componentWorld = context.ComponentWorld;
            if (componentWorld == null)
            {
                EnqueueRejected(context, command, default, GameplayComponentBuffEvents.MissingComponentWorldReason);
                return false;
            }

            if (!TryReadEntity(command, out entityId))
            {
                EnqueueRejected(context, command, default, GameplayComponentBuffEvents.InvalidComponentEntityReason);
                return false;
            }

            if (!componentWorld.IsAlive(entityId))
            {
                EnqueueRejected(context, command, entityId, GameplayComponentBuffEvents.MissingComponentEntityReason);
                return false;
            }

            int buffId = command.Payload1;
            if (buffId <= 0)
            {
                EnqueueRejected(context, command, entityId, GameplayComponentBuffEvents.InvalidBuffIdReason);
                return false;
            }

            if (command.CommandId == GameplayRuntimeCommandIds.ApplyComponentBuff && command.Payload2 != 0)
            {
                EnqueueRejected(context, command, entityId, GameplayComponentBuffEvents.InvalidCommandPayloadReason);
                return false;
            }

            if (requireDefinition && !_definitions.TryGet(buffId, out _))
            {
                EnqueueRejected(context, command, entityId, GameplayComponentBuffEvents.MissingBuffDefinitionReason);
                return false;
            }

            return true;
        }

        private static bool TryReadEntity(RuntimeCommand command, out GameplayEntityId entityId)
        {
            int index = command.TargetId;
            int generation = command.Payload0;
            if (index <= 0 || generation <= 0)
            {
                entityId = default;
                return false;
            }

            entityId = new GameplayEntityId(index, generation);
            return true;
        }

        private static void EnqueueRejected(
            GameplaySystemContext context,
            RuntimeCommand command,
            GameplayEntityId entityId,
            string reason)
        {
            context.Events.Enqueue(context.Frame, new GameplayRuntimeEvent(
                context.Frame,
                GameplayRuntimeEventType.ComponentBuffRejected,
                command.CommandId,
                casterEntityId: 0,
                abilityId: 0,
                targetEntityId: entityId.Index,
                failureCode: GameplayAbilityRuntimeFailureCode.None,
                reason: reason,
                traceId: command.TraceId,
                componentEntityIndex: entityId.Index,
                componentEntityGeneration: entityId.Generation,
                buffId: command.Payload1 > 0 ? command.Payload1 : 0));
        }
    }

    public sealed class GameplayBuffApplicationSystem : IGameplaySystem
    {
        public const string DefaultSystemId = "mxframework.gameplay.buff.application";

        private readonly GameplayBuffDefinitionRegistry _definitions;
        private readonly GameplayBuffRequestBuffer _requests;
        private readonly List<GameplayBuffApplicationRequest> _drainedRequests;
        private readonly IGameplayBuffApplicationResultSink _resultSink;

        public GameplayBuffApplicationSystem(
            GameplayBuffDefinitionRegistry definitions,
            GameplayBuffRequestBuffer requests,
            string systemId = DefaultSystemId,
            int priority = 50,
            IGameplayBuffApplicationResultSink resultSink = null)
        {
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _requests = requests ?? throw new ArgumentNullException(nameof(requests));
            _drainedRequests = new List<GameplayBuffApplicationRequest>(requests.PrewarmedDrainCapacity);
            _resultSink = resultSink;
            SystemId = systemId ?? string.Empty;
            Priority = priority;
        }

        public string SystemId { get; }
        public GameplaySystemPhase Phase => GameplaySystemPhase.Resolution;
        public int Priority { get; }
        public bool IsEnabled { get; private set; } = true;

        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(GameplaySystemContext context)
        {
            _drainedRequests.Clear();
            _requests.DrainTo(_drainedRequests);
            if (_drainedRequests.Count == 0)
                return;

            GameplayComponentWorld componentWorld = context.ComponentWorld;
            if (componentWorld == null)
            {
                for (int i = 0; i < _drainedRequests.Count; i++)
                    EnqueueRejected(
                        context,
                        _drainedRequests[i],
                        GameplayComponentBuffEvents.MissingComponentWorldReason,
                        GameplayBuffApplicationResultReasonCode.MissingComponentWorld);
                _drainedRequests.Clear();
                return;
            }

            for (int i = 0; i < _drainedRequests.Count; i++)
            {
                GameplayBuffApplicationRequest request = _drainedRequests[i];
                if (!componentWorld.IsAlive(request.TargetEntityId))
                {
                    EnqueueRejected(
                        context,
                        request,
                        GameplayComponentBuffEvents.MissingComponentEntityReason,
                        GameplayBuffApplicationResultReasonCode.MissingComponentEntity);
                    continue;
                }

                if (request.Kind == GameplayBuffApplicationRequestKind.Apply)
                    Apply(context, componentWorld, request);
                else
                    Remove(context, componentWorld, request);
            }

            _drainedRequests.Clear();
        }

        private void Apply(
            GameplaySystemContext context,
            GameplayComponentWorld componentWorld,
            GameplayBuffApplicationRequest request)
        {
            if (!_definitions.TryGet(request.BuffId, out GameplayBuffDefinition definition))
            {
                EnqueueRejected(
                    context,
                    request,
                    GameplayComponentBuffEvents.MissingBuffDefinitionReason,
                    GameplayBuffApplicationResultReasonCode.MissingBuffDefinition);
                return;
            }

            if (!TryResolveEndFrame(context.Frame.Value, definition, out long endFrame))
            {
                EnqueueRejected(
                    context,
                    request,
                    GameplayComponentBuffEvents.DurationOverflowReason,
                    GameplayBuffApplicationResultReasonCode.DurationOverflow);
                return;
            }

            GameplayComponentStore<GameplayComponentBuffSetComponent> buffStore =
                componentWorld.GetOrCreateStore<GameplayComponentBuffSetComponent>();
            buffStore.TryGet(request.TargetEntityId, out GameplayComponentBuffSetComponent currentSet);
            bool hadBuff = currentSet.TryGet(request.BuffId, out GameplayComponentBuffEntry current);
            GameplayComponentBuffEntry updated = hadBuff
                ? RefreshEntry(context.Frame.Value, current, definition, endFrame, request.SourceId)
                : new GameplayComponentBuffEntry(
                    request.BuffId,
                    definition.ApplyStackCount,
                    context.Frame.Value,
                    endFrame,
                    definition.IsPermanent,
                    request.SourceId);

            buffStore.Set(request.TargetEntityId, currentSet.Upsert(updated));
            context.Events.Enqueue(context.Frame, new GameplayRuntimeEvent(
                context.Frame,
                hadBuff ? GameplayRuntimeEventType.ComponentBuffRefreshed : GameplayRuntimeEventType.ComponentBuffApplied,
                request.CommandId,
                casterEntityId: 0,
                abilityId: 0,
                targetEntityId: request.TargetEntityId.Index,
                failureCode: GameplayAbilityRuntimeFailureCode.None,
                reason: hadBuff ? GameplayComponentBuffEvents.RefreshedReason : GameplayComponentBuffEvents.AppliedReason,
                traceId: request.TraceId,
                componentEntityIndex: request.TargetEntityId.Index,
                componentEntityGeneration: request.TargetEntityId.Generation,
                buffId: request.BuffId,
                oldBuffStackCount: hadBuff ? current.StackCount : 0,
                newBuffStackCount: updated.StackCount,
                oldBuffEndFrame: hadBuff ? current.EndFrame : 0L,
                newBuffEndFrame: updated.EndFrame));
            PublishResult(
                context.Frame,
                request,
                GameplayBuffApplicationResultStatus.Succeeded,
                hadBuff
                    ? GameplayBuffApplicationResultReasonCode.Refreshed
                    : GameplayBuffApplicationResultReasonCode.Applied,
                hadBuff ? current.StackCount : 0,
                updated.StackCount,
                hadBuff ? current.EndFrame : 0L,
                updated.EndFrame);
        }

        private void Remove(
            GameplaySystemContext context,
            GameplayComponentWorld componentWorld,
            GameplayBuffApplicationRequest request)
        {
            if (!componentWorld.TryGetStore(out GameplayComponentStore<GameplayComponentBuffSetComponent> buffStore) ||
                !buffStore.TryGet(request.TargetEntityId, out GameplayComponentBuffSetComponent currentSet) ||
                !currentSet.TryGet(request.BuffId, out GameplayComponentBuffEntry current))
            {
                PublishResult(
                    context.Frame,
                    request,
                    GameplayBuffApplicationResultStatus.Succeeded,
                    GameplayBuffApplicationResultReasonCode.RemoveNoOp);
                return;
            }

            GameplayComponentBuffSetComponent updated = currentSet.Remove(request.BuffId);
            if (updated.Count == 0)
                buffStore.Remove(request.TargetEntityId);
            else
                buffStore.Set(request.TargetEntityId, updated);

            context.Events.Enqueue(context.Frame, new GameplayRuntimeEvent(
                context.Frame,
                GameplayRuntimeEventType.ComponentBuffRemoved,
                request.CommandId,
                casterEntityId: 0,
                abilityId: 0,
                targetEntityId: request.TargetEntityId.Index,
                failureCode: GameplayAbilityRuntimeFailureCode.None,
                reason: GameplayComponentBuffEvents.RemovedReason,
                traceId: request.TraceId,
                componentEntityIndex: request.TargetEntityId.Index,
                componentEntityGeneration: request.TargetEntityId.Generation,
                buffId: request.BuffId,
                oldBuffStackCount: current.StackCount,
                newBuffStackCount: 0,
                oldBuffEndFrame: current.EndFrame,
                newBuffEndFrame: 0L));
            PublishResult(
                context.Frame,
                request,
                GameplayBuffApplicationResultStatus.Succeeded,
                GameplayBuffApplicationResultReasonCode.Removed,
                current.StackCount,
                0,
                current.EndFrame,
                0L);
        }

        private static GameplayComponentBuffEntry RefreshEntry(
            long frame,
            GameplayComponentBuffEntry current,
            GameplayBuffDefinition definition,
            long endFrame,
            int sourceId)
        {
            int stackCount;
            long startFrame = current.StartFrame;
            long resolvedEndFrame = endFrame;
            switch (definition.StackPolicy)
            {
                case GameplayBuffStackPolicy.Replace:
                    stackCount = definition.ApplyStackCount;
                    startFrame = frame;
                    break;
                case GameplayBuffStackPolicy.Stack:
                    stackCount = Math.Min(definition.MaxStackCount, checked(current.StackCount + definition.ApplyStackCount));
                    break;
                default:
                    stackCount = Math.Min(definition.MaxStackCount, Math.Max(current.StackCount, definition.ApplyStackCount));
                    break;
            }

            return new GameplayComponentBuffEntry(
                current.BuffId,
                stackCount,
                startFrame,
                resolvedEndFrame,
                definition.IsPermanent,
                sourceId);
        }

        private static bool TryResolveEndFrame(
            long frame,
            GameplayBuffDefinition definition,
            out long endFrame)
        {
            if (definition.IsPermanent)
            {
                endFrame = 0L;
                return true;
            }

            long duration = definition.DefaultDurationFrames;
            if (duration <= 0L || duration > long.MaxValue - frame)
            {
                endFrame = 0L;
                return false;
            }

            endFrame = frame + duration;
            return true;
        }

        private void EnqueueRejected(
            GameplaySystemContext context,
            GameplayBuffApplicationRequest request,
            string reason,
            GameplayBuffApplicationResultReasonCode reasonCode)
        {
            context.Events.Enqueue(context.Frame, new GameplayRuntimeEvent(
                context.Frame,
                GameplayRuntimeEventType.ComponentBuffRejected,
                request.CommandId,
                casterEntityId: 0,
                abilityId: 0,
                targetEntityId: request.TargetEntityId.Index,
                failureCode: GameplayAbilityRuntimeFailureCode.None,
                reason: reason,
                traceId: request.TraceId,
                componentEntityIndex: request.TargetEntityId.Index,
                componentEntityGeneration: request.TargetEntityId.Generation,
                buffId: request.BuffId));
            PublishResult(
                context.Frame,
                request,
                GameplayBuffApplicationResultStatus.Rejected,
                reasonCode);
        }

        private void PublishResult(
            RuntimeFrame frame,
            GameplayBuffApplicationRequest request,
            GameplayBuffApplicationResultStatus status,
            GameplayBuffApplicationResultReasonCode reasonCode,
            int oldStackCount = 0,
            int newStackCount = 0,
            long oldEndFrame = 0L,
            long newEndFrame = 0L)
        {
            if (_resultSink == null || !request.Correlation.IsValid)
                return;

            var result = new GameplayBuffApplicationResult(
                frame,
                request.Kind,
                request.TargetEntityId,
                request.BuffId,
                request.SourceId,
                request.Correlation,
                status,
                reasonCode,
                oldStackCount,
                newStackCount,
                oldEndFrame,
                newEndFrame);
            _resultSink.Publish(in result);
        }
    }

    public sealed class GameplayBuffEffectProjectionSystem : IGameplaySystem
    {
        public const string DefaultSystemId = "mxframework.gameplay.buff.effect_projection";

        private static readonly IComparer<GameplayComponentModifierEntry> ModifierEntryComparer =
            new GameplayComponentModifierEntryComparer();
        private static readonly IComparer<GameplayStatusEntry> StatusEntryComparer =
            new GameplayStatusEntryComparer();

        private readonly GameplayBuffDefinitionRegistry _definitions;
        private readonly List<GameplayComponentSnapshot<GameplayComponentBuffSetComponent>> _buffSnapshot =
            new List<GameplayComponentSnapshot<GameplayComponentBuffSetComponent>>();
        private readonly List<GameplayComponentSnapshot<GameplayComponentModifierSetComponent>> _modifierSnapshot =
            new List<GameplayComponentSnapshot<GameplayComponentModifierSetComponent>>();
        private readonly List<GameplayComponentSnapshot<GameplayStatusComponent>> _statusSnapshot =
            new List<GameplayComponentSnapshot<GameplayStatusComponent>>();
        private readonly HashSet<GameplayEntityId> _entities = new HashSet<GameplayEntityId>();
        private readonly List<GameplayComponentBuffEntry> _buffEntries =
            new List<GameplayComponentBuffEntry>();
        private readonly List<GameplayComponentModifierEntry> _projectedModifiers =
            new List<GameplayComponentModifierEntry>();
        private readonly List<GameplayComponentModifierEntry> _mergedModifiers =
            new List<GameplayComponentModifierEntry>();
        private readonly List<GameplayStatusEntry> _projectedStatuses =
            new List<GameplayStatusEntry>();
        private readonly List<GameplayStatusEntry> _mergedStatuses =
            new List<GameplayStatusEntry>();

        public GameplayBuffEffectProjectionSystem(
            GameplayBuffDefinitionRegistry definitions,
            string systemId = DefaultSystemId,
            int priority = 52)
        {
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            SystemId = systemId ?? string.Empty;
            Priority = priority;
        }

        public string SystemId { get; }
        public GameplaySystemPhase Phase => GameplaySystemPhase.Resolution;
        public int Priority { get; }
        public bool IsEnabled { get; private set; } = true;

        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(GameplaySystemContext context)
        {
            GameplayComponentWorld componentWorld = context.ComponentWorld;
            if (componentWorld == null)
                return;

            GameplayComponentStore<GameplayComponentBuffSetComponent> buffStore = null;
            GameplayComponentStore<GameplayComponentModifierSetComponent> modifierStore = null;
            GameplayComponentStore<GameplayStatusComponent> statusStore = null;
            componentWorld.TryGetStore(out buffStore);
            componentWorld.TryGetStore(out modifierStore);
            componentWorld.TryGetStore(out statusStore);

            _entities.Clear();
            if (buffStore != null)
            {
                _buffSnapshot.Clear();
                buffStore.CopyTo(_buffSnapshot);
                for (int i = 0; i < _buffSnapshot.Count; i++)
                    _entities.Add(_buffSnapshot[i].EntityId);
            }

            if (modifierStore != null)
            {
                _modifierSnapshot.Clear();
                modifierStore.CopyTo(_modifierSnapshot);
                for (int i = 0; i < _modifierSnapshot.Count; i++)
                    _entities.Add(_modifierSnapshot[i].EntityId);
            }

            if (statusStore != null)
            {
                _statusSnapshot.Clear();
                statusStore.CopyTo(_statusSnapshot);
                for (int i = 0; i < _statusSnapshot.Count; i++)
                    _entities.Add(_statusSnapshot[i].EntityId);
            }

            foreach (GameplayEntityId entityId in _entities)
                ProjectEntity(componentWorld, buffStore, entityId);

            _entities.Clear();
            _buffSnapshot.Clear();
            _modifierSnapshot.Clear();
            _statusSnapshot.Clear();
        }

        private void ProjectEntity(
            GameplayComponentWorld componentWorld,
            GameplayComponentStore<GameplayComponentBuffSetComponent> buffStore,
            GameplayEntityId entityId)
        {
            _buffEntries.Clear();
            if (buffStore != null && buffStore.TryGet(entityId, out GameplayComponentBuffSetComponent buffSet))
                buffSet.CopyTo(_buffEntries);

            _projectedModifiers.Clear();
            _projectedStatuses.Clear();
            for (int i = 0; i < _buffEntries.Count; i++)
            {
                GameplayComponentBuffEntry buff = _buffEntries[i];
                if (!_definitions.TryGet(buff.BuffId, out GameplayBuffDefinition definition))
                    continue;

                for (int effectIndex = 0; effectIndex < definition.EffectCount; effectIndex++)
                {
                    GameplayBuffEffectDefinition effect = definition.GetEffectAt(effectIndex);
                    if (effect.Kind == GameplayBuffEffectKind.AttributeModifier)
                    {
                        _projectedModifiers.Add(new GameplayComponentModifierEntry(
                            CreateModifierId(buff.BuffId, effect.EffectId),
                            effect.AttributeId,
                            checked(effect.AddValue * buff.StackCount),
                            sourceBuffId: buff.BuffId));
                    }
                    else if (effect.Kind == GameplayBuffEffectKind.Status)
                    {
                        _projectedStatuses.Add(new GameplayStatusEntry(effect.StatusId, buff.BuffId));
                    }
                }
            }

            ProjectModifiers(componentWorld, entityId, _projectedModifiers);
            ProjectStatuses(componentWorld, entityId, _projectedStatuses);

            _buffEntries.Clear();
            _projectedModifiers.Clear();
            _projectedStatuses.Clear();
        }

        private void ProjectModifiers(
            GameplayComponentWorld componentWorld,
            GameplayEntityId entityId,
            List<GameplayComponentModifierEntry> projectedModifiers)
        {
            GameplayComponentStore<GameplayComponentModifierSetComponent> store =
                componentWorld.GetOrCreateStore<GameplayComponentModifierSetComponent>();
            store.TryGet(entityId, out GameplayComponentModifierSetComponent current);
            _mergedModifiers.Clear();
            current.CopyEntriesWithoutSourceBuffsTo(_mergedModifiers);

            for (int i = 0; i < projectedModifiers.Count; i++)
                _mergedModifiers.Add(projectedModifiers[i]);

            if (_mergedModifiers.Count > 1)
                _mergedModifiers.Sort(ModifierEntryComparer);

            if (_mergedModifiers.Count == 0)
            {
                if (current.Count != 0)
                    store.Remove(entityId);
                _mergedModifiers.Clear();
                return;
            }

            if (current.ContentEquals(_mergedModifiers))
            {
                _mergedModifiers.Clear();
                return;
            }

            store.Set(entityId, new GameplayComponentModifierSetComponent(_mergedModifiers.ToArray()));
            _mergedModifiers.Clear();
        }

        private void ProjectStatuses(
            GameplayComponentWorld componentWorld,
            GameplayEntityId entityId,
            List<GameplayStatusEntry> projectedStatuses)
        {
            GameplayComponentStore<GameplayStatusComponent> store =
                componentWorld.GetOrCreateStore<GameplayStatusComponent>();
            store.TryGet(entityId, out GameplayStatusComponent current);
            _mergedStatuses.Clear();
            current.CopyEntriesWithoutSourceBuffsTo(_mergedStatuses);

            for (int i = 0; i < projectedStatuses.Count; i++)
                _mergedStatuses.Add(projectedStatuses[i]);

            if (_mergedStatuses.Count > 1)
                _mergedStatuses.Sort(StatusEntryComparer);

            if (_mergedStatuses.Count == 0)
            {
                if (current.Count != 0)
                    store.Remove(entityId);
                _mergedStatuses.Clear();
                return;
            }

            if (current.ContentEquals(_mergedStatuses))
            {
                _mergedStatuses.Clear();
                return;
            }

            store.Set(entityId, new GameplayStatusComponent(_mergedStatuses.ToArray()));
            _mergedStatuses.Clear();
        }

        private static int CreateModifierId(int buffId, int effectId)
        {
            long id = ((long)buffId * 1000L) + effectId;
            if (id > int.MaxValue)
                throw new InvalidOperationException("Gameplay buff modifier id overflowed for buff " + buffId + " effect " + effectId + ".");

            return (int)id;
        }

        private sealed class GameplayComponentModifierEntryComparer :
            IComparer<GameplayComponentModifierEntry>
        {
            public int Compare(GameplayComponentModifierEntry left, GameplayComponentModifierEntry right)
            {
                return left.ModifierId.CompareTo(right.ModifierId);
            }
        }

        private sealed class GameplayStatusEntryComparer : IComparer<GameplayStatusEntry>
        {
            public int Compare(GameplayStatusEntry left, GameplayStatusEntry right)
            {
                return left.CompareTo(right);
            }
        }
    }
}
