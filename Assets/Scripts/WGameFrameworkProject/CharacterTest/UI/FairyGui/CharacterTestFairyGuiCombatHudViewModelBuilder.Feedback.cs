using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.Gameplay;
using MxFramework.Network;
using MxFramework.Runtime;
using MxFramework.UI;
using Unity.Profiling;

namespace MxFramework.Game.CharacterTest
{
    public static partial class CharacterTestFairyGuiCombatHudViewModelBuilder
    {
        private static bool TryBuildFeedback(
            GameSlice slice,
            CharacterTestCharacterEntity localPlayer,
            IMxUiTextProvider textProvider,
            out string feedback)
        {
            feedback = string.Empty;
            if (localPlayer != null
                && !string.IsNullOrWhiteSpace(localPlayer.Action.LastRejectReason)
                && !HasBufferedActionAttempt(localPlayer))
            {
                feedback =
                    L(textProvider, "ui.character_test.combat.action_rejected", "Action rejected")
                    + ": "
                    + localPlayer.Action.LastRejectReason;
                return true;
            }

            IReadOnlyList<CharacterTestRuntimeEvent> events = slice.RecentRuntimeEvents;
            if (events != null)
            {
                RuntimeFrame currentFrame = slice.CurrentFrame;
                for (int i = events.Count - 1; i >= 0; i--)
                {
                    CharacterTestRuntimeEvent evt = events[i];
                    if (!IsFeedbackEventRecent(currentFrame, evt.Frame))
                        continue;

                    string formatted = FormatRuntimeEvent(evt, textProvider);
                    if (!string.IsNullOrEmpty(formatted))
                    {
                        feedback = formatted;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasBufferedActionAttempt(CharacterTestCharacterEntity entity)
        {
            return entity != null
                && entity.PendingGameplayIntentCount > 0
                && CharacterTestCharacterActionSystem.IsTemporaryActionReject(entity);
        }

        private static string FormatRuntimeEvent(CharacterTestRuntimeEvent evt, IMxUiTextProvider textProvider)
        {
            switch (evt.Type)
            {
                case CharacterTestRuntimeEventType.ControlIntentRejected:
                    return L(textProvider, "ui.character_test.combat.input_rejected", "Input rejected")
                        + ": "
                        + EmptyAsNone(evt.Message, textProvider)
                        + ".";
                case CharacterTestRuntimeEventType.CharacterSpawnRejected:
                case CharacterTestRuntimeEventType.CommandRejected:
                    return L(textProvider, "ui.character_test.combat.command_rejected", "Command rejected")
                        + ": "
                        + EmptyAsNone(evt.Message, textProvider)
                        + ".";
                default:
                    return string.Empty;
            }
        }

        private static bool IsFeedbackEventRecent(RuntimeFrame currentFrame, RuntimeFrame eventFrame)
        {
            long age = currentFrame.Value - eventFrame.Value;
            return age < 0 || age <= FeedbackEventVisibleFrames;
        }

        private static void SetFeedback(CharacterTestFairyGuiCombatHudViewModel viewModel, string feedback)
        {
            viewModel.FeedbackText = feedback ?? string.Empty;
            viewModel.FeedbackVisible = !string.IsNullOrWhiteSpace(viewModel.FeedbackText);
        }

        private static void PopulateNetworkLatency(
            CharacterTestFairyGuiCombatHudViewModel viewModel,
            CharacterTestNetworkSessionStatus status)
        {
            if (viewModel == null || status == null || !status.IsNetworkActive)
                return;

            viewModel.NetworkLatencyVisible = true;
            if (HasNetworkFailure(status))
            {
                viewModel.NetworkLatencyText = "NET LOST";
                viewModel.NetworkLatencyTone = "bad";
                return;
            }

            if (status.ClientStatus != NetworkSessionStatus.Connected)
            {
                viewModel.NetworkLatencyText = "NET WAIT";
                viewModel.NetworkLatencyTone = "warn";
                return;
            }

            int rttFrames = Math.Max(0, status.RttEstimateFrames);
            int tickRate = status.NetworkTickRate > 0 ? status.NetworkTickRate : DefaultNetworkTickRate;
            int rttMilliseconds = (int)Math.Round(rttFrames * 1000d / tickRate);
            viewModel.NetworkLatencyText =
                "NET " + rttFrames + "f / " + rttMilliseconds + "ms";
            viewModel.NetworkLatencyTone = rttFrames <= 3
                ? "good"
                : rttFrames <= 6
                    ? "warn"
                    : "bad";
        }

        private static bool HasNetworkFailure(CharacterTestNetworkSessionStatus status)
        {
            return status != null
                && (!string.IsNullOrWhiteSpace(status.DisconnectReason)
                    || status.ServerStatus == NetworkSessionStatus.Rejected
                    || status.ClientStatus == NetworkSessionStatus.Rejected);
        }

        private static CharacterTestCharacterEntity FindLocalPlayer(IReadOnlyList<CharacterTestCharacterEntity> entities)
        {
            if (entities == null)
                return null;

            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer)
                    return entity;
            }

            return null;
        }

        private static CharacterTestCharacterEntity ResolveTarget(
            GameSlice slice,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestCharacterEntity localPlayer,
            CharacterTestFairyGuiCombatHudBuildBuffers buffers,
            out bool targetLocked)
        {
            targetLocked = false;
            if (slice == null || entities == null)
                return null;

            if (slice.TargetLockEnabled)
            {
                int targetId = slice.LockedTargetEntityId;
                if (targetId > 0)
                {
                    for (int i = 0; i < entities.Count; i++)
                    {
                        CharacterTestCharacterEntity entity = entities[i];
                        if (entity.Id.Value == targetId && entity != localPlayer)
                        {
                            targetLocked = true;
                            return entity;
                        }
                    }
                }
            }

            return ResolveRecentBuffTarget(slice, entities, localPlayer, buffers);
        }

        private static CharacterTestCharacterEntity ResolveRecentBuffTarget(
            GameSlice slice,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestCharacterEntity localPlayer,
            CharacterTestFairyGuiCombatHudBuildBuffers buffers)
        {
            if (slice?.CharacterRuntime?.World == null || localPlayer == null)
                return null;

            CharacterTestCharacterEntity best = null;
            long bestFrame = long.MinValue;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == null
                    || entity == localPlayer
                    || entity.CombatState.LastHitSourceEntityId != localPlayer.Id.Value
                    || entity.CombatState.LastHitFrame < 0L)
                {
                    continue;
                }

                int targetBuffCount;
                if (buffers != null)
                {
                    targetBuffCount = GameplayBuffPresentationSnapshotBuilder.BuildInto(
                        slice.CharacterRuntime.World.GameplayComponentWorld,
                        entity.GameplayEntityId,
                        slice.BuffDefinitions,
                        slice.BuffPresentationDefinitions,
                        slice.CurrentFrame,
                        GameplayBuffPresentationSurface.TargetHud,
                        buffers.RecentTargetBuffItems,
                        buffers.BuffEntryScratch,
                        maxItems: 1);
                }
                else
                {
                    GameplayBuffPresentationItem[] targetBuffs =
                        GameplayBuffPresentationSnapshotBuilder.Build(
                            slice.CharacterRuntime.World.GameplayComponentWorld,
                            entity.GameplayEntityId,
                            slice.BuffDefinitions,
                            slice.BuffPresentationDefinitions,
                            slice.CurrentFrame,
                            GameplayBuffPresentationSurface.TargetHud,
                            maxItems: 1);
                    targetBuffCount = targetBuffs.Length;
                }

                if (targetBuffCount == 0)
                    continue;

                if (best == null || entity.CombatState.LastHitFrame > bestFrame)
                {
                    best = entity;
                    bestFrame = entity.CombatState.LastHitFrame;
                }
            }

            return best;
        }

