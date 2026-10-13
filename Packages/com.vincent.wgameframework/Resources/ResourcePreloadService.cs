using System;
using System.Collections.Generic;
using System.Threading;

namespace MxFramework.Resources
{
    public interface IResourcePreloadService
    {
        IResourceOperation<ResourcePreloadResult> PreloadAsync(
            ResourcePreloadPlan plan,
            CancellationToken cancellationToken = default);

        void ReleaseGroup(ResourceGroupHandle handle);
    }

    public sealed class ResourcePreloadService : IResourcePreloadService
    {
        private readonly IResourceManager _resourceManager;
        private readonly IResourceCatalogQuery _catalogQuery;

        public ResourcePreloadService(IResourceManager resourceManager)
        {
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
            _catalogQuery = resourceManager as IResourceCatalogQuery;
        }

        public IResourceOperation<ResourcePreloadResult> PreloadAsync(
            ResourcePreloadPlan plan,
            CancellationToken cancellationToken = default)
        {
            if (plan == null)
            {
                return new ImmediateResourceOperation<ResourcePreloadResult>(
                    ResourceLoadResult<ResourcePreloadResult>.Failed(new ResourceError(
                        ResourceErrorCode.InvalidCatalog,
                        default,
                        string.Empty,
                        "Resource preload plan is missing.")));
            }

            var requestedKeys = CollectKeys(plan);
            return new ResourcePreloadOperation(this, _resourceManager, plan, requestedKeys, cancellationToken);
        }

        public void ReleaseGroup(ResourceGroupHandle handle)
        {
            if (handle == null || !handle.IsOwnedBy(_resourceManager) || !handle.TryMarkReleased())
                return;

            for (int i = handle.Handles.Count - 1; i >= 0; i--)
                _resourceManager.Release(handle.Handles[i]);
        }

        private List<ResourceKey> CollectKeys(ResourcePreloadPlan plan)
        {
            var keys = new List<ResourceKey>();
            var unique = new HashSet<ResourceKey>();

            for (int i = 0; i < plan.ExplicitKeys.Count; i++)
                AddKey(plan.ExplicitKeys[i], keys, unique);

            if (_catalogQuery == null)
                return keys;

            for (int i = 0; i < plan.Labels.Count; i++)
            {
                IReadOnlyList<ResourceKey> labelKeys = _catalogQuery.FindKeysByLabel(plan.Labels[i]);
                for (int j = 0; j < labelKeys.Count; j++)
                    AddKey(labelKeys[j], keys, unique);
            }

            return keys;
        }

        private static void AddKey(ResourceKey key, List<ResourceKey> keys, HashSet<ResourceKey> unique)
        {
            if (!unique.Add(key))
                return;

            keys.Add(key);
        }

        private void ReleaseHandles(List<ResourceHandle<object>> handles)
        {
            for (int i = handles.Count - 1; i >= 0; i--)
                _resourceManager.Release(handles[i]);

            handles.Clear();
        }

        private sealed class ResourcePreloadOperation : IResourceOperation<ResourcePreloadResult>
        {
            private readonly ResourcePreloadService _owner;
            private readonly IResourceManager _resourceManager;
            private readonly ResourcePreloadPlan _plan;
            private readonly List<ResourceKey> _requestedKeys;
            private readonly CancellationToken _cancellationToken;
            private readonly List<ResourceHandle<object>> _handles;
            private readonly List<ResourceKey> _acquiredKeys;
            private readonly List<ResourceError> _errors;
            private readonly List<InFlightLoad> _inFlight;
            private int _nextIndex;
            private int _completedCount;
            private bool _isDone;
            private bool _isCancelled;
            private bool _stopStarting;
            private bool _cancelRequested;
            private bool _cancelDispatched;
            private ResourceKey _cancellationKey;
            private ResourceLoadResult<ResourcePreloadResult> _result;

