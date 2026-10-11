using System;
using System.Collections.Generic;
using MxFramework.Gameplay;

namespace MxFramework.Story.GameplayBridge
{
    public sealed class StoryBeatGameplayLocator
    {
        private readonly Dictionary<StoryBeatGameplayLocatorKey, StoryGameplayEntityRef> _beatRefs =
            new Dictionary<StoryBeatGameplayLocatorKey, StoryGameplayEntityRef>();
        private readonly Dictionary<int, StoryGameplayEntityRef> _triggerRefs =
            new Dictionary<int, StoryGameplayEntityRef>();
        private readonly StoryGameplayBridgeDiagnostics _diagnostics;

        public StoryBeatGameplayLocator(StoryGameplayBridgeDiagnostics diagnostics = null)
        {
            _diagnostics = diagnostics;
        }

        public int BeatRefCount => _beatRefs.Count;
        public int TriggerRefCount => _triggerRefs.Count;

        public void RegisterBeatEntity(int graphId, int beatId, StoryGameplayEntityRef entityRef)
        {
            if (graphId <= 0)
                throw new ArgumentOutOfRangeException(nameof(graphId), "Story graph id must be positive.");
            if (beatId <= 0)
                throw new ArgumentOutOfRangeException(nameof(beatId), "Story beat id must be positive.");
            if (entityRef.IsNone)
                throw new ArgumentException("Story gameplay entity ref cannot be none.", nameof(entityRef));

            _beatRefs[new StoryBeatGameplayLocatorKey(graphId, beatId)] = entityRef;
        }

        public void RegisterTriggerEntity(int triggerId, StoryGameplayEntityRef entityRef)
        {
            if (triggerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(triggerId), "Story trigger id must be positive.");
            if (entityRef.IsNone)
                throw new ArgumentException("Story gameplay entity ref cannot be none.", nameof(entityRef));

            _triggerRefs[triggerId] = entityRef;
        }

        public bool RemoveBeatEntity(int graphId, int beatId)
        {
            return _beatRefs.Remove(new StoryBeatGameplayLocatorKey(graphId, beatId));
        }

        public bool RemoveTriggerEntity(int triggerId)
        {
            return _triggerRefs.Remove(triggerId);
        }

        public void Clear()
        {
            _beatRefs.Clear();
            _triggerRefs.Clear();
        }

        public bool TryGetBeatRef(int graphId, int beatId, out StoryGameplayEntityRef entityRef)
        {
            return _beatRefs.TryGetValue(new StoryBeatGameplayLocatorKey(graphId, beatId), out entityRef);
        }

        public bool TryGetTriggerRef(int triggerId, out StoryGameplayEntityRef entityRef)
        {
            return _triggerRefs.TryGetValue(triggerId, out entityRef);
        }

        public StoryGameplayEntityResolutionResult ResolveBeat(
            int graphId,
            int beatId,
            GameplayComponentWorld componentWorld = null)
        {
            if (!TryGetBeatRef(graphId, beatId, out StoryGameplayEntityRef entityRef))
            {
                return Failed(new StoryGameplayBridgeDiagnostic(
                    StoryGameplayBridgeDiagnosticCode.MissingEntityRef,
                    "No gameplay entity ref is registered for the Story beat."));
            }

            return ResolveRef(entityRef, componentWorld);
        }

        public StoryGameplayEntityResolutionResult ResolveTrigger(
            int triggerId,
            GameplayComponentWorld componentWorld = null)
        {
            if (!TryGetTriggerRef(triggerId, out StoryGameplayEntityRef entityRef))
            {
                return Failed(new StoryGameplayBridgeDiagnostic(
                    StoryGameplayBridgeDiagnosticCode.MissingEntityRef,
                    "No gameplay entity ref is registered for the Story trigger."));
            }

            return ResolveRef(entityRef, componentWorld);
        }

