using System;
using System.Collections.Generic;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Core;
using Newtonsoft.Json.Linq;

namespace MxFramework.CharacterAction
{
    public static partial class CharacterActionJsonImporter
    {
        private static int ReadStableEventOrder(JObject item)
        {
            int eventId = ReadDefinitionId(item, "stableEventId");
            int order = ReadInt(item, "stableEventOrder");
            if (order < 0 || (eventId > 0 && order == 0))
                throw new ArgumentException("An authored event requires its persisted numeric order.");
            return order;
        }
        private static MotionTrackConfig ReadMotionTrack(
            JObject track,
            string sourcePath,
            string jsonPath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            if (track == null)
                return null;

            JArray events = track["events"] as JArray;
            var results = new List<MotionTrackEvent>();
            for (int i = 0; events != null && i < events.Count; i++)
            {
                JObject item = events[i] as JObject;
                string path = jsonPath + ".events[" + i + "]";
                if (item == null)
                    continue;
                string rawKind = ReadString(item, "kind");
                if (string.Equals(rawKind, "ResetControlScale", StringComparison.Ordinal)
                    || string.Equals(rawKind, "ResetCollisionProfile", StringComparison.Ordinal))
                {
                    AddError(
                        diagnostics,
                        sourcePath,
                        path + ".kind",
                        CharacterActionDiagnosticCodes.JsonInvalidConfig,
                        "Reset track events were removed in CharacterAction schema 0.2.0; author one inclusive range instead.");
                    continue;
                }
                bool parsed = TryReadEnum(item, "kind", sourcePath, path + ".kind", CharacterActionTrackEventKind.None, diagnostics, out CharacterActionTrackEventKind kind);
                parsed &= TryReadEnum(item, "movementMode", sourcePath, path + ".movementMode", CharacterMovementMode.Idle, diagnostics, out CharacterMovementMode movementMode);
                parsed &= TryReadEnum(item, "space", sourcePath, path + ".space", CharacterActionMotionVectorSpace.World, diagnostics, out CharacterActionMotionVectorSpace space);
                parsed &= TryReadEnum(
                    item,
                    "collisionRestorePolicy",
                    sourcePath,
                    path + ".collisionRestorePolicy",
                    CharacterActionCollisionProfileRestorePolicy.OnActionEnd,
                    diagnostics,
                    out CharacterActionCollisionProfileRestorePolicy collisionRestorePolicy);
                if (!parsed)
                    continue;

                bool hasMovementControlScale = item["movementControlScale"] != null;
                bool hasRotationControlScale = item["rotationControlScale"] != null;
                bool requiresRange = kind == CharacterActionTrackEventKind.LockMovement
                    || kind == CharacterActionTrackEventKind.SetControlScale
                    || kind == CharacterActionTrackEventKind.SetCollisionProfile
                    || kind == CharacterActionTrackEventKind.SetVerticalVelocity
                    || kind == CharacterActionTrackEventKind.IgnoreGravity
                    || kind == CharacterActionTrackEventKind.SampleRootMotionY;
                if (requiresRange
                    && (item["endFrame"] == null || ReadDefinitionId(item, "stableEventId") <= 0))
                {
                    AddError(
                        diagnostics,
                        sourcePath,
                        path,
                        CharacterActionDiagnosticCodes.InvalidTrackLifecycle,
                        "Stateful motion track ranges require endFrame and stableEventId.");
                    continue;
                }

                try
                {
                    results.Add(new MotionTrackEvent(
                        ReadInt(item, "frame"),
                        kind,
                        movementMode,
                        ReadFloat(item, "x"),
                        ReadFloat(item, "y"),
                        ReadFloat(item, "z"),
                        ReadDefinitionId(item, "stableEventId"),
                        ReadFloat(item["movementControlScale"], 1f),
                        ReadFloat(item["rotationControlScale"], 1f),
                        space,
                        hasMovementControlScale,
                        hasRotationControlScale,
                        ReadDefinitionId(item, "collisionProfileId"),
                        collisionRestorePolicy,
                        item["collisionTransitionFrames"] != null
                            ? ReadInt(item, "collisionTransitionFrames")
                            : 0,
                        requiresRange ? ReadInt(item, "endFrame") : ReadInt(item, "frame"), stableEventOrder: ReadStableEventOrder(item)));
                }
                catch (Exception ex) when (IsConfigException(ex))
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidFrame, "Motion track event is invalid.");
                }
            }

