using System;
using MxFramework.CharacterAction;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private bool _characterActionWorkspacePreviewActive;
        private string _characterActionWorkspacePreviewSource = string.Empty;

        internal bool TryStartCharacterActionWorkspacePreview(
            CharacterActionId actionId,
            string sourceDirectory,
            bool play,
            int targetFrame,
            out string message)
        {
            message = string.Empty;
            if (_context?.Slice?.Services == null
                || !actionId.IsValid
                || string.IsNullOrWhiteSpace(sourceDirectory))
            {
                message = "当前角色草稿预览上下文不可用。";
                return false;
            }

            SetPaused(true);
            var options = new CharacterTestCharacterActionConfigSourceOptions(
                new[] { sourceDirectory },
                allowEmbeddedFallback: false);
            CharacterTestCharacterActionReloadResult reload =
                _context.Slice.Services.ReloadCharacterActionConfig(options);
            if (!reload.Success)
            {
                message = reload.Message;
                return false;
            }

            int entityId = ResolveSelectedAgentEntityId();
            CharacterTestCharacterActionRuntime runtime = _context.Slice.Services.ActionRuntime;
            RuntimeFrame frame = _context.Slice.Services.CurrentFrame;
            if (entityId <= 0
                || runtime == null
                || !runtime.ReplaceForOrchestration(
                    new CharacterTestCharacterEntityId(entityId),
                    actionId,
                    CharacterActionOrchestrationReason.BehaviorRecipeTimelineJump,
                    frame,
                    MxFramework.Runtime.RuntimeTraceIdentity.None))
            {
                _context.Slice.Services.ReloadCharacterActionConfig();
                message = "草稿动作无法在当前测试角色上启动。";
                return false;
            }

            _characterActionWorkspacePreviewActive = true;
            _characterActionWorkspacePreviewSource = sourceDirectory;
            if (targetFrame > 0)
            {
                for (int i = 0; i < targetFrame; i++)
                {
                    if (!StepFrame())
                        break;
                }
            }
            SetPaused(!play);
            message = play
                ? "当前测试角色正在播放未保存草稿动作。"
                : "当前测试角色草稿预览已暂停。";
            return true;
        }

        internal bool TryStopCharacterActionWorkspacePreview(out string message)
        {
            message = string.Empty;
            if (_context?.Slice?.Services == null)
            {
                message = "当前测试会话不可用。";
                return false;
            }
            SetPaused(true);
            CharacterTestCharacterActionReloadResult reload =
                _context.Slice.Services.ReloadCharacterActionConfig();
            _characterActionWorkspacePreviewActive = false;
            _characterActionWorkspacePreviewSource = string.Empty;
            message = reload.Success
                ? "草稿预览已停止，正式动作配置已恢复。"
                : reload.Message;
            return reload.Success;
        }

        internal bool TryStepCharacterActionWorkspacePreview(out string message)
        {
            SetPaused(true);
            bool success = _characterActionWorkspacePreviewActive && StepFrame();
            message = success ? "草稿预览已前进一帧。" : "草稿预览单帧执行失败。";
            return success;
        }

        internal bool TryPauseCharacterActionWorkspacePreview(bool paused, out string message)
        {
            if (!_characterActionWorkspacePreviewActive)
            {
                message = "草稿预览尚未启动。";
                return false;
            }
            SetPaused(paused);
            message = paused ? "草稿预览已暂停。" : "草稿预览继续播放。";
            return true;
        }
    }
}