            public ResourcePreloadOperation(
                ResourcePreloadService owner,
                IResourceManager resourceManager,
                ResourcePreloadPlan plan,
                List<ResourceKey> requestedKeys,
                CancellationToken cancellationToken)
            {
                _owner = owner;
                _resourceManager = resourceManager;
                _plan = plan;
                _requestedKeys = requestedKeys ?? new List<ResourceKey>();
                _cancellationToken = cancellationToken;
                _handles = new List<ResourceHandle<object>>(_requestedKeys.Count);
                _acquiredKeys = new List<ResourceKey>(_requestedKeys.Count);
                _errors = new List<ResourceError>(_requestedKeys.Count);
                _inFlight = new List<InFlightLoad>(Math.Min(_requestedKeys.Count, _plan.MaxConcurrentLoads));
                _result = PendingResult();
                Pump();
            }

            public bool IsDone
            {
                get
                {
                    Pump();
                    return _isDone;
                }
            }

            public bool IsCancelled
            {
                get
                {
                    Pump();
                    return _isCancelled;
                }
            }

            public float Progress
            {
                get
                {
                    Pump();
                    if (_requestedKeys.Count == 0)
                        return 1f;

                    if (_isDone)
                        return 1f;

                    float progress = _completedCount;
                    for (int i = 0; i < _inFlight.Count; i++)
                    {
                        IResourceOperation<ResourceHandle<object>> operation = _inFlight[i].Operation;
                        if (operation != null)
                            progress += Clamp01(operation.Progress);
                    }

                    return Clamp01(progress / _requestedKeys.Count);
                }
            }

            public ResourceLoadResult<ResourcePreloadResult> Result
            {
                get
                {
                    Pump();
                    return _result;
                }
            }

            public void Cancel()
            {
                if (_isDone)
                    return;

                RequestCancellation(GetCancellationKey());
                Pump();
            }

            private void Pump()
            {
                if (_isDone)
                    return;

                if (_cancellationToken.IsCancellationRequested)
                    RequestCancellation(GetCancellationKey());

                bool progressed;
                do
                {
                    progressed = false;

                    if (ProcessCompletedInFlight(true))
                    {
                        if (_isDone)
                            return;
                        progressed = true;
                    }

                    if (!_stopStarting && StartLoads())
                    {
                        if (_isDone)
                            return;
                        progressed = true;
                    }
                }
                while (progressed && !_isDone);

                if (_stopStarting && _inFlight.Count == 0)
                {
                    if (_cancelRequested)
                        CompleteCancelled();
                    else
                        CompleteLoaded();
                    return;
                }

                if (_nextIndex >= _requestedKeys.Count && _inFlight.Count == 0)
                    CompleteLoaded();
            }

            private bool StartLoads()
            {
                bool progressed = false;
                while (!_isDone && !_stopStarting && _inFlight.Count < _plan.MaxConcurrentLoads && _nextIndex < _requestedKeys.Count)
                {
                    ResourceKey key = _requestedKeys[_nextIndex++];
                    if (_cancellationToken.IsCancellationRequested)
                    {
                        RequestCancellation(key);
                        return true;
                    }

                    IResourceOperation<ResourceHandle<object>> operation = _resourceManager.LoadAsync<object>(key, _cancellationToken);
                    progressed = true;
                    if (operation == null)
                    {
                        _completedCount++;
                        AddError(new ResourceError(
                            ResourceErrorCode.ProviderFailed,
                            key,
                            string.Empty,
                            "Resource manager returned no load operation."));

                        if (_plan.FailFast)
                        {
                            _stopStarting = true;
                            ReleaseAcquiredHandles();
                            RequestCancelInFlight();
                            return true;
                        }

                        continue;
                    }

                    _inFlight.Add(new InFlightLoad(key, operation));
                    if (operation.IsDone && ProcessCompletedInFlight(true) && _isDone)
                        return true;
                }

                return progressed;
            }

            private void CompleteLoaded()
            {
                if (_isDone)
                    return;

                if (_inFlight.Count != 0)
                    return;

                var handle = new ResourceGroupHandle(_plan.GroupId, _handles, _resourceManager, _acquiredKeys);
                var preloadResult = new ResourcePreloadResult(_plan.GroupId, handle, _requestedKeys, _errors);
                _result = ResourceLoadResult<ResourcePreloadResult>.Loaded(preloadResult);
                _isDone = true;
            }

