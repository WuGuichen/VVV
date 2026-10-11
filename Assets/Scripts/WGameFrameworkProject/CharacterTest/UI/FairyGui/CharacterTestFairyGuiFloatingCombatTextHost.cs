using System;
using System.Collections.Generic;
using MxFramework.Camera.URP;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Game.CharacterTest
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(106)]
    [AddComponentMenu("MxFramework/Game/Character Test/FairyGUI Floating Combat Text Host")]
    public sealed class CharacterTestFairyGuiFloatingCombatTextHost : MonoBehaviour
    {
        public const int PrewarmCount = CharacterTestFloatingCombatTextPool.DefaultPrewarmCount;
        public const int MaxActiveCount = CharacterTestFloatingCombatTextPool.DefaultMaxActiveCount;
        public const int MaxSpawnPerRefresh = 16;
        public const int MaxQueuedEvents = 128;

        private readonly List<CharacterTestFloatingCombatTextEvent> _eventBuffer =
            new List<CharacterTestFloatingCombatTextEvent>(32);
        private readonly List<CharacterTestFloatingCombatTextEvent> _queuedEvents =
            new List<CharacterTestFloatingCombatTextEvent>(MaxQueuedEvents);
        private readonly CharacterTestFloatingCombatTextProjector _projector =
            new CharacterTestFloatingCombatTextProjector();
        private UnityCamera _cachedMainCamera;
        private GameSlice _slice;
        private IRuntimeLogger _logger = NullRuntimeLogger.Instance;
        private MxFairyGuiLayerHost _layerHost;
        private bool _ownsLayerHost;
        private CharacterTestFloatingCombatTextPool _pool;
        private bool _configured;
        private string _lastStatusMessage = string.Empty;
        private long _lastConsumedSliceFrame = long.MinValue;

        public bool IsConfigured => _configured;
        public string LastStatusMessage => _lastStatusMessage;
        public int RefreshAttemptCount { get; private set; }
        public int SkippedRefreshCount { get; private set; }
        public int SpawnedCount { get; private set; }
        public int DroppedCount { get; private set; }
        public int QueuedCount => _queuedEvents.Count;
        public int ActiveCount => _pool != null ? _pool.ActiveCount : 0;
        public int CreatedTextObjectCount => _pool != null ? _pool.CreatedCount : 0;

        public void Configure(GameSlice slice, IRuntimeLogger logger = null, MxFairyGuiLayerHost layerHost = null)
        {
            Release();
            _slice = slice;
            _logger = logger ?? NullRuntimeLogger.Instance;

            if (_slice == null)
            {
                Warn("Floating combat text disabled because CharacterTest runtime is not available.");
                return;
            }

            ConfigureFairyGuiCameraStack();
            _layerHost = layerHost ?? CharacterTestFairyGuiLayerRoots.SharedLayerHost;
            _ownsLayerHost = false;
            CharacterTestFairyGuiLayerRoots.EnsureLayerRoots(_layerHost);
            if (!_layerHost.TryGetLayerRoot(MxUiLayer.Toast, out Fgui.GComponent layerRoot) || layerRoot == null)
            {
                Warn("Floating combat text disabled because FairyGUI Toast layer is not available.");
                return;
            }

            _pool = new CharacterTestFloatingCombatTextPool(layerRoot, PrewarmCount, MaxActiveCount);
            _configured = true;
            _lastStatusMessage = string.Empty;
            Refresh();
        }

        public void Refresh()
        {
            RefreshAttemptCount++;
            if (!_configured || _slice == null || _pool == null)
                return;

            long sliceFrame = _slice.CurrentFrame.Value;
            if (_lastConsumedSliceFrame == sliceFrame && _queuedEvents.Count == 0)
            {
                SkippedRefreshCount++;
                return;
            }

            CharacterTestFairyGuiLayerRoots.NormalizeRootOrder(_layerHost);
            ConsumeCombatOutputsOnce(sliceFrame);
            if (_queuedEvents.Count > 0)
            {
                SpawnQueuedEvents();
                _projector.Camera = ResolveMainCamera();
            }
        }

        public void TickPresentation(float deltaTime)
        {
            if (!_configured || _slice == null || _pool == null)
                return;

            _projector.Camera = ResolveMainCamera();
            _pool.Tick(Mathf.Max(0f, deltaTime), _projector);
        }

        private UnityCamera ResolveMainCamera()
        {
            return CharacterTestMainCameraCache.Resolve(ref _cachedMainCamera);
        }

        public void Clear()
        {
            _eventBuffer.Clear();
            _queuedEvents.Clear();
            _pool?.Clear();
        }

        public void DetachSlice()
        {
            Clear();
            _slice = null;
            _lastConsumedSliceFrame = long.MinValue;
            _cachedMainCamera = null;
        }

        public void RebindSlice(GameSlice slice)
        {
            _slice = slice;
            _lastConsumedSliceFrame = long.MinValue;
            _cachedMainCamera = null;
            Clear();
            if (_slice != null && _pool != null)
            {
                Refresh();
            }
        }

        public void Release()
        {
            Clear();
            _pool?.Dispose();
            _pool = null;
            if (_ownsLayerHost)
                _layerHost?.Dispose();
            _layerHost = null;
            _ownsLayerHost = false;
            _slice = null;
            _configured = false;
            _lastStatusMessage = string.Empty;
            _lastConsumedSliceFrame = long.MinValue;
            _cachedMainCamera = null;
            RefreshAttemptCount = 0;
            SkippedRefreshCount = 0;
            SpawnedCount = 0;
            DroppedCount = 0;
        }

        private void ConsumeCombatOutputsOnce(long sliceFrame)
        {
            if (_lastConsumedSliceFrame == sliceFrame)
                return;

            _lastConsumedSliceFrame = sliceFrame;
            _eventBuffer.Clear();
            CharacterTestFloatingCombatTextEventBuilder.AppendFrameEvents(_slice, _eventBuffer);
            for (int i = 0; i < _eventBuffer.Count; i++)
                Enqueue(_eventBuffer[i]);
        }

        private void Enqueue(CharacterTestFloatingCombatTextEvent evt)
        {
            if (!evt.IsValid)
            {
                DroppedCount++;
                return;
            }

            if (_queuedEvents.Count < MaxQueuedEvents)
            {
                _queuedEvents.Add(evt);
                return;
            }

            int lowIndex = FindLowestPriorityQueuedIndex();
            if (lowIndex >= 0 && _queuedEvents[lowIndex].Priority < evt.Priority)
            {
                _queuedEvents[lowIndex] = evt;
                DroppedCount++;
                return;
            }

            DroppedCount++;
        }

        private void SpawnQueuedEvents()
        {
            int spawnCount = 0;
            while (_queuedEvents.Count > 0 && spawnCount < MaxSpawnPerRefresh)
            {
                int index = FindHighestPriorityQueuedIndex();
                CharacterTestFloatingCombatTextEvent evt = _queuedEvents[index];
                _queuedEvents.RemoveAt(index);
                if (_pool.Spawn(evt))
                {
                    SpawnedCount++;
                    spawnCount++;
                }
                else
                {
                    DroppedCount++;
                }
            }
        }

        private int FindHighestPriorityQueuedIndex()
        {
            int index = 0;
            int priority = int.MinValue;
            for (int i = 0; i < _queuedEvents.Count; i++)
            {
                int candidatePriority = _queuedEvents[i].Priority;
                if (candidatePriority > priority)
                {
                    priority = candidatePriority;
                    index = i;
                }
            }

            return index;
        }

        private int FindLowestPriorityQueuedIndex()
        {
            int index = -1;
            int priority = int.MaxValue;
            for (int i = 0; i < _queuedEvents.Count; i++)
            {
                int candidatePriority = _queuedEvents[i].Priority;
                if (candidatePriority < priority)
                {
                    priority = candidatePriority;
                    index = i;
                }
            }

            return index;
        }

        private void ConfigureFairyGuiCameraStack()
        {
            if (!Application.isPlaying)
                return;

            Fgui.StageCamera.CheckMainCamera();
            UnityCamera baseCamera = UnityCamera.main;
            UnityCamera overlayCamera = Fgui.StageCamera.main;
            if (baseCamera == null || overlayCamera == null)
                return;

            overlayCamera.clearFlags = CameraClearFlags.Depth;
            overlayCamera.targetTexture = baseCamera.targetTexture;
            overlayCamera.targetDisplay = baseCamera.targetDisplay;
            overlayCamera.rect = baseCamera.rect;
            overlayCamera.allowHDR = baseCamera.allowHDR;
            overlayCamera.allowMSAA = baseCamera.allowMSAA;

            MxCameraUrpStackResult result = MxCameraUrpOverlayStackBinder.Bind(
                baseCamera,
                overlayCamera,
                excludeOverlayLayersFromBaseCamera: true);
            if (!result.Success)
            {
                Warn(
                    "Floating combat text FairyGUI camera stack bind failed. code="
                    + result.Code
                    + " message="
                    + result.Message);
            }
        }

        private void Warn(string message)
        {
            _lastStatusMessage = message ?? string.Empty;
            _logger.Warning("CharacterTest.FairyGUI", _lastStatusMessage);
        }
    }
}