        private static string FormatAction(CharacterTestCharacterEntity entity, IMxUiTextProvider textProvider)
        {
            CharacterTestCharacterActionComponent action = entity.Action;
            string actionId = action.ActiveActionId.IsValid ? action.ActiveActionId.ToString() : "none";
            if (action.Phase == CharacterTestActionPhaseId.None)
                return actionId;

            return actionId + " " + CharacterTestCharacterActionComponent.ResolvePhaseText(action.Phase);
        }

        private static string FormatPlayerActionText(
            CharacterTestCharacterEntity entity,
            IMxUiTextProvider textProvider,
            CharacterTestFairyGuiCombatHudBuildBuffers buffers)
        {
            CharacterTestCharacterActionComponent action = entity.Action;
            string activeActionId = !action.ActiveActionId.IsValid
                ? string.Empty
                : action.ActiveActionId.ToString();
            string phase = action.Phase == CharacterTestActionPhaseId.None
                ? string.Empty
                : CharacterTestCharacterActionComponent.ResolvePhaseText(action.Phase);
            CharacterTestUiTextProviderSignature textProviderSignature =
                CharacterTestUiTextProviderSignature.Create(textProvider);
            if (buffers != null
                && buffers.HasPlayerActionTextCache
                && buffers.PlayerActionTextProviderSignature == textProviderSignature
                && string.Equals(buffers.PlayerActionTextActionId, activeActionId, StringComparison.Ordinal)
                && string.Equals(buffers.PlayerActionTextPhase, phase, StringComparison.Ordinal))
            {
                return buffers.PlayerActionText;
            }

            string actionText = FormatAction(entity, textProvider);
            string label = L(textProvider, "ui.character_test.combat.action", "Action");
            string result;
            if (string.Equals(label, "Action", StringComparison.Ordinal)
                && string.Equals(actionText, "none", StringComparison.Ordinal))
            {
                result = DefaultActionNoneText;
            }
            else
            {
                result = label + ": " + actionText;
            }

            if (buffers != null)
            {
                buffers.HasPlayerActionTextCache = true;
                buffers.PlayerActionTextProviderSignature = textProviderSignature;
                buffers.PlayerActionTextActionId = activeActionId;
                buffers.PlayerActionTextPhase = phase;
                buffers.PlayerActionText = result;
            }

            return result;
        }

