using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    [Serializable]
    public sealed class CharacterTestNavigationSceneSettings
    {
        public const int DefaultRevision = 1;
        public const float DefaultCellSizeMeters = 2f;
        public const float DefaultPlaneYMeters = 0f;
        public const float DefaultEndpointSnapDistanceMeters = 4f;
        public const int DefaultMaxNodeCount = 4096;
        public const int DefaultMaxEdgeCount = 32768;
        public const int DefaultMaxNodeExpansions = 1024;
        public const int DefaultMaxPathNodeCount = 256;

        [SerializeField] private bool _enabled = true;
        [SerializeField] private int _revision = DefaultRevision;
        [SerializeField] private string _boundsRegionStableId = "";
        [SerializeField] private float _planeYMeters = DefaultPlaneYMeters;
        [SerializeField] private float _cellSizeMeters = DefaultCellSizeMeters;
        [SerializeField] private bool _connectDiagonals = true;
        [SerializeField] private int _maxNodeCount = DefaultMaxNodeCount;
        [SerializeField] private int _maxEdgeCount = DefaultMaxEdgeCount;
        [SerializeField] private float _maxEndpointSnapDistanceMeters =
            DefaultEndpointSnapDistanceMeters;
        [SerializeField] private int _maxNodeExpansions = DefaultMaxNodeExpansions;
        [SerializeField] private int _maxPathNodeCount = DefaultMaxPathNodeCount;

        public bool Enabled => _enabled;

        public int Revision => _revision;

        public string BoundsRegionStableId => string.IsNullOrWhiteSpace(_boundsRegionStableId)
            ? string.Empty
            : _boundsRegionStableId.Trim();

        public float PlaneYMeters => _planeYMeters;

        public float CellSizeMeters => _cellSizeMeters;

        public bool ConnectDiagonals => _connectDiagonals;

        public int MaxNodeCount => _maxNodeCount;

        public int MaxEdgeCount => _maxEdgeCount;

        public float MaxEndpointSnapDistanceMeters => _maxEndpointSnapDistanceMeters;

        public int MaxNodeExpansions => _maxNodeExpansions;

        public int MaxPathNodeCount => _maxPathNodeCount;

        internal static CharacterTestNavigationSceneSettings CreateForTests(
            int revision = DefaultRevision,
            string boundsRegionStableId = "",
            float planeYMeters = DefaultPlaneYMeters,
            float cellSizeMeters = DefaultCellSizeMeters,
            bool connectDiagonals = true,
            int maxNodeCount = DefaultMaxNodeCount,
            int maxEdgeCount = DefaultMaxEdgeCount,
            float maxEndpointSnapDistanceMeters = DefaultEndpointSnapDistanceMeters,
            int maxNodeExpansions = DefaultMaxNodeExpansions,
            int maxPathNodeCount = DefaultMaxPathNodeCount,
            bool enabled = true)
        {
            return new CharacterTestNavigationSceneSettings
            {
                _enabled = enabled,
                _revision = revision,
                _boundsRegionStableId = boundsRegionStableId ?? string.Empty,
                _planeYMeters = planeYMeters,
                _cellSizeMeters = cellSizeMeters,
                _connectDiagonals = connectDiagonals,
                _maxNodeCount = maxNodeCount,
                _maxEdgeCount = maxEdgeCount,
                _maxEndpointSnapDistanceMeters = maxEndpointSnapDistanceMeters,
                _maxNodeExpansions = maxNodeExpansions,
                _maxPathNodeCount = maxPathNodeCount
            };
        }
    }

    internal enum CharacterTestNavigationSceneInputStatus
    {
        None = 0,
        Success = 1,
        Disabled = 2,
        MissingSettings = 3,
        InvalidRevision = 4,
        InvalidCellSize = 5,
        InvalidPlane = 6,
        InvalidEndpointSnapDistance = 7,
        InvalidBudget = 8,
        MissingSceneCache = 9,
        MissingRegions = 10,
        BoundsRegionNotFound = 11,
        AmbiguousBoundsRegion = 12,
        InvalidRegionTransform = 13,
        InvalidBounds = 14
    }

    internal readonly struct CharacterTestNavigationSceneInputResult
    {
        public CharacterTestNavigationSceneInputResult(
            CharacterTestNavigationSceneInputStatus status,
            int sourceIndex = -1,
            string stableId = "")
        {
            Status = status;
            SourceIndex = sourceIndex;
            StableId = stableId ?? string.Empty;
        }

        public CharacterTestNavigationSceneInputStatus Status { get; }

        public int SourceIndex { get; }

        public string StableId { get; }

        public bool Success => Status == CharacterTestNavigationSceneInputStatus.Success;
    }

    internal readonly struct CharacterTestNavigationSessionBuildInput : IEquatable<CharacterTestNavigationSessionBuildInput>
    {
        public CharacterTestNavigationSessionBuildInput(
            int revision,
            int minimumXMillimeters,
            int maximumXMillimeters,
            int minimumZMillimeters,
            int maximumZMillimeters,
            int planeYMillimeters,
            int cellSizeMillimeters,
            bool connectDiagonals,
            int maxNodeCount,
            int maxEdgeCount,
            int maxEndpointSnapDistanceMillimeters,
            int maxNodeExpansions,
            int maxPathNodeCount)
        {
            Revision = revision;
            MinimumXMillimeters = minimumXMillimeters;
            MaximumXMillimeters = maximumXMillimeters;
            MinimumZMillimeters = minimumZMillimeters;
            MaximumZMillimeters = maximumZMillimeters;
            PlaneYMillimeters = planeYMillimeters;
            CellSizeMillimeters = cellSizeMillimeters;
            ConnectDiagonals = connectDiagonals;
            MaxNodeCount = maxNodeCount;
            MaxEdgeCount = maxEdgeCount;
            MaxEndpointSnapDistanceMillimeters = maxEndpointSnapDistanceMillimeters;
            MaxNodeExpansions = maxNodeExpansions;
            MaxPathNodeCount = maxPathNodeCount;
        }

        public int Revision { get; }
        public int MinimumXMillimeters { get; }
        public int MaximumXMillimeters { get; }
        public int MinimumZMillimeters { get; }
        public int MaximumZMillimeters { get; }
        public int PlaneYMillimeters { get; }
        public int CellSizeMillimeters { get; }
        public bool ConnectDiagonals { get; }
        public int MaxNodeCount { get; }
        public int MaxEdgeCount { get; }
        public int MaxEndpointSnapDistanceMillimeters { get; }
        public int MaxNodeExpansions { get; }
        public int MaxPathNodeCount { get; }

        public bool Equals(CharacterTestNavigationSessionBuildInput other)
        {
            return Revision == other.Revision
                && MinimumXMillimeters == other.MinimumXMillimeters
                && MaximumXMillimeters == other.MaximumXMillimeters
                && MinimumZMillimeters == other.MinimumZMillimeters
                && MaximumZMillimeters == other.MaximumZMillimeters
                && PlaneYMillimeters == other.PlaneYMillimeters
                && CellSizeMillimeters == other.CellSizeMillimeters
                && ConnectDiagonals == other.ConnectDiagonals
                && MaxNodeCount == other.MaxNodeCount
                && MaxEdgeCount == other.MaxEdgeCount
                && MaxEndpointSnapDistanceMillimeters == other.MaxEndpointSnapDistanceMillimeters
                && MaxNodeExpansions == other.MaxNodeExpansions
                && MaxPathNodeCount == other.MaxPathNodeCount;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestNavigationSessionBuildInput other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Revision;
                hash = hash * 397 ^ MinimumXMillimeters;
                hash = hash * 397 ^ MaximumXMillimeters;
                hash = hash * 397 ^ MinimumZMillimeters;
                hash = hash * 397 ^ MaximumZMillimeters;
                hash = hash * 397 ^ PlaneYMillimeters;
                hash = hash * 397 ^ CellSizeMillimeters;
                hash = hash * 397 ^ (ConnectDiagonals ? 1 : 0);
                hash = hash * 397 ^ MaxNodeCount;
                hash = hash * 397 ^ MaxEdgeCount;
                hash = hash * 397 ^ MaxEndpointSnapDistanceMillimeters;
                hash = hash * 397 ^ MaxNodeExpansions;
                hash = hash * 397 ^ MaxPathNodeCount;
                return hash;
            }
        }
    }

    internal static class CharacterTestNavigationSceneInputCompiler
    {
        private const int AbsoluteMaxNodeCount = 16384;
        private const int AbsoluteMaxEdgeCount = 131072;

        public static bool TryCompile(
            CharacterTestNavigationSceneSettings settings,
            CharacterTestWorldObjectSceneCache sceneCache,
            out CharacterTestNavigationSessionBuildInput input,
            out CharacterTestNavigationSceneInputResult result)
        {
            input = default;
            if (settings == null)
                return Fail(CharacterTestNavigationSceneInputStatus.MissingSettings, out result);
            if (!settings.Enabled)
                return Fail(CharacterTestNavigationSceneInputStatus.Disabled, out result);
            if (settings.Revision <= 0)
                return Fail(CharacterTestNavigationSceneInputStatus.InvalidRevision, out result);
            if (!IsFinite(settings.CellSizeMeters) || settings.CellSizeMeters <= 0f)
                return Fail(CharacterTestNavigationSceneInputStatus.InvalidCellSize, out result);
            if (!IsFinite(settings.PlaneYMeters))
                return Fail(CharacterTestNavigationSceneInputStatus.InvalidPlane, out result);
            if (!IsFinite(settings.MaxEndpointSnapDistanceMeters)
                || settings.MaxEndpointSnapDistanceMeters < 0f)
            {
                return Fail(CharacterTestNavigationSceneInputStatus.InvalidEndpointSnapDistance, out result);
            }
            if (settings.MaxNodeCount <= 0
                || settings.MaxNodeCount > AbsoluteMaxNodeCount
                || settings.MaxEdgeCount <= 0
                || settings.MaxEdgeCount > AbsoluteMaxEdgeCount
                || settings.MaxNodeExpansions <= 0
                || settings.MaxPathNodeCount <= 0
                || settings.MaxPathNodeCount > settings.MaxNodeCount)
            {
                return Fail(CharacterTestNavigationSceneInputStatus.InvalidBudget, out result);
            }
            if (sceneCache == null || !sceneCache.Enabled)
                return Fail(CharacterTestNavigationSceneInputStatus.MissingSceneCache, out result);

            IReadOnlyList<CharacterTestWorldObjectSceneSource.LevelRegionInstance> regions =
                sceneCache.RegionInstances;
            if (regions == null || regions.Count == 0)
                return Fail(CharacterTestNavigationSceneInputStatus.MissingRegions, out result);

            string requestedStableId = settings.BoundsRegionStableId;
            bool unionAllRegions = requestedStableId.Length == 0
                || string.Equals(requestedStableId, "*", StringComparison.Ordinal);
            bool hasBounds = false;
            int matchedRegionCount = 0;
            double minX = 0d;
            double maxX = 0d;
            double minZ = 0d;
            double maxZ = 0d;
            for (int i = 0; i < regions.Count; i++)
            {
                CharacterTestWorldObjectSceneSource.LevelRegionInstance region = regions[i];
                if (region == null || !region.IsEnabled)
                    continue;
                if (!unionAllRegions
                    && !string.Equals(region.StableId, requestedStableId, StringComparison.Ordinal))
                {
                    continue;
                }

                matchedRegionCount++;
                Transform transform = region.Transform;
                if (transform == null)
                {
                    return Fail(
                        CharacterTestNavigationSceneInputStatus.InvalidRegionTransform,
                        i,
                        region.StableId,
                        out result);
                }

                Vector3 position = transform.position;
                Vector3 halfExtents = ResolveRegionHalfExtents(region, transform);
                if (!IsFinite(position.x)
                    || !IsFinite(position.z)
                    || !IsFinite(halfExtents.x)
                    || !IsFinite(halfExtents.z)
                    || halfExtents.x <= 0f
                    || halfExtents.z <= 0f)
                {
                    return Fail(
                        CharacterTestNavigationSceneInputStatus.InvalidRegionTransform,
                        i,
                        region.StableId,
                        out result);
                }

                double candidateMinX = position.x - halfExtents.x;
                double candidateMaxX = position.x + halfExtents.x;
                double candidateMinZ = position.z - halfExtents.z;
                double candidateMaxZ = position.z + halfExtents.z;
                if (!hasBounds)
                {
                    minX = candidateMinX;
                    maxX = candidateMaxX;
                    minZ = candidateMinZ;
                    maxZ = candidateMaxZ;
                    hasBounds = true;
                }
                else
                {
                    minX = Math.Min(minX, candidateMinX);
                    maxX = Math.Max(maxX, candidateMaxX);
                    minZ = Math.Min(minZ, candidateMinZ);
                    maxZ = Math.Max(maxZ, candidateMaxZ);
                }
            }

            if (!unionAllRegions && matchedRegionCount == 0)
            {
                return Fail(
                    CharacterTestNavigationSceneInputStatus.BoundsRegionNotFound,
                    -1,
                    requestedStableId,
                    out result);
            }
            if (!unionAllRegions && matchedRegionCount > 1)
            {
                return Fail(
                    CharacterTestNavigationSceneInputStatus.AmbiguousBoundsRegion,
                    -1,
                    requestedStableId,
                    out result);
            }
            if (!hasBounds)
                return Fail(CharacterTestNavigationSceneInputStatus.MissingRegions, out result);

            if (!TryMetersToMillimeters(settings.CellSizeMeters, out int cellSizeMillimeters)
                || cellSizeMillimeters <= 0)
            {
                return Fail(CharacterTestNavigationSceneInputStatus.InvalidCellSize, out result);
            }
            if (!TryMetersToMillimeters(settings.PlaneYMeters, out int planeYMillimeters))
                return Fail(CharacterTestNavigationSceneInputStatus.InvalidPlane, out result);
            if (!TryMetersToMillimeters(
                    settings.MaxEndpointSnapDistanceMeters,
                    out int maxEndpointSnapDistanceMillimeters))
            {
                return Fail(
                    CharacterTestNavigationSceneInputStatus.InvalidEndpointSnapDistance,
                    out result);
            }

            if (!TryFloorToMillimeters(minX, out int minimumXMillimeters)
                || !TryCeilingToMillimeters(maxX, out int maximumXMillimeters)
                || !TryFloorToMillimeters(minZ, out int minimumZMillimeters)
                || !TryCeilingToMillimeters(maxZ, out int maximumZMillimeters))
            {
                return Fail(CharacterTestNavigationSceneInputStatus.InvalidBounds, out result);
            }
            if (maximumXMillimeters <= minimumXMillimeters
                || maximumZMillimeters <= minimumZMillimeters)
            {
                return Fail(CharacterTestNavigationSceneInputStatus.InvalidBounds, out result);
            }

            if (!TryFloorToGrid(
                    minimumXMillimeters,
                    cellSizeMillimeters,
                    out minimumXMillimeters)
                || !TryCeilingToGrid(
                    maximumXMillimeters,
                    cellSizeMillimeters,
                    out maximumXMillimeters)
                || !TryFloorToGrid(
                    minimumZMillimeters,
                    cellSizeMillimeters,
                    out minimumZMillimeters)
                || !TryCeilingToGrid(
                    maximumZMillimeters,
                    cellSizeMillimeters,
                    out maximumZMillimeters))
            {
                return Fail(CharacterTestNavigationSceneInputStatus.InvalidBounds, out result);
            }
            input = new CharacterTestNavigationSessionBuildInput(
                settings.Revision,
                minimumXMillimeters,
                maximumXMillimeters,
                minimumZMillimeters,
                maximumZMillimeters,
                planeYMillimeters,
                cellSizeMillimeters,
                settings.ConnectDiagonals,
                settings.MaxNodeCount,
                settings.MaxEdgeCount,
                maxEndpointSnapDistanceMillimeters,
                settings.MaxNodeExpansions,
                settings.MaxPathNodeCount);
            result = new CharacterTestNavigationSceneInputResult(
                CharacterTestNavigationSceneInputStatus.Success,
                stableId: requestedStableId);
            return true;
        }

        private static Vector3 ResolveRegionHalfExtents(
            CharacterTestWorldObjectSceneSource.LevelRegionInstance region,
            Transform transform)
        {
            if (region.ScaleSource == CharacterTestWorldObjectInstanceScaleSource.Definition
                && CharacterTestWorldObjectSceneSource.BuiltInCatalog.TryGetDefinition(
                    region.DefinitionId,
                    out CharacterTestWorldObjectDefinition definition))
            {
                return new Vector3(
                    CharacterTestDeterministicUnits.MillimetersToMeters(
                        definition.Shape.HalfExtentXMillimeters),
                    CharacterTestDeterministicUnits.MillimetersToMeters(
                        definition.Shape.HalfExtentYMillimeters),
                    CharacterTestDeterministicUnits.MillimetersToMeters(
                        definition.Shape.HalfExtentZMillimeters));
            }

            Vector3 scale = transform.lossyScale;
            return new Vector3(
                Math.Abs(scale.x) * 0.5f,
                Math.Abs(scale.y) * 0.5f,
                Math.Abs(scale.z) * 0.5f);
        }

        private static bool TryMetersToMillimeters(float meters, out int millimeters)
        {
            double value = (double)meters * CharacterTestDeterministicUnits.PositionScale;
            if (!IsInIntegerRange(value))
            {
                millimeters = 0;
                return false;
            }

            millimeters = (int)Math.Round(value, MidpointRounding.AwayFromZero);
            return true;
        }

        private static bool TryFloorToMillimeters(double meters, out int millimeters)
        {
            double value = Math.Floor(meters * CharacterTestDeterministicUnits.PositionScale);
            if (!IsInIntegerRange(value))
            {
                millimeters = 0;
                return false;
            }

            millimeters = (int)value;
            return true;
        }

        private static bool TryCeilingToMillimeters(double meters, out int millimeters)
        {
            double value = Math.Ceiling(meters * CharacterTestDeterministicUnits.PositionScale);
            if (!IsInIntegerRange(value))
            {
                millimeters = 0;
                return false;
            }

            millimeters = (int)value;
            return true;
        }

        private static bool TryFloorToGrid(int value, int cellSize, out int gridValue)
        {
            long quotient = value / (long)cellSize;
            long remainder = value % (long)cellSize;
            if (remainder < 0)
                quotient--;
            long floor = quotient * cellSize;
            if (floor < int.MinValue || floor > int.MaxValue)
            {
                gridValue = 0;
                return false;
            }

            gridValue = (int)floor;
            return true;
        }

        private static bool TryCeilingToGrid(int value, int cellSize, out int gridValue)
        {
            if (!TryFloorToGrid(value, cellSize, out int floor))
            {
                gridValue = 0;
                return false;
            }

            long ceiling = floor == value ? value : (long)floor + cellSize;
            if (ceiling < int.MinValue || ceiling > int.MaxValue)
            {
                gridValue = 0;
                return false;
            }

            gridValue = (int)ceiling;
            return true;
        }

        private static bool IsInIntegerRange(double value)
        {
            return !double.IsNaN(value)
                && !double.IsInfinity(value)
                && value >= int.MinValue
                && value <= int.MaxValue;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool Fail(
            CharacterTestNavigationSceneInputStatus status,
            out CharacterTestNavigationSceneInputResult result)
        {
            return Fail(status, -1, string.Empty, out result);
        }

        private static bool Fail(
            CharacterTestNavigationSceneInputStatus status,
            int sourceIndex,
            string stableId,
            out CharacterTestNavigationSceneInputResult result)
        {
            result = new CharacterTestNavigationSceneInputResult(status, sourceIndex, stableId);
            return false;
        }
    }

    internal enum CharacterTestNavigationSessionBuildStatus
    {
        None = 0,
        Success = 1,
        InputUnavailable = 2,
        MissingPhysicsRuntime = 3,
        InvalidInput = 4,
        NodeCapacityExceeded = 5,
        EdgeCapacityExceeded = 6,
        NoWalkableNodes = 7,
        GraphValidationFailed = 8
    }

    internal readonly struct CharacterTestNavigationSessionBuildResult
    {
        public CharacterTestNavigationSessionBuildResult(
            CharacterTestNavigationSessionBuildStatus status,
            int candidateNodeCount = 0,
            int walkableNodeCount = 0,
            int edgeCount = 0,
            CharacterTestNavigationGraphBuildResult graphBuildResult = default)
        {
            Status = status;
            CandidateNodeCount = candidateNodeCount;
            WalkableNodeCount = walkableNodeCount;
            EdgeCount = edgeCount;
            GraphBuildResult = graphBuildResult;
        }

        public CharacterTestNavigationSessionBuildStatus Status { get; }
        public int CandidateNodeCount { get; }
        public int WalkableNodeCount { get; }
        public int EdgeCount { get; }
        public CharacterTestNavigationGraphBuildResult GraphBuildResult { get; }
        public bool Success => Status == CharacterTestNavigationSessionBuildStatus.Success;
    }

    internal sealed class CharacterTestNavigationSession
    {
        internal CharacterTestNavigationSession(
            CharacterTestNavigationSessionBuildInput input,
            CharacterTestNavigationGraph graph,
            CharacterTestNavigationSessionBuildResult buildResult,
            CharacterTestNavigationSceneInputResult sceneInputResult = default)
        {
            Input = input;
            Graph = graph;
            BuildResult = buildResult;
            SceneInputResult = sceneInputResult;
            if (graph != null && buildResult.Success)
            {
                Pathfinder = new CharacterTestBoundedAStarPathfinder(graph);
                PathBuffer = new CharacterTestNavigationPathBuffer(input.MaxPathNodeCount);
            }
        }

        public CharacterTestNavigationSessionBuildInput Input { get; }
        public CharacterTestNavigationGraph Graph { get; }
        public CharacterTestBoundedAStarPathfinder Pathfinder { get; }
        public CharacterTestNavigationPathBuffer PathBuffer { get; }
        public CharacterTestNavigationSessionBuildResult BuildResult { get; }
        public CharacterTestNavigationSceneInputResult SceneInputResult { get; }
        public bool IsAvailable => Graph != null && Pathfinder != null && PathBuffer != null;

        public static CharacterTestNavigationSession Unavailable(
            CharacterTestNavigationSessionBuildStatus status,
            CharacterTestNavigationSceneInputResult sceneInputResult = default)
        {
            return new CharacterTestNavigationSession(
                default,
                null,
                new CharacterTestNavigationSessionBuildResult(status),
                sceneInputResult);
        }
    }

    internal static class CharacterTestNavigationSessionBuilder
    {
        private const int QueryId = 910200;
        private const int QueryResultCapacity = 32;
        private const int DirectionSlotCount = 8;
        private const int BuildClearanceSkinMillimeters = 10;

        private static readonly int[] NeighborX = { 1, 0, -1, 0, 1, -1, -1, 1 };
        private static readonly int[] NeighborZ = { 0, 1, 0, -1, 1, 1, -1, -1 };

        /// <remarks>LowFreqAlloc. Call only during composition/session build.</remarks>
        public static CharacterTestNavigationSession Build(
            CharacterTestNavigationSessionBuildInput? optionalInput,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestNavigationSceneInputResult sceneInputResult = default,
            FixVector3? supportedAgentHalfExtents = null)
        {
            if (sceneInputResult.Status == CharacterTestNavigationSceneInputStatus.None
                && optionalInput.HasValue)
            {
                sceneInputResult = new CharacterTestNavigationSceneInputResult(
                    CharacterTestNavigationSceneInputStatus.Success);
            }
            if (!optionalInput.HasValue)
            {
                return CharacterTestNavigationSession.Unavailable(
                    CharacterTestNavigationSessionBuildStatus.InputUnavailable,
                    sceneInputResult);
            }
            if (physicsRuntime == null
                || !physicsRuntime.IsReady
                || physicsRuntime.World == null)
            {
                return CharacterTestNavigationSession.Unavailable(
                    CharacterTestNavigationSessionBuildStatus.MissingPhysicsRuntime,
                    sceneInputResult);
            }

            CharacterTestNavigationSessionBuildInput input = optionalInput.Value;
            if (!IsValid(input))
            {
                return new CharacterTestNavigationSession(
                    input,
                    null,
                    new CharacterTestNavigationSessionBuildResult(
                        CharacterTestNavigationSessionBuildStatus.InvalidInput),
                    sceneInputResult);
            }

            long xCountLong = ((long)input.MaximumXMillimeters - input.MinimumXMillimeters)
                / input.CellSizeMillimeters + 1L;
            long zCountLong = ((long)input.MaximumZMillimeters - input.MinimumZMillimeters)
                / input.CellSizeMillimeters + 1L;
            long candidateCountLong = xCountLong * zCountLong;
            if (xCountLong <= 0
                || zCountLong <= 0
                || xCountLong > int.MaxValue
                || zCountLong > int.MaxValue
                || candidateCountLong > input.MaxNodeCount)
            {
                return new CharacterTestNavigationSession(
                    input,
                    null,
                    new CharacterTestNavigationSessionBuildResult(
                        CharacterTestNavigationSessionBuildStatus.NodeCapacityExceeded,
                        candidateCountLong > int.MaxValue ? int.MaxValue : (int)Math.Max(0, candidateCountLong)),
                    sceneInputResult);
            }

            int xCount = (int)xCountLong;
            int zCount = (int)zCountLong;
            int candidateCount = (int)candidateCountLong;
            var walkable = new bool[candidateCount];
            var nodeIdByOrdinal = new int[candidateCount];
            var nodeDefinitions = new List<CharacterTestNavigationNodeDefinition>(candidateCount);
            var queryResults = new List<CombatQueryResult>(QueryResultCapacity);
            FixVector3 halfExtents = ResolveSupportedAgentHalfExtents(
                physicsRuntime.CharacterHalfExtents,
                supportedAgentHalfExtents);
            int radiusMillimeters = ToPositiveMillimeters(
                halfExtents.X > halfExtents.Z ? halfExtents.X : halfExtents.Z);
            int heightMillimeters = ToPositiveMillimeters(halfExtents.Y + halfExtents.Y);
            int walkableNodeCount = 0;
            for (int z = 0; z < zCount; z++)
            {
                for (int x = 0; x < xCount; x++)
                {
                    int ordinal = z * xCount + x;
                    int xMillimeters = input.MinimumXMillimeters + x * input.CellSizeMillimeters;
                    int zMillimeters = input.MinimumZMillimeters + z * input.CellSizeMillimeters;
                    if (!IsStandingCapsuleClear(
                            physicsRuntime,
                            input.PlaneYMillimeters,
                            xMillimeters,
                            zMillimeters,
                            radiusMillimeters,
                            heightMillimeters,
                            ordinal,
                            queryResults))
                    {
                        continue;
                    }

                    int nodeId = ordinal + 1;
                    walkable[ordinal] = true;
                    nodeIdByOrdinal[ordinal] = nodeId;
                    walkableNodeCount++;
                    nodeDefinitions.Add(new CharacterTestNavigationNodeDefinition(
                        nodeId,
                        Position(xMillimeters, input.PlaneYMillimeters, zMillimeters),
                        radiusMillimeters,
                        heightMillimeters));
                }
            }

            if (walkableNodeCount == 0)
            {
                return new CharacterTestNavigationSession(
                    input,
                    null,
                    new CharacterTestNavigationSessionBuildResult(
                        CharacterTestNavigationSessionBuildStatus.NoWalkableNodes,
                        candidateCount),
                    sceneInputResult);
            }

            var edgeDefinitions = new List<CharacterTestNavigationEdgeDefinition>(
                Math.Min(input.MaxEdgeCount, walkableNodeCount * (input.ConnectDiagonals ? 8 : 4)));
            int directionCount = input.ConnectDiagonals ? DirectionSlotCount : 4;
            for (int z = 0; z < zCount; z++)
            {
                for (int x = 0; x < xCount; x++)
                {
                    int fromOrdinal = z * xCount + x;
                    if (!walkable[fromOrdinal])
                        continue;

                    int fromXMillimeters = input.MinimumXMillimeters + x * input.CellSizeMillimeters;
                    int fromZMillimeters = input.MinimumZMillimeters + z * input.CellSizeMillimeters;
                    for (int directionIndex = 0; directionIndex < directionCount; directionIndex++)
                    {
                        int toX = x + NeighborX[directionIndex];
                        int toZ = z + NeighborZ[directionIndex];
                        if ((uint)toX >= (uint)xCount || (uint)toZ >= (uint)zCount)
                            continue;

                        int toOrdinal = toZ * xCount + toX;
                        if (!walkable[toOrdinal])
                            continue;

                        int toXMillimeters = input.MinimumXMillimeters
                            + toX * input.CellSizeMillimeters;
                        int toZMillimeters = input.MinimumZMillimeters
                            + toZ * input.CellSizeMillimeters;
                        if (!IsSweptCapsuleClear(
                                physicsRuntime,
                                input.PlaneYMillimeters,
                                fromXMillimeters,
                                fromZMillimeters,
                                toXMillimeters,
                                toZMillimeters,
                                radiusMillimeters,
                                heightMillimeters,
                                fromOrdinal,
                                directionIndex,
                                queryResults))
                        {
                            continue;
                        }

                        if (edgeDefinitions.Count >= input.MaxEdgeCount)
                        {
                            return new CharacterTestNavigationSession(
                                input,
                                null,
                                new CharacterTestNavigationSessionBuildResult(
                                    CharacterTestNavigationSessionBuildStatus.EdgeCapacityExceeded,
                                    candidateCount,
                                    walkableNodeCount,
                                    edgeDefinitions.Count),
                                sceneInputResult);
                        }

                        int deltaX = toXMillimeters - fromXMillimeters;
                        int deltaZ = toZMillimeters - fromZMillimeters;
                        int lengthMillimeters = (int)Math.Ceiling(
                            Math.Sqrt((double)deltaX * deltaX + (double)deltaZ * deltaZ));
                        edgeDefinitions.Add(new CharacterTestNavigationEdgeDefinition(
                            fromOrdinal * DirectionSlotCount + directionIndex + 1,
                            nodeIdByOrdinal[fromOrdinal],
                            nodeIdByOrdinal[toOrdinal],
                            lengthMillimeters,
                            radiusMillimeters,
                            heightMillimeters));
                    }
                }
            }

            if (!CharacterTestNavigationGraphBuilder.TryBuild(
                    input.Revision,
                    nodeDefinitions,
                    edgeDefinitions,
                    out CharacterTestNavigationGraph graph,
                    out CharacterTestNavigationGraphBuildResult graphBuildResult))
            {
                return new CharacterTestNavigationSession(
                    input,
                    null,
                    new CharacterTestNavigationSessionBuildResult(
                        CharacterTestNavigationSessionBuildStatus.GraphValidationFailed,
                        candidateCount,
                        walkableNodeCount,
                        edgeDefinitions.Count,
                        graphBuildResult),
                    sceneInputResult);
            }

            return new CharacterTestNavigationSession(
                input,
                graph,
                new CharacterTestNavigationSessionBuildResult(
                    CharacterTestNavigationSessionBuildStatus.Success,
                    candidateCount,
                    walkableNodeCount,
                    edgeDefinitions.Count,
                    graphBuildResult),
                sceneInputResult);
        }

        private static bool IsStandingCapsuleClear(
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            int planeYMillimeters,
            int xMillimeters,
            int zMillimeters,
            int radiusMillimeters,
            int heightMillimeters,
            int ordinal,
            List<CombatQueryResult> results)
        {
            int bottomY = planeYMillimeters + radiusMillimeters + BuildClearanceSkinMillimeters;
            int topY = planeYMillimeters
                + Math.Max(radiusMillimeters, heightMillimeters - radiusMillimeters)
                + BuildClearanceSkinMillimeters;
            return QueryCapsuleClear(
                physicsRuntime,
                Position(xMillimeters, bottomY, zMillimeters),
                Position(xMillimeters, topY, zMillimeters),
                radiusMillimeters,
                ordinal * 4,
                results);
        }

        private static bool IsSweptCapsuleClear(
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            int planeYMillimeters,
            int fromXMillimeters,
            int fromZMillimeters,
            int toXMillimeters,
            int toZMillimeters,
            int radiusMillimeters,
            int heightMillimeters,
            int ordinal,
            int directionIndex,
            List<CombatQueryResult> results)
        {
            int bottomY = planeYMillimeters + radiusMillimeters + BuildClearanceSkinMillimeters;
            int topY = planeYMillimeters
                + Math.Max(radiusMillimeters, heightMillimeters - radiusMillimeters)
                + BuildClearanceSkinMillimeters;
            int middleY = planeYMillimeters + heightMillimeters / 2 + BuildClearanceSkinMillimeters;
            int sourceOrder = ordinal * DirectionSlotCount + directionIndex;
            return QueryCapsuleClear(
                    physicsRuntime,
                    Position(fromXMillimeters, bottomY, fromZMillimeters),
                    Position(toXMillimeters, bottomY, toZMillimeters),
                    radiusMillimeters,
                    sourceOrder * 3,
                    results)
                && QueryCapsuleClear(
                    physicsRuntime,
                    Position(fromXMillimeters, middleY, fromZMillimeters),
                    Position(toXMillimeters, middleY, toZMillimeters),
                    radiusMillimeters,
                    sourceOrder * 3 + 1,
                    results)
                && QueryCapsuleClear(
                    physicsRuntime,
                    Position(fromXMillimeters, topY, fromZMillimeters),
                    Position(toXMillimeters, topY, toZMillimeters),
                    radiusMillimeters,
                    sourceOrder * 3 + 2,
                    results);
        }

        private static bool QueryCapsuleClear(
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            FixVector3 pointA,
            FixVector3 pointB,
            int radiusMillimeters,
            int sourceOrder,
            List<CombatQueryResult> results)
        {
            var header = new CombatQueryHeader(
                QueryId + sourceOrder,
                CombatQueryKind.Capsule,
                CombatEntityId.None,
                QueryId,
                0,
                sourceOrder,
                physicsRuntime.StaticObstacleLayerMask);
            results.Clear();
            physicsRuntime.World.QueryCapsule(
                new CombatCapsuleQuery(
                    header,
                    pointA,
                    pointB,
                    CharacterTestDeterministicUnits.MillimetersToFix64Meters(radiusMillimeters)),
                results);
            return results.Count == 0;
        }

        private static bool IsValid(CharacterTestNavigationSessionBuildInput input)
        {
            return input.Revision > 0
                && input.CellSizeMillimeters > 0
                && input.MaximumXMillimeters > input.MinimumXMillimeters
                && input.MaximumZMillimeters > input.MinimumZMillimeters
                && input.MaxNodeCount > 0
                && input.MaxEdgeCount > 0
                && input.MaxEndpointSnapDistanceMillimeters >= 0
                && input.MaxNodeExpansions > 0
                && input.MaxPathNodeCount > 0
                && input.MaxPathNodeCount <= input.MaxNodeCount;
        }

        private static FixVector3 ResolveSupportedAgentHalfExtents(
            FixVector3 physicsFallback,
            FixVector3? supportedAgentHalfExtents)
        {
            if (!supportedAgentHalfExtents.HasValue)
                return physicsFallback;

            FixVector3 supported = supportedAgentHalfExtents.Value;
            return new FixVector3(
                supported.X > physicsFallback.X ? supported.X : physicsFallback.X,
                supported.Y > physicsFallback.Y ? supported.Y : physicsFallback.Y,
                supported.Z > physicsFallback.Z ? supported.Z : physicsFallback.Z);
        }

        private static FixVector3 Position(int xMillimeters, int yMillimeters, int zMillimeters)
        {
            return new FixVector3(
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(xMillimeters),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(yMillimeters),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(zMillimeters));
        }

        private static int ToPositiveMillimeters(Fix64 value)
        {
            int millimeters = CharacterTestDeterministicUnits.Fix64MetersToMillimeters(
                value < Fix64.Zero ? Fix64.Zero : value);
            return Math.Max(1, millimeters);
        }
    }
}
