using System;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabCharacterActionAuthoringOperation
    {
        Open = 0,
        RetryApply = 1,
        OpenEmbedded = 2,
    }

    internal enum CharacterTestRuntimeAiLabCharacterActionAuthoringStatus
    {
        Unavailable = 0,
        Opening = 1,
        Opened = 2,
        Saved = 3,
        Importing = 4,
        Reloading = 5,
        Restarting = 6,
        Succeeded = 7,
        Failed = 8,
    }

    internal readonly struct CharacterTestRuntimeAiLabCharacterActionAuthoringContext
    {
        internal CharacterTestRuntimeAiLabCharacterActionAuthoringContext(
            int entityId,
            string characterStableId,
            string equipmentStateStableId,
            string actionSetStableId)
        {
            EntityId = entityId;
            CharacterStableId = characterStableId ?? string.Empty;
            EquipmentStateStableId = equipmentStateStableId ?? string.Empty;
            ActionSetStableId = actionSetStableId ?? string.Empty;
        }

        internal int EntityId { get; }
        internal string CharacterStableId { get; }
        internal string EquipmentStateStableId { get; }
        internal string ActionSetStableId { get; }
        internal bool IsValid => EntityId > 0 && ActionSetStableId.Length > 0;
    }

    internal sealed class CharacterTestRuntimeAiLabCharacterActionAuthoringRequest
    {
        internal CharacterTestRuntimeAiLabCharacterActionAuthoringOperation Operation;
        internal CharacterActionId ActionId;
        internal CharacterTestRuntimeAiLabCharacterActionAuthoringContext Context;
        internal int SelectedAgentIndex;
        internal string StableCardId = string.Empty;
        internal string TimelineId = string.Empty;
        internal string EventId = string.Empty;
        internal CharacterTestRuntimeAiLabTimelineMode TimelineMode;
        internal Action<CharacterTestRuntimeAiLabCharacterActionAuthoringResult> StatusChanged;
    }

    internal sealed class CharacterTestRuntimeAiLabCharacterActionAuthoringCommandPayload
    {
        internal CharacterActionId ActionId;
        internal string StableCardId = string.Empty;
        internal string TimelineId = string.Empty;
        internal string EventId = string.Empty;
    }

    internal readonly struct CharacterTestRuntimeAiLabCharacterActionAuthoringResult
    {
        internal CharacterTestRuntimeAiLabCharacterActionAuthoringResult(
            CharacterTestRuntimeAiLabCharacterActionAuthoringStatus status,
            CharacterActionId actionId,
            string packagePath,
            string message,
            bool sourceSaved = false)
        {
            Status = status;
            ActionId = actionId;
            PackagePath = packagePath ?? string.Empty;
            Message = message ?? string.Empty;
            SourceSaved = sourceSaved;
        }

        internal CharacterTestRuntimeAiLabCharacterActionAuthoringStatus Status { get; }
        internal CharacterActionId ActionId { get; }
        internal string PackagePath { get; }
        internal string Message { get; }
        internal bool SourceSaved { get; }
        internal bool Success => Status == CharacterTestRuntimeAiLabCharacterActionAuthoringStatus.Succeeded;
        internal bool CanRetry => Status == CharacterTestRuntimeAiLabCharacterActionAuthoringStatus.Failed
            && SourceSaved
            && PackagePath.Length > 0;
    }

    internal static class CharacterTestRuntimeAiLabCharacterActionAuthoringBridge
    {
        internal static Func<CharacterTestRuntimeAiLabCharacterActionAuthoringRequest,
            CharacterTestRuntimeAiLabCharacterActionAuthoringResult> Handler { get; set; }

        internal static bool IsRegistered => Handler != null;
        internal static CharacterTestRuntimeAiLabCharacterActionAuthoringResult LastResult { get; private set; }

        internal static void Publish(CharacterTestRuntimeAiLabCharacterActionAuthoringResult result)
        {
            LastResult = result;
        }

        internal static CharacterTestRuntimeAiLabCharacterActionAuthoringResult Execute(
            CharacterTestRuntimeAiLabCharacterActionAuthoringRequest request)
        {
            if (Handler != null)
            {
                CharacterTestRuntimeAiLabCharacterActionAuthoringResult handled = Handler(request);
                Publish(handled);
                return handled;
            }
            CharacterTestRuntimeAiLabCharacterActionAuthoringResult unavailable =
                new CharacterTestRuntimeAiLabCharacterActionAuthoringResult(
                CharacterTestRuntimeAiLabCharacterActionAuthoringStatus.Unavailable,
                request?.ActionId ?? CharacterActionId.None,
                string.Empty,
                "角色动作时间轴只能在 Unity Editor 中编辑。");
            Publish(unavailable);
            return unavailable;
        }
    }
}