        private static string FormatTargetStatus(
            CharacterTestCharacterCombatStateComponent combat,
            bool targetLocked,
            IMxUiTextProvider textProvider)
        {
            if (!combat.IsAlive)
                return L(textProvider, "ui.character_test.combat.status.dead", "DEAD");
            if (combat.IsCohesionBroken)
                return L(textProvider, "ui.character_test.combat.status.broken", "BROKEN");
            if (combat.IsReacting)
                return L(textProvider, "ui.character_test.combat.status.hitstun", "HITSTUN")
                    + " "
                    + combat.HitStunFramesRemaining
                    + "f";
            return targetLocked
                ? L(textProvider, "ui.character_test.combat.status.lock", "LOCK")
                : L(textProvider, "ui.character_test.combat.status.target", "TARGET");
        }

        private static string ResolveDisplayName(CharacterTestCharacterEntity entity, string fallback)
        {
            string displayName = entity.Identity.DisplayName;
            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName;

            return fallback + " #" + entity.Id.Value;
        }

        private static string FormatPressureBand(PressureBand band)
        {
            switch (band)
            {
                case PressureBand.Stable:
                    return "Stable";
                case PressureBand.Pressed:
                    return "Pressed";
                case PressureBand.Cracked:
                    return "Cracked";
                case PressureBand.Critical:
                    return "Critical";
                case PressureBand.Broken:
                    return "Broken";
                default:
                    return string.Empty;
            }
        }

        private static float Ratio(int current, int max)
        {
            if (max <= 0)
                return 0f;

            float ratio = (float)current / max;
            if (ratio <= 0f)
                return 0f;
            return ratio >= 1f ? 1f : ratio;
        }

        private static string EmptyAsNone(string value, IMxUiTextProvider textProvider)
        {
            return string.IsNullOrWhiteSpace(value)
                ? L(textProvider, "ui.character_test.combat.none", "none")
                : value;
        }

        private static string L(IMxUiTextProvider provider, string key, string fallback)
        {
            return CharacterTestUiText.Resolve(provider, key, fallback);
        }
    }
}