            private void RequestCancellation(ResourceKey key)
            {
                if (_isDone || _cancelRequested)
                    return;
                if (_stopStarting)
                    return;

                _cancelRequested = true;
                _stopStarting = true;
                _cancellationKey = key;
                ReleaseAcquiredHandles();
                RequestCancelInFlight();
            }

            private void CompleteCancelled()
            {
                if (_isDone || _inFlight.Count != 0)
                    return;

                _isCancelled = true;
                ReleaseAcquiredHandles();
                ResourceError error = new ResourceError(
                    ResourceErrorCode.Cancelled,
                    _cancellationKey,
                    string.Empty,
                    "Resource preload operation was cancelled.");
                _errors.Add(error);
                _result = ResourceLoadResult<ResourcePreloadResult>.Failed(error);
                _isDone = true;
            }

            private bool ProcessCompletedInFlight(bool completeOnFailFast)
            {
                bool progressed = false;
                for (int i = 0; i < _inFlight.Count; i++)
                {
                    InFlightLoad load = _inFlight[i];
                    if (load.Operation == null || !load.Operation.IsDone)
                        continue;

                    _inFlight.RemoveAt(i);
                    i--;
                    _completedCount++;
                    progressed = true;
                    ResourceLoadResult<ResourceHandle<object>> result = load.Operation.Result;
                    if (result.Success && result.Value != null)
                    {
                        if (_stopStarting)
                            _resourceManager.Release(result.Value);
                        else
                        {
                            _handles.Add(result.Value);
                            _acquiredKeys.Add(load.Key);
                        }
                    }
                    else if (result.Success)
                    {
                        AddError(new ResourceError(
                            ResourceErrorCode.ProviderFailed,
                            load.Key,
                            string.Empty,
                            "Resource preload load completed without a handle."));
                    }
                    else if (!_stopStarting || result.Error.Code != ResourceErrorCode.Cancelled)
                    {
                        AddError(result.Error);
                    }

                    if (completeOnFailFast && _plan.FailFast && _errors.Count > 0 && !_stopStarting)
                    {
                        _stopStarting = true;
                        ReleaseAcquiredHandles();
                        RequestCancelInFlight();
                    }
                }

                return progressed;
            }

            private void AddError(ResourceError error)
            {
                _errors.Add(error);
            }

            private void ReleaseAcquiredHandles()
            {
                _owner.ReleaseHandles(_handles);
                _acquiredKeys.Clear();
            }

            private void RequestCancelInFlight()
            {
                if (_cancelDispatched)
                    return;

                _cancelDispatched = true;
                for (int i = 0; i < _inFlight.Count; i++)
                {
                    InFlightLoad load = _inFlight[i];
                    IResourceOperation<ResourceHandle<object>> operation = load.Operation;
                    if (operation != null && !operation.IsDone)
                    {
                        try
                        {
                            operation.Cancel();
                        }
                        catch (Exception exception)
                        {
                            AddError(new ResourceError(
                                ResourceErrorCode.ProviderFailed,
                                load.Key,
                                string.Empty,
                                "Resource provider cancellation threw: " + exception.Message));
                        }
                    }
                }
            }

            private ResourceKey GetCancellationKey()
            {
                if (_inFlight.Count > 0)
                    return _inFlight[0].Key;
                if (_nextIndex < _requestedKeys.Count)
                    return _requestedKeys[_nextIndex];
                if (_requestedKeys.Count > 0)
                    return _requestedKeys[_requestedKeys.Count - 1];

                return default;
            }

            private static ResourceLoadResult<ResourcePreloadResult> PendingResult()
            {
                return ResourceLoadResult<ResourcePreloadResult>.Failed(new ResourceError(
                    ResourceErrorCode.ProviderFailed,
                    default,
                    string.Empty,
                    "Resource preload operation is not done."));
            }

            private static float Clamp01(float value)
            {
                if (value < 0f)
                    return 0f;
                if (value > 1f)
                    return 1f;

                return value;
            }

            private readonly struct InFlightLoad
            {
                public InFlightLoad(ResourceKey key, IResourceOperation<ResourceHandle<object>> operation)
                {
                    Key = key;
                    Operation = operation;
                }

                public ResourceKey Key { get; }
                public IResourceOperation<ResourceHandle<object>> Operation { get; }
            }
        }
    }
}