        public StoryGameplayEntityResolutionResult ResolveRef(
            StoryGameplayEntityRef entityRef,
            GameplayComponentWorld componentWorld = null)
        {
            if (entityRef.IsNone)
            {
                return Failed(new StoryGameplayBridgeDiagnostic(
                    StoryGameplayBridgeDiagnosticCode.InvalidEntityRef,
                    "Story gameplay entity ref is none.",
                    entityRef));
            }

            switch (entityRef.Kind)
            {
                case StoryGameplayEntityRefKinds.LegacyRuntimeEntity:
                    if (entityRef.Id0 <= 0)
                    {
                        return Failed(new StoryGameplayBridgeDiagnostic(
                            StoryGameplayBridgeDiagnosticCode.InvalidEntityRef,
                            "Legacy runtime entity id must be positive.",
                            entityRef));
                    }

                    _diagnostics?.RecordResolvedEntity();
                    return StoryGameplayEntityResolutionResult.ResolvedLegacy(entityRef, entityRef.Id0);

                case StoryGameplayEntityRefKinds.ComponentEntity:
                    if (!entityRef.TryGetComponentEntityId(out GameplayEntityId entityId))
                    {
                        return Failed(new StoryGameplayBridgeDiagnostic(
                            StoryGameplayBridgeDiagnosticCode.InvalidEntityRef,
                            "Gameplay component entity ref must include positive index and generation.",
                            entityRef));
                    }

                    if (componentWorld != null && !componentWorld.IsAlive(entityId))
                    {
                        return Failed(new StoryGameplayBridgeDiagnostic(
                            StoryGameplayBridgeDiagnosticCode.StaleEntityRef,
                            "Gameplay component entity ref is not alive in the supplied component world.",
                            entityRef));
                    }

                    _diagnostics?.RecordResolvedEntity();
                    return StoryGameplayEntityResolutionResult.ResolvedComponent(entityRef, entityId);

                default:
                    return Failed(new StoryGameplayBridgeDiagnostic(
                        StoryGameplayBridgeDiagnosticCode.UnsupportedEntityRefKind,
                        "Story gameplay entity ref kind is not supported by the default locator.",
                        entityRef));
            }
        }

        private StoryGameplayEntityResolutionResult Failed(StoryGameplayBridgeDiagnostic diagnostic)
        {
            _diagnostics?.RecordUnresolvedEntity(diagnostic);
            return StoryGameplayEntityResolutionResult.Failed(diagnostic);
        }

        private readonly struct StoryBeatGameplayLocatorKey : IEquatable<StoryBeatGameplayLocatorKey>
        {
            public StoryBeatGameplayLocatorKey(int graphId, int beatId)
            {
                GraphId = graphId;
                BeatId = beatId;
            }

            private int GraphId { get; }
            private int BeatId { get; }

            public bool Equals(StoryBeatGameplayLocatorKey other)
            {
                return GraphId == other.GraphId && BeatId == other.BeatId;
            }

            public override bool Equals(object obj)
            {
                return obj is StoryBeatGameplayLocatorKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (GraphId * 397) ^ BeatId;
                }
            }
        }
    }

    public readonly struct StoryGameplayEntityResolutionResult
    {
        private StoryGameplayEntityResolutionResult(
            bool success,
            StoryGameplayEntityRef entityRef,
            GameplayEntityId componentEntityId,
            int legacyRuntimeEntityId,
            StoryGameplayBridgeDiagnostic diagnostic)
        {
            Success = success;
            EntityRef = entityRef;
            ComponentEntityId = componentEntityId;
            LegacyRuntimeEntityId = legacyRuntimeEntityId;
            Diagnostic = diagnostic;
        }

        public bool Success { get; }
        public StoryGameplayEntityRef EntityRef { get; }
        public GameplayEntityId ComponentEntityId { get; }
        public int LegacyRuntimeEntityId { get; }
        public StoryGameplayBridgeDiagnostic Diagnostic { get; }

        public static StoryGameplayEntityResolutionResult ResolvedComponent(
            StoryGameplayEntityRef entityRef,
            GameplayEntityId componentEntityId)
        {
            return new StoryGameplayEntityResolutionResult(true, entityRef, componentEntityId, 0, StoryGameplayBridgeDiagnostic.None);
        }

        public static StoryGameplayEntityResolutionResult ResolvedLegacy(
            StoryGameplayEntityRef entityRef,
            int runtimeEntityId)
        {
            return new StoryGameplayEntityResolutionResult(true, entityRef, default, runtimeEntityId, StoryGameplayBridgeDiagnostic.None);
        }

        public static StoryGameplayEntityResolutionResult Failed(StoryGameplayBridgeDiagnostic diagnostic)
        {
            return new StoryGameplayEntityResolutionResult(false, default, default, 0, diagnostic);
        }
    }
}
