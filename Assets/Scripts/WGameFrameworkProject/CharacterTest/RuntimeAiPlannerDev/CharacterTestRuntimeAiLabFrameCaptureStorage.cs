using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>One owned history slot. Allocated during session preparation and published exactly once.</summary>
    internal sealed class CharacterTestRuntimeAiLabFrameCaptureStorage
    {
        internal readonly CharacterTestRuntimeAiPlannerSnapshot Display;
        internal readonly CharacterTestRuntimeAiPlannerCommandModule.StateCaptureStorage State;
        internal readonly CharacterTestRuntimeAiPlannerDevFrameSnapshot Frame = new CharacterTestRuntimeAiPlannerDevFrameSnapshot();
        internal readonly CharacterTestRuntimeAiPlannerDevTimelineRecord Record = new CharacterTestRuntimeAiPlannerDevTimelineRecord();
        internal readonly CharacterTestRuntimeAiLabCommittedFrameBundle Bundle = new CharacterTestRuntimeAiLabCommittedFrameBundle();
        internal readonly CharacterTestAnimationInputPresentationState[] AnimationInputs;
        private readonly CharacterTestRuntimeAiPlannerAgentSnapshot[] _agents;
        private RuntimeCommand[] _commands;

        internal CharacterTestRuntimeAiLabFrameCaptureStorage(
            CharacterTestRuntimeAiPlannerCommandModule module, int entityCount, int commandCapacity)
        {
            Display = module.CreateSnapshot();
            _agents = (CharacterTestRuntimeAiPlannerAgentSnapshot[])Display.Agents;
            State = new CharacterTestRuntimeAiPlannerCommandModule.StateCaptureStorage(module);
            AnimationInputs = new CharacterTestAnimationInputPresentationState[entityCount];
            _commands = new RuntimeCommand[commandCapacity];
        }

        internal bool IsCompatible(CharacterTestRuntimeAiPlannerCommandModule module, int entityCount)
            => _agents.Length == module.SnapshotAgentCount && AnimationInputs.Length == entityCount && State.IsCompatible(module);

        internal void Capture(CharacterTestRuntimeAiPlannerCommandModule module,
            CharacterTestCharacterRuntimeModule characters, int frame,
            CharacterTestRuntimeAiLabFrameSyncRuntimeEventSample events)
        {
            if (Frame.IsInitialized) throw new InvalidOperationException("A published capture cannot be overwritten.");
            module.CopySnapshotAgentsTo(_agents);
            State.Capture(module);
            characters.CopyRuntimeAiLabAnimationInputsTo(AnimationInputs);
            Frame.Initialize(frame, Time.realtimeSinceStartup, Display, events, State.Snapshot);
        }

        internal CharacterTestRuntimeAiLabFrozenArray<RuntimeCommand> CopyCommands(IReadOnlyList<RuntimeCommand> commands)
        {
            int count = commands?.Count ?? 0;
            if (count > _commands.Length)
                Array.Resize(ref _commands, count);
            for (int i = 0; i < count; i++) _commands[i] = commands[i];
            return new CharacterTestRuntimeAiLabFrozenArray<RuntimeCommand>(_commands, count);
        }
    }

    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private CharacterTestRuntimeAiLabFrameCaptureStorage[] _frameCaptureStorage;
        private int _nextFrameCaptureStorage;
        private CharacterTestRuntimeAiLabFrameCaptureStorage _activeFrameCapture;

        private void PrepareFrameCaptureStorage()
        {
            using var startupProfile = CharacterTestStartupProfiler.PrepareFrameStorage.Auto();
            var slice = _context?.Slice;
            var module = slice?.RuntimeAiPlannerCommands;
            _activeFrameCapture = null;
            _nextFrameCaptureStorage = 0;
            if (module == null || slice.CharacterRuntime == null)
            {
                _frameCaptureStorage = null;
                return;
            }
            var commands = slice.Services.Modules.RuntimeCommands.CommandBuffer;
            int commandCapacity = commands.IsPendingCapacityBounded ? commands.PendingCapacityLimit
                : Math.Max(64, slice.CharacterRuntime.Entities.Count * 8);
            if (_context.NetworkRunner is CharacterTestNetworkSessionRunner sessionRunner)
                sessionRunner.PrepareReplayCaptureCapacity(_frameLimit, commandCapacity);
            int count = _timelineSessionStarted ? Math.Max(1, _frameLimit - (_timeline?.Count ?? 0)) : 1;
            _frameCaptureStorage = new CharacterTestRuntimeAiLabFrameCaptureStorage[count];
            for (int i = 0; i < count; i++)
                _frameCaptureStorage[i] = new CharacterTestRuntimeAiLabFrameCaptureStorage(
                    module, slice.CharacterRuntime.Entities.Count, commandCapacity);
        }

        private CharacterTestRuntimeAiLabFrameCaptureStorage AcquireFrameCaptureStorage(
            CharacterTestRuntimeAiPlannerCommandModule module, int entityCount)
        {
            // A branch, a new composition, or a changed roster needs new ownership. Old published objects are never recycled.
            if (_frameCaptureStorage == null || _nextFrameCaptureStorage >= _frameCaptureStorage.Length
                || !_frameCaptureStorage[_nextFrameCaptureStorage].IsCompatible(module, entityCount))
                PrepareFrameCaptureStorage();
            return _frameCaptureStorage[_nextFrameCaptureStorage++];
        }
    }
}
