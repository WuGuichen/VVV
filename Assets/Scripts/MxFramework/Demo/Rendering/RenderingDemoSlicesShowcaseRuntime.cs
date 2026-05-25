using System;
using System.Collections.Generic;
using MxFramework.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace MxFramework.Demo.Rendering
{
    public enum RenderingDemoCommand
    {
        ToggleWind = 0,
        PulseMaterial = 1,
        PublishEventBurst = 2,
        CycleVolumePriority = 3,
        Reset = 4
    }

    public sealed class RenderingDemoSlicesShowcaseRuntime : IDisposable
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private readonly Queue<RenderingDemoCommand> _commands = new Queue<RenderingDemoCommand>();
        private readonly GlobalFrameContext _globalFrameContext = new GlobalFrameContext();
        private readonly MxRenderSubjectRegistry _subjects = new MxRenderSubjectRegistry();
        private readonly RenderDataPublisher _publisher;
        private readonly MaterialBindingHub _materialHub;
        private readonly VolumeBlender _volumeBlender = new VolumeBlender();
        private readonly SharedRenderTextureRegistry _sharedRenderTextures = new SharedRenderTextureRegistry();
        private readonly MxRenderPipeline _localPipeline = new MxRenderPipeline();
        private readonly RenderingDemoSyntheticPassProvider _passProvider;
        private readonly List<IMxRenderPipeline> _registeredPipelines = new List<IMxRenderPipeline>();
        private readonly List<string> _events = new List<string>();

        private MaterialBinding _statusBinding;
        private MaterialBinding _overlayBinding;
        private MxRenderSubjectId _subject;
        private Renderer _targetRenderer;
        private float _time;
        private float _windAngle;
        private float _windStrength = 0.6f;
        private float _materialPulse;
        private int _volumePriority = 10;
        private MxVolumeRequestId _globalVolume;
        private MxVolumeRequestId _cameraVolume;
        private RenderingDemoSlicesSnapshot _snapshot;
        private bool _disposed;

        public RenderingDemoSlicesShowcaseRuntime()
        {
            _publisher = new RenderDataPublisher(_subjects, recentCapacity: 12);
            _materialHub = new MaterialBindingHub(_subjects);
            _passProvider = new RenderingDemoSyntheticPassProvider();
            _localPipeline.RegisterProvider(_passProvider);
            _registeredPipelines.Add(_localPipeline);
        }

        public RenderingDemoSlicesSnapshot Snapshot => _snapshot;

        public void Initialize(Renderer targetRenderer, IEnumerable<IMxRenderPipeline> externalPipelines = null)
        {
            _targetRenderer = targetRenderer;
            _subject = _subjects.Register(MxRenderSubjectRole.Primary);
            if (_targetRenderer != null)
            {
                _statusBinding = _materialHub.Bind(_subject, MxMaterialChannel.StatusTint, MaterialBindingScope.ForRenderer(_targetRenderer));
                _overlayBinding = _materialHub.Bind(_subject, MxMaterialChannel.DebugOverlay, MaterialBindingScope.ForRenderer(_targetRenderer));
            }

            if (externalPipelines != null)
            {
                foreach (IMxRenderPipeline pipeline in externalPipelines)
                {
                    if (pipeline == null || _registeredPipelines.Contains(pipeline))
                        continue;

                    pipeline.RegisterProvider(_passProvider);
                    _registeredPipelines.Add(pipeline);
                }
            }

            RequestInitialVolumes();
            AddEvent("Showcase initialized with generic render subject " + _subject + ".");
            Step(0f);
        }

        public void Enqueue(RenderingDemoCommand command)
        {
            _commands.Enqueue(command);
        }

        public void Step(float deltaTime)
        {
            if (_disposed)
                return;

            _time += Mathf.Max(0f, deltaTime);
            _publisher.BeginFrame();
            ProcessCommands();
            ApplyContext();
            ApplyMaterialBinding(deltaTime);
            PublishMovement();
            RunSyntheticPipeline();
            RefreshSnapshot();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            for (int i = 0; i < _registeredPipelines.Count; i++)
                _registeredPipelines[i]?.UnregisterProvider(_passProvider.DebugName);

            _materialHub.Release(_subject);
            _sharedRenderTextures.Dispose();
            _disposed = true;
        }

        private void ProcessCommands()
        {
            while (_commands.Count > 0)
            {
                switch (_commands.Dequeue())
                {
                    case RenderingDemoCommand.ToggleWind:
                        _windStrength = _windStrength > 0.7f ? 0.25f : 1.0f;
                        AddEvent("Wind strength toggled through GlobalFrameContext.");
                        break;
                    case RenderingDemoCommand.PulseMaterial:
                        _materialPulse = 1f;
                        AddEvent("Material pulse queued through MaterialBindingHub.");
                        break;
                    case RenderingDemoCommand.PublishEventBurst:
                        PublishEventBurst();
                        break;
                    case RenderingDemoCommand.CycleVolumePriority:
                        CycleVolumePriority();
                        break;
                    case RenderingDemoCommand.Reset:
                        Reset();
                        break;
                }
            }
        }

        private void ApplyContext()
        {
            _windAngle += 36f * TimeDeltaForDisplay();
            Vector3 wind = new Vector3(Mathf.Cos(_windAngle * Mathf.Deg2Rad), 0f, Mathf.Sin(_windAngle * Mathf.Deg2Rad)).normalized;
            _globalFrameContext.SetTime(_time, _time, TimeDeltaForDisplay());
            _globalFrameContext.SetWind(wind, _windStrength, 0.35f);
            _globalFrameContext.SetWeather(0.2f + _windStrength * 0.2f, 0.05f, 0f);
            _globalFrameContext.SetPrimarySubjectPose(new Vector3(Mathf.Sin(_time), 0.5f, 0f), wind * _windStrength);
            _globalFrameContext.SetLocalSubjectPose(new Vector3(0f, 0.5f, Mathf.Cos(_time)), wind);
        }

        private void ApplyMaterialBinding(float deltaTime)
        {
            if (_statusBinding.IsValid)
            {
                Color status = Color.Lerp(new Color(0.15f, 0.55f, 0.95f, 1f), new Color(0.9f, 0.72f, 0.18f, 1f), _windStrength);
                _materialHub.SetColor(_statusBinding, BaseColorId, status);
            }

            if (_overlayBinding.IsValid)
            {
                _materialPulse = Mathf.Max(0f, _materialPulse - Mathf.Max(0f, deltaTime));
                Color pulse = Color.Lerp(Color.black, new Color(0.2f, 0.95f, 0.78f, 1f), _materialPulse);
                _materialHub.SetColor(_overlayBinding, EmissionColorId, pulse);
            }

            _materialHub.Flush();
        }

        private void PublishMovement()
        {
            if (!_subject.IsValid)
                return;

            Vector3 velocity = _globalFrameContext.Snapshot().PrimarySubjectVelocity;
            _publisher.PublishSubjectMovement(_subject, velocity);
        }

        private void PublishEventBurst()
        {
            _publisher.PublishImpact(_subject, new MxRenderImpactEvent(new Vector3(0f, 0.5f, 0f), new Color(0.2f, 0.95f, 0.78f, 1f), 1f, 0.35f));
            _publisher.PublishSurfaceContact(_subject, new MxRenderSurfaceContactEvent(Vector3.zero, 1.2f, 0.65f));
            _publisher.PublishFieldImpulse(_subject, new MxRenderFieldImpulseEvent(Vector3.zero, 1.8f, 0.9f, 1));
            _publisher.PublishSubjectLifecycle(_subject, MxSubjectLifecycleKind.Enabled);
            AddEvent("Published generic impact/contact/field/lifecycle render events.");
        }

        private void CycleVolumePriority()
        {
            _volumePriority = _volumePriority >= 30 ? 5 : _volumePriority + 5;
            if (_cameraVolume.IsValid)
                _volumeBlender.Release(_cameraVolume, _time);

            _cameraVolume = _volumeBlender.Request(
                new MxVolumeRequestDescriptor(
                    new MxVolumeProfileReference("mxframework.demo.rendering.camera-focus"),
                    MxVolumeRequestScope.ForCameraKind(MxCameraRenderKind.Game),
                    _volumePriority,
                    new MxVolumeBlendTiming(0.15f, 0.9f, 0.35f),
                    "Camera diagnostics profile"),
                _time);
            AddEvent("VolumeBlender camera request priority set to " + _volumePriority + ".");
        }

        private void Reset()
        {
            _windStrength = 0.6f;
            _materialPulse = 0f;
            _volumePriority = 10;
            _events.Clear();
            RequestInitialVolumes();
            AddEvent("Showcase state reset.");
        }

        private void RequestInitialVolumes()
        {
            if (_globalVolume.IsValid)
                _volumeBlender.Release(_globalVolume, _time);
            if (_cameraVolume.IsValid)
                _volumeBlender.Release(_cameraVolume, _time);

            _volumeBlender.SetPresentationTime(_time);
            _globalVolume = _volumeBlender.Request(
                new MxVolumeRequestDescriptor(
                    new MxVolumeProfileReference("mxframework.demo.rendering.global-base"),
                    MxVolumeRequestScope.Global(),
                    10,
                    new MxVolumeBlendTiming(0.1f, 0f, 0.3f),
                    "Global diagnostics profile"),
                _time);
            CycleVolumePriority();
        }

        private void RunSyntheticPipeline()
        {
            _sharedRenderTextures.BeginFrame();
            var descriptor = new MxCameraRenderContextDescriptor(MxCameraRenderKind.Game, null, Vector3.forward);
            IReadOnlyList<IMxRenderPass> passes = _localPipeline.CollectPasses(descriptor);
            var cameraSnapshot = new CameraRenderSnapshot(MxCameraRenderKind.Game, null, Vector3.forward, Array.Empty<CameraShaderOverride>());
            for (int i = 0; i < passes.Count; i++)
                passes[i].Configure(new MxRenderPassConfigureContext(descriptor, cameraSnapshot, _sharedRenderTextures));
            _sharedRenderTextures.EndFrame();
        }

        private void RefreshSnapshot()
        {
            MxVolumeBlendStateSnapshot blendState = _volumeBlender.CaptureBlendState(
                new MxVolumeEvaluationContext(MxCameraRenderKind.Game, default, _time));
            _snapshot = new RenderingDemoSlicesSnapshot(
                _globalFrameContext.Snapshot(),
                _localPipeline.CaptureTopology(),
                _sharedRenderTextures.CaptureDiagnostics(),
                _materialHub.CaptureDiagnostics(),
                _publisher.CaptureSnapshot(),
                _volumeBlender.CaptureDiagnostics(),
                blendState,
                _events.ToArray());
        }

        private void AddEvent(string text)
        {
            _events.Add(text);
            while (_events.Count > 8)
                _events.RemoveAt(0);
        }

        private float TimeDeltaForDisplay()
        {
            return 1f / 30f;
        }

        private sealed class RenderingDemoSyntheticPassProvider : IMxRenderPassProvider
        {
            private readonly RenderingDemoSyntheticPass _writer;
            private readonly RenderingDemoSyntheticPass _reader;

            public RenderingDemoSyntheticPassProvider()
            {
                SharedRTWriterSetId writerSet = new SharedRTWriterSetId("mxframework.demo.rendering.writer-set");
                SharedRTOwnerId writer = new SharedRTOwnerId("mxframework.demo.rendering.writer");
                SharedRTOwnerId reader = new SharedRTOwnerId("mxframework.demo.rendering.reader");
                SharedRenderTextureKey key = new SharedRenderTextureKey(
                    new SharedRTId("mxframework.demo.rendering.synthetic-rt"),
                    "Rendering Demo Synthetic RT",
                    writer,
                    new SharedRTAccessPolicy(false, SharedRTOrderRule.ReadAfterWriteSameFrame, writerSet),
                    SharedRTAnchor.MainCamera,
                    SharedRTFormat.ARGB32,
                    new SharedRTSize(64, 64),
                    new SharedRTClearSpec(SharedRTClearKind.ClearEveryFrame, Color.clear),
                    SharedRTResizePolicy.KeepLargest,
                    64L * 64L * 4L);

                _writer = new RenderingDemoSyntheticPass("RenderingDemo.WriteSyntheticRT", true, key, writer, writerSet, reader);
                _reader = new RenderingDemoSyntheticPass("RenderingDemo.ReadSyntheticRT", false, key, writer, writerSet, reader);
            }

            public string DebugName => "RenderingDemoSyntheticPassProvider";

            public void CollectPasses(IMxRenderPassRegistry registry, in MxCameraRenderContextDescriptor cameraContext)
            {
                if (cameraContext.CameraKind == MxCameraRenderKind.Game || cameraContext.CameraKind == MxCameraRenderKind.Unknown)
                {
                    registry.RegisterPass(_writer);
                    registry.RegisterPass(_reader);
                }
            }
        }

        private sealed class RenderingDemoSyntheticPass : IMxRenderPass
        {
            private readonly SharedRenderTextureKey _key;
            private readonly SharedRTOwnerId _writer;
            private readonly SharedRTWriterSetId _writerSet;
            private readonly SharedRTOwnerId _reader;
            private readonly bool _writes;
            private readonly SharedRenderTextureKey[] _reads;
            private readonly SharedRenderTextureKey[] _writesList;
            private SharedRTHandle _handle;

            public RenderingDemoSyntheticPass(
                string debugName,
                bool writes,
                SharedRenderTextureKey key,
                SharedRTOwnerId writer,
                SharedRTWriterSetId writerSet,
                SharedRTOwnerId reader)
            {
                DebugName = debugName;
                _writes = writes;
                _key = key;
                _writer = writer;
                _writerSet = writerSet;
                _reader = reader;
                _reads = writes ? Array.Empty<SharedRenderTextureKey>() : new[] { key };
                _writesList = writes ? new[] { key } : Array.Empty<SharedRenderTextureKey>();
                Order = writes ? 10 : 20;
            }

            public string DebugName { get; }
            public MxRenderPhase Phase => MxRenderPhase.AfterRenderingOpaques;
            public int Order { get; }
            public bool IsEnabled => true;
            public IReadOnlyList<SharedRenderTextureKey> Reads => _reads;
            public IReadOnlyList<SharedRenderTextureKey> Writes => _writesList;

            public void Configure(in MxRenderPassConfigureContext context)
            {
                context.SharedRenderTextures.RegisterWriterSet(_writerSet, new[] { _writer });
                if (_writes)
                {
                    _handle = context.SharedRenderTextures.Register(_key);
                    if (context.SharedRenderTextures is SharedRenderTextureRegistry concrete)
                        concrete.RecordWriter(_handle, _writer, Phase, Order);
                    return;
                }

                _handle = context.SharedRenderTextures.Register(_key);
                if (context.SharedRenderTextures is SharedRenderTextureRegistry registry)
                    registry.RecordReader(_handle, _reader, Phase, Order);
            }

            public void Execute(in MxRenderPassExecuteContext context)
            {
            }
        }
    }

    public readonly struct RenderingDemoSlicesSnapshot
    {
        public RenderingDemoSlicesSnapshot(
            GlobalFrameSnapshot globals,
            MxRenderPipelineTopologySnapshot topology,
            SharedRTDiagnosticsSnapshot sharedRT,
            MaterialBindingDiagnosticsSnapshot materialBindings,
            RenderDataPublisherSnapshot publisher,
            MxVolumeDiagnosticsSnapshot volumeDiagnostics,
            MxVolumeBlendStateSnapshot volumeBlendState,
            IReadOnlyList<string> events)
        {
            Globals = globals;
            Topology = topology;
            SharedRT = sharedRT;
            MaterialBindings = materialBindings;
            Publisher = publisher;
            VolumeDiagnostics = volumeDiagnostics;
            VolumeBlendState = volumeBlendState;
            Events = events ?? Array.Empty<string>();
        }

        public GlobalFrameSnapshot Globals { get; }
        public MxRenderPipelineTopologySnapshot Topology { get; }
        public SharedRTDiagnosticsSnapshot SharedRT { get; }
        public MaterialBindingDiagnosticsSnapshot MaterialBindings { get; }
        public RenderDataPublisherSnapshot Publisher { get; }
        public MxVolumeDiagnosticsSnapshot VolumeDiagnostics { get; }
        public MxVolumeBlendStateSnapshot VolumeBlendState { get; }
        public IReadOnlyList<string> Events { get; }
    }
}