            CharacterActionVerticalRootMotionConfig verticalRootMotion = ReadVerticalRootMotionTrack(
                track["verticalRootMotion"] as JObject, sourcePath, jsonPath + ".verticalRootMotion", diagnostics);
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].Kind == CharacterActionTrackEventKind.SampleRootMotionY
                    && (verticalRootMotion == null || !verticalRootMotion.HasFrames))
                {
                    AddError(diagnostics, sourcePath, jsonPath + ".events[" + i + "]",
                        CharacterActionDiagnosticCodes.JsonInvalidConfig,
                        "SampleRootMotionY requires baked verticalRootMotion frames.");
                }
                if (results[i].Kind == CharacterActionTrackEventKind.SampleRootMotionY
                    && verticalRootMotion != null)
                {
                    for (int frame = results[i].Frame; frame <= results[i].EndFrame; frame++)
                    {
                        if (verticalRootMotion.TryGetDelta(frame, out _))
                            continue;
                        AddError(diagnostics, sourcePath, jsonPath + ".events[" + i + "]",
                            CharacterActionDiagnosticCodes.JsonInvalidFrame,
                            "SampleRootMotionY is missing a baked sample at frame " + frame + ".");
                        break;
                    }
                }
            }
            return new MotionTrackConfig(
                ReadBool(track, "usesRootMotion"),
                results.ToArray(),
                ReadRootMotionTrack(track["rootMotion"] as JObject, sourcePath, jsonPath + ".rootMotion", diagnostics),
                verticalRootMotion);
        }

        private static CharacterActionRootMotionConfig ReadRootMotionTrack(
            JObject rootMotion,
            string sourcePath,
            string jsonPath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            if (rootMotion == null)
                return null;

            if (!TryReadEnum(
                    rootMotion,
                    "space",
                    sourcePath,
                    jsonPath + ".space",
                    CharacterActionRootMotionSpace.Local,
                    diagnostics,
                    out CharacterActionRootMotionSpace space))
            {
                return null;
            }
            if (!TryReadEnum(
                    rootMotion,
                    "obstaclePolicy",
                    sourcePath,
                    jsonPath + ".obstaclePolicy",
                    CharacterActionRootMotionObstaclePolicy.Default,
                    diagnostics,
                    out CharacterActionRootMotionObstaclePolicy obstaclePolicy))
            {
                return null;
            }

            int sampleTickRate = rootMotion["sampleTickRate"] != null
                ? ReadInt(rootMotion, "sampleTickRate")
                : CharacterActionAnimationKeyUtility.TimelineFramesPerSecond;
            JArray frames = rootMotion["frames"] as JArray;
            var results = new List<CharacterActionRootMotionFrame>();
            for (int i = 0; frames != null && i < frames.Count; i++)
            {
                JObject item = frames[i] as JObject;
                string path = jsonPath + ".frames[" + i + "]";
                if (item == null)
                    continue;

                try
                {
                    int frame = item["frame"] != null
                        ? ReadInt(item, "frame")
                        : ReadInt(item, "localFrame");
                    if (frame < 0)
                    {
                        AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidFrame, "RootMotion frame cannot be negative.");
                        continue;
                    }

                    ReadRootMotionDelta(item, out float x, out float y, out float z);
                    if (Math.Abs(y) > 0.000001f)
                    {
                        AddError(diagnostics, sourcePath, path + ".y",
                            CharacterActionDiagnosticCodes.JsonInvalidConfig,
                            "RootMotion Y is no longer implicit; use SampleRootMotionY with verticalRootMotion frames.");
                    }
                    results.Add(new CharacterActionRootMotionFrame(frame, x, 0f, z));
                }
                catch (Exception ex) when (IsConfigException(ex))
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidFrame, "RootMotion frame is invalid.");
                }
            }

            try
            {
                return new CharacterActionRootMotionConfig(
                    ReadDefinitionId(rootMotion, "sourceAnimationEventId"),
                    ReadString(rootMotion, "sourceAnimationClipResourceKey"),
                    space,
                    sampleTickRate,
                    obstaclePolicy,
                    results.ToArray());
            }
            catch (Exception ex) when (IsConfigException(ex))
            {
                AddError(diagnostics, sourcePath, jsonPath, CharacterActionDiagnosticCodes.JsonInvalidConfig, "RootMotion track could not be converted to runtime config.");
                return null;
            }
        }

        private static CharacterActionVerticalRootMotionConfig ReadVerticalRootMotionTrack(
            JObject source,
            string sourcePath,
            string jsonPath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            if (source == null)
                return null;

            JArray frames = source["frames"] as JArray;
            var samples = new List<CharacterActionVerticalRootMotionFrame>();
            var seenFrames = new HashSet<int>();
            for (int i = 0; frames != null && i < frames.Count; i++)
            {
                JObject item = frames[i] as JObject;
                if (item == null)
                    continue;
                try
                {
                    int frame = ReadInt(item, "frame");
                    float y = ReadFloat(item, "y");
                    if (frame < 0 || !seenFrames.Add(frame) || float.IsNaN(y) || float.IsInfinity(y))
                    {
                        AddError(diagnostics, sourcePath, jsonPath + ".frames[" + i + "]",
                            CharacterActionDiagnosticCodes.JsonInvalidFrame,
                            "Vertical root motion sample has an invalid frame or Y value.");
                        continue;
                    }
                    samples.Add(new CharacterActionVerticalRootMotionFrame(frame, y));
                }
                catch (Exception ex) when (IsConfigException(ex))
                {
                    AddError(diagnostics, sourcePath, jsonPath + ".frames[" + i + "]",
                        CharacterActionDiagnosticCodes.JsonInvalidFrame,
                        "Vertical root motion sample is invalid.");
                }
            }
            return new CharacterActionVerticalRootMotionConfig(
                ReadDefinitionId(source, "sourceAnimationEventId"),
                ReadString(source, "sourceAnimationClipResourceKey"),
                samples.ToArray());
        }

        private static void ReadRootMotionDelta(JObject item, out float x, out float y, out float z)
        {
            JArray deltaArray = item["delta"] as JArray;
            if (deltaArray != null && deltaArray.Count >= 3)
            {
                x = ReadFloat(deltaArray[0], 0f);
                y = ReadFloat(deltaArray[1], 0f);
                z = ReadFloat(deltaArray[2], 0f);
                return;
            }

            JObject deltaObject = (item["delta"] as JObject)
                ?? (item["deltaPosition"] as JObject);
            if (deltaObject != null)
            {
                x = ReadFloat(deltaObject, "x");
                y = ReadFloat(deltaObject, "y");
                z = ReadFloat(deltaObject, "z");
                return;
            }

            x = item["x"] != null ? ReadFloat(item, "x") : ReadFloat(item, "deltaX");
            y = item["y"] != null ? ReadFloat(item, "y") : ReadFloat(item, "deltaY");
            z = item["z"] != null ? ReadFloat(item, "z") : ReadFloat(item, "deltaZ");
        }

        private static CombatTrackConfig ReadCombatTrack(
            JObject track,
            string sourcePath,
            string jsonPath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            if (track == null)
                return null;

            JArray events = track["events"] as JArray;
            var results = new List<CombatTrackEvent>();
            for (int i = 0; events != null && i < events.Count; i++)
            {
                JObject item = events[i] as JObject;
                string path = jsonPath + ".events[" + i + "]";
                if (item == null)
                    continue;
                string rawKind = ReadString(item, "kind");
                if (string.Equals(rawKind, "StartHitTrace", StringComparison.Ordinal)
                    || string.Equals(rawKind, "StopHitTrace", StringComparison.Ordinal))
                {
                    AddError(
                        diagnostics,
                        sourcePath,
                        path + ".kind",
                        CharacterActionDiagnosticCodes.JsonInvalidConfig,
                        "StartHitTrace and StopHitTrace were removed in CharacterAction schema 0.2.0; author one HitTraceWindow range instead.");
                    continue;
                }
                if (!TryReadEnum(item, "kind", sourcePath, path + ".kind", CharacterActionTrackEventKind.None, diagnostics, out CharacterActionTrackEventKind kind))
                    continue;

                try
                {
                    int stableEventId = ReadDefinitionId((item["hitEvent"] as JObject) ?? item, "stableEventId");
                    RejectLegacyDamageFields(item, sourcePath, path, diagnostics);
                    CharacterActionDamagePayload damagePayload = ReadDamagePayload(item);
                    CombatHitEventConfig hitEvent = ReadHitEventConfig(
                        item,
                        stableEventId,
                        sourcePath,
                        path,
                        diagnostics);
                    if (kind == CharacterActionTrackEventKind.HitTraceWindow
                        && !damagePayload.HasInlineData)
                    {
                        AddError(
                            diagnostics,
                            sourcePath,
                            path,
                            CharacterActionDiagnosticCodes.JsonInvalidConfig,
                            "HitTraceWindow requires hitEvent.damage common damage fields.");
                    }
                    if (kind == CharacterActionTrackEventKind.HitTraceWindow
                        && (item["endFrame"] == null || (stableEventId <= 0)))
                    {
                        AddError(
                            diagnostics,
                            sourcePath,
                            path,
                            CharacterActionDiagnosticCodes.InvalidTrackLifecycle,
                            "HitTraceWindow requires endFrame and stableEventId.");
                        continue;
                    }
                    if (kind == CharacterActionTrackEventKind.SetHitTargetState
                        && (item["endFrame"] == null || (stableEventId <= 0)))
                    {
                        AddError(
                            diagnostics,
                            sourcePath,
                            path,
                            CharacterActionDiagnosticCodes.InvalidTrackLifecycle,
                            "SetHitTargetState requires endFrame and stableEventId.");
                        continue;
                    }

                    HitTargetStateFlags targetStateFlags =
                        (HitTargetStateFlags)ReadInt(item, "targetStateMask");
                    const HitTargetStateFlags allowedTransientStates =
                        HitTargetStateFlags.Invincible
                        | HitTargetStateFlags.Parrying
                        | HitTargetStateFlags.Blocking
                        | HitTargetStateFlags.SuperArmor
                        | HitTargetStateFlags.Unhittable;
                    if (kind == CharacterActionTrackEventKind.SetHitTargetState
                        && (targetStateFlags == HitTargetStateFlags.None
                            || (targetStateFlags & ~allowedTransientStates) != 0))
                    {
                        AddError(
                            diagnostics,
                            sourcePath,
                            path + ".targetStateMask",
                            CharacterActionDiagnosticCodes.JsonInvalidConfig,
                            "SetHitTargetState requires a non-zero mask containing only transient hit-target state flags; Alive is lifecycle-owned.");
                        continue;
                    }
                    if (kind == CharacterActionTrackEventKind.SetHitTargetState
                        && ReadInt(item, "endFrame") < ReadInt(item, "frame"))
                    {
                        AddError(
                            diagnostics,
                            sourcePath,
                            path + ".endFrame",
                            CharacterActionDiagnosticCodes.InvalidTrackLifecycle,
                            "SetHitTargetState endFrame must be greater than or equal to frame.");
                        continue;
                    }

                    results.Add(new CombatTrackEvent(
                        ReadInt(item, "frame"),
                        kind,
                        ReadDefinitionId(item, "combatActionId"),
                        ReadDefinitionId(item, "traceProfileId"),
                        stableEventId,
                        ReadDefinitionId(item, "equipmentSlotId"),
                        damagePayload,
                        hitEvent,
                        ReadHitVolumeConfig(
                            item["hitVolume"] as JObject,
                            ReadDefinitionId(item, "equipmentSlotId"),
                            ReadDefinitionId(item, "traceProfileId"),
                            sourcePath,
                            path + ".hitVolume",
                            diagnostics),
                        kind == CharacterActionTrackEventKind.HitTraceWindow
                            || kind == CharacterActionTrackEventKind.SetHitTargetState
                            ? ReadInt(item, "endFrame")
                            : ReadInt(item, "frame"),
                        ReadDefinitionId(item, "requiredPartStateId"),
                        (CharacterActionPartStructureStateMask)ReadInt(
                            item,
                            "allowedPartStructureStateMask"),
                        targetStateFlags, ReadString(item, "requiredPartStateLabel"), stableEventOrder: ReadStableEventOrder(item)));
                }
                catch (Exception ex) when (IsConfigException(ex))
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidFrame, "Combat track event is invalid.");
                }
            }

            return new CombatTrackConfig(ReadDefinitionId(track, "combatActionId"), results.ToArray());
        }

        private static GameplayTrackConfig ReadGameplayTrack(
            JObject track,
            string sourcePath,
            string jsonPath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            if (track == null)
                return null;

            JArray events = track["events"] as JArray;
            var results = new List<GameplayTrackEvent>();
            for (int i = 0; events != null && i < events.Count; i++)
            {
                JObject item = events[i] as JObject;
                string path = jsonPath + ".events[" + i + "]";
                if (item == null)
                    continue;
                if (!TryReadEnum(item, "kind", sourcePath, path + ".kind", CharacterActionTrackEventKind.None, diagnostics, out CharacterActionTrackEventKind kind))
                    continue;

                try
                {
                    int stableEventId = ReadDefinitionId(item, "stableEventId");
                    RejectLegacyDamageFields(item, sourcePath, path, diagnostics);
                    CharacterActionDamagePayload damagePayload = ReadDamagePayload(item);
                    CombatHitEventConfig hitEvent = ReadHitEventConfig(
                        item,
                        stableEventId,
                        sourcePath,
                        path,
                        diagnostics);
                    if (kind == CharacterActionTrackEventKind.ApplyGameplayEffect
                        && ReadDefinitionId(item, "requestId") <= 0
                        && !damagePayload.HasInlineData)
                    {
                        AddError(
                            diagnostics,
                            sourcePath,
                            path,
                            CharacterActionDiagnosticCodes.JsonInvalidConfig,
                            "Damage-producing ApplyGameplayEffect requires hitEvent.damage common damage fields.");
                    }

                    results.Add(new GameplayTrackEvent(
                        ReadInt(item, "frame"),
                        kind,
                        ReadDefinitionId(item, "requestId"),
                        ReadDefinitionId(item, "abilityStableId"),
                        stableEventId,
                        ReadDefinitionId(item, "targetPolicyId"),
                        ReadDefinitionId(item, "effectTimelineId"),
                        damagePayload,
                        hitEvent, stableEventOrder: ReadStableEventOrder(item)));
                }
                catch (Exception ex) when (IsConfigException(ex))
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidFrame, "Gameplay track event is invalid.");
                }
            }

            return new GameplayTrackConfig(results.ToArray());
        }

        private static AnimationTrackConfig ReadAnimationTrack(
            JObject track,
            string sourcePath,
            string jsonPath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            if (track == null)
                return null;

            JArray events = track["events"] as JArray;
            var results = new List<AnimationTrackEvent>();
            for (int i = 0; events != null && i < events.Count; i++)
            {
                JObject item = events[i] as JObject;
                string path = jsonPath + ".events[" + i + "]";
                if (item == null)
                    continue;
                if (!TryReadEnum(item, "kind", sourcePath, path + ".kind", CharacterActionTrackEventKind.None, diagnostics, out CharacterActionTrackEventKind kind))
                    continue;

                try
                {
                    int frame = ReadInt(item, "frame");
                    int endFrame = item["endFrame"] != null ? ReadInt(item, "endFrame") : frame;
                    float playbackSpeed = item["playbackSpeed"] != null ? ReadFloat(item, "playbackSpeed") : 1f;
                    results.Add(new AnimationTrackEvent(
                        frame,
                        kind,
                        ReadString(item, "animationActionKey"),
                        ReadFloat(item, "transitionSeconds"),
                        ReadDefinitionId(item, "stableEventId"),
                        endFrame,
                        playbackSpeed,
                        ReadString(item, "animationClipResourceKey"),
                        item["animationStartOffsetFrames"] != null ? ReadInt(item, "animationStartOffsetFrames") : 0,
                        item["animationEndOffsetFrames"] != null ? ReadInt(item, "animationEndOffsetFrames") : 0, stableEventOrder: ReadStableEventOrder(item)));
                }
                catch (Exception ex) when (IsConfigException(ex))
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidFrame,
                        "Animation track event is invalid: " + ex.Message);
                }
            }

            return new AnimationTrackConfig(results.ToArray());
        }

        private static PresentationTrackConfig ReadPresentationTrack(
            JObject track,
            string sourcePath,
            string jsonPath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            if (track == null)
                return null;

            JArray events = track["events"] as JArray;
            var results = new List<PresentationTrackEvent>();
            for (int i = 0; events != null && i < events.Count; i++)
            {
                JObject item = events[i] as JObject;
                string path = jsonPath + ".events[" + i + "]";
                if (item == null)
                    continue;
                if (!TryReadEnum(item, "kind", sourcePath, path + ".kind", CharacterActionTrackEventKind.None, diagnostics, out CharacterActionTrackEventKind kind))
                    continue;

                try
                {
                    int frame = ReadInt(item, "frame");
                    int endFrame = item["endFrame"] != null ? ReadInt(item, "endFrame") : frame;
                    JObject attachment = item["vfxAttachment"] as JObject;
                    JObject transform = attachment?["transform"] as JObject;
                    MxAttachmentMode attachmentMode = ReadPresentationEnum(
                        attachment,
                        "mode",
                        MxAttachmentMode.FollowSubject);
                    MxTransformSpace offsetSpace = ReadPresentationEnum(
                        attachment,
                        "offsetSpace",
                        MxTransformSpace.Local);
                    CharacterActionVfxPlaybackMode vfxPlaybackMode = ReadPresentationEnum(
                        item,
                        "vfxPlaybackMode",
                        CharacterActionVfxPlaybackMode.PlayToCompletion);
                    CharacterActionVfxStopPolicy vfxStopPolicy = ReadPresentationEnum(
                        item,
                        "vfxNaturalEndPolicy",
                        CharacterActionVfxStopPolicy.Complete);
                    if (kind == CharacterActionTrackEventKind.SpawnVisualCue
                        && (ReadDefinitionId(item, "vfxDefinitionId") <= 0
                            || (attachmentMode == MxAttachmentMode.FollowSocket
                                && ReadDefinitionId(item, "numericSocketId") <= 0)))
                        throw new ArgumentException("Visual cue requires authored VfxDefinitionId and, for FollowSocket, NumericSocketId.");
                    results.Add(new PresentationTrackEvent(
                        frame,
                        kind,
                        ReadDefinitionId(item, "cueId"),
                        ReadString(item, "resourceKey"),
                        ReadDefinitionId(item, "stableEventId"),
                        endFrame,
                        new CharacterActionVfxEventConfig(
                            new MxAttachmentTransform(
                                attachmentMode,
                                ReadString(attachment, "socketId"),
                                offsetSpace,
                                new MxTransformOffset(
                                    ReadFloat(transform?["positionX"], 0f),
                                    ReadFloat(transform?["positionY"], 0f),
                                    ReadFloat(transform?["positionZ"], 0f),
                                    ReadFloat(transform?["rotationEulerX"], 0f),
                                    ReadFloat(transform?["rotationEulerY"], 0f),
                                    ReadFloat(transform?["rotationEulerZ"], 0f),
                                    ReadFloat(transform?["scaleX"], 1f),
                                    ReadFloat(transform?["scaleY"], 1f),
                                    ReadFloat(transform?["scaleZ"], 1f))),
                            vfxPlaybackMode,
                            ReadFloat(item["vfxPlaybackRate"], 1f),
                            ReadFloat(item["vfxScaleMultiplier"], 1f),
                            ReadInt(item, "vfxStartOffsetFrames"),
                            ReadInt(item, "vfxEndOffsetFrames"),
                            vfxStopPolicy,
                            item["vfxParameter0"] != null ? ReadInt(item, "vfxParameter0") : 0,
                            item["vfxParameter1"] != null ? ReadInt(item, "vfxParameter1") : 0,
                            item["vfxParameter2"] != null ? ReadInt(item, "vfxParameter2") : 0,
                            item["vfxParameter3"] != null ? ReadInt(item, "vfxParameter3") : 0,
                            ReadDefinitionId(item, "vfxDefinitionId"),
                            ReadDefinitionId(item, "numericSocketId"),
                            item.Value<bool?>("useWeaponBladeTrailStyle") ?? false), stableEventOrder: ReadStableEventOrder(item)));
                }
                catch (Exception ex) when (IsConfigException(ex))
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidFrame, "Presentation track event is invalid: " + ex.Message);
                }
            }

            return new PresentationTrackConfig(results.ToArray());
        }

        private static TEnum ReadPresentationEnum<TEnum>(JObject item, string name, TEnum fallback)
            where TEnum : struct
        {
            if (item == null)
                return fallback;

            string value = ReadString(item, name);
            return Enum.TryParse(value, ignoreCase: true, out TEnum parsed) ? parsed : fallback;
        }

        private static DebugTrackConfig ReadDebugTrack(
            JObject track,
            string sourcePath,
            string jsonPath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            if (track == null)
                return null;

            JArray events = track["events"] as JArray;
            var results = new List<DebugTrackEvent>();
            for (int i = 0; events != null && i < events.Count; i++)
            {
                JObject item = events[i] as JObject;
                string path = jsonPath + ".events[" + i + "]";
                if (item == null)
                    continue;
                if (!TryReadEnum(item, "kind", sourcePath, path + ".kind", CharacterActionTrackEventKind.None, diagnostics, out CharacterActionTrackEventKind kind))
                    continue;

                try
                {
                    results.Add(new DebugTrackEvent(ReadInt(item, "frame"), kind, ReadString(item, "markerId"), ReadDefinitionId(item, "stableEventId"), stableEventOrder: ReadStableEventOrder(item)));
                }
                catch (Exception ex) when (IsConfigException(ex))
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidFrame, "Debug track event is invalid.");
                }
            }

            return new DebugTrackConfig(results.ToArray());
        }

        private static CharacterActionDamagePayload ReadDamagePayload(JObject item)
        {
            if (item == null)
                return CharacterActionDamagePayload.Empty;

            JObject hitEvent = item["hitEvent"] as JObject;
            JObject damage = hitEvent?["damage"] as JObject;
            return ReadDamagePayloadFromObject(
                damage,
                0,
                includeLegacyFormulaFields: true);
        }

        private static void RejectLegacyDamageFields(
            JObject item,
            string sourcePath,
            string jsonPath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            if (item == null)
                return;

            if (item["damageProfileId"] != null)
            {
                AddError(
                    diagnostics,
                    sourcePath,
                    jsonPath + ".damageProfileId",
                    CharacterActionDiagnosticCodes.JsonInvalidConfig,
                    "damageProfileId is no longer supported. Use hitEvent.damage.");
            }

            if (HasLegacyTopLevelDamageFields(item))
            {
                AddError(
                    diagnostics,
                    sourcePath,
                    jsonPath,
                    CharacterActionDiagnosticCodes.JsonInvalidConfig,
                    "Top-level damage fields are no longer supported. Move them to hitEvent.damage.");
            }
        }

        private static bool HasLegacyTopLevelDamageFields(JObject item)
        {
            return item["damageTypeId"] != null
                || item["formulaId"] != null
                || item["baseDamage"] != null
                || item["sourceAttributeScales"] != null
                || item["impactPower"] != null
                || item["launchPower"] != null
                || item["tags"] != null
                || item["analysisCategory"] != null
                || item["presentationProfileId"] != null;
        }

        private static CharacterActionDamagePayload ReadDamagePayloadFromObject(
            JObject source,
            int presentationProfileId,
            bool includeLegacyFormulaFields)
        {
            if (source == null)
                return CharacterActionDamagePayload.Empty;

            return new CharacterActionDamagePayload(
                ReadDefinitionId(source, "damageTypeId"),
                includeLegacyFormulaFields ? ReadDefinitionId(source, "formulaId") : 0,
                ReadInt(source, "baseDamage"),
                ReadDamageAttributeScales(source["sourceAttributeScales"] as JArray),
                ReadInt(source, "impactPower"),
                ReadDefinitionIds(source["tags"] as JArray),
                ReadDefinitionId(source, "analysisCategory"),
                presentationProfileId,
                ReadInt(source, "launchPower"),
                damageTypeLabel: ReadString(source, "damageTypeLabel"),
                formulaLabel: ReadString(source, "formulaLabel"),
                analysisCategoryLabel: ReadString(source, "analysisCategoryLabel"),
                presentationProfileLabel: ReadString(source, "presentationProfileLabel"),
                tagLabels: ReadStringArray(source["tagLabels"] as JArray));
        }

        private static int ReadDefinitionId(JObject source, string field)
        {
            JToken token = source?[field];
            if (token == null || token.Type == JTokenType.Null) return 0;
            if (token.Type != JTokenType.Integer)
                throw new FormatException("Expected a nonnegative numeric definition identity: " + field);
            long value = token.Value<long>();
            if (value < 0 || value > int.MaxValue)
                throw new FormatException("Numeric definition identity is outside Int32: " + field);
            return (int)value;
        }

        private static int[] ReadDefinitionIds(JArray values)
        {
            if (values == null || values.Count == 0) return Array.Empty<int>();
            var result = new int[values.Count];
            for (int i = 0; i < result.Length; i++)
            {
                JToken token = values[i];
                if (token == null || token.Type != JTokenType.Integer)
                    throw new FormatException("Definition identity arrays require numeric values.");
                result[i] = token.Value<int>();
                if (result[i] <= 0) throw new FormatException("Definition identity arrays require positive values.");
            }
            return result;
        }

        private static CombatHitEventConfig ReadHitEventConfig(
            JObject item,
            int stableEventId,
            string sourcePath,
            string jsonPath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            JObject hitEvent = item?["hitEvent"] as JObject;
            CombatHitTargetTypes targetTypes = CombatHitTargetTypes.HostileCharacter;
            CombatEnvironmentHitPolicy environmentPolicy = CombatEnvironmentHitPolicy.None;
            CombatHitSameTargetPolicy sameTargetPolicy = CombatHitSameTargetPolicy.AllowRepeat;
            int maxAcceptedHits = 1;

            if (hitEvent != null)
            {
                targetTypes = ReadHitTargetTypes(
                    hitEvent["targetTypes"],
                    sourcePath,
                    jsonPath + ".hitEvent.targetTypes",
                    diagnostics);
                environmentPolicy = ReadHitEventEnum(
                    hitEvent,
                    "environmentHitPolicy",
                    CombatEnvironmentHitPolicy.None,
                    sourcePath,
                    jsonPath + ".hitEvent.environmentHitPolicy",
                    diagnostics);
                sameTargetPolicy = ReadHitEventEnum(
                    hitEvent,
                    "hitSameTargetPolicy",
                    CombatHitSameTargetPolicy.AllowRepeat,
                    sourcePath,
                    jsonPath + ".hitEvent.hitSameTargetPolicy",
                    diagnostics);
                maxAcceptedHits = ReadInt(hitEvent, "maxAcceptedHits");
                if (maxAcceptedHits <= 0)
                    maxAcceptedHits = 1;
            }

            return new CombatHitEventConfig(
                targetTypes,
                environmentPolicy,
                stableEventId,
                maxAcceptedHits,
                sameTargetPolicy,
                ReadHitDamageConfig(hitEvent),
                ReadOnHitBuffs(
                    hitEvent?["onHitBuffs"] as JArray,
                    sourcePath,
                    jsonPath + ".hitEvent.onHitBuffs",
                    diagnostics));
        }

        private static CharacterActionHitVolumeConfig ReadHitVolumeConfig(
            JObject hitVolume,
            int equipmentSlotId,
            int traceProfileId,
            string sourcePath,
            string jsonPath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            if (hitVolume == null)
                return default;

            CharacterActionHitVolumeSourceKind sourceKind = CharacterActionHitVolumeSourceKind.ManualCapsule;
            string sourceText = ReadString(hitVolume, "sourceKind");
            if (!string.IsNullOrWhiteSpace(sourceText)
                && (!Enum.TryParse(sourceText, false, out sourceKind)
                    || !Enum.IsDefined(typeof(CharacterActionHitVolumeSourceKind), sourceKind)))
            {
                AddWarning(
                    diagnostics,
                    sourcePath,
                    jsonPath + ".sourceKind",
                    CharacterActionDiagnosticCodes.JsonInvalidConfig,
                    "hitVolume source kind is unsupported: " + sourceText + ".");
                return default;
            }

            CharacterActionHitVolumeShapeKind shapeKind = CharacterActionHitVolumeShapeKind.Capsule;
            string shapeText = ReadString(hitVolume, "shapeKind");
            if (!string.IsNullOrWhiteSpace(shapeText)
                && (!Enum.TryParse(shapeText, false, out shapeKind)
                    || !Enum.IsDefined(typeof(CharacterActionHitVolumeShapeKind), shapeKind)))
            {
                AddWarning(
                    diagnostics,
                    sourcePath,
                    jsonPath + ".shapeKind",
                    CharacterActionDiagnosticCodes.JsonInvalidConfig,
                    "hitVolume shape kind is unsupported: " + shapeText + ".");
                return default;
            }

            if (sourceKind == CharacterActionHitVolumeSourceKind.CasterBody)
            {
                ReadHitVolumePoint(hitVolume["bodyOffset"], out float bodyOffsetX, out float bodyOffsetY, out float bodyOffsetZ);
                float bodyRadiusScale = hitVolume["bodyRadiusScale"] != null
                    ? ReadFloat(hitVolume, "bodyRadiusScale")
                    : 1f;
                float bodyRadiusAdd = ReadFloat(hitVolume, "bodyRadiusAdd");
                float bodyHeightScale = hitVolume["bodyHeightScale"] != null
                    ? ReadFloat(hitVolume, "bodyHeightScale")
                    : 1f;
                float bodyHeightAdd = ReadFloat(hitVolume, "bodyHeightAdd");
                if (bodyRadiusScale <= 0f)
                {
                    AddWarning(
                        diagnostics,
                        sourcePath,
                        jsonPath + ".bodyRadiusScale",
                        CharacterActionDiagnosticCodes.JsonInvalidConfig,
                        "hitVolume bodyRadiusScale must be positive.");
                    return default;
                }
                if (bodyHeightScale <= 0f)
                {
                    AddWarning(
                        diagnostics,
                        sourcePath,
                        jsonPath + ".bodyHeightScale",
                        CharacterActionDiagnosticCodes.JsonInvalidConfig,
                        "hitVolume bodyHeightScale must be positive.");
                    return default;
                }

                return CharacterActionHitVolumeConfig.CasterBody(
                    bodyOffsetX,
                    bodyOffsetY,
                    bodyOffsetZ,
                    bodyRadiusScale,
                    bodyRadiusAdd,
                    bodyHeightScale,
                    bodyHeightAdd);
            }

            if (sourceKind == CharacterActionHitVolumeSourceKind.BakedSocketCapsule)
            {
                int startSocketId = ReadDefinitionId(hitVolume, "startSocketId");
                int endSocketId = ReadDefinitionId(hitVolume, "endSocketId");
                float bakedRadius = ReadFloat(hitVolume, "radius");
                bool hasConflictingFields = (equipmentSlotId > 0)
                    || (traceProfileId > 0)
                    || hitVolume["localPointA"] != null
                    || hitVolume["localPointB"] != null
                    || hitVolume["bodyOffset"] != null
                    || hitVolume["bodyRadiusScale"] != null
                    || hitVolume["bodyRadiusAdd"] != null
                    || hitVolume["bodyHeightScale"] != null
                    || hitVolume["bodyHeightAdd"] != null;
                if ((startSocketId <= 0)
                    || (endSocketId <= 0)
                    || bakedRadius <= 0f
                    || hasConflictingFields)
                {
                    AddWarning(
                        diagnostics,
                        sourcePath,
                        jsonPath,
                        CharacterActionDiagnosticCodes.JsonInvalidConfig,
                        "BakedSocketCapsule requires startSocketId, endSocketId and a positive radius, and cannot include manual points, body modifiers, equipmentSlotId or traceProfileId.");
                    return default;
                }

                return CharacterActionHitVolumeConfig.BakedSocketCapsule(
                    startSocketId,
                    endSocketId,
                    bakedRadius);
            }

            ReadHitVolumePoint(hitVolume["localPointA"], out float ax, out float ay, out float az);
            ReadHitVolumePoint(hitVolume["localPointB"], out float bx, out float by, out float bz);
            float radius = ReadFloat(hitVolume, "radius");
            if (radius <= 0f)
            {
                AddWarning(
                    diagnostics,
                    sourcePath,
                    jsonPath + ".radius",
                    CharacterActionDiagnosticCodes.JsonInvalidConfig,
                    "hitVolume radius must be positive.");
                return default;
            }

            return new CharacterActionHitVolumeConfig(
                sourceKind,
                shapeKind,
                ax,
                ay,
                az,
                bx,
                by,
                bz,
                radius);
        }

        private static void ReadHitVolumePoint(JToken token, out float x, out float y, out float z)
        {
            x = 0f;
            y = 0f;
            z = 0f;
            if (token is JArray array)
            {
                if (array.Count > 0)
                    x = ReadFloat(array[0], 0f);
                if (array.Count > 1)
                    y = ReadFloat(array[1], 0f);
                if (array.Count > 2)
                    z = ReadFloat(array[2], 0f);
                return;
            }

            if (token is JObject obj)
            {
                x = ReadFloat(obj, "x");
                y = ReadFloat(obj, "y");
                z = ReadFloat(obj, "z");
            }
        }

        private static CombatHitBuffApplicationConfig[] ReadOnHitBuffs(
            JArray array,
            string sourcePath,
            string jsonPath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            if (array == null || array.Count == 0)
                return Array.Empty<CombatHitBuffApplicationConfig>();

            var values = new List<CombatHitBuffApplicationConfig>(array.Count);
            var seenBuffIds = new HashSet<int>();
            for (int i = 0; i < array.Count; i++)
            {
                JObject item = array[i] as JObject;
                string path = jsonPath + "[" + i + "]";
                if (item == null)
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidConfig, "onHitBuffs entry must be an object.");
                    continue;
                }

                int buffId = ReadInt(item, "buffId");
                if (buffId <= 0)
                {
                    AddError(diagnostics, sourcePath, path + ".buffId", CharacterActionDiagnosticCodes.JsonInvalidConfig, "onHitBuffs buffId must be positive.");
                    continue;
                }

                if (item["durationOverrideFrames"] != null)
                {
                    AddError(diagnostics, sourcePath, path + ".durationOverrideFrames", CharacterActionDiagnosticCodes.JsonInvalidConfig, "onHitBuffs durationOverrideFrames is not supported; create a separate BuffId with its own duration.");
                    continue;
                }

                if (item["sourceIdOverride"] != null)
                {
                    AddError(diagnostics, sourcePath, path + ".sourceIdOverride", CharacterActionDiagnosticCodes.JsonInvalidConfig, "onHitBuffs sourceIdOverride is not supported; source context comes from the hit result.");
                    continue;
                }

                if (!seenBuffIds.Add(buffId))
                {
                    AddError(diagnostics, sourcePath, path + ".buffId", CharacterActionDiagnosticCodes.JsonInvalidConfig, "onHitBuffs cannot repeat the same buffId in one hitEvent.");
                    continue;
                }

                values.Add(new CombatHitBuffApplicationConfig(buffId));
            }

            return values.ToArray();
        }

        private static CombatHitDamageConfig ReadHitDamageConfig(JObject hitEvent)
        {
            JObject damage = hitEvent?["damage"] as JObject;
            if (damage == null)
                return default;

            return new CombatHitDamageConfig(
                ReadDefinitionId(damage, "damageTypeId"),
                ReadInt(damage, "baseDamage"),
                ReadInt(damage, "impactPower"),
                ReadDefinitionIds(damage["tags"] as JArray),
                ReadDefinitionId(damage, "analysisCategory"),
                ReadInt(damage, "launchPower"));
        }

        private static string ReadHitEventString(JObject item, string name)
        {
            if (item == null)
                return string.Empty;

            JObject hitEvent = item["hitEvent"] as JObject;
            JObject damage = hitEvent?["damage"] as JObject;
            string value = ReadString(damage, name);
            if (!string.IsNullOrWhiteSpace(value))
                return value;

            value = ReadString(hitEvent, name);
            return string.IsNullOrWhiteSpace(value) ? ReadString(item, name) : value;
        }

        private static CombatHitTargetTypes ReadHitTargetTypes(
            JToken token,
            string sourcePath,
            string jsonPath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            if (token == null)
                return CombatHitTargetTypes.HostileCharacter;

            CombatHitTargetTypes result = CombatHitTargetTypes.None;
            if (token.Type == JTokenType.String)
            {
                AddHitTargetType((string)token, ref result, sourcePath, jsonPath, diagnostics);
            }
            else if (token is JArray array)
            {
                for (int i = 0; i < array.Count; i++)
                    AddHitTargetType((string)array[i], ref result, sourcePath, jsonPath + "[" + i + "]", diagnostics);
            }
            else
            {
                AddWarning(
                    diagnostics,
                    sourcePath,
                    jsonPath,
                    CharacterActionDiagnosticCodes.JsonInvalidConfig,
                    "hitEvent.targetTypes should be a string or string array.");
            }

            return result == CombatHitTargetTypes.None ? CombatHitTargetTypes.HostileCharacter : result;
        }

        private static void AddHitTargetType(
            string value,
            ref CombatHitTargetTypes result,
            string sourcePath,
            string jsonPath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (Enum.TryParse(value, false, out CombatHitTargetTypes parsed)
                && Enum.IsDefined(typeof(CombatHitTargetTypes), parsed)
                && parsed != CombatHitTargetTypes.None)
            {
                result |= parsed;
                return;
            }

            AddWarning(
                diagnostics,
                sourcePath,
                jsonPath,
                CharacterActionDiagnosticCodes.JsonInvalidConfig,
                "hitEvent target type is unsupported: " + value + ".");
        }

        private static TEnum ReadHitEventEnum<TEnum>(
            JObject source,
            string name,
            TEnum fallback,
            string sourcePath,
            string jsonPath,
            List<CharacterActionJsonDiagnostic> diagnostics)
            where TEnum : struct
        {
            string value = ReadString(source, name);
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            if (Enum.TryParse(value, false, out TEnum parsed) && Enum.IsDefined(typeof(TEnum), parsed))
                return parsed;

            AddWarning(
                diagnostics,
                sourcePath,
                jsonPath,
                CharacterActionDiagnosticCodes.JsonInvalidConfig,
                "hitEvent enum value is unsupported: " + value + ".");
            return fallback;
        }

        private static DamageAttributeScale[] ReadDamageAttributeScales(JArray array)
        {
            if (array == null || array.Count == 0)
                return Array.Empty<DamageAttributeScale>();

            var results = new List<DamageAttributeScale>();
            for (int i = 0; i < array.Count; i++)
            {
                JObject item = array[i] as JObject;
                if (item == null)
                    continue;

                int attributeId = ReadInt(item, "attributeId");
                float scale = ReadFloat(item, "scale");
                if (attributeId <= 0 || scale.Equals(0f))
                    continue;

                results.Add(new DamageAttributeScale(attributeId, scale));
            }

            return results.ToArray();
        }

    }
}
