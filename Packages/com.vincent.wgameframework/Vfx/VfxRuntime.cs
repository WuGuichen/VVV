using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MxFramework.Diagnostics;
using MxFramework.Resources;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Vfx
{
    internal static class VfxRuntimeHashFields
    {
        public const int Frame = 1;
        public const int Sourcedomain = 2;
        public const int Sourceframe = 3;
        public const int Localframe = 4;
        public const int Sourceorder = 5;
        public const int Triggerid = 6;
        public const int Sourceid = 7;
        public const int Targetid = 8;
        public const int Definitionid = 9;
        public const int Lifetimeoverride = 10;
        public const int Sequence = 11;
        public const int Expirestopmode = 12;
        public const int Playbackratepermille = 13;
        public const int Sourcestartoffsetframes = 14;
        public const int Sourceendoffsetframes = 15;
        public const int Instanceid = 16;
        public const int Stopmode = 17;
        public const int Reason = 18;
        public const int Definitionhash = 19;
        public const int Prefab = 20;
        public const int Startframe = 21;
        public const int Expireframe = 22;
        public const int Reasoncode = 23;
        public const int Reasonvalue = 24;
        public const int Definitioncount = 26;
        public const int Nextinstanceid = 27;
        public const int Nextrequestsequence = 28;
        public const int Activecount = 29;
        public const int Pendingspawncount = 30;
        public const int Pendingstopcount = 31;
        public const int Pendingrebuildcount = 32;
        public const int Definitionstablehash = 33;
    }

    public static class VfxRuntimeCommandIds
    {
        public const int Spawn = 710001;
        public const int Stop = 710002;
        public const int Rebuild = 710003;
    }

    /// <summary>Selects the view-only exit phase for an authoritative VFX stop.</summary>
    public enum VfxStopMode
    {
        Complete = 0,
        Dissipate = 1,
        Immediate = 2
    }

    /// <summary>Stable source for a stop reason. Custom text is diagnostic only and is prepared once.</summary>
    public enum VfxStopReasonCode
    {
        Custom = 0,
        Command = 1,
        LifetimeExpired = 2,
        UnknownInstance = 3,
        UnknownOrStoppedInstance = 4,
        UnsupportedCommand = 5,
        SourceRemoved = 6
    }

    public static class VfxRuntimeCommandFactory
    {
        public static RuntimeCommand Spawn(
            RuntimeFrame frame,
            int sourceId,
            VfxDefinitionId definitionId,
            int targetId = 0,
            int parameter0 = 0,
            RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                VfxRuntimeCommandIds.Spawn,
                targetId,
                definitionId.Value,
                parameter0,
                0,
                traceId);
        }

        public static RuntimeCommand Stop(RuntimeFrame frame, VfxInstanceId instanceId, RuntimeTraceIdentity traceId = default)
        {
            return Stop(frame, instanceId, VfxStopMode.Complete, traceId);
        }

        public static RuntimeCommand Stop(RuntimeFrame frame, VfxInstanceId instanceId, VfxStopMode stopMode, RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(
                frame,
                sourceId: 0,
                commandId: VfxRuntimeCommandIds.Stop,
                targetId: unchecked((int)(instanceId.Value & 0xFFFFFFFFL)),
                payload0: unchecked((int)((ulong)instanceId.Value >> 32)),
                payload1: (int)stopMode,
                payload2: 0,
                traceId: traceId);
        }

        /// <summary>Creates an allocation-free command that rebuilds one active VFX instance.</summary>
        public static RuntimeCommand Rebuild(RuntimeFrame frame, VfxInstanceId instanceId, RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(
                frame,
                sourceId: 0,
                commandId: VfxRuntimeCommandIds.Rebuild,
                targetId: unchecked((int)(instanceId.Value & 0xFFFFFFFFL)),
                payload0: unchecked((int)((ulong)instanceId.Value >> 32)),
                payload1: 0,
                payload2: 0,
                traceId: traceId);
        }

        public static VfxInstanceId DecodeStopInstanceId(RuntimeCommand command)
        {
            long low = (uint)command.TargetId;
            long high = (long)(uint)command.Payload0 << 32;
            return new VfxInstanceId(high | low);
        }

        public static VfxStopMode DecodeStopMode(RuntimeCommand command)
        {
            return (VfxStopMode)command.Payload1;
        }

        public static VfxInstanceId DecodeRebuildInstanceId(RuntimeCommand command)
        {
            return DecodeStopInstanceId(command);
        }
    }

    public sealed class VfxRuntimeCommandValidator : IRuntimeCommandValidator
    {
        private readonly VfxDefinitionRegistry _definitions;

        public VfxRuntimeCommandValidator(VfxDefinitionRegistry definitions)
        {
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        }

        public RuntimeCommandValidationResult Validate(RuntimeCommand command)
        {
            switch (command.CommandId)
            {
                case VfxRuntimeCommandIds.Spawn:
                    var definitionId = new VfxDefinitionId(command.Payload0);
                    if (!definitionId.IsValid)
                        return Invalid(command, "VFX spawn command definition id must be positive.");
                    if (!_definitions.TryGet(definitionId, out var _))
                        return Invalid(command, "VFX spawn command references an unknown definition.");
                    return RuntimeCommandValidationResult.Accepted(command);
                case VfxRuntimeCommandIds.Stop:
                    if (!VfxRuntimeCommandFactory.DecodeStopInstanceId(command).IsValid)
                        return Invalid(command, "VFX stop command target instance id must be positive.");
                    VfxStopMode stopMode = VfxRuntimeCommandFactory.DecodeStopMode(command);
                    if (stopMode != VfxStopMode.Complete
                        && stopMode != VfxStopMode.Dissipate
                        && stopMode != VfxStopMode.Immediate)
                        return Invalid(command, "VFX stop command mode is invalid.");
                    return RuntimeCommandValidationResult.Accepted(command);
                case VfxRuntimeCommandIds.Rebuild:
                    if (!VfxRuntimeCommandFactory.DecodeRebuildInstanceId(command).IsValid)
                        return Invalid(command, "VFX rebuild command target instance id must be positive.");
                    return RuntimeCommandValidationResult.Accepted(command);
                default:
                    return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                        RuntimeCommandErrorCode.UnregisteredCommandId,
                        command,
                        command.Frame,
                        "Command is not registered for VFX runtime."));
            }
        }

        private static RuntimeCommandValidationResult Invalid(RuntimeCommand command, string message)
        {
            return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                RuntimeCommandErrorCode.InvalidPayload,
                command,
                command.Frame,
                message));
        }
    }

    public readonly struct VfxSpawnRequest : IComparable<VfxSpawnRequest>, IEquatable<VfxSpawnRequest>
    {
        public VfxSpawnRequest(
            RuntimeFrame frame,
            int sourceDomain,
            long sourceFrame,
            int localFrame,
            int sourceOrder,
            int triggerId,
            int sourceId,
            int targetId,
            VfxDefinitionId definitionId,
            VfxAnchor anchor,
            VfxParameterBlock parameters,
            int lifetimeFramesOverride,
            RuntimeTraceIdentity traceId,
            long sequence = 0L,
            VfxStopMode expireStopMode = VfxStopMode.Complete,
            int playbackRatePermille = 1000,
            int sourceStartOffsetFrames = 0,
            int sourceEndOffsetFrames = 0)
        {
            if (sequence < 0L)
                throw new ArgumentOutOfRangeException(nameof(sequence), "VFX request sequence cannot be negative.");

            Frame = frame;
            SourceDomain = sourceDomain;
            SourceFrame = sourceFrame < 0L ? 0L : sourceFrame;
            LocalFrame = localFrame < 0 ? 0 : localFrame;
            SourceOrder = sourceOrder < 0 ? 0 : sourceOrder;
            TriggerId = triggerId < 0 ? 0 : triggerId;
            SourceId = sourceId < 0 ? 0 : sourceId;
            TargetId = targetId < 0 ? 0 : targetId;
            DefinitionId = definitionId;
            Anchor = anchor;
            Parameters = parameters;
            LifetimeFramesOverride = lifetimeFramesOverride < 0 ? 0 : lifetimeFramesOverride;
            TraceId = traceId;
            Sequence = sequence;
            ExpireStopMode = expireStopMode;
            PlaybackRatePermille = playbackRatePermille > 0 ? playbackRatePermille : 1000;
            SourceStartOffsetFrames = Math.Max(0, sourceStartOffsetFrames);
            SourceEndOffsetFrames = Math.Max(0, sourceEndOffsetFrames);
        }

        public RuntimeFrame Frame { get; }
        public int SourceDomain { get; }
        public long SourceFrame { get; }
        public int LocalFrame { get; }
        public int SourceOrder { get; }
        public int TriggerId { get; }
        public int SourceId { get; }
        public int TargetId { get; }
        public VfxDefinitionId DefinitionId { get; }
        public VfxAnchor Anchor { get; }
        public VfxParameterBlock Parameters { get; }
        public int LifetimeFramesOverride { get; }
        public RuntimeTraceIdentity TraceId { get; }
        public long Sequence { get; }
        public VfxStopMode ExpireStopMode { get; }
        /// <summary>Gets the deterministic instance playback multiplier where 1000 equals 1x.</summary>
        /// <remarks>NoAlloc.</remarks>
        public int PlaybackRatePermille { get; }
        /// <summary>Gets the number of 60 Hz source frames pre-simulated before presentation begins.</summary>
        /// <remarks>NoAlloc.</remarks>
        public int SourceStartOffsetFrames { get; }
        /// <summary>Gets the number of 60 Hz source frames removed from a definition-owned natural lifetime.</summary>
        /// <remarks>NoAlloc.</remarks>
        public int SourceEndOffsetFrames { get; }

        public VfxSpawnRequest WithSequence(long sequence)
        {
            return new VfxSpawnRequest(
                Frame,
                SourceDomain,
                SourceFrame,
                LocalFrame,
                SourceOrder,
                TriggerId,
                SourceId,
                TargetId,
                DefinitionId,
                Anchor,
                Parameters,
                LifetimeFramesOverride,
                TraceId,
                sequence,
                ExpireStopMode,
                PlaybackRatePermille,
                SourceStartOffsetFrames,
                SourceEndOffsetFrames);
        }

        public int CompareTo(VfxSpawnRequest other)
        {
            int compare = Frame.CompareTo(other.Frame);
            if (compare != 0)
                return compare;
            compare = SourceDomain.CompareTo(other.SourceDomain);
            if (compare != 0)
                return compare;
            compare = SourceFrame.CompareTo(other.SourceFrame);
            if (compare != 0)
                return compare;
            compare = LocalFrame.CompareTo(other.LocalFrame);
            if (compare != 0)
                return compare;
            compare = SourceOrder.CompareTo(other.SourceOrder);
            if (compare != 0)
                return compare;
            compare = TriggerId.CompareTo(other.TriggerId);
            if (compare != 0)
                return compare;
            compare = SourceId.CompareTo(other.SourceId);
            if (compare != 0)
                return compare;
            compare = TargetId.CompareTo(other.TargetId);
            if (compare != 0)
                return compare;
            compare = DefinitionId.CompareTo(other.DefinitionId);
            if (compare != 0)
                return compare;
            return Sequence.CompareTo(other.Sequence);
        }

        public bool Equals(VfxSpawnRequest other)
        {
            return Frame == other.Frame
                && SourceDomain == other.SourceDomain
                && SourceFrame == other.SourceFrame
                && LocalFrame == other.LocalFrame
                && SourceOrder == other.SourceOrder
                && TriggerId == other.TriggerId
                && SourceId == other.SourceId
                && TargetId == other.TargetId
                && DefinitionId.Equals(other.DefinitionId)
                && Anchor.Equals(other.Anchor)
                && Parameters.Equals(other.Parameters)
                && LifetimeFramesOverride == other.LifetimeFramesOverride
                && TraceId.Equals(other.TraceId)
                && Sequence == other.Sequence
                && ExpireStopMode == other.ExpireStopMode
                && PlaybackRatePermille == other.PlaybackRatePermille
                && SourceStartOffsetFrames == other.SourceStartOffsetFrames
                && SourceEndOffsetFrames == other.SourceEndOffsetFrames;
        }

        public override bool Equals(object obj)
        {
            return obj is VfxSpawnRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Frame.GetHashCode();
                hash = (hash * 397) ^ SourceDomain;
                hash = (hash * 397) ^ SourceFrame.GetHashCode();
                hash = (hash * 397) ^ LocalFrame;
                hash = (hash * 397) ^ SourceOrder;
                hash = (hash * 397) ^ TriggerId;
                hash = (hash * 397) ^ SourceId;
                hash = (hash * 397) ^ TargetId;
                hash = (hash * 397) ^ DefinitionId.Value;
                hash = (hash * 397) ^ Anchor.GetHashCode();
                hash = (hash * 397) ^ Parameters.GetHashCode();
                hash = (hash * 397) ^ LifetimeFramesOverride;
                hash = (hash * 397) ^ TraceId.GetHashCode();
                hash = (hash * 397) ^ Sequence.GetHashCode();
                hash = (hash * 397) ^ (int)ExpireStopMode;
                hash = (hash * 397) ^ PlaybackRatePermille;
                hash = (hash * 397) ^ SourceStartOffsetFrames;
                hash = (hash * 397) ^ SourceEndOffsetFrames;
                return hash;
            }
        }

        internal void Contribute(RuntimeHashAccumulator accumulator)
        {
            accumulator.AddLong(VfxRuntimeHashFields.Frame, Frame.Value);
            accumulator.AddInt(VfxRuntimeHashFields.Sourcedomain, SourceDomain);
            accumulator.AddLong(VfxRuntimeHashFields.Sourceframe, SourceFrame);
            accumulator.AddInt(VfxRuntimeHashFields.Localframe, LocalFrame);
            accumulator.AddInt(VfxRuntimeHashFields.Sourceorder, SourceOrder);
            accumulator.AddInt(VfxRuntimeHashFields.Triggerid, TriggerId);
            accumulator.AddInt(VfxRuntimeHashFields.Sourceid, SourceId);
            accumulator.AddInt(VfxRuntimeHashFields.Targetid, TargetId);
            accumulator.AddInt(VfxRuntimeHashFields.Definitionid, DefinitionId.Value);
            Anchor.Contribute(accumulator);
            Parameters.Contribute(accumulator);
            accumulator.AddInt(VfxRuntimeHashFields.Lifetimeoverride, LifetimeFramesOverride);
            TraceId.Contribute(accumulator, 15);
            accumulator.AddLong(VfxRuntimeHashFields.Sequence, Sequence);
            accumulator.AddInt(VfxRuntimeHashFields.Expirestopmode, (int)ExpireStopMode);
            accumulator.AddInt(VfxRuntimeHashFields.Playbackratepermille, PlaybackRatePermille);
            accumulator.AddInt(VfxRuntimeHashFields.Sourcestartoffsetframes, SourceStartOffsetFrames);
            accumulator.AddInt(VfxRuntimeHashFields.Sourceendoffsetframes, SourceEndOffsetFrames);
        }
    }

    public readonly struct VfxStopRequest : IComparable<VfxStopRequest>, IEquatable<VfxStopRequest>
    {
        public VfxStopRequest(RuntimeFrame frame, VfxInstanceId instanceId, string reason, RuntimeTraceIdentity traceId, long sequence = 0L)
            : this(frame, instanceId, VfxStopMode.Complete, VfxStopReasonCode.Custom, 0, reason, traceId, sequence)
        {
        }

        public VfxStopRequest(RuntimeFrame frame, VfxInstanceId instanceId, VfxStopMode stopMode, string reason, RuntimeTraceIdentity traceId, long sequence = 0L)
            : this(frame, instanceId, stopMode, VfxStopReasonCode.Custom, 0, reason, traceId, sequence)
        {
        }

        public VfxStopRequest(
            RuntimeFrame frame,
            VfxInstanceId instanceId,
            VfxStopMode stopMode,
            VfxStopReasonCode reasonCode,
            int reasonValue,
            RuntimeTraceIdentity traceId,
            long sequence = 0L)
            : this(frame, instanceId, stopMode, reasonCode, reasonValue, string.Empty, traceId, sequence)
        {
        }

        public VfxStopRequest(
            RuntimeFrame frame,
            VfxInstanceId instanceId,
            VfxStopMode stopMode,
            VfxStopReasonCode reasonCode,
            int reasonValue,
            string reason,
            RuntimeTraceIdentity traceId,
            long sequence = 0L)
        {
            if ((uint)reasonCode > (uint)VfxStopReasonCode.SourceRemoved)
                throw new ArgumentOutOfRangeException(nameof(reasonCode));
            if (sequence < 0L)
                throw new ArgumentOutOfRangeException(nameof(sequence), "VFX stop sequence cannot be negative.");
            if (stopMode != VfxStopMode.Complete
                && stopMode != VfxStopMode.Dissipate
                && stopMode != VfxStopMode.Immediate)
                throw new ArgumentOutOfRangeException(nameof(stopMode), "VFX stop mode is invalid.");

            Frame = frame;
            InstanceId = instanceId;
            StopMode = stopMode;
            ReasonCode = reasonCode;
            ReasonValue = reasonValue;
            Reason = reason ?? string.Empty;
            ReasonDigest = reasonCode == VfxStopReasonCode.Custom
                ? RuntimeHashDigest.ComputeContentDigest(Reason)
                : default;
            TraceId = traceId;
            Sequence = sequence;
        }

        private VfxStopRequest(
            RuntimeFrame frame,
            VfxInstanceId instanceId,
            VfxStopMode stopMode,
            VfxStopReasonCode reasonCode,
            int reasonValue,
            string reason,
            RuntimeTraceIdentity traceId,
            long sequence,
            RuntimeHashDigest reasonDigest)
        {
            if (sequence < 0L)
                throw new ArgumentOutOfRangeException(nameof(sequence), "VFX stop sequence cannot be negative.");
            if (stopMode != VfxStopMode.Complete
                && stopMode != VfxStopMode.Dissipate
                && stopMode != VfxStopMode.Immediate)
                throw new ArgumentOutOfRangeException(nameof(stopMode), "VFX stop mode is invalid.");

            Frame = frame;
            InstanceId = instanceId;
            StopMode = stopMode;
            ReasonCode = reasonCode;
            ReasonValue = reasonValue;
            Reason = reason ?? string.Empty;
            ReasonDigest = reasonDigest;
            TraceId = traceId;
            Sequence = sequence;
        }

        public RuntimeFrame Frame { get; }
        public VfxInstanceId InstanceId { get; }
        public VfxStopMode StopMode { get; }
        public VfxStopReasonCode ReasonCode { get; }
        public int ReasonValue { get; }
        public string Reason { get; }
        internal RuntimeHashDigest ReasonDigest { get; }
        public RuntimeTraceIdentity TraceId { get; }
        public long Sequence { get; }

        public VfxStopRequest WithSequence(long sequence)
        {
            return new VfxStopRequest(
                Frame,
                InstanceId,
                StopMode,
                ReasonCode,
                ReasonValue,
                Reason,
                TraceId,
                sequence,
                ReasonDigest);
        }

        public int CompareTo(VfxStopRequest other)
        {
            int compare = Frame.CompareTo(other.Frame);
            if (compare != 0)
                return compare;
            compare = InstanceId.CompareTo(other.InstanceId);
            if (compare != 0)
                return compare;
            return Sequence.CompareTo(other.Sequence);
        }

        public bool Equals(VfxStopRequest other)
        {
            return Frame == other.Frame
                && InstanceId.Equals(other.InstanceId)
                && StopMode == other.StopMode
                && ReasonCode == other.ReasonCode
                && ReasonValue == other.ReasonValue
                && string.Equals(Reason, other.Reason, StringComparison.Ordinal)
                && TraceId.Equals(other.TraceId)
                && Sequence == other.Sequence;
        }

        public override bool Equals(object obj)
        {
            return obj is VfxStopRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Frame.GetHashCode();
                hash = (hash * 397) ^ InstanceId.GetHashCode();
                hash = (hash * 397) ^ (int)StopMode;
                hash = (hash * 397) ^ (int)ReasonCode;
                hash = (hash * 397) ^ ReasonValue;
                hash = (hash * 397) ^ (Reason == null ? 0 : StringComparer.Ordinal.GetHashCode(Reason));
                hash = (hash * 397) ^ TraceId.GetHashCode();
                hash = (hash * 397) ^ Sequence.GetHashCode();
                return hash;
            }
        }

        internal void Contribute(RuntimeHashAccumulator accumulator)
        {
            accumulator.AddLong(VfxRuntimeHashFields.Frame, Frame.Value);
            accumulator.AddLong(VfxRuntimeHashFields.Instanceid, InstanceId.Value);
            accumulator.AddInt(VfxRuntimeHashFields.Stopmode, (int)StopMode);
            accumulator.AddInt(VfxRuntimeHashFields.Reasoncode, (int)ReasonCode);
            accumulator.AddInt(VfxRuntimeHashFields.Reasonvalue, ReasonValue);
            if (ReasonCode == VfxStopReasonCode.Custom)
                accumulator.AddDigest(VfxRuntimeHashFields.Reason, ReasonDigest);
            TraceId.Contribute(accumulator, 15);
            accumulator.AddLong(VfxRuntimeHashFields.Sequence, Sequence);
        }
    }

    /// <summary>Deterministic request to replay Rebuild then Loop for an active instance.</summary>
    /// <remarks>NoAlloc value type.</remarks>
    public readonly struct VfxRebuildRequest : IComparable<VfxRebuildRequest>, IEquatable<VfxRebuildRequest>
    {
        public VfxRebuildRequest(RuntimeFrame frame, VfxInstanceId instanceId, RuntimeTraceIdentity traceId, long sequence = 0L)
        {
            if (sequence < 0L)
                throw new ArgumentOutOfRangeException(nameof(sequence), "VFX rebuild sequence cannot be negative.");
            Frame = frame;
            InstanceId = instanceId;
            TraceId = traceId;
            Sequence = sequence;
        }

        public RuntimeFrame Frame { get; }
        public VfxInstanceId InstanceId { get; }
        public RuntimeTraceIdentity TraceId { get; }
        public long Sequence { get; }

        public VfxRebuildRequest WithSequence(long sequence)
        {
            return new VfxRebuildRequest(Frame, InstanceId, TraceId, sequence);
        }

        public int CompareTo(VfxRebuildRequest other)
        {
            int compare = Frame.CompareTo(other.Frame);
            if (compare != 0)
                return compare;
            compare = InstanceId.CompareTo(other.InstanceId);
            if (compare != 0)
                return compare;
            return Sequence.CompareTo(other.Sequence);
        }

        public bool Equals(VfxRebuildRequest other)
        {
            return Frame == other.Frame
                && InstanceId.Equals(other.InstanceId)
                && TraceId.Equals(other.TraceId)
                && Sequence == other.Sequence;
        }

        public override bool Equals(object obj)
        {
            return obj is VfxRebuildRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Frame.GetHashCode();
                hash = (hash * 397) ^ InstanceId.GetHashCode();
                hash = (hash * 397) ^ TraceId.GetHashCode();
                hash = (hash * 397) ^ Sequence.GetHashCode();
                return hash;
            }
        }

        internal void Contribute(RuntimeHashAccumulator accumulator)
        {
            accumulator.AddLong(VfxRuntimeHashFields.Frame, Frame.Value);
            accumulator.AddLong(VfxRuntimeHashFields.Instanceid, InstanceId.Value);
            TraceId.Contribute(accumulator, 15);
            accumulator.AddLong(VfxRuntimeHashFields.Sequence, Sequence);
        }
    }

    public enum VfxRuntimeEventType
    {
        None = 0,
        Spawned = 1,
        Stopped = 2,
        Expired = 3,
        Rejected = 4,
        Rebuilt = 5
    }

    public readonly struct VfxRuntimeEvent
    {
        public VfxRuntimeEvent(
            RuntimeFrame frame,
            VfxRuntimeEventType type,
            VfxInstanceId instanceId,
            VfxDefinitionId definitionId,
            VfxAnchor anchor,
            VfxParameterBlock parameters,
            string reason,
            RuntimeTraceIdentity traceId,
            VfxStopMode stopMode = VfxStopMode.Complete)
        {
            Frame = frame;
            Type = type;
            InstanceId = instanceId;
            DefinitionId = definitionId;
            Anchor = anchor;
            Parameters = parameters;
            Reason = reason ?? string.Empty;
            TraceId = traceId;
            StopMode = stopMode;
        }

        public RuntimeFrame Frame { get; }
        public VfxRuntimeEventType Type { get; }
        public VfxInstanceId InstanceId { get; }
        public VfxDefinitionId DefinitionId { get; }
        public VfxAnchor Anchor { get; }
        public VfxParameterBlock Parameters { get; }
        public string Reason { get; }
        public RuntimeTraceIdentity TraceId { get; }
        public VfxStopMode StopMode { get; }
    }

    public interface IVfxRuntimeEventSink
    {
        void OnVfxRuntimeEvents(RuntimeFrame frame, IReadOnlyList<VfxRuntimeEvent> events);
    }

    public sealed class VfxRuntimeEventFanout
    {
        private readonly VfxRuntimeModule _vfx;
        private readonly List<IVfxRuntimeEventSink> _sinks = new List<IVfxRuntimeEventSink>();
        private readonly List<VfxRuntimeEvent> _events = new List<VfxRuntimeEvent>();

        public VfxRuntimeEventFanout(VfxRuntimeModule vfx)
        {
            _vfx = vfx ?? throw new ArgumentNullException(nameof(vfx));
        }

        public int SinkCount => _sinks.Count;

        public bool AddSink(IVfxRuntimeEventSink sink)
        {
            if (sink == null)
                throw new ArgumentNullException(nameof(sink));

            if (_sinks.Contains(sink))
                return false;

            _sinks.Add(sink);
            return true;
        }

        public bool RemoveSink(IVfxRuntimeEventSink sink)
        {
            if (sink == null)
                return false;

            return _sinks.Remove(sink);
        }

        public int DrainFrame(RuntimeFrame frame)
        {
            _events.Clear();
            int drained = _vfx.DrainEvents(frame, _events);
            if (_events.Count == 0)
                return drained;

            for (int i = 0; i < _sinks.Count; i++)
                _sinks[i].OnVfxRuntimeEvents(frame, _events);

            _events.Clear();
            return drained;
        }
    }

    public sealed class VfxInstanceSnapshot
    {
        public VfxInstanceSnapshot(
            VfxInstanceId instanceId,
            VfxDefinitionId definitionId,
            string definitionHash,
            ResourceKey prefabResource,
            RuntimeFrame startFrame,
            RuntimeFrame expireFrame,
            VfxAnchor anchor,
            VfxParameterBlock parameters,
            int sourceDomain,
            long sourceFrame,
            int localFrame,
            int sourceOrder,
            int triggerId,
            int sourceId,
            int targetId,
            RuntimeTraceIdentity traceId,
            VfxStopMode expireStopMode = VfxStopMode.Complete,
            int playbackRatePermille = 1000,
            int sourceStartOffsetFrames = 0,
            int sourceEndOffsetFrames = 0)
            : this(
                instanceId,
                definitionId,
                definitionHash,
                prefabResource,
                startFrame,
                expireFrame,
                anchor,
                parameters,
                sourceDomain,
                sourceFrame,
                localFrame,
                sourceOrder,
                triggerId,
                sourceId,
                targetId,
                traceId,
                expireStopMode,
                playbackRatePermille,
                sourceStartOffsetFrames,
                sourceEndOffsetFrames,
                RuntimePreparedDigest.PrepareExisting(definitionHash),
                VfxPreparedResourceFingerprint.Prepare(prefabResource))
        {
        }

        internal VfxInstanceSnapshot(
            VfxInstanceId instanceId,
            VfxDefinitionId definitionId,
            string definitionHash,
            ResourceKey prefabResource,
            RuntimeFrame startFrame,
            RuntimeFrame expireFrame,
            VfxAnchor anchor,
            VfxParameterBlock parameters,
            int sourceDomain,
            long sourceFrame,
            int localFrame,
            int sourceOrder,
            int triggerId,
            int sourceId,
            int targetId,
            RuntimeTraceIdentity traceId,
            VfxStopMode expireStopMode,
            int playbackRatePermille,
            int sourceStartOffsetFrames,
            int sourceEndOffsetFrames,
            RuntimePreparedDigest definitionContentDigest,
            VfxPreparedResourceFingerprint prefabResourceFingerprint)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            DefinitionHash = definitionHash ?? string.Empty;
            DefinitionContentDigest = definitionContentDigest;
            PrefabResource = prefabResource;
            PrefabResourceFingerprint = prefabResourceFingerprint;
            StartFrame = startFrame;
            ExpireFrame = expireFrame;
            Anchor = anchor;
            Parameters = parameters;
            SourceDomain = sourceDomain;
            SourceFrame = sourceFrame < 0L ? 0L : sourceFrame;
            LocalFrame = localFrame < 0 ? 0 : localFrame;
            SourceOrder = sourceOrder < 0 ? 0 : sourceOrder;
            TriggerId = triggerId;
            SourceId = sourceId;
            TargetId = targetId;
            TraceId = traceId;
            ExpireStopMode = expireStopMode;
            PlaybackRatePermille = playbackRatePermille > 0 ? playbackRatePermille : 1000;
            SourceStartOffsetFrames = Math.Max(0, sourceStartOffsetFrames);
            SourceEndOffsetFrames = Math.Max(0, sourceEndOffsetFrames);
        }

        public VfxInstanceId InstanceId { get; }
        public VfxDefinitionId DefinitionId { get; }
        public string DefinitionHash { get; }
        internal RuntimePreparedDigest DefinitionContentDigest { get; }
        public ResourceKey PrefabResource { get; }
        internal VfxPreparedResourceFingerprint PrefabResourceFingerprint { get; }
        public RuntimeFrame StartFrame { get; }
        public RuntimeFrame ExpireFrame { get; }
        public bool HasFiniteLifetime => ExpireFrame.Value > 0L;
        public VfxAnchor Anchor { get; }
        public VfxParameterBlock Parameters { get; }
        public int SourceDomain { get; }
        public long SourceFrame { get; }
        public int LocalFrame { get; }
        public int SourceOrder { get; }
        public int TriggerId { get; }
        public int SourceId { get; }
        public int TargetId { get; }
        public RuntimeTraceIdentity TraceId { get; }
        public VfxStopMode ExpireStopMode { get; }
        /// <summary>Gets the deterministic instance playback multiplier where 1000 equals 1x.</summary>
        /// <remarks>NoAlloc.</remarks>
        public int PlaybackRatePermille { get; }
        public int SourceStartOffsetFrames { get; }
        public int SourceEndOffsetFrames { get; }

        internal void Contribute(RuntimeHashAccumulator accumulator)
        {
            accumulator.AddLong(VfxRuntimeHashFields.Instanceid, InstanceId.Value);
            accumulator.AddInt(VfxRuntimeHashFields.Definitionid, DefinitionId.Value);
            DefinitionContentDigest.Contribute(accumulator, VfxRuntimeHashFields.Definitionhash);
            accumulator.AddDigest(VfxRuntimeHashFields.Prefab, PrefabResourceFingerprint.ContentDigest);
            accumulator.AddLong(VfxRuntimeHashFields.Startframe, StartFrame.Value);
            accumulator.AddLong(VfxRuntimeHashFields.Expireframe, ExpireFrame.Value);
            Anchor.Contribute(accumulator);
            Parameters.Contribute(accumulator);
            accumulator.AddInt(VfxRuntimeHashFields.Sourcedomain, SourceDomain);
            accumulator.AddLong(VfxRuntimeHashFields.Sourceframe, SourceFrame);
            accumulator.AddInt(VfxRuntimeHashFields.Localframe, LocalFrame);
            accumulator.AddInt(VfxRuntimeHashFields.Sourceorder, SourceOrder);
            accumulator.AddInt(VfxRuntimeHashFields.Triggerid, TriggerId);
            accumulator.AddInt(VfxRuntimeHashFields.Sourceid, SourceId);
            accumulator.AddInt(VfxRuntimeHashFields.Targetid, TargetId);
            TraceId.Contribute(accumulator, 15);
            accumulator.AddInt(VfxRuntimeHashFields.Expirestopmode, (int)ExpireStopMode);
            accumulator.AddInt(VfxRuntimeHashFields.Playbackratepermille, PlaybackRatePermille);
            accumulator.AddInt(VfxRuntimeHashFields.Sourcestartoffsetframes, SourceStartOffsetFrames);
            accumulator.AddInt(VfxRuntimeHashFields.Sourceendoffsetframes, SourceEndOffsetFrames);
        }
    }

    public sealed class VfxDebugSnapshot
    {
        public VfxDebugSnapshot(
            string moduleId,
            long nextInstanceId,
            int activeCount,
            int pendingSpawnCount,
            int pendingStopCount,
            int pendingEventCount,
            long definitionHash)
            : this(moduleId, nextInstanceId, activeCount, pendingSpawnCount, pendingStopCount, 0, pendingEventCount, definitionHash)
        {
        }

        public VfxDebugSnapshot(
            string moduleId,
            long nextInstanceId,
            int activeCount,
            int pendingSpawnCount,
            int pendingStopCount,
            int pendingRebuildCount,
            int pendingEventCount,
            long definitionHash)
        {
            ModuleId = moduleId ?? string.Empty;
            NextInstanceId = nextInstanceId;
            ActiveCount = activeCount;
            PendingSpawnCount = pendingSpawnCount;
            PendingStopCount = pendingStopCount;
            PendingRebuildCount = pendingRebuildCount;
            PendingEventCount = pendingEventCount;
            DefinitionHash = definitionHash;
        }

        public string ModuleId { get; }
        public long NextInstanceId { get; }
        public int ActiveCount { get; }
        public int PendingSpawnCount { get; }
        public int PendingStopCount { get; }
        public int PendingRebuildCount { get; }
        public int PendingEventCount { get; }
        public long DefinitionHash { get; }
    }

    public sealed class VfxRuntimeModule : RuntimeModule
    {
        public const string DefaultModuleId = "mxframework.vfx.runtime";

        private readonly List<RuntimeCommand> _drainedCommands = new List<RuntimeCommand>();
        private readonly List<VfxSpawnRequest> _pendingSpawns = new List<VfxSpawnRequest>();
        private readonly List<VfxStopRequest> _pendingStops = new List<VfxStopRequest>();
        private readonly List<VfxRebuildRequest> _pendingRebuilds = new List<VfxRebuildRequest>();
        private readonly List<VfxSpawnRequest> _dueSpawns = new List<VfxSpawnRequest>();
        private readonly List<VfxStopRequest> _dueStops = new List<VfxStopRequest>();
        private readonly List<VfxRebuildRequest> _dueRebuilds = new List<VfxRebuildRequest>();
        private readonly List<VfxInstanceSnapshot> _expiredInstances = new List<VfxInstanceSnapshot>();
        private readonly Dictionary<long, VfxInstanceSnapshot> _activeById = new Dictionary<long, VfxInstanceSnapshot>();
        private readonly List<VfxInstanceSnapshot> _activeInstanceOrder = new List<VfxInstanceSnapshot>();
        private readonly List<VfxSpawnRequest> _hashPendingSpawns = new List<VfxSpawnRequest>();
        private readonly List<VfxStopRequest> _hashPendingStops = new List<VfxStopRequest>();
        private readonly List<VfxRebuildRequest> _hashPendingRebuilds = new List<VfxRebuildRequest>();
        private readonly RuntimeEventQueue<VfxRuntimeEvent> _events = new RuntimeEventQueue<VfxRuntimeEvent>();
        private long _nextRequestSequence;
        private long _nextInstanceId = 1L;

        public VfxRuntimeModule(
            VfxDefinitionRegistry definitions,
            RuntimeCommandBuffer commandBuffer,
            string moduleId = DefaultModuleId,
            RuntimeTickStage tickStage = RuntimeTickStage.PostSimulation,
            int priority = 200)
            : base(moduleId, tickStage, priority)
        {
            Definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            CommandBuffer = commandBuffer ?? throw new ArgumentNullException(nameof(commandBuffer));
        }

        public VfxDefinitionRegistry Definitions { get; }
        public RuntimeCommandBuffer CommandBuffer { get; }
        public int ActiveCount => _activeById.Count;
        public int PendingSpawnCount => _pendingSpawns.Count;
        public int PendingStopCount => _pendingStops.Count;
        public int PendingRebuildCount => _pendingRebuilds.Count;

        /// <remarks>AllocByDesign when spawning: one immutable VfxInstanceSnapshot per accepted spawn.
        /// Tick without new spawns is NoAlloc after buffer capacities stabilize; diagnostics may allocate.</remarks>
        public override void Tick(RuntimeTickContext context)
        {
            RuntimeFrame frame = new RuntimeFrame(context.FrameIndex);
            DrainCommands(frame);
            ProcessStopRequests(frame);
            ProcessRebuildRequests(frame);
            ProcessSpawnRequests(frame);
            ExpireInstances(frame);
        }

        public VfxSpawnRequest EnqueueSpawnRequest(VfxSpawnRequest request)
        {
            VfxSpawnRequest sequenced = request.WithSequence(_nextRequestSequence);
            _nextRequestSequence++;
            _pendingSpawns.Add(sequenced);
            return sequenced;
        }

        public VfxStopRequest EnqueueStopRequest(VfxStopRequest request)
        {
            VfxStopRequest sequenced = request.WithSequence(_nextRequestSequence);
            _nextRequestSequence++;
            _pendingStops.Add(sequenced);
            return sequenced;
        }

        /// <summary>Queues a deterministic rebuild request.</summary>
        /// <remarks>NoAlloc after list capacity has stabilized.</remarks>
        public VfxRebuildRequest EnqueueRebuildRequest(VfxRebuildRequest request)
        {
            VfxRebuildRequest sequenced = request.WithSequence(_nextRequestSequence);
            _nextRequestSequence++;
            _pendingRebuilds.Add(sequenced);
            return sequenced;
        }

        public bool TryGetInstance(VfxInstanceId instanceId, out VfxInstanceSnapshot instance)
        {
            return _activeById.TryGetValue(instanceId.Value, out instance);
        }

        public int DrainEvents(RuntimeFrame frame, List<VfxRuntimeEvent> output)
        {
            return _events.Drain(frame, output);
        }

        public int CopyActiveInstances(List<VfxInstanceSnapshot> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            int start = output.Count;
            for (int i = 0; i < _activeInstanceOrder.Count; i++)
                output.Add(_activeInstanceOrder[i]);
            return output.Count - start;
        }

        /// <summary>Clears transient VFX runtime state for tooling or deterministic replay reset.</summary>
        /// <remarks>
        /// NoAlloc after initialization. This does not emit Stop or Expire events and preserves the registered
        /// definition registry and module ownership. Normal gameplay Tick, restore, and disposal behavior is unchanged.
        /// </remarks>
        public void ResetTransientState(RuntimeFrame frame)
        {
            _drainedCommands.Clear();
            _pendingSpawns.Clear();
            _pendingStops.Clear();
            _pendingRebuilds.Clear();
            _dueSpawns.Clear();
            _dueStops.Clear();
            _dueRebuilds.Clear();
            _expiredInstances.Clear();
            _activeById.Clear();
            _activeInstanceOrder.Clear();
            _hashPendingSpawns.Clear();
            _hashPendingStops.Clear();
            _hashPendingRebuilds.Clear();
            _events.Clear();
            _nextRequestSequence = 0L;
            _nextInstanceId = 1L;
            CommandBuffer.Reset(frame);
        }

        public int CopyPendingSpawnRequests(List<VfxSpawnRequest> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            int start = output.Count;
            for (int i = 0; i < _pendingSpawns.Count; i++)
                output.Add(_pendingSpawns[i]);

            output.Sort(start, output.Count - start, Comparer<VfxSpawnRequest>.Default);
            return output.Count - start;
        }

        public int CopyPendingStopRequests(List<VfxStopRequest> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            int start = output.Count;
            for (int i = 0; i < _pendingStops.Count; i++)
                output.Add(_pendingStops[i]);

            output.Sort(start, output.Count - start, Comparer<VfxStopRequest>.Default);
            return output.Count - start;
        }

        public int CopyPendingRebuildRequests(List<VfxRebuildRequest> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            int start = output.Count;
            for (int i = 0; i < _pendingRebuilds.Count; i++)
                output.Add(_pendingRebuilds[i]);

            output.Sort(start, output.Count - start, Comparer<VfxRebuildRequest>.Default);
            return output.Count - start;
        }

        public VfxDebugSnapshot CreateDebugSnapshot()
        {
            return new VfxDebugSnapshot(
                ModuleId,
                _nextInstanceId,
                _activeById.Count,
                _pendingSpawns.Count,
                _pendingStops.Count,
                _pendingRebuilds.Count,
                _events.PendingCount,
                Definitions.ComputeStableHash());
        }

        internal VfxRuntimeState CaptureRuntimeState()
        {
            var active = new List<VfxInstanceSnapshot>();
            CopyActiveInstances(active);
            var spawns = new List<VfxSpawnRequest>();
            CopyPendingSpawnRequests(spawns);
            var stops = new List<VfxStopRequest>();
            CopyPendingStopRequests(stops);
            var rebuilds = new List<VfxRebuildRequest>();
            CopyPendingRebuildRequests(rebuilds);
            return new VfxRuntimeState(
                _nextInstanceId,
                _nextRequestSequence,
                active,
                spawns,
                stops,
                rebuilds,
                Definitions.ComputeStableHash());
        }

        /// <summary>Contributes live VFX state without creating a save snapshot or recomputing text digests.</summary>
        /// <remarks>The iteration lists are owned by this module and reused across state-hash calls.</remarks>
        internal void ContributeStateHash(RuntimeHashAccumulator accumulator)
        {
            accumulator.AddInt(VfxRuntimeHashFields.Definitioncount, Definitions.Count);
            accumulator.AddLong(VfxRuntimeHashFields.Definitionstablehash, Definitions.ComputeStableHash());
            accumulator.AddLong(VfxRuntimeHashFields.Nextinstanceid, _nextInstanceId);
            accumulator.AddLong(VfxRuntimeHashFields.Nextrequestsequence, _nextRequestSequence);

            accumulator.AddInt(VfxRuntimeHashFields.Activecount, _activeInstanceOrder.Count);
            for (int i = 0; i < _activeInstanceOrder.Count; i++)
                _activeInstanceOrder[i].Contribute(accumulator);

            _hashPendingSpawns.Clear();
            for (int i = 0; i < _pendingSpawns.Count; i++)
                _hashPendingSpawns.Add(_pendingSpawns[i]);
            _hashPendingSpawns.Sort(Comparer<VfxSpawnRequest>.Default);
            accumulator.AddInt(VfxRuntimeHashFields.Pendingspawncount, _hashPendingSpawns.Count);
            for (int i = 0; i < _hashPendingSpawns.Count; i++)
                _hashPendingSpawns[i].Contribute(accumulator);

            _hashPendingStops.Clear();
            for (int i = 0; i < _pendingStops.Count; i++)
                _hashPendingStops.Add(_pendingStops[i]);
            _hashPendingStops.Sort(Comparer<VfxStopRequest>.Default);
            accumulator.AddInt(VfxRuntimeHashFields.Pendingstopcount, _hashPendingStops.Count);
            for (int i = 0; i < _hashPendingStops.Count; i++)
                _hashPendingStops[i].Contribute(accumulator);

            _hashPendingRebuilds.Clear();
            for (int i = 0; i < _pendingRebuilds.Count; i++)
                _hashPendingRebuilds.Add(_pendingRebuilds[i]);
            _hashPendingRebuilds.Sort(Comparer<VfxRebuildRequest>.Default);
            accumulator.AddInt(VfxRuntimeHashFields.Pendingrebuildcount, _hashPendingRebuilds.Count);
            for (int i = 0; i < _hashPendingRebuilds.Count; i++)
                _hashPendingRebuilds[i].Contribute(accumulator);
        }

        internal void RestoreRuntimeState(VfxRuntimeState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            _activeById.Clear();
            _activeInstanceOrder.Clear();
            _pendingSpawns.Clear();
            _pendingStops.Clear();
            _pendingRebuilds.Clear();
            _events.Clear();
            _nextInstanceId = state.NextInstanceId <= 0L ? 1L : state.NextInstanceId;
            _nextRequestSequence = state.NextRequestSequence < 0L ? 0L : state.NextRequestSequence;
            for (int i = 0; i < state.ActiveInstances.Count; i++)
            {
                VfxInstanceSnapshot instance = state.ActiveInstances[i];
                if (instance.InstanceId.IsValid)
                {
                    _activeById[instance.InstanceId.Value] = instance;
                    _activeInstanceOrder.Add(instance);
                }
            }
            _activeInstanceOrder.Sort(InstanceComparer.Instance);

            for (int i = 0; i < state.PendingSpawns.Count; i++)
                _pendingSpawns.Add(state.PendingSpawns[i]);
            for (int i = 0; i < state.PendingStops.Count; i++)
                _pendingStops.Add(state.PendingStops[i]);
            for (int i = 0; i < state.PendingRebuilds.Count; i++)
                _pendingRebuilds.Add(state.PendingRebuilds[i]);
        }

        private void DrainCommands(RuntimeFrame frame)
        {
            _drainedCommands.Clear();
            CommandBuffer.DrainForFrame(frame, _drainedCommands);

            for (int i = 0; i < _drainedCommands.Count; i++)
                ApplyCommand(frame, _drainedCommands[i]);

            _drainedCommands.Clear();
        }

        private void ApplyCommand(RuntimeFrame frame, RuntimeCommand command)
        {
            switch (command.CommandId)
            {
                case VfxRuntimeCommandIds.Spawn:
                    var definitionId = new VfxDefinitionId(command.Payload0);
                    if (!definitionId.IsValid || !Definitions.TryGet(definitionId, out var _))
                    {
                        Reject(frame, default, definitionId, "Unknown VFX definition.", command.TraceId);
                        return;
                    }

                    EnqueueSpawnRequest(new VfxSpawnRequest(
                        command.Frame,
                        VfxSourceDomains.Manual,
                        command.Frame.Value,
                        localFrame: 0,
                        sourceOrder: checked((int)Math.Min(command.Sequence, int.MaxValue)),
                        triggerId: command.CommandId,
                        sourceId: command.SourceId,
                        targetId: command.TargetId,
                        definitionId: definitionId,
                        anchor: VfxAnchor.Target(command.SourceId, command.TargetId, VfxPose.Identity),
                        parameters: new VfxParameterBlock(command.Payload1),
                        lifetimeFramesOverride: 0,
                        traceId: command.TraceId));
                    break;
                case VfxRuntimeCommandIds.Stop:
                    EnqueueStopRequest(new VfxStopRequest(
                        command.Frame,
                        VfxRuntimeCommandFactory.DecodeStopInstanceId(command),
                        VfxRuntimeCommandFactory.DecodeStopMode(command),
                        VfxStopReasonCode.Command,
                        command.CommandId,
                        "CommandStop",
                        command.TraceId));
                    break;
                case VfxRuntimeCommandIds.Rebuild:
                    EnqueueRebuildRequest(new VfxRebuildRequest(
                        command.Frame,
                        VfxRuntimeCommandFactory.DecodeRebuildInstanceId(command),
                        command.TraceId));
                    break;
                default:
                    Reject(frame, default, default, "Unsupported VFX command: " + command.CommandId + ".", command.TraceId);
                    break;
            }
        }

        private void ProcessSpawnRequests(RuntimeFrame frame)
        {
            List<VfxSpawnRequest> due = _dueSpawns;
            due.Clear();
            for (int i = _pendingSpawns.Count - 1; i >= 0; i--)
            {
                VfxSpawnRequest request = _pendingSpawns[i];
                if (request.Frame <= frame)
                {
                    _pendingSpawns.RemoveAt(i);
                    due.Add(request);
                }
            }

            due.Sort();
            for (int i = 0; i < due.Count; i++)
                Spawn(frame, due[i]);
        }

        private void ProcessStopRequests(RuntimeFrame frame)
        {
            List<VfxStopRequest> due = _dueStops;
            due.Clear();
            for (int i = _pendingStops.Count - 1; i >= 0; i--)
            {
                VfxStopRequest request = _pendingStops[i];
                if (request.Frame <= frame)
                {
                    _pendingStops.RemoveAt(i);
                    due.Add(request);
                }
            }

            due.Sort();
            for (int i = 0; i < due.Count; i++)
                Stop(frame, due[i]);
        }

        private void ProcessRebuildRequests(RuntimeFrame frame)
        {
            List<VfxRebuildRequest> due = _dueRebuilds;
            due.Clear();
            for (int i = _pendingRebuilds.Count - 1; i >= 0; i--)
            {
                VfxRebuildRequest request = _pendingRebuilds[i];
                if (request.Frame <= frame)
                {
                    _pendingRebuilds.RemoveAt(i);
                    due.Add(request);
                }
            }

            due.Sort();
            for (int i = 0; i < due.Count; i++)
                Rebuild(frame, due[i]);
        }

        private void Spawn(RuntimeFrame frame, in VfxSpawnRequest request)
        {
            if (!Definitions.TryGet(request.DefinitionId, out VfxDefinition definition))
            {
                Reject(frame, default, request.DefinitionId, "Unknown VFX definition.", request.TraceId);
                return;
            }

            int sourceStartOffsetFrames = request.SourceStartOffsetFrames;
            int sourceEndOffsetFrames = request.SourceEndOffsetFrames;
            int definitionLifetime = definition.DefaultLifetimeFrames;
            if (definitionLifetime > 0)
            {
                sourceStartOffsetFrames = Math.Min(sourceStartOffsetFrames, definitionLifetime - 1);
                sourceEndOffsetFrames = Math.Min(
                    sourceEndOffsetFrames,
                    definitionLifetime - sourceStartOffsetFrames - 1);
            }
            else
            {
                sourceEndOffsetFrames = 0;
            }

            int lifetime = request.LifetimeFramesOverride > 0
                ? request.LifetimeFramesOverride
                : Math.Max(0, definitionLifetime - sourceStartOffsetFrames - sourceEndOffsetFrames);
            if (request.LifetimeFramesOverride == 0 && lifetime > 0)
            {
                long scaledLifetime = ((long)lifetime * 1000L + request.PlaybackRatePermille - 1L)
                    / request.PlaybackRatePermille;
                lifetime = (int)Math.Max(1L, Math.Min(int.MaxValue, scaledLifetime));
            }
            RuntimeFrame expireFrame = lifetime > 0
                ? new RuntimeFrame(checked(frame.Value + lifetime))
                : RuntimeFrame.Zero;
            var instanceId = new VfxInstanceId(_nextInstanceId);
            _nextInstanceId++;
            var instance = new VfxInstanceSnapshot(
                instanceId,
                definition.DefinitionId,
                definition.DefinitionHash,
                definition.PrefabResource,
                frame,
                expireFrame,
                request.Anchor,
                request.Parameters,
                request.SourceDomain,
                request.SourceFrame,
                request.LocalFrame,
                request.SourceOrder,
                request.TriggerId,
                request.SourceId,
                request.TargetId,
                request.TraceId,
                request.ExpireStopMode,
                request.PlaybackRatePermille,
                sourceStartOffsetFrames,
                sourceEndOffsetFrames,
                definition.DefinitionContentDigest,
                definition.PrefabResourceFingerprint);
            _activeById.Add(instanceId.Value, instance);
            _activeInstanceOrder.Add(instance);
            EnqueueEvent(frame, VfxRuntimeEventType.Spawned, instance, string.Empty);
        }

        private void Stop(RuntimeFrame frame, in VfxStopRequest request)
        {
            if (!_activeById.TryGetValue(request.InstanceId.Value, out VfxInstanceSnapshot instance))
            {
                Reject(frame, request.InstanceId, default, "Unknown VFX instance.", request.TraceId);
                return;
            }

            _activeById.Remove(request.InstanceId.Value);
            RemoveActiveInstance(request.InstanceId.Value);
            EnqueueEvent(frame, VfxRuntimeEventType.Stopped, instance, request.Reason, request.StopMode);
        }

        private void Rebuild(RuntimeFrame frame, in VfxRebuildRequest request)
        {
            if (!_activeById.TryGetValue(request.InstanceId.Value, out VfxInstanceSnapshot instance))
            {
                Reject(frame, request.InstanceId, default, "Unknown or stopped VFX instance.", request.TraceId);
                return;
            }

            EnqueueEvent(frame, VfxRuntimeEventType.Rebuilt, instance, string.Empty);
        }

        private void ExpireInstances(RuntimeFrame frame)
        {
            List<VfxInstanceSnapshot> expired = _expiredInstances;
            expired.Clear();
            for (int activeIndex = 0; activeIndex < _activeInstanceOrder.Count; activeIndex++)
            {
                VfxInstanceSnapshot instance = _activeInstanceOrder[activeIndex];
                if (instance.HasFiniteLifetime && frame.Value >= instance.ExpireFrame.Value)
                    expired.Add(instance);
            }

            expired.Sort(InstanceComparer.Instance);
            for (int i = 0; i < expired.Count; i++)
            {
                VfxInstanceSnapshot instance = expired[i];
                if (_activeById.Remove(instance.InstanceId.Value))
                {
                    RemoveActiveInstance(instance.InstanceId.Value);
                    EnqueueEvent(frame, VfxRuntimeEventType.Expired, instance, "LifetimeExpired", instance.ExpireStopMode);
                }
            }

            expired.Clear();
        }

        private void RemoveActiveInstance(long instanceId)
        {
            for (int i = 0; i < _activeInstanceOrder.Count; i++)
            {
                if (_activeInstanceOrder[i].InstanceId.Value != instanceId)
                    continue;

                _activeInstanceOrder.RemoveAt(i);
                return;
            }
        }

        private void Reject(
            RuntimeFrame frame,
            VfxInstanceId instanceId,
            VfxDefinitionId definitionId,
            string reason,
            RuntimeTraceIdentity traceId)
        {
            _events.Enqueue(frame, new VfxRuntimeEvent(
                frame,
                VfxRuntimeEventType.Rejected,
                instanceId,
                definitionId,
                VfxAnchor.World(VfxPose.Identity),
                VfxParameterBlock.Empty,
                reason,
                traceId));
        }

        private void EnqueueEvent(
            RuntimeFrame frame,
            VfxRuntimeEventType type,
            VfxInstanceSnapshot instance,
            string reason,
            VfxStopMode stopMode = VfxStopMode.Complete)
        {
            _events.Enqueue(frame, new VfxRuntimeEvent(
                frame,
                type,
                instance.InstanceId,
                instance.DefinitionId,
                instance.Anchor,
                instance.Parameters,
                reason,
                instance.TraceId,
                stopMode));
        }

        private sealed class InstanceComparer : IComparer<VfxInstanceSnapshot>
        {
            public static readonly InstanceComparer Instance = new InstanceComparer();

            public int Compare(VfxInstanceSnapshot left, VfxInstanceSnapshot right)
            {
                if (ReferenceEquals(left, right))
                    return 0;
                if (left == null)
                    return -1;
                if (right == null)
                    return 1;
                return left.InstanceId.CompareTo(right.InstanceId);
            }
        }
    }

    public sealed class VfxRuntimeState
    {
        public VfxRuntimeState(
            long nextInstanceId,
            long nextRequestSequence,
            IReadOnlyList<VfxInstanceSnapshot> activeInstances,
            IReadOnlyList<VfxSpawnRequest> pendingSpawns,
            IReadOnlyList<VfxStopRequest> pendingStops,
            long definitionHash)
            : this(
                nextInstanceId,
                nextRequestSequence,
                activeInstances,
                pendingSpawns,
                pendingStops,
                Array.Empty<VfxRebuildRequest>(),
                definitionHash)
        {
        }

        public VfxRuntimeState(
            long nextInstanceId,
            long nextRequestSequence,
            IReadOnlyList<VfxInstanceSnapshot> activeInstances,
            IReadOnlyList<VfxSpawnRequest> pendingSpawns,
            IReadOnlyList<VfxStopRequest> pendingStops,
            IReadOnlyList<VfxRebuildRequest> pendingRebuilds,
            long definitionHash)
        {
            NextInstanceId = nextInstanceId;
            NextRequestSequence = nextRequestSequence;
            ActiveInstances = Copy(activeInstances);
            PendingSpawns = Copy(pendingSpawns);
            PendingStops = Copy(pendingStops);
            PendingRebuilds = Copy(pendingRebuilds);
            DefinitionHash = definitionHash;
        }

        public long NextInstanceId { get; }
        public long NextRequestSequence { get; }
        public IReadOnlyList<VfxInstanceSnapshot> ActiveInstances { get; }
        public IReadOnlyList<VfxSpawnRequest> PendingSpawns { get; }
        public IReadOnlyList<VfxStopRequest> PendingStops { get; }
        public IReadOnlyList<VfxRebuildRequest> PendingRebuilds { get; }
        public long DefinitionHash { get; }

        private static ReadOnlyCollection<T> Copy<T>(IReadOnlyList<T> source)
        {
            return new ReadOnlyCollection<T>(source == null ? new List<T>() : new List<T>(source));
        }
    }

    public sealed class VfxRuntimeHashContributor : IRuntimeHashContributor
    {
        private readonly VfxRuntimeModule _module;

        public VfxRuntimeHashContributor(VfxRuntimeModule module, int contributorId = RuntimeHashContributorIds.VfxRuntime)
        {
            _module = module ?? throw new ArgumentNullException(nameof(module));
            ContributorId = contributorId;
        }

        public int ContributorId { get; }
        public int HashSchemaId => RuntimeHashSchemaIds.VfxRuntime;
        public int HashSchemaVersion => RuntimeHashSchemaIds.Version1;

        public void Contribute(RuntimeHashContext context, RuntimeHashAccumulator accumulator)
        {
            _module.ContributeStateHash(accumulator);
        }
    }

    public sealed class VfxRuntimeSaveStateProvider : IRuntimeSaveStateProvider, IRuntimeSaveStateRestorer
    {
        public const int CustomStateNumericTypeId = 3303;
        public const string CustomStateTypeId = "mxframework.vfx.runtime";
        public const int CustomStateSchemaVersion = 1;

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None
        };

        private readonly VfxRuntimeModule _module;

        public VfxRuntimeSaveStateProvider(VfxRuntimeModule module)
        {
            _module = module ?? throw new ArgumentNullException(nameof(module));
        }

        public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
        {
            VfxRuntimeState state = _module.CaptureRuntimeState();
            string payloadJson = JsonConvert.SerializeObject(VfxRuntimeStateDto.FromState(state), JsonSettings);
            var customState = new RuntimeCustomState(CustomStateNumericTypeId, CustomStateTypeId, CustomStateSchemaVersion, payloadJson);
            var moduleState = new RuntimeModuleSaveState(_module.ModuleId, CustomStateSchemaVersion, customState);
            var saveState = new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion,
                DateTime.UtcNow,
                frameworkVersion: string.Empty,
                configVersion: string.Empty,
                resourceCatalogVersion: string.Empty,
                frame: 0L,
                entities: null,
                globalCounters: null,
                moduleStates: new[] { moduleState });
            return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(saveState);
        }

        public RuntimeSaveStateResult<bool> RestoreSaveState(RuntimeSaveState saveState)
        {
            if (saveState == null)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$",
                    "VFX restore state is missing."));
            }

            RuntimeModuleSaveState moduleState = null;
            for (int i = 0; i < saveState.ModuleStates.Count; i++)
            {
                RuntimeModuleSaveState candidate = saveState.ModuleStates[i];
                if (string.Equals(candidate.ModuleId, _module.ModuleId, StringComparison.Ordinal))
                {
                    moduleState = candidate;
                    break;
                }
            }

            if (moduleState == null || moduleState.CustomState == null)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.moduleStates",
                    "VFX module state was not found."));
            }

            if (!string.Equals(moduleState.CustomState.TypeId, CustomStateTypeId, StringComparison.Ordinal))
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.CustomStateMismatch,
                    "$.moduleStates['" + _module.ModuleId + "']",
                    "VFX custom state type mismatch."));
            }

            if (moduleState.CustomState.SchemaVersion != CustomStateSchemaVersion)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.UnsupportedVersion,
                    "$.moduleStates['" + _module.ModuleId + "']",
                    "Unsupported VFX custom state schema.",
                    moduleState.CustomState.SchemaVersion,
                    CustomStateSchemaVersion));
            }

            try
            {
                VfxRuntimeStateDto dto = JsonConvert.DeserializeObject<VfxRuntimeStateDto>(
                    moduleState.CustomState.PayloadJson,
                    JsonSettings);
                if (dto == null)
                    throw new JsonException("VFX state payload was empty.");

                VfxRuntimeState state = dto.ToState();
                long currentDefinitionHash = _module.Definitions.ComputeStableHash();
                if (state.DefinitionHash != currentDefinitionHash)
                {
                    return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.CustomStateMismatch,
                        "$.moduleStates['" + _module.ModuleId + "'].customState.definitionHash",
                        "VFX definition hash mismatch."));
                }

                _module.RestoreRuntimeState(state);
                return RuntimeSaveStateResult<bool>.Succeeded(true);
            }
            catch (Exception ex)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.moduleStates['" + _module.ModuleId + "'].customState.payloadJson",
                    "VFX custom state could not be parsed: " + ex.Message,
                    exception: ex));
            }
        }

        private sealed class VfxRuntimeStateDto
        {
            public long nextInstanceId;
            public long nextRequestSequence;
            public long definitionHash;
            public VfxInstanceDto[] activeInstances;
            public VfxSpawnRequestDto[] pendingSpawns;
            public VfxStopRequestDto[] pendingStops;
            public VfxRebuildRequestDto[] pendingRebuilds;

            public static VfxRuntimeStateDto FromState(VfxRuntimeState state)
            {
                return new VfxRuntimeStateDto
                {
                    nextInstanceId = state.NextInstanceId,
                    nextRequestSequence = state.NextRequestSequence,
                    definitionHash = state.DefinitionHash,
                    activeInstances = Convert(state.ActiveInstances),
                    pendingSpawns = Convert(state.PendingSpawns),
                    pendingStops = Convert(state.PendingStops),
                    pendingRebuilds = Convert(state.PendingRebuilds)
                };
            }

            public VfxRuntimeState ToState()
            {
                return new VfxRuntimeState(
                    nextInstanceId,
                    nextRequestSequence,
                    Convert(activeInstances),
                    Convert(pendingSpawns),
                    Convert(pendingStops),
                    Convert(pendingRebuilds),
                    definitionHash);
            }

            private static VfxInstanceDto[] Convert(IReadOnlyList<VfxInstanceSnapshot> values)
            {
                if (values == null || values.Count == 0)
                    return Array.Empty<VfxInstanceDto>();

                var result = new VfxInstanceDto[values.Count];
                for (int i = 0; i < values.Count; i++)
                    result[i] = VfxInstanceDto.From(values[i]);
                return result;
            }

            private static List<VfxInstanceSnapshot> Convert(VfxInstanceDto[] values)
            {
                var result = new List<VfxInstanceSnapshot>();
                if (values == null)
                    return result;
                for (int i = 0; i < values.Length; i++)
                    result.Add(values[i].ToInstance());
                return result;
            }

            private static VfxSpawnRequestDto[] Convert(IReadOnlyList<VfxSpawnRequest> values)
            {
                if (values == null || values.Count == 0)
                    return Array.Empty<VfxSpawnRequestDto>();

                var result = new VfxSpawnRequestDto[values.Count];
                for (int i = 0; i < values.Count; i++)
                    result[i] = VfxSpawnRequestDto.From(values[i]);
                return result;
            }

            private static List<VfxSpawnRequest> Convert(VfxSpawnRequestDto[] values)
            {
                var result = new List<VfxSpawnRequest>();
                if (values == null)
                    return result;
                for (int i = 0; i < values.Length; i++)
                    result.Add(values[i].ToRequest());
                return result;
            }

            private static VfxStopRequestDto[] Convert(IReadOnlyList<VfxStopRequest> values)
            {
                if (values == null || values.Count == 0)
                    return Array.Empty<VfxStopRequestDto>();

                var result = new VfxStopRequestDto[values.Count];
                for (int i = 0; i < values.Count; i++)
                    result[i] = VfxStopRequestDto.From(values[i]);
                return result;
            }

            private static List<VfxStopRequest> Convert(VfxStopRequestDto[] values)
            {
                var result = new List<VfxStopRequest>();
                if (values == null)
                    return result;
                for (int i = 0; i < values.Length; i++)
                    result.Add(values[i].ToRequest());
                return result;
            }

            private static VfxRebuildRequestDto[] Convert(IReadOnlyList<VfxRebuildRequest> values)
            {
                if (values == null || values.Count == 0)
                    return Array.Empty<VfxRebuildRequestDto>();

                var result = new VfxRebuildRequestDto[values.Count];
                for (int i = 0; i < values.Count; i++)
                    result[i] = VfxRebuildRequestDto.From(values[i]);
                return result;
            }

            private static List<VfxRebuildRequest> Convert(VfxRebuildRequestDto[] values)
            {
                var result = new List<VfxRebuildRequest>();
                if (values == null)
                    return result;
                for (int i = 0; i < values.Length; i++)
                    result.Add(values[i].ToRequest());
                return result;
            }
        }

        private sealed class VfxInstanceDto
        {
            public long instanceId;
            public int definitionId;
            public string definitionHash;
            public string prefabId;
            public string prefabTypeId;
            public string prefabVariant;
            public string prefabPackageId;
            public long startFrame;
            public long expireFrame;
            public VfxAnchorDto anchor;
            public VfxParameterDto parameters;
            public int sourceDomain;
            public long sourceFrame;
            public int localFrame;
            public int sourceOrder;
            public int triggerId;
            public int sourceId;
            public int targetId;
            public RuntimeTraceIdentity traceId;
            public int expireStopMode;
            public int playbackRatePermille;
            public int sourceStartOffsetFrames;
            public int sourceEndOffsetFrames;

            public static VfxInstanceDto From(VfxInstanceSnapshot value)
            {
                return new VfxInstanceDto
                {
                    instanceId = value.InstanceId.Value,
                    definitionId = value.DefinitionId.Value,
                    definitionHash = value.DefinitionHash,
                    prefabId = value.PrefabResource.Id,
                    prefabTypeId = value.PrefabResource.TypeId,
                    prefabVariant = value.PrefabResource.Variant,
                    prefabPackageId = value.PrefabResource.PackageId,
                    startFrame = value.StartFrame.Value,
                    expireFrame = value.ExpireFrame.Value,
                    anchor = VfxAnchorDto.From(value.Anchor),
                    parameters = VfxParameterDto.From(value.Parameters),
                    sourceDomain = value.SourceDomain,
                    sourceFrame = value.SourceFrame,
                    localFrame = value.LocalFrame,
                    sourceOrder = value.SourceOrder,
                    triggerId = value.TriggerId,
                    sourceId = value.SourceId,
                    targetId = value.TargetId,
                    traceId = value.TraceId,
                    expireStopMode = (int)value.ExpireStopMode,
                    playbackRatePermille = value.PlaybackRatePermille,
                    sourceStartOffsetFrames = value.SourceStartOffsetFrames,
                    sourceEndOffsetFrames = value.SourceEndOffsetFrames
                };
            }

            public VfxInstanceSnapshot ToInstance()
            {
                return new VfxInstanceSnapshot(
                    new VfxInstanceId(instanceId),
                    new VfxDefinitionId(definitionId),
                    definitionHash,
                    new ResourceKey(prefabId, prefabTypeId, prefabVariant, prefabPackageId),
                    new RuntimeFrame(startFrame),
                    new RuntimeFrame(expireFrame),
                    anchor != null ? anchor.ToAnchor() : VfxAnchor.World(VfxPose.Identity),
                    parameters != null ? parameters.ToParameters() : VfxParameterBlock.Empty,
                    sourceDomain,
                    sourceFrame,
                    localFrame,
                    sourceOrder,
                    triggerId,
                    sourceId,
                    targetId,
                    traceId,
                    (VfxStopMode)expireStopMode,
                    playbackRatePermille,
                    sourceStartOffsetFrames,
                    sourceEndOffsetFrames);
            }
        }

        private sealed class VfxSpawnRequestDto
        {
            public long frame;
            public int sourceDomain;
            public long sourceFrame;
            public int localFrame;
            public int sourceOrder;
            public int triggerId;
            public int sourceId;
            public int targetId;
            public int definitionId;
            public VfxAnchorDto anchor;
            public VfxParameterDto parameters;
            public int lifetimeFramesOverride;
            public RuntimeTraceIdentity traceId;
            public long sequence;
            public int expireStopMode;
            public int playbackRatePermille;
            public int sourceStartOffsetFrames;
            public int sourceEndOffsetFrames;

            public static VfxSpawnRequestDto From(VfxSpawnRequest value)
            {
                return new VfxSpawnRequestDto
                {
                    frame = value.Frame.Value,
                    sourceDomain = value.SourceDomain,
                    sourceFrame = value.SourceFrame,
                    localFrame = value.LocalFrame,
                    sourceOrder = value.SourceOrder,
                    triggerId = value.TriggerId,
                    sourceId = value.SourceId,
                    targetId = value.TargetId,
                    definitionId = value.DefinitionId.Value,
                    anchor = VfxAnchorDto.From(value.Anchor),
                    parameters = VfxParameterDto.From(value.Parameters),
                    lifetimeFramesOverride = value.LifetimeFramesOverride,
                    traceId = value.TraceId,
                    sequence = value.Sequence,
                    expireStopMode = (int)value.ExpireStopMode,
                    playbackRatePermille = value.PlaybackRatePermille,
                    sourceStartOffsetFrames = value.SourceStartOffsetFrames,
                    sourceEndOffsetFrames = value.SourceEndOffsetFrames
                };
            }

            public VfxSpawnRequest ToRequest()
            {
                return new VfxSpawnRequest(
                    new RuntimeFrame(frame),
                    sourceDomain,
                    sourceFrame,
                    localFrame,
                    sourceOrder,
                    triggerId,
                    sourceId,
                    targetId,
                    new VfxDefinitionId(definitionId),
                    anchor != null ? anchor.ToAnchor() : VfxAnchor.World(VfxPose.Identity),
                    parameters != null ? parameters.ToParameters() : VfxParameterBlock.Empty,
                    lifetimeFramesOverride,
                    traceId,
                    sequence,
                    (VfxStopMode)expireStopMode,
                    playbackRatePermille,
                    sourceStartOffsetFrames,
                    sourceEndOffsetFrames);
            }
        }

        private sealed class VfxStopRequestDto
        {
            public long frame;
            public long instanceId;
            public string reason;
            public int reasonCode;
            public int reasonValue;
            public RuntimeTraceIdentity traceId;
            public long sequence;
            public int stopMode;

            public static VfxStopRequestDto From(VfxStopRequest value)
            {
                return new VfxStopRequestDto
                {
                    frame = value.Frame.Value,
                    instanceId = value.InstanceId.Value,
                    reason = value.Reason,
                    reasonCode = (int)value.ReasonCode,
                    reasonValue = value.ReasonValue,
                    traceId = value.TraceId,
                    sequence = value.Sequence,
                    stopMode = (int)value.StopMode
                };
            }

            public VfxStopRequest ToRequest()
            {
                return new VfxStopRequest(
                    new RuntimeFrame(frame),
                    new VfxInstanceId(instanceId),
                    (VfxStopMode)stopMode,
                    (VfxStopReasonCode)reasonCode,
                    reasonValue,
                    reason,
                    traceId,
                    sequence);
            }
        }

        private sealed class VfxRebuildRequestDto
        {
            public long frame;
            public long instanceId;
            public RuntimeTraceIdentity traceId;
            public long sequence;

            public static VfxRebuildRequestDto From(VfxRebuildRequest value)
            {
                return new VfxRebuildRequestDto
                {
                    frame = value.Frame.Value,
                    instanceId = value.InstanceId.Value,
                    traceId = value.TraceId,
                    sequence = value.Sequence
                };
            }

            public VfxRebuildRequest ToRequest()
            {
                return new VfxRebuildRequest(
                    new RuntimeFrame(frame),
                    new VfxInstanceId(instanceId),
                    traceId,
                    sequence);
            }
        }

        private sealed class VfxAnchorDto
        {
            public int kind;
            public int sourceId;
            public int targetId;
            public int socketId;
            public VfxPoseDto pose;

            public static VfxAnchorDto From(VfxAnchor value)
            {
                return new VfxAnchorDto
                {
                    kind = (int)value.Kind,
                    sourceId = value.SourceId,
                    targetId = value.TargetId,
                    socketId = value.SocketId,
                    pose = VfxPoseDto.From(value.Pose)
                };
            }

            public VfxAnchor ToAnchor()
            {
                return new VfxAnchor((VfxAnchorKind)kind, sourceId, targetId, socketId, pose != null ? pose.ToPose() : VfxPose.Identity);
            }
        }

        private sealed class VfxPoseDto
        {
            public long px;
            public long py;
            public long pz;
            public long fx;
            public long fy;
            public long fz;
            public long rx;
            public long ry;
            public long rz;
            public long sx;
            public long sy;
            public long sz;

            public static VfxPoseDto From(VfxPose value)
            {
                return new VfxPoseDto
                {
                    px = value.Position.RawX,
                    py = value.Position.RawY,
                    pz = value.Position.RawZ,
                    fx = value.Forward.RawX,
                    fy = value.Forward.RawY,
                    fz = value.Forward.RawZ,
                    rx = value.RotationEulerDegrees.RawX,
                    ry = value.RotationEulerDegrees.RawY,
                    rz = value.RotationEulerDegrees.RawZ,
                    sx = value.Scale.RawX,
                    sy = value.Scale.RawY,
                    sz = value.Scale.RawZ
                };
            }

            public VfxPose ToPose()
            {
                return new VfxPose(
                    VfxVector3.FromRaw(px, py, pz),
                    VfxVector3.FromRaw(fx, fy, fz),
                    VfxVector3.FromRaw(rx, ry, rz),
                    sx == 0L && sy == 0L && sz == 0L
                        ? VfxVector3.One
                        : VfxVector3.FromRaw(sx, sy, sz));
            }
        }

        private sealed class VfxParameterDto
        {
            public int v0;
            public int v1;
            public int v2;
            public int v3;

            public static VfxParameterDto From(VfxParameterBlock value)
            {
                return new VfxParameterDto
                {
                    v0 = value.Value0,
                    v1 = value.Value1,
                    v2 = value.Value2,
                    v3 = value.Value3
                };
            }

            public VfxParameterBlock ToParameters()
            {
                return new VfxParameterBlock(v0, v1, v2, v3);
            }
        }
    }

    public sealed class VfxDebugSource : IFrameworkDebugSource
    {
        private readonly VfxRuntimeModule _module;

        public VfxDebugSource(VfxRuntimeModule module, string name = "VFX Runtime")
        {
            _module = module ?? throw new ArgumentNullException(nameof(module));
            Name = name ?? string.Empty;
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => true;

        public FrameworkDebugSnapshot CreateSnapshot()
        {
            VfxDebugSnapshot snapshot = _module.CreateDebugSnapshot();
            string body = "moduleId: " + snapshot.ModuleId + "\n"
                + "active: " + snapshot.ActiveCount + "\n"
                + "pendingSpawns: " + snapshot.PendingSpawnCount + "\n"
                + "pendingStops: " + snapshot.PendingStopCount + "\n"
                + "pendingRebuilds: " + snapshot.PendingRebuildCount + "\n"
                + "pendingEvents: " + snapshot.PendingEventCount + "\n"
                + "nextInstanceId: " + snapshot.NextInstanceId + "\n"
                + "definitionHash: " + snapshot.DefinitionHash;
            return new FrameworkDebugSnapshot(
                Name,
                Mode,
                new[] { new FrameworkDebugSection("VFX Runtime", body) });
        }
    }
}
