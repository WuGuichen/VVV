using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

[assembly: InternalsVisibleTo("MxFramework.Tests")]

namespace MxFramework.Rendering
{
    public sealed class MxRenderingPipelineFeature : ScriptableRendererFeature
    {
        internal const string SharedRTLifecycleScope = "PerCameraRenderInvocation";
        private const string SharedRTBeginFrameStep = "SharedRT.BeginFrame(sync)";
        private const string CameraGlobalsStep = "CameraGlobals";
        private const string SharedRTEndFrameStep = "SharedRT.EndFrame";

        private readonly List<MxRenderPassWrapper> _passWrappers = new List<MxRenderPassWrapper>();
        private MxRenderPipeline _pipeline;
        private CameraRenderContext _cameraContext;
        private MxCameraGlobalsPass _cameraGlobalsPass;
        private MxSharedRTLifecyclePass _sharedRTEndFramePass;
        private SharedRenderTextureRegistry _sharedRenderTextures;

        public IMxRenderPipeline Pipeline
        {
            get
            {
                EnsureCreated();
                return _pipeline;
            }
        }

        public override void Create()
        {
            EnsureCreated();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderer == null)
                return;

            EnsureCreated();

            MxCameraRenderContextDescriptor descriptor = CreateDescriptor(ref renderingData);
            CameraRenderSnapshot cameraSnapshot = BeginSharedRTFrameForCameraInvocation(descriptor);
            IReadOnlyList<IMxRenderPass> passes = _pipeline.CollectPasses(descriptor);

            _cameraGlobalsPass.Setup(cameraSnapshot);
            renderer.EnqueuePass(_cameraGlobalsPass);

            EnsureWrapperCapacity(passes.Count);
            for (int i = 0; i < passes.Count; i++)
            {
                IMxRenderPass pass = passes[i];
                ConfigureMxPass(pass, descriptor, cameraSnapshot, _sharedRenderTextures);
                _passWrappers[i].Setup(pass, descriptor, cameraSnapshot, _sharedRenderTextures);
                renderer.EnqueuePass(_passWrappers[i]);
            }

            ClearInactiveWrappers(passes.Count);

            _sharedRTEndFramePass.Setup(_sharedRenderTextures);
            renderer.EnqueuePass(_sharedRTEndFramePass);
        }

        protected override void Dispose(bool disposing)
        {
            DisposeFeatureResources();
            base.Dispose(disposing);
        }

        private void EnsureCreated()
        {
            if (_pipeline == null)
                _pipeline = new MxRenderPipeline();
            if (_cameraContext == null)
                _cameraContext = new CameraRenderContext();
            if (_sharedRenderTextures == null)
                _sharedRenderTextures = new SharedRenderTextureRegistry();
            if (_cameraGlobalsPass == null)
                _cameraGlobalsPass = new MxCameraGlobalsPass();
            if (_sharedRTEndFramePass == null)
                _sharedRTEndFramePass = new MxSharedRTLifecyclePass(false);
        }

        private void EnsureWrapperCapacity(int count)
        {
            while (_passWrappers.Count < count)
                _passWrappers.Add(new MxRenderPassWrapper());
        }

        private void ClearInactiveWrappers(int activeCount)
        {
            for (int i = activeCount; i < _passWrappers.Count; i++)
                _passWrappers[i].Clear();
        }

        private void DisposeFeatureResources()
        {
            _sharedRTEndFramePass?.Clear();
            _cameraGlobalsPass?.Clear();

            for (int i = 0; i < _passWrappers.Count; i++)
                _passWrappers[i].Clear();
            _passWrappers.Clear();

            _sharedRenderTextures?.Dispose();
            _sharedRenderTextures = null;
            _cameraGlobalsPass = null;
            _sharedRTEndFramePass = null;
            _cameraContext = null;
            _pipeline = null;
        }

        internal SharedRenderTextureRegistry EnsureSharedRenderTexturesForTests()
        {
            EnsureCreated();
            return _sharedRenderTextures;
        }

        internal SharedRenderTextureRegistry CurrentSharedRenderTexturesForTests => _sharedRenderTextures;

        internal void BeginSharedRTFrameForTests()
        {
            EnsureCreated();
            _sharedRenderTextures.BeginFrame();
        }

        internal void ConfigureRegisteredPassesForTests(MxCameraRenderContextDescriptor descriptor)
        {
            EnsureCreated();
            CameraRenderSnapshot cameraSnapshot = BeginSharedRTFrameForCameraInvocation(descriptor);
            IReadOnlyList<IMxRenderPass> passes = _pipeline.CollectPasses(descriptor);
            for (int i = 0; i < passes.Count; i++)
                ConfigureMxPass(passes[i], descriptor, cameraSnapshot, _sharedRenderTextures);
        }

        internal void EndSharedRTFrameForTests()
        {
            EnsureCreated();
            _sharedRenderTextures.EndFrame();
        }

        internal void DisposeFeatureResourcesForTests()
        {
            DisposeFeatureResources();
        }

