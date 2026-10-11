using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay.Triggers;
using UnityEngine;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal enum TriggerTestRuntimeAiLabSetupCommandKind
    {
        SelectSlot = 0,
        SelectCharacter = 1,
        SelectBehaviorGroup = 2,
        EnableSlot = 3,
        DisableSlot = 4
    }

    internal sealed class TriggerTestRuntimeAiLabSetupArguments :
        ITriggerTestFunctionArguments
    {
        internal TriggerTestRuntimeAiLabSetupArguments(
            TriggerTestRuntimeAiLabSetupCommandKind commandKind)
        {
            CommandKind = commandKind;
        }

        internal TriggerTestRuntimeAiLabSetupCommandKind CommandKind
        {
            get;
        }
    }

    /// <summary>
    /// Executes one explicit Runtime AI Lab setup operation.
    /// </summary>
    /// <remarks>
    /// Allocation classification: LowFreqAlloc. This test-only adapter runs
    /// only after an explicit manual Trigger request.
    /// </remarks>
    internal sealed class TriggerTestRuntimeAiLabSetupFunction :
        TriggerTestFunction<TriggerTestRuntimeAiLabSetupArguments>
    {
        internal const int TargetTeamId =
            CharacterTestArenaMatchConfig.Team1Id;
        internal const int TargetSlotIndex = 1;

        internal static readonly TriggerTestFunctionId Id =
            new TriggerTestFunctionId(
                "mx.trigger.test.function.character-test.runtime-ai-lab-setup");

        public override TriggerTestFunctionId FunctionId => Id;

        public override TriggerTestFunctionCapability Capability =>
            TriggerTestFunctionCapability.UnityAdapter;

        protected override TriggerTestOperationResult ExecuteTyped(
            in TriggerTestFunctionContext context,
            TriggerTestRuntimeAiLabSetupArguments arguments)
        {
            if (!TriggerTestRuntimeAiLabCommandProbe.TryResolveController(
                    out CharacterTestRuntimeAiPlannerDevSceneController controller,
                    out string diagnostic))
            {
                return TriggerTestOperationResult.Failed(diagnostic);
            }
            if (controller.Phase !=
                CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
            {
                return TriggerTestOperationResult.Failed(
                    "Setup Trigger 只能在实验设置阶段运行。");
            }

            if (!TryExecute(
                    controller,
                    arguments.CommandKind,
                    out string message))
            {
                return TriggerTestOperationResult.Failed(message);
            }

            Debug.Log(
                "[TriggerTest][RuntimeAiLabSetup][" +
                arguments.CommandKind +
                "][PASS] " +
                message);
            return TriggerTestOperationResult.Succeeded(message);
        }

        private static bool TryExecute(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            TriggerTestRuntimeAiLabSetupCommandKind commandKind,
            out string message)
        {
            switch (commandKind)
            {
                case TriggerTestRuntimeAiLabSetupCommandKind.SelectSlot:
                    if (!controller.SelectSetupSpawnSlot(
                            TargetTeamId,
                            TargetSlotIndex))
                    {
                        message = controller.StatusMessage;
                        return false;
                    }

                    message = "已选择队伍 1 的第 2 个测试槽位。";
                    return true;
                case TriggerTestRuntimeAiLabSetupCommandKind
                    .SelectCharacter:
                    if (!TryGetSelectedSlot(
                            controller,
                            out CharacterTestArenaSlotConfig _,
                            out message))
                    {
                        return false;
                    }

                    int characterId =
                        CharacterTestCharacterDefinitionRegistry
                            .DefaultPlayableCharacterId;
                    if (!controller.SelectSetupCharacterId(characterId))
                    {
                        message = controller.StatusMessage;
                        return false;
                    }

                    message = "已选择角色 " + characterId + "。";
                    return true;
                case TriggerTestRuntimeAiLabSetupCommandKind
                    .SelectBehaviorGroup:
                    if (!TryGetSelectedSlot(
                            controller,
                            out CharacterTestArenaSlotConfig selectedSlot,
                            out message))
                    {
                        return false;
                    }
                    if (!TryFindAlternativeBehaviorGroup(
                            controller.MatchConfig.BehaviorGroups,
                            selectedSlot.BehaviorGroupDraftId,
                            out int draftId,
                            out message))
                    {
                        return false;
                    }
                    if (!controller.SelectSetupBehaviorGroupDraft(draftId))
                    {
                        message = controller.StatusMessage;
                        return false;
                    }

                    message = "已选择可运行行为组 " + draftId + "。";
                    return true;
                case TriggerTestRuntimeAiLabSetupCommandKind.EnableSlot:
                    return TrySetSelectedSlotEnabled(
                        controller,
                        enabled: true,
                        out message);
                case TriggerTestRuntimeAiLabSetupCommandKind.DisableSlot:
                    return TrySetSelectedSlotEnabled(
                        controller,
                        enabled: false,
                        out message);
                default:
                    message = "未知的 Runtime AI Lab Setup 命令。";
                    return false;
            }
        }

        private static bool TrySetSelectedSlotEnabled(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            bool enabled,
            out string message)
        {
            if (!TryGetSelectedSlot(
                    controller,
                    out CharacterTestArenaSlotConfig slot,
                    out message))
            {
                return false;
            }

            if (slot.Enabled != enabled &&
                !controller.ToggleSelectedSetupSlotEnabled())
            {
                message = controller.StatusMessage;
                return false;
            }

            message = enabled
                ? "所选测试槽位已启用。"
                : "所选测试槽位已禁用。";
            return true;
        }

        internal static bool TryGetSelectedSlot(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            out CharacterTestArenaSlotConfig slot,
            out string diagnostic)
        {
            slot = controller.MatchConfig?.TryGetSlot(
                controller.SelectedSetupTeamId,
                controller.SelectedSetupSlotIndex);
            if (slot != null)
            {
                diagnostic = string.Empty;
                return true;
            }

            diagnostic = "当前没有可编辑的测试槽位。";
            return false;
        }

        private static bool TryFindAlternativeBehaviorGroup(
            CharacterTestArenaBehaviorGroupDraftStore store,
            int currentDraftId,
            out int draftId,
            out string diagnostic)
        {
            draftId = 0;
            if (store == null || store.Drafts.Count == 0)
            {
                diagnostic = "行为组目录为空。";
                return false;
            }

            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < store.Drafts.Count; i++)
                {
                    CharacterTestArenaBehaviorGroupDraft draft =
                        store.Drafts[i];
                    if (pass == 0 && draft.DraftId == currentDraftId)
                        continue;
                    if (!store.TryGetDraftDisplay(
                            draft.DraftId,
                            out _,
                            out _,
                            out bool compileSuccess) ||
                        !compileSuccess)
                    {
                        continue;
                    }

                    draftId = draft.DraftId;
                    diagnostic = string.Empty;
                    return true;
                }
            }

            diagnostic = "没有可用于测试的行为组。";
            return false;
        }
    }

    internal sealed class TriggerTestRuntimeAiLabSetupAssertion :
        ITriggerTestAssertion
    {
        private readonly TriggerTestRuntimeAiLabSetupCommandKind
            _commandKind;

        internal TriggerTestRuntimeAiLabSetupAssertion(
            TriggerTestRuntimeAiLabSetupCommandKind commandKind)
        {
            _commandKind = commandKind;
        }

        public string AssertionId =>
            "runtime-ai-lab-setup-" +
            _commandKind.ToString().ToLowerInvariant();

        public TriggerTestOperationResult Evaluate(
            in TriggerTestAssertionContext context)
        {
            if (!TriggerTestRuntimeAiLabCommandProbe.TryResolveController(
                    out CharacterTestRuntimeAiPlannerDevSceneController controller,
                    out string diagnostic))
            {
                return TriggerTestOperationResult.Failed(diagnostic);
            }
            if (controller.Phase !=
                CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
            {
                return TriggerTestOperationResult.Failed(
                    "操作完成后已离开实验设置阶段。");
            }

            bool succeeded;
            switch (_commandKind)
            {
                case TriggerTestRuntimeAiLabSetupCommandKind.SelectSlot:
                    succeeded =
                        controller.SelectedSetupTeamId ==
                        TriggerTestRuntimeAiLabSetupFunction.TargetTeamId &&
                        controller.SelectedSetupSlotIndex ==
                        TriggerTestRuntimeAiLabSetupFunction.TargetSlotIndex;
                    diagnostic = succeeded
                        ? "已验证目标测试槽位成为当前选择。"
                        : "目标测试槽位没有成为当前选择。";
                    break;
                case TriggerTestRuntimeAiLabSetupCommandKind
                    .SelectCharacter:
                    succeeded = TryReadSelectedSlot(
                        controller,
                        out CharacterTestArenaSlotConfig characterSlot) &&
                        characterSlot.CharacterId ==
                        CharacterTestCharacterDefinitionRegistry
                            .DefaultPlayableCharacterId;
                    diagnostic = succeeded
                        ? "已验证角色选择写入所选槽位。"
                        : "角色选择没有写入所选槽位。";
                    break;
                case TriggerTestRuntimeAiLabSetupCommandKind
                    .SelectBehaviorGroup:
                    succeeded = TryReadSelectedSlot(
                        controller,
                        out CharacterTestArenaSlotConfig behaviorSlot) &&
                        controller.MatchConfig.BehaviorGroups
                            .TryGetDraftDisplay(
                                behaviorSlot.BehaviorGroupDraftId,
                                out _,
                                out _,
                                out bool compileSuccess) &&
                        compileSuccess;
                    diagnostic = succeeded
                        ? "已验证可运行行为组写入所选槽位。"
                        : "所选槽位没有有效的可运行行为组。";
                    break;
                case TriggerTestRuntimeAiLabSetupCommandKind.EnableSlot:
                    succeeded = TryReadSelectedSlot(
                        controller,
                        out CharacterTestArenaSlotConfig enabledSlot) &&
                        enabledSlot.Enabled;
                    diagnostic = succeeded
                        ? "已验证所选测试槽位处于启用状态。"
                        : "所选测试槽位没有启用。";
                    break;
                case TriggerTestRuntimeAiLabSetupCommandKind.DisableSlot:
                    succeeded = TryReadSelectedSlot(
                        controller,
                        out CharacterTestArenaSlotConfig disabledSlot) &&
                        !disabledSlot.Enabled;
                    diagnostic = succeeded
                        ? "已验证所选测试槽位处于禁用状态。"
                        : "所选测试槽位没有禁用。";
                    break;
                default:
                    succeeded = false;
                    diagnostic = "未知的 Runtime AI Lab Setup 命令。";
                    break;
            }

            return succeeded
                ? TriggerTestOperationResult.Succeeded(diagnostic)
                : TriggerTestOperationResult.Failed(diagnostic);
        }

        private static bool TryReadSelectedSlot(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            out CharacterTestArenaSlotConfig slot)
        {
            return TriggerTestRuntimeAiLabSetupFunction.TryGetSelectedSlot(
                controller,
                out slot,
                out _);
        }
    }

    internal abstract class TriggerTestRuntimeAiLabSetupScenarioBase :
        ITriggerTestScenario
    {
        private const int FunctionCallId = 1;
        private readonly TriggerTestDescriptor _descriptor;
        private readonly TriggerTestRuntimeAiLabSetupCommandKind _commandKind;

        protected TriggerTestRuntimeAiLabSetupScenarioBase(
            string scenarioId,
            string displayName,
            string operationTag,
            TriggerTestRuntimeAiLabSetupCommandKind commandKind)
        {
            _commandKind = commandKind;
            _descriptor = new TriggerTestDescriptor(
                scenarioId,
                displayName,
                "character-test",
                TriggerTestExecutionProfile.PlayMode,
                maxFrames: 1,
                tags: new[]
                {
                    "trigger",
                    "character-test",
                    "runtime-ai-lab",
                    "playmode",
                    "manual",
                    "setup",
                    operationTag
                });
        }

        public TriggerTestDescriptor Descriptor => _descriptor;

        public void Build(TriggerTestScenarioBuilder builder)
        {
            builder.RegisterFunction(
                new TriggerTestRuntimeAiLabSetupFunction());
            builder.RegisterCall(
                FunctionCallId,
                TriggerTestRuntimeAiLabSetupFunction.Id,
                new TriggerTestRuntimeAiLabSetupArguments(_commandKind));
            builder.UseFixture(CreateFixture);
            builder.AtFrame(
                0L,
                new TriggerTestSubmitSignalsStep(
                    "request-setup-" +
                    _commandKind.ToString().ToLowerInvariant(),
                    new GameplayTriggerSourceBindingId(
                        TriggerTestSignalFunctionFixtureFactory.SourceBindingId),
                    GameplayTriggerSignalOrdering.UnorderedMultiset,
                    new[] { new GameplayTriggerSignal(1) }));
            builder.AssertAtFrame(
                0L,
                new TriggerTestRuntimeCountAssertion(
                    "activation-count",
                    TriggerTestRuntimeCountKind.Activation,
                    1));
            builder.AssertAtFrame(
                0L,
                new TriggerTestRuntimeCountAssertion(
                    "intent-count",
                    TriggerTestRuntimeCountKind.Intent,
                    1));
            builder.AssertAtFrame(
                0L,
                new TriggerTestRuntimeAiLabSetupAssertion(_commandKind));
        }

        private static ITriggerTestFixture CreateFixture(
            TriggerTestFunctionActionSink functionSink) =>
            TriggerTestSignalFunctionFixtureFactory.Create(
                functionSink,
                new[] { FunctionCallId });
    }
}