        internal static IReadOnlyList<string> CaptureLifecycleTopologyForTests(IReadOnlyList<IMxRenderPass> passes)
        {
            var topology = new List<string> { SharedRTBeginFrameStep, CameraGlobalsStep };
            if (passes != null)
            {
                for (int i = 0; i < passes.Count; i++)
                    topology.Add(passes[i] != null ? passes[i].DebugName : string.Empty);
            }

            topology.Add(SharedRTEndFrameStep);
            return topology;
        }

        private CameraRenderSnapshot BeginSharedRTFrameForCameraInvocation(in MxCameraRenderContextDescriptor descriptor)
        {
            _cameraContext.SetDescriptor(descriptor);
            CameraRenderSnapshot cameraSnapshot = _cameraContext.Snapshot();
            _sharedRenderTextures.BeginFrame();
            return cameraSnapshot;
        }

        private static void ConfigureMxPass(
            IMxRenderPass pass,
            MxCameraRenderContextDescriptor descriptor,
            CameraRenderSnapshot cameraSnapshot,
            ISharedRenderTextureRegistry sharedRenderTextures)
        {
            pass.Configure(new MxRenderPassConfigureContext(descriptor, cameraSnapshot, sharedRenderTextures));
        }

        private static MxCameraRenderContextDescriptor CreateDescriptor(ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            MxCameraRenderKind kind = ToCameraRenderKind(renderingData.cameraData.cameraType);
            Vector3 focus = camera != null ? camera.transform.position + camera.transform.forward : Vector3.zero;
            return new MxCameraRenderContextDescriptor(kind, camera, focus);
        }

        private static MxCameraRenderKind ToCameraRenderKind(CameraType cameraType)
        {
            switch (cameraType)
            {
                case CameraType.Game:
                    return MxCameraRenderKind.Game;
                case CameraType.SceneView:
                    return MxCameraRenderKind.SceneView;
                case CameraType.Reflection:
                    return MxCameraRenderKind.Reflection;
                case CameraType.Preview:
                    return MxCameraRenderKind.Preview;
                default:
                    return MxCameraRenderKind.Unknown;
            }
        }
    }

    public interface IMxRenderPipeline
    {
        bool RegisterPass(IMxRenderPass pass);
        bool UnregisterPass(string debugName);
        bool RegisterProvider(IMxRenderPassProvider provider);
        bool UnregisterProvider(string debugName);
        IReadOnlyList<IMxRenderPass> CollectPasses(in MxCameraRenderContextDescriptor cameraContext);
        MxRenderPipelineTopologySnapshot CaptureTopology();
    }

    public interface IMxRenderPass
    {
        string DebugName { get; }
        MxRenderPhase Phase { get; }
        int Order { get; }
        bool IsEnabled { get; }
        IReadOnlyList<SharedRenderTextureKey> Reads { get; }
        IReadOnlyList<SharedRenderTextureKey> Writes { get; }
        void Configure(in MxRenderPassConfigureContext context);
        void Execute(in MxRenderPassExecuteContext context);
    }

    public interface IMxRenderPassProvider
    {
        string DebugName { get; }
        void CollectPasses(IMxRenderPassRegistry registry, in MxCameraRenderContextDescriptor cameraContext);
    }

    public interface IMxRenderPassRegistry
    {
        bool RegisterPass(IMxRenderPass pass);
    }

    public readonly struct MxRenderPassConfigureContext
    {
        public MxRenderPassConfigureContext(
            MxCameraRenderContextDescriptor cameraContext,
            CameraRenderSnapshot cameraSnapshot,
            ISharedRenderTextureRegistry sharedRenderTextures)
        {
            CameraContext = cameraContext;
            CameraSnapshot = cameraSnapshot;
            SharedRenderTextures = sharedRenderTextures;
        }

        public MxCameraRenderContextDescriptor CameraContext { get; }
        public CameraRenderSnapshot CameraSnapshot { get; }
        public ISharedRenderTextureRegistry SharedRenderTextures { get; }
    }

    public readonly struct MxRenderPassExecuteContext
    {
        public MxRenderPassExecuteContext(
            CommandBuffer commandBuffer,
            MxCameraRenderContextDescriptor cameraContext,
            CameraRenderSnapshot cameraSnapshot,
            ISharedRenderTextureRegistry sharedRenderTextures)
        {
            CommandBuffer = commandBuffer;
            CameraContext = cameraContext;
            CameraSnapshot = cameraSnapshot;
            SharedRenderTextures = sharedRenderTextures;
        }

        public CommandBuffer CommandBuffer { get; }
        public MxCameraRenderContextDescriptor CameraContext { get; }
        public CameraRenderSnapshot CameraSnapshot { get; }
        public ISharedRenderTextureRegistry SharedRenderTextures { get; }
    }

    public sealed class MxRenderPipeline : IMxRenderPipeline
    {
        private static readonly IReadOnlyList<IMxRenderPass> EmptyPasses = Array.Empty<IMxRenderPass>();
        private readonly List<IMxRenderPass> _staticPasses = new List<IMxRenderPass>();
        private readonly List<IMxRenderPassProvider> _providers = new List<IMxRenderPassProvider>();
        private readonly List<IMxRenderPass> _scratchPasses = new List<IMxRenderPass>();
        private readonly List<IMxRenderPass> _lastSortedPasses = new List<IMxRenderPass>();
        private MxRenderPipelineTopologySnapshot _lastTopology = MxRenderPipelineTopologySnapshot.Empty;

        public bool RegisterPass(IMxRenderPass pass)
        {
            if (pass == null)
                return false;

            _staticPasses.Add(pass);
            return true;
        }

        public bool UnregisterPass(string debugName)
        {
            return RemoveByDebugName(_staticPasses, debugName);
        }

        public bool RegisterProvider(IMxRenderPassProvider provider)
        {
            if (provider == null)
                return false;

            _providers.Add(provider);
            return true;
        }

        public bool UnregisterProvider(string debugName)
        {
            return RemoveByDebugName(_providers, debugName);
        }

        public IReadOnlyList<IMxRenderPass> CollectPasses(in MxCameraRenderContextDescriptor cameraContext)
        {
            _scratchPasses.Clear();
            _lastSortedPasses.Clear();
            var diagnostics = new List<MxRenderPipelineDiagnostic>();

            for (int i = 0; i < _staticPasses.Count; i++)
                _scratchPasses.Add(_staticPasses[i]);

            var registry = new CollectorRegistry(_scratchPasses);
            AddProviderMetadataDiagnostics(_providers, diagnostics);
            for (int i = 0; i < _providers.Count; i++)
            {
                IMxRenderPassProvider provider = _providers[i];
                if (provider == null)
                {
                    diagnostics.Add(MxRenderPipelineDiagnostic.InvalidMetadata("Provider entry is null."));
                    continue;
                }

                try
                {
                    provider.CollectPasses(registry, cameraContext);
                }
                catch (Exception exception)
                {
                    diagnostics.Add(MxRenderPipelineDiagnostic.InvalidMetadata("Provider '" + (provider.DebugName ?? string.Empty) + "' failed: " + exception.GetType().Name));
                }
            }

            for (int i = 0; i < _scratchPasses.Count; i++)
            {
                IMxRenderPass pass = _scratchPasses[i];
                if (!IsUsable(pass, diagnostics))
                    continue;
                if (!pass.IsEnabled)
                    continue;

                _lastSortedPasses.Add(pass);
            }

            _lastSortedPasses.Sort(ComparePasses);
            AddDuplicateNameDiagnostics(_lastSortedPasses, diagnostics);
            AddSharedRTTopologyDiagnostics(_lastSortedPasses, diagnostics);
            _lastTopology = MxRenderPipelineTopologySnapshot.Create(cameraContext.CameraKind, _lastSortedPasses, diagnostics);

            return _lastSortedPasses.Count == 0 ? EmptyPasses : _lastSortedPasses.ToArray();
        }

        public MxRenderPipelineTopologySnapshot CaptureTopology()
        {
            return _lastTopology;
        }

        private static bool RemoveByDebugName<T>(List<T> items, string debugName)
        {
            if (string.IsNullOrWhiteSpace(debugName))
                return false;

            bool removed = false;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                string itemName = string.Empty;
                if (items[i] is IMxRenderPass pass)
                    itemName = pass.DebugName;
                else if (items[i] is IMxRenderPassProvider provider)
                    itemName = provider.DebugName;

                if (string.Equals(itemName, debugName, StringComparison.Ordinal))
                {
                    items.RemoveAt(i);
                    removed = true;
                }
            }

            return removed;
        }

        private static bool IsUsable(IMxRenderPass pass, List<MxRenderPipelineDiagnostic> diagnostics)
        {
            if (pass == null)
            {
                diagnostics.Add(MxRenderPipelineDiagnostic.InvalidMetadata("Pass entry is null."));
                return false;
            }

            bool usable = true;
            if (string.IsNullOrWhiteSpace(pass.DebugName))
            {
                diagnostics.Add(MxRenderPipelineDiagnostic.InvalidMetadata("Pass has an empty DebugName."));
                usable = false;
            }

            if (!Enum.IsDefined(typeof(MxRenderPhase), pass.Phase))
            {
                diagnostics.Add(MxRenderPipelineDiagnostic.InvalidMetadata("Pass '" + (pass.DebugName ?? string.Empty) + "' has an invalid phase."));
                usable = false;
            }

            if (pass.Reads == null || pass.Writes == null)
            {
                diagnostics.Add(MxRenderPipelineDiagnostic.InvalidMetadata("Pass '" + (pass.DebugName ?? string.Empty) + "' has null SharedRT metadata."));
                usable = false;
            }

            return usable;
        }

        private static int ComparePasses(IMxRenderPass left, IMxRenderPass right)
        {
            int phase = left.Phase.CompareTo(right.Phase);
            if (phase != 0)
                return phase;

            int order = left.Order.CompareTo(right.Order);
            if (order != 0)
                return order;

            return string.Compare(left.DebugName, right.DebugName, StringComparison.Ordinal);
        }

        private static void AddProviderMetadataDiagnostics(IReadOnlyList<IMxRenderPassProvider> providers, List<MxRenderPipelineDiagnostic> diagnostics)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var duplicates = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < providers.Count; i++)
            {
                IMxRenderPassProvider provider = providers[i];
                if (provider == null)
                    continue;

                string debugName = provider.DebugName;
                if (string.IsNullOrWhiteSpace(debugName))
                {
                    diagnostics.Add(MxRenderPipelineDiagnostic.InvalidProviderMetadata("Provider has an empty DebugName."));
                    continue;
                }

                if (!names.Add(debugName))
                    duplicates.Add(debugName);
            }

            foreach (string duplicate in duplicates)
                diagnostics.Add(MxRenderPipelineDiagnostic.DuplicateProviderDebugName(duplicate));
        }

        private static void AddDuplicateNameDiagnostics(IReadOnlyList<IMxRenderPass> passes, List<MxRenderPipelineDiagnostic> diagnostics)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var duplicates = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < passes.Count; i++)
            {
                string debugName = passes[i].DebugName;
                if (!names.Add(debugName))
                    duplicates.Add(debugName);
            }

            foreach (string duplicate in duplicates)
                diagnostics.Add(MxRenderPipelineDiagnostic.DuplicateDebugName(duplicate));
        }

        private static void AddSharedRTTopologyDiagnostics(IReadOnlyList<IMxRenderPass> passes, List<MxRenderPipelineDiagnostic> diagnostics)
        {
            AddSharedRTCollisionDiagnostics(passes, diagnostics);
            AddSharedRTOrderDiagnostics(passes, diagnostics);
        }

        private static void AddSharedRTCollisionDiagnostics(IReadOnlyList<IMxRenderPass> passes, List<MxRenderPipelineDiagnostic> diagnostics)
        {
            for (int leftIndex = 0; leftIndex < passes.Count; leftIndex++)
            {
                IMxRenderPass left = passes[leftIndex];
                for (int rightIndex = leftIndex + 1; rightIndex < passes.Count; rightIndex++)
                {
                    IMxRenderPass right = passes[rightIndex];
                    if (left.Phase != right.Phase || left.Order != right.Order)
                        continue;

                    if (HasSharedRTCollision(left, right, out SharedRTId id))
                    {
                        diagnostics.Add(MxRenderPipelineDiagnostic.SharedRTPhaseOrderCollision(
                            left.DebugName,
                            right.DebugName,
                            id,
                            left.Phase,
                            left.Order));
                    }
                }
            }
        }

        private static void AddSharedRTOrderDiagnostics(IReadOnlyList<IMxRenderPass> passes, List<MxRenderPipelineDiagnostic> diagnostics)
        {
            var writersById = new Dictionary<SharedRTId, List<SharedRTTopologyUse>>();
            var reads = new List<SharedRTTopologyUse>();

            for (int passIndex = 0; passIndex < passes.Count; passIndex++)
            {
                IMxRenderPass pass = passes[passIndex];
                CollectSharedRTUses(pass, passIndex, false, pass.Reads, reads, diagnostics);
                CollectSharedRTUses(pass, passIndex, true, pass.Writes, writersById, diagnostics);
            }

            foreach (KeyValuePair<SharedRTId, List<SharedRTTopologyUse>> pair in writersById)
            {
                List<SharedRTTopologyUse> writers = pair.Value;
                if (writers.Count <= 1)
                    continue;

                bool additiveAllowed = true;
                for (int i = 0; i < writers.Count; i++)
                {
                    if (!writers[i].Key.Access.AllowAdditiveWriters)
                    {
                        additiveAllowed = false;
                        break;
                    }
                }

                if (!additiveAllowed)
                    diagnostics.Add(MxRenderPipelineDiagnostic.SharedRTMultipleWriters(pair.Key, writers[0].PassDebugName, writers[1].PassDebugName));
            }

            for (int i = 0; i < reads.Count; i++)
            {
                SharedRTTopologyUse read = reads[i];
                if (read.Key.Access.Order != SharedRTOrderRule.ReadAfterWriteSameFrame)
                    continue;

                if (!writersById.TryGetValue(read.Key.Id, out List<SharedRTTopologyUse> writers) || writers.Count == 0)
                {
                    diagnostics.Add(MxRenderPipelineDiagnostic.SharedRTMissingWriter(read.PassDebugName, read.Key.Id));
                    continue;
                }

                int firstWriterIndex = int.MaxValue;
                string firstWriterName = string.Empty;
                for (int writerIndex = 0; writerIndex < writers.Count; writerIndex++)
                {
                    if (writers[writerIndex].PassIndex < firstWriterIndex)
                    {
                        firstWriterIndex = writers[writerIndex].PassIndex;
                        firstWriterName = writers[writerIndex].PassDebugName;
                    }
                }

                if (read.PassIndex <= firstWriterIndex)
                    diagnostics.Add(MxRenderPipelineDiagnostic.SharedRTReadBeforeWrite(read.PassDebugName, firstWriterName, read.Key.Id));
            }
        }

        private static void CollectSharedRTUses(
            IMxRenderPass pass,
            int passIndex,
            bool isWrite,
            IReadOnlyList<SharedRenderTextureKey> keys,
            List<SharedRTTopologyUse> reads,
            List<MxRenderPipelineDiagnostic> diagnostics)
        {
            for (int keyIndex = 0; keyIndex < keys.Count; keyIndex++)
            {
                SharedRenderTextureKey key = keys[keyIndex];
                if (!IsValidSharedRTKey(key, out string message))
                {
                    diagnostics.Add(MxRenderPipelineDiagnostic.InvalidSharedRTMetadata(pass.DebugName, key.Id, isWrite, message));
                    continue;
                }

                reads.Add(new SharedRTTopologyUse(pass.DebugName, passIndex, key));
            }
        }

        private static void CollectSharedRTUses(
            IMxRenderPass pass,
            int passIndex,
            bool isWrite,
            IReadOnlyList<SharedRenderTextureKey> keys,
            Dictionary<SharedRTId, List<SharedRTTopologyUse>> writersById,
            List<MxRenderPipelineDiagnostic> diagnostics)
        {
            for (int keyIndex = 0; keyIndex < keys.Count; keyIndex++)
            {
                SharedRenderTextureKey key = keys[keyIndex];
                if (!IsValidSharedRTKey(key, out string message))
                {
                    diagnostics.Add(MxRenderPipelineDiagnostic.InvalidSharedRTMetadata(pass.DebugName, key.Id, isWrite, message));
                    continue;
                }

                if (!writersById.TryGetValue(key.Id, out List<SharedRTTopologyUse> writers))
                {
                    writers = new List<SharedRTTopologyUse>();
                    writersById.Add(key.Id, writers);
                }

                writers.Add(new SharedRTTopologyUse(pass.DebugName, passIndex, key));
            }
        }

        private static bool IsValidSharedRTKey(SharedRenderTextureKey key, out string message)
        {
            if (!key.Id.IsValid)
            {
                message = "SharedRT key has an invalid id.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(key.DebugName))
            {
                message = "SharedRT key has an empty DebugName.";
                return false;
            }

            if (!key.Owner.IsValid)
            {
                message = "SharedRT key has an invalid owner.";
                return false;
            }

            if (!key.Access.WriterSetId.IsValid)
            {
                message = "SharedRT key has an invalid writer set id.";
                return false;
            }

            if (!Enum.IsDefined(typeof(SharedRTOrderRule), key.Access.Order) ||
                !Enum.IsDefined(typeof(SharedRTAnchor), key.Anchor) ||
                !Enum.IsDefined(typeof(SharedRTFormat), key.Format) ||
                !Enum.IsDefined(typeof(SharedRTClearKind), key.Clear.Kind) ||
                !Enum.IsDefined(typeof(SharedRTResizePolicy), key.Resize))
            {
                message = "SharedRT key contains invalid enum metadata.";
                return false;
            }

            if (!key.Size.IsValid)
            {
                message = "SharedRT key has an invalid size.";
                return false;
            }

            if (key.EstimatedMemoryBytes < 0)
            {
                message = "SharedRT key has a negative estimated memory value.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static bool HasSharedRTCollision(IMxRenderPass left, IMxRenderPass right, out SharedRTId id)
        {
            if (Intersects(left.Writes, right.Writes, out id))
                return true;
            if (Intersects(left.Writes, right.Reads, out id))
                return true;
            if (Intersects(left.Reads, right.Writes, out id))
                return true;

            id = default;
            return false;
        }

        private static bool Intersects(IReadOnlyList<SharedRenderTextureKey> left, IReadOnlyList<SharedRenderTextureKey> right, out SharedRTId id)
        {
            for (int i = 0; i < left.Count; i++)
            {
                SharedRTId leftId = left[i].Id;
                if (!leftId.IsValid)
                    continue;

                for (int j = 0; j < right.Count; j++)
                {
                    if (leftId == right[j].Id)
                    {
                        id = leftId;
                        return true;
                    }
                }
            }

            id = default;
            return false;
        }

        private sealed class CollectorRegistry : IMxRenderPassRegistry
        {
            private readonly List<IMxRenderPass> _passes;

            public CollectorRegistry(List<IMxRenderPass> passes)
            {
                _passes = passes;
            }

            public bool RegisterPass(IMxRenderPass pass)
            {
                if (pass == null)
                    return false;

                _passes.Add(pass);
                return true;
            }
        }

        private readonly struct SharedRTTopologyUse
        {
            public SharedRTTopologyUse(string passDebugName, int passIndex, SharedRenderTextureKey key)
            {
                PassDebugName = passDebugName ?? string.Empty;
                PassIndex = passIndex;
                Key = key;
            }

            public string PassDebugName { get; }
            public int PassIndex { get; }
            public SharedRenderTextureKey Key { get; }
        }
    }

    public enum MxRenderPipelineDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public enum MxRenderPipelineDiagnosticCode
    {
        DuplicateDebugName = 1,
        SharedRTPhaseOrderCollision = 2,
        InvalidMetadata = 3,
        InvalidProviderMetadata = 4,
        DuplicateProviderDebugName = 5,
        SharedRTReadBeforeWrite = 6,
        SharedRTMissingWriter = 7,
        SharedRTMultipleWriters = 8,
        InvalidSharedRTMetadata = 9
    }

    public readonly struct MxRenderPipelineDiagnostic
    {
        public MxRenderPipelineDiagnostic(
            MxRenderPipelineDiagnosticCode code,
            MxRenderPipelineDiagnosticSeverity severity,
            string debugName,
            MxRenderPhase phase,
            int order,
            string message)
        {
            Code = code;
            Severity = severity;
            DebugName = debugName ?? string.Empty;
            Phase = phase;
            Order = order;
            Message = message ?? string.Empty;
        }

        public MxRenderPipelineDiagnosticCode Code { get; }
        public MxRenderPipelineDiagnosticSeverity Severity { get; }
        public string DebugName { get; }
        public MxRenderPhase Phase { get; }
        public int Order { get; }
        public string Message { get; }

        public static MxRenderPipelineDiagnostic DuplicateDebugName(string debugName)
        {
            return new MxRenderPipelineDiagnostic(
                MxRenderPipelineDiagnosticCode.DuplicateDebugName,
                MxRenderPipelineDiagnosticSeverity.Warning,
                debugName,
                default,
                0,
                "Duplicate render pass DebugName: " + debugName);
        }

        public static MxRenderPipelineDiagnostic SharedRTPhaseOrderCollision(
            string leftDebugName,
            string rightDebugName,
            SharedRTId sharedRTId,
            MxRenderPhase phase,
            int order)
        {
            return new MxRenderPipelineDiagnostic(
                MxRenderPipelineDiagnosticCode.SharedRTPhaseOrderCollision,
                MxRenderPipelineDiagnosticSeverity.Warning,
                leftDebugName,
                phase,
                order,
                "Passes '" + leftDebugName + "' and '" + rightDebugName + "' share SharedRT '" + sharedRTId.Value + "' at the same phase/order.");
        }

        public static MxRenderPipelineDiagnostic InvalidMetadata(string message)
        {
            return new MxRenderPipelineDiagnostic(
                MxRenderPipelineDiagnosticCode.InvalidMetadata,
                MxRenderPipelineDiagnosticSeverity.Error,
                string.Empty,
                default,
                0,
                message);
        }

        public static MxRenderPipelineDiagnostic InvalidProviderMetadata(string message)
        {
            return new MxRenderPipelineDiagnostic(
                MxRenderPipelineDiagnosticCode.InvalidProviderMetadata,
                MxRenderPipelineDiagnosticSeverity.Error,
                string.Empty,
                default,
                0,
                message);
        }

        public static MxRenderPipelineDiagnostic DuplicateProviderDebugName(string debugName)
        {
            return new MxRenderPipelineDiagnostic(
                MxRenderPipelineDiagnosticCode.DuplicateProviderDebugName,
                MxRenderPipelineDiagnosticSeverity.Warning,
                debugName,
                default,
                0,
                "Duplicate render pass provider DebugName: " + debugName);
        }

        public static MxRenderPipelineDiagnostic SharedRTReadBeforeWrite(string readerDebugName, string writerDebugName, SharedRTId sharedRTId)
        {
            return new MxRenderPipelineDiagnostic(
                MxRenderPipelineDiagnosticCode.SharedRTReadBeforeWrite,
                MxRenderPipelineDiagnosticSeverity.Error,
                readerDebugName,
                default,
                0,
                "Pass '" + readerDebugName + "' reads SharedRT '" + sharedRTId.Value + "' before writer '" + writerDebugName + "' in the sorted topology.");
        }

        public static MxRenderPipelineDiagnostic SharedRTMissingWriter(string readerDebugName, SharedRTId sharedRTId)
        {
            return new MxRenderPipelineDiagnostic(
                MxRenderPipelineDiagnosticCode.SharedRTMissingWriter,
                MxRenderPipelineDiagnosticSeverity.Error,
                readerDebugName,
                default,
                0,
                "Pass '" + readerDebugName + "' reads SharedRT '" + sharedRTId.Value + "' but no same-frame writer is present in the sorted topology.");
        }

        public static MxRenderPipelineDiagnostic SharedRTMultipleWriters(SharedRTId sharedRTId, string firstWriterDebugName, string secondWriterDebugName)
        {
            return new MxRenderPipelineDiagnostic(
                MxRenderPipelineDiagnosticCode.SharedRTMultipleWriters,
                MxRenderPipelineDiagnosticSeverity.Error,
                firstWriterDebugName,
                default,
                0,
                "SharedRT '" + sharedRTId.Value + "' has multiple writers ('" + firstWriterDebugName + "', '" + secondWriterDebugName + "') but additive writers are not allowed.");
        }

        public static MxRenderPipelineDiagnostic InvalidSharedRTMetadata(string passDebugName, SharedRTId sharedRTId, bool isWrite, string message)
        {
            string access = isWrite ? "write" : "read";
            string id = sharedRTId.IsValid ? sharedRTId.Value : "<invalid>";
            return new MxRenderPipelineDiagnostic(
                MxRenderPipelineDiagnosticCode.InvalidSharedRTMetadata,
                MxRenderPipelineDiagnosticSeverity.Error,
                passDebugName,
                default,
                0,
                "Pass '" + passDebugName + "' has invalid SharedRT " + access + " metadata for '" + id + "': " + message);
        }
    }

    public readonly struct MxRenderPipelinePassTopology
    {
        public MxRenderPipelinePassTopology(string debugName, MxRenderPhase phase, int order, int readCount, int writeCount)
        {
            DebugName = debugName ?? string.Empty;
            Phase = phase;
            Order = order;
            ReadCount = readCount;
            WriteCount = writeCount;
        }

        public string DebugName { get; }
        public MxRenderPhase Phase { get; }
        public int Order { get; }
        public int ReadCount { get; }
        public int WriteCount { get; }
    }

    public sealed class MxRenderPipelineTopologySnapshot
    {
        public static readonly MxRenderPipelineTopologySnapshot Empty = new MxRenderPipelineTopologySnapshot(
            MxCameraRenderKind.Unknown,
            Array.Empty<MxRenderPipelinePassTopology>(),
            Array.Empty<MxRenderPipelineDiagnostic>());

        private readonly List<MxRenderPipelinePassTopology> _passes;
        private readonly List<MxRenderPipelineDiagnostic> _diagnostics;

        public MxRenderPipelineTopologySnapshot(
            MxCameraRenderKind cameraKind,
            IReadOnlyList<MxRenderPipelinePassTopology> passes,
            IReadOnlyList<MxRenderPipelineDiagnostic> diagnostics)
        {
            CameraKind = cameraKind;
            _passes = passes != null ? new List<MxRenderPipelinePassTopology>(passes) : new List<MxRenderPipelinePassTopology>();
            _diagnostics = diagnostics != null ? new List<MxRenderPipelineDiagnostic>(diagnostics) : new List<MxRenderPipelineDiagnostic>();
        }

        public MxCameraRenderKind CameraKind { get; }
        public IReadOnlyList<MxRenderPipelinePassTopology> Passes => _passes;
        public IReadOnlyList<MxRenderPipelineDiagnostic> Diagnostics => _diagnostics;

        public static MxRenderPipelineTopologySnapshot Create(
            MxCameraRenderKind cameraKind,
            IReadOnlyList<IMxRenderPass> passes,
            IReadOnlyList<MxRenderPipelineDiagnostic> diagnostics)
        {
            var entries = new List<MxRenderPipelinePassTopology>(passes.Count);
            for (int i = 0; i < passes.Count; i++)
            {
                IMxRenderPass pass = passes[i];
                entries.Add(new MxRenderPipelinePassTopology(
                    pass.DebugName,
                    pass.Phase,
                    pass.Order,
                    pass.Reads.Count,
                    pass.Writes.Count));
            }

            return new MxRenderPipelineTopologySnapshot(cameraKind, entries, diagnostics);
        }
    }

    public static class MxRenderPhaseExtensions
    {
        public static RenderPassEvent ToRenderPassEvent(this MxRenderPhase phase)
        {
            switch (phase)
            {
                case MxRenderPhase.BeforeRendering:
                    return RenderPassEvent.BeforeRendering;
                case MxRenderPhase.BeforeRenderingShadows:
                    return RenderPassEvent.BeforeRenderingShadows;
                case MxRenderPhase.AfterRenderingShadows:
                    return RenderPassEvent.AfterRenderingShadows;
                case MxRenderPhase.BeforeRenderingPrePasses:
                    return RenderPassEvent.BeforeRenderingPrePasses;
                case MxRenderPhase.AfterRenderingPrePasses:
                    return RenderPassEvent.AfterRenderingPrePasses;
                case MxRenderPhase.BeforeRenderingOpaques:
                    return RenderPassEvent.BeforeRenderingOpaques;
                case MxRenderPhase.AfterRenderingOpaques:
                    return RenderPassEvent.AfterRenderingOpaques;
                case MxRenderPhase.BeforeRenderingTransparents:
                    return RenderPassEvent.BeforeRenderingTransparents;
                case MxRenderPhase.AfterRenderingTransparents:
                    return RenderPassEvent.AfterRenderingTransparents;
                case MxRenderPhase.BeforeRenderingPostProcessing:
                    return RenderPassEvent.BeforeRenderingPostProcessing;
                case MxRenderPhase.AfterRenderingPostProcessing:
                    return RenderPassEvent.AfterRenderingPostProcessing;
                case MxRenderPhase.AfterRendering:
                    return RenderPassEvent.AfterRendering;
                default:
                    return RenderPassEvent.AfterRendering;
            }
        }
    }

    internal sealed class MxCameraGlobalsPass : ScriptableRenderPass
    {
        private CameraRenderSnapshot _snapshot;

        public MxCameraGlobalsPass()
        {
            renderPassEvent = MxRenderPhase.BeforeRendering.ToRenderPassEvent();
        }

        public void Setup(CameraRenderSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public void Clear()
        {
            _snapshot = null;
        }

        [Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_snapshot == null)
                return;

            CommandBuffer commandBuffer = CommandBufferPool.Get("Mx Camera Globals");
            try
            {
                ExecuteCameraGlobals(commandBuffer, _snapshot);
                context.ExecuteCommandBuffer(commandBuffer);
            }
            finally
            {
                CommandBufferPool.Release(commandBuffer);
            }
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_snapshot == null)
                return;

            using (var builder = renderGraph.AddUnsafePass<CameraGlobalsPassData>(passName, out var passData, profilingSampler))
            {
                passData.Snapshot = _snapshot;
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc((CameraGlobalsPassData data, UnsafeGraphContext graphContext) =>
                {
                    ExecuteCameraGlobals(CommandBufferHelpers.GetNativeCommandBuffer(graphContext.cmd), data.Snapshot);
                });
            }
        }

        private static void ExecuteCameraGlobals(CommandBuffer commandBuffer, CameraRenderSnapshot snapshot)
        {
            commandBuffer.SetGlobalVector(MxRenderingShaderIds.MxViewFocusWorldPos, ToVector4(snapshot.ViewFocusWorldPosition));
            for (int i = 0; i < snapshot.Overrides.Count; i++)
            {
                CameraShaderOverride cameraOverride = snapshot.Overrides[i];
                commandBuffer.SetGlobalVector(cameraOverride.PropertyId, cameraOverride.Value);
            }
        }

        private static Vector4 ToVector4(Vector3 value)
        {
            return new Vector4(value.x, value.y, value.z, 0f);
        }

        private sealed class CameraGlobalsPassData
        {
            public CameraRenderSnapshot Snapshot;
        }
    }

    internal sealed class MxSharedRTLifecyclePass : ScriptableRenderPass
    {
        private readonly bool _beginFrame;
        private SharedRenderTextureRegistry _registry;

        public MxSharedRTLifecyclePass(bool beginFrame)
        {
            _beginFrame = beginFrame;
            renderPassEvent = beginFrame
                ? MxRenderPhase.BeforeRendering.ToRenderPassEvent()
                : MxRenderPhase.AfterRendering.ToRenderPassEvent();
        }

        public void Setup(SharedRenderTextureRegistry registry)
        {
            _registry = registry;
        }

        public void Clear()
        {
            _registry = null;
        }

        [Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_registry == null)
                return;

            ExecuteLifecycle(_registry, _beginFrame);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_registry == null)
                return;

            using (var builder = renderGraph.AddUnsafePass<LifecyclePassData>(passName, out var passData, profilingSampler))
            {
                passData.Registry = _registry;
                passData.BeginFrame = _beginFrame;
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc((LifecyclePassData data, UnsafeGraphContext graphContext) =>
                {
                    ExecuteLifecycle(data.Registry, data.BeginFrame);
                });
            }
        }

        private static void ExecuteLifecycle(SharedRenderTextureRegistry registry, bool beginFrame)
        {
            if (beginFrame)
                registry.BeginFrame();
            else
                registry.EndFrame();
        }

        private sealed class LifecyclePassData
        {
            public SharedRenderTextureRegistry Registry;
            public bool BeginFrame;
        }
    }

    internal sealed class MxRenderPassWrapper : ScriptableRenderPass
    {
        private IMxRenderPass _pass;
        private MxCameraRenderContextDescriptor _cameraContext;
        private CameraRenderSnapshot _cameraSnapshot;
        private ISharedRenderTextureRegistry _sharedRenderTextures;

        public void Setup(
            IMxRenderPass pass,
            MxCameraRenderContextDescriptor cameraContext,
            CameraRenderSnapshot cameraSnapshot,
            ISharedRenderTextureRegistry sharedRenderTextures)
        {
            _pass = pass;
            _cameraContext = cameraContext;
            _cameraSnapshot = cameraSnapshot;
            _sharedRenderTextures = sharedRenderTextures;
            renderPassEvent = pass.Phase.ToRenderPassEvent();
        }

        public void Clear()
        {
            _pass = null;
            _cameraContext = default;
            _cameraSnapshot = null;
            _sharedRenderTextures = null;
        }

        [Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_pass == null)
                return;

            CommandBuffer commandBuffer = CommandBufferPool.Get(_pass.DebugName);
            try
            {
                ExecuteMxPass(commandBuffer, _pass, _cameraContext, _cameraSnapshot, _sharedRenderTextures);
                context.ExecuteCommandBuffer(commandBuffer);
            }
            finally
            {
                CommandBufferPool.Release(commandBuffer);
            }
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_pass == null)
                return;

            using (var builder = renderGraph.AddUnsafePass<MxRenderPassData>(_pass.DebugName, out var passData, profilingSampler))
            {
                passData.Pass = _pass;
                passData.CameraContext = _cameraContext;
                passData.CameraSnapshot = _cameraSnapshot;
                passData.SharedRenderTextures = _sharedRenderTextures;
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc((MxRenderPassData data, UnsafeGraphContext graphContext) =>
                {
                    ExecuteMxPass(
                        CommandBufferHelpers.GetNativeCommandBuffer(graphContext.cmd),
                        data.Pass,
                        data.CameraContext,
                        data.CameraSnapshot,
                        data.SharedRenderTextures);
                });
            }
        }

        private static void ExecuteMxPass(
            CommandBuffer commandBuffer,
            IMxRenderPass pass,
            MxCameraRenderContextDescriptor cameraContext,
            CameraRenderSnapshot cameraSnapshot,
            ISharedRenderTextureRegistry sharedRenderTextures)
        {
            pass.Execute(new MxRenderPassExecuteContext(commandBuffer, cameraContext, cameraSnapshot, sharedRenderTextures));
        }

        private sealed class MxRenderPassData
        {
            public IMxRenderPass Pass;
            public MxCameraRenderContextDescriptor CameraContext;
            public CameraRenderSnapshot CameraSnapshot;
            public ISharedRenderTextureRegistry SharedRenderTextures;
        }
    }
}
