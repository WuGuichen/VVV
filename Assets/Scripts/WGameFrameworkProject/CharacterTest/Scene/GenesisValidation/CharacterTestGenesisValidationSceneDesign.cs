using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestGenesisValidationZoneLayoutPattern
    {
        Unknown = 0,
        ComparisonLanes = 1,
        LinearApproach = 2,
        StepAndEdgeCourse = 3,
        Sandbox = 4,
        DebugBay = 5
    }

    internal static class CharacterTestGenesisValidationStationTemplateIds
    {
        internal const string SurfaceComparisonLane = "SurfaceComparisonLane";
        internal const string BoundaryApproachBay = "BoundaryApproachBay";
        internal const string StepAndEdgeCourse = "StepAndEdgeCourse";
        internal const string MovableObjectSandbox = "MovableObjectSandbox";
        internal const string PotencyDebugBay = "PotencyDebugBay";
    }

    internal static class CharacterTestGenesisValidationSpatialRoleIds
    {
        internal const string ZoneEntryGate = "ZoneEntryGate";
        internal const string ApproachStart = "ApproachStart";
        internal const string ResetMarker = "ResetMarker";
        internal const string ObservationMarker = "ObservationMarker";
        internal const string ZoneExitGate = "ZoneExitGate";
        internal const string CourseLabel = "CourseLabel";
    }

    internal sealed class CharacterTestGenesisValidationScenePlan
    {
        public CharacterTestGenesisValidationScenePlan()
        {
            LayoutPolicy = new CharacterTestGenesisValidationSceneLayoutPolicy();
            Zones = new List<CharacterTestGenesisValidationZone>();
            Routes = new List<CharacterTestGenesisValidationRoute>();
        }

        public CharacterTestGenesisValidationRegionRef SceneBounds { get; set; }
        public CharacterTestGenesisValidationPointRef OriginPoint { get; set; }
        public CharacterTestGenesisValidationSceneLayoutPolicy LayoutPolicy { get; set; }
        public List<CharacterTestGenesisValidationZone> Zones { get; set; }
        public List<CharacterTestGenesisValidationRoute> Routes { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }

        public CharacterTestGenesisValidationScenePlan Normalize()
        {
            var result = new CharacterTestGenesisValidationScenePlan
            {
                SceneBounds = SceneBounds?.Normalize(),
                OriginPoint = OriginPoint?.Normalize(),
                LayoutPolicy = (LayoutPolicy ?? new CharacterTestGenesisValidationSceneLayoutPolicy()).Normalize(),
                Zones = new List<CharacterTestGenesisValidationZone>(),
                Routes = new List<CharacterTestGenesisValidationRoute>(),
                ExtensionData = CharacterTestGenesisValidationAuthoringDocument.CloneExtensionData(ExtensionData)
            };

            if (Zones != null)
            {
                for (int i = 0; i < Zones.Count; i++)
                {
                    if (Zones[i] != null)
                        result.Zones.Add(Zones[i].Normalize());
                }
            }
            result.Zones.Sort(CharacterTestGenesisValidationZoneComparer.Instance);

            if (Routes != null)
            {
                for (int i = 0; i < Routes.Count; i++)
                {
                    if (Routes[i] != null)
                        result.Routes.Add(Routes[i].Normalize());
                }
            }
            result.Routes.Sort(CharacterTestGenesisValidationRouteComparer.Instance);
            return result;
        }
    }

    internal sealed class CharacterTestGenesisValidationSceneLayoutPolicy
    {
        public CharacterTestGenesisValidationSceneLayoutPolicy()
        {
            MinimumZoneClearanceMillimeters = 1000;
            DefaultRouteWidthMillimeters = 1800;
            RequireConnectedZones = true;
            RequireSpatialRolesInsideFootprint = true;
        }

        public int MinimumZoneClearanceMillimeters { get; set; }
        public int DefaultRouteWidthMillimeters { get; set; }
        public bool RequireConnectedZones { get; set; }
        public bool RequireSpatialRolesInsideFootprint { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }

        public CharacterTestGenesisValidationSceneLayoutPolicy Normalize()
        {
            return new CharacterTestGenesisValidationSceneLayoutPolicy
            {
                MinimumZoneClearanceMillimeters = Math.Max(0, MinimumZoneClearanceMillimeters),
                DefaultRouteWidthMillimeters = Math.Max(0, DefaultRouteWidthMillimeters),
                RequireConnectedZones = RequireConnectedZones,
                RequireSpatialRolesInsideFootprint = RequireSpatialRolesInsideFootprint,
                ExtensionData = CharacterTestGenesisValidationAuthoringDocument.CloneExtensionData(ExtensionData)
            };
        }
    }

    internal sealed class CharacterTestGenesisValidationZone
    {
        public CharacterTestGenesisValidationZone()
        {
            StableId = string.Empty;
            DisplayName = string.Empty;
            GroupId = string.Empty;
            LayoutPattern = CharacterTestGenesisValidationZoneLayoutPattern.Unknown;
        }

        public string StableId { get; set; }
        public string DisplayName { get; set; }
        public string GroupId { get; set; }
        public int SortOrder { get; set; }
        public CharacterTestGenesisValidationZoneLayoutPattern LayoutPattern { get; set; }
        public CharacterTestGenesisValidationRegionRef FootprintRegion { get; set; }
        public CharacterTestGenesisValidationPointRef EntryPoint { get; set; }
        public CharacterTestGenesisValidationPointRef ExitPoint { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }

        public CharacterTestGenesisValidationZone Normalize()
        {
            return new CharacterTestGenesisValidationZone
            {
                StableId = CharacterTestGenesisValidationAuthoringDocument.Trim(StableId),
                DisplayName = CharacterTestGenesisValidationAuthoringDocument.Trim(DisplayName),
                GroupId = CharacterTestGenesisValidationAuthoringDocument.Trim(GroupId),
                SortOrder = SortOrder,
                LayoutPattern = Enum.IsDefined(typeof(CharacterTestGenesisValidationZoneLayoutPattern), LayoutPattern)
                    ? LayoutPattern
                    : CharacterTestGenesisValidationZoneLayoutPattern.Unknown,
                FootprintRegion = FootprintRegion?.Normalize(),
                EntryPoint = EntryPoint?.Normalize(),
                ExitPoint = ExitPoint?.Normalize(),
                ExtensionData = CharacterTestGenesisValidationAuthoringDocument.CloneExtensionData(ExtensionData)
            };
        }
    }

    internal sealed class CharacterTestGenesisValidationRoute
    {
        public CharacterTestGenesisValidationRoute()
        {
            StableId = string.Empty;
            FromZoneId = string.Empty;
            ToZoneId = string.Empty;
            Bidirectional = true;
            WidthMillimeters = 1800;
            Waypoints = new List<CharacterTestGenesisValidationPointRef>();
        }

        public string StableId { get; set; }
        public string FromZoneId { get; set; }
        public string ToZoneId { get; set; }
        public bool Bidirectional { get; set; }
        public int WidthMillimeters { get; set; }
        public List<CharacterTestGenesisValidationPointRef> Waypoints { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }

        public CharacterTestGenesisValidationRoute Normalize()
        {
            var waypoints = new List<CharacterTestGenesisValidationPointRef>();
            if (Waypoints != null)
            {
                for (int i = 0; i < Waypoints.Count; i++)
                {
                    if (Waypoints[i] != null)
                        waypoints.Add(Waypoints[i].Normalize());
                }
            }
            return new CharacterTestGenesisValidationRoute
            {
                StableId = CharacterTestGenesisValidationAuthoringDocument.Trim(StableId),
                FromZoneId = CharacterTestGenesisValidationAuthoringDocument.Trim(FromZoneId),
                ToZoneId = CharacterTestGenesisValidationAuthoringDocument.Trim(ToZoneId),
                Bidirectional = Bidirectional,
                WidthMillimeters = Math.Max(0, WidthMillimeters),
                Waypoints = waypoints,
                ExtensionData = CharacterTestGenesisValidationAuthoringDocument.CloneExtensionData(ExtensionData)
            };
        }
    }

    internal sealed class CharacterTestGenesisValidationStationSpatialProgram
    {
        public CharacterTestGenesisValidationStationSpatialProgram()
        {
            TemplateId = string.Empty;
            ZoneId = string.Empty;
        }

        public string TemplateId { get; set; }
        public string ZoneId { get; set; }
        public CharacterTestGenesisValidationRegionRef FootprintRegion { get; set; }
        public CharacterTestGenesisValidationPointRef EntryPoint { get; set; }
        public CharacterTestGenesisValidationPointRef ObservationPoint { get; set; }
        public CharacterTestGenesisValidationPointRef ExitPoint { get; set; }
        public int MinimumApproachLengthMillimeters { get; set; }
        public int MinimumClearanceMillimeters { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }

        public CharacterTestGenesisValidationStationSpatialProgram Normalize()
        {
            return new CharacterTestGenesisValidationStationSpatialProgram
            {
                TemplateId = CharacterTestGenesisValidationAuthoringDocument.Trim(TemplateId),
                ZoneId = CharacterTestGenesisValidationAuthoringDocument.Trim(ZoneId),
                FootprintRegion = FootprintRegion?.Normalize(),
                EntryPoint = EntryPoint?.Normalize(),
                ObservationPoint = ObservationPoint?.Normalize(),
                ExitPoint = ExitPoint?.Normalize(),
                MinimumApproachLengthMillimeters = Math.Max(0, MinimumApproachLengthMillimeters),
                MinimumClearanceMillimeters = Math.Max(0, MinimumClearanceMillimeters),
                ExtensionData = CharacterTestGenesisValidationAuthoringDocument.CloneExtensionData(ExtensionData)
            };
        }
    }

    internal interface ICharacterTestGenesisValidationStationTemplateDescriptor
    {
        string TemplateId { get; }
        string DisplayName { get; }
        string ScenarioTypeId { get; }
        string DefaultGroupId { get; }
        CharacterTestGenesisValidationZoneLayoutPattern LayoutPattern { get; }
        int MinimumApproachLengthMillimeters { get; }
        int MinimumClearanceMillimeters { get; }
        int MinimumSampleStaticCount { get; }
    }

    internal sealed class CharacterTestGenesisValidationStationTemplateDescriptor :
        ICharacterTestGenesisValidationStationTemplateDescriptor
    {
        public CharacterTestGenesisValidationStationTemplateDescriptor(
            string templateId,
            string displayName,
            string scenarioTypeId,
            string defaultGroupId,
            CharacterTestGenesisValidationZoneLayoutPattern layoutPattern,
            int minimumApproachLengthMillimeters,
            int minimumClearanceMillimeters,
            int minimumSampleStaticCount)
        {
            TemplateId = templateId;
            DisplayName = displayName;
            ScenarioTypeId = scenarioTypeId;
            DefaultGroupId = defaultGroupId;
            LayoutPattern = layoutPattern;
            MinimumApproachLengthMillimeters = minimumApproachLengthMillimeters;
            MinimumClearanceMillimeters = minimumClearanceMillimeters;
            MinimumSampleStaticCount = minimumSampleStaticCount;
        }

        public string TemplateId { get; }
        public string DisplayName { get; }
        public string ScenarioTypeId { get; }
        public string DefaultGroupId { get; }
        public CharacterTestGenesisValidationZoneLayoutPattern LayoutPattern { get; }
        public int MinimumApproachLengthMillimeters { get; }
        public int MinimumClearanceMillimeters { get; }
        public int MinimumSampleStaticCount { get; }
    }

    internal static class CharacterTestGenesisValidationStationTemplateRegistry
    {
        private static readonly ICharacterTestGenesisValidationStationTemplateDescriptor[]
            Descriptors =
            {
                new CharacterTestGenesisValidationStationTemplateDescriptor(
                    CharacterTestGenesisValidationStationTemplateIds.SurfaceComparisonLane,
                    "表面对比跑道",
                    CharacterTestGenesisValidationAuthoringScenarioRegistry.SurfaceContactResponse,
                    "surface-materials",
                    CharacterTestGenesisValidationZoneLayoutPattern.ComparisonLanes,
                    5000,
                    1500,
                    3),
                new CharacterTestGenesisValidationStationTemplateDescriptor(
                    CharacterTestGenesisValidationStationTemplateIds.BoundaryApproachBay,
                    "边界接近舱",
                    CharacterTestGenesisValidationAuthoringScenarioRegistry.BoundaryBodyResponse,
                    "boundary-forms",
                    CharacterTestGenesisValidationZoneLayoutPattern.LinearApproach,
                    5000,
                    1500,
                    0),
                new CharacterTestGenesisValidationStationTemplateDescriptor(
                    CharacterTestGenesisValidationStationTemplateIds.StepAndEdgeCourse,
                    "台阶与边缘路线",
                    CharacterTestGenesisValidationAuthoringScenarioRegistry.BoundaryBodyResponse,
                    "boundary-forms",
                    CharacterTestGenesisValidationZoneLayoutPattern.StepAndEdgeCourse,
                    6000,
                    1200,
                    0),
                new CharacterTestGenesisValidationStationTemplateDescriptor(
                    CharacterTestGenesisValidationStationTemplateIds.MovableObjectSandbox,
                    "可动物沙盒",
                    CharacterTestGenesisValidationAuthoringScenarioRegistry.MovableNaturalObjectResponse,
                    "movable-natural-objects",
                    CharacterTestGenesisValidationZoneLayoutPattern.Sandbox,
                    4000,
                    2000,
                    0),
                new CharacterTestGenesisValidationStationTemplateDescriptor(
                    CharacterTestGenesisValidationStationTemplateIds.PotencyDebugBay,
                    "三势调试舱",
                    CharacterTestGenesisValidationAuthoringScenarioRegistry.PotencyReceptionDebug,
                    "later-candidates",
                    CharacterTestGenesisValidationZoneLayoutPattern.DebugBay,
                    0,
                    1500,
                    0)
            };

        public static IReadOnlyList<ICharacterTestGenesisValidationStationTemplateDescriptor> All =>
            Descriptors;

        public static bool TryGet(
            string templateId,
            out ICharacterTestGenesisValidationStationTemplateDescriptor descriptor)
        {
            for (int i = 0; i < Descriptors.Length; i++)
            {
                if (string.Equals(Descriptors[i].TemplateId, templateId, StringComparison.Ordinal))
                {
                    descriptor = Descriptors[i];
                    return true;
                }
            }
            descriptor = null;
            return false;
        }
    }

    internal static class CharacterTestGenesisValidationSceneDesignValidator
    {
        public static Dictionary<string, CharacterTestGenesisValidationZone> ValidateScenePlan(
            CharacterTestGenesisValidationScenePlan plan,
            HashSet<string> groups,
            CharacterTestGenesisValidationOwnerCatalog owners,
            List<CharacterTestGenesisValidationAuthoringIssue> issues)
        {
            plan ??= new CharacterTestGenesisValidationScenePlan();
            var zones = new Dictionary<string, CharacterTestGenesisValidationZone>(StringComparer.Ordinal);
            if (plan.Zones.Count == 0 && plan.Routes.Count == 0)
                return zones;

            ValidateRegionRef(plan.SceneBounds, owners, issues, "scenePlan.sceneBounds", true);
            ValidatePointRef(plan.OriginPoint, owners, issues, "scenePlan.originPoint", true);
            if (plan.LayoutPolicy.DefaultRouteWidthMillimeters <= 0)
            {
                issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                    "InvalidDefaultRouteWidth",
                    "scenePlan.layoutPolicy.defaultRouteWidthMillimeters",
                    "Default route width must be positive."));
            }

            for (int i = 0; i < plan.Zones.Count; i++)
            {
                CharacterTestGenesisValidationZone zone = plan.Zones[i];
                string path = "scenePlan.zones[" + i + "]";
                if (string.IsNullOrWhiteSpace(zone.StableId))
                {
                    issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                        "MissingZoneStableId", path + ".stableId", "Zone StableId is required."));
                }
                else if (!zones.TryAdd(zone.StableId, zone))
                {
                    issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                        "DuplicateZoneStableId", path + ".stableId", "Zone StableId must be unique."));
                }
                if (!groups.Contains(zone.GroupId))
                {
                    issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                        "MissingZoneGroup", path + ".groupId", "Zone GroupId must resolve to one root group."));
                }
                if (zone.LayoutPattern == CharacterTestGenesisValidationZoneLayoutPattern.Unknown)
                {
                    issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                        "MissingZoneLayoutPattern", path + ".layoutPattern", "Zone layout pattern is required."));
                }
                ValidateRegionRef(zone.FootprintRegion, owners, issues, path + ".footprintRegion", true);
                ValidatePointRef(zone.EntryPoint, owners, issues, path + ".entryPoint", true);
                ValidatePointRef(zone.ExitPoint, owners, issues, path + ".exitPoint", true);
            }

            var routeIds = new HashSet<string>(StringComparer.Ordinal);
            var edges = new HashSet<string>(StringComparer.Ordinal);
            var adjacency = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (string zoneId in zones.Keys)
                adjacency.Add(zoneId, new HashSet<string>(StringComparer.Ordinal));
            for (int i = 0; i < plan.Routes.Count; i++)
            {
                CharacterTestGenesisValidationRoute route = plan.Routes[i];
                string path = "scenePlan.routes[" + i + "]";
                if (string.IsNullOrWhiteSpace(route.StableId) || !routeIds.Add(route.StableId))
                {
                    issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                        string.IsNullOrWhiteSpace(route.StableId) ? "MissingRouteStableId" : "DuplicateRouteStableId",
                        path + ".stableId",
                        "Route StableId is required and must be unique."));
                }
                bool fromExists = zones.ContainsKey(route.FromZoneId);
                bool toExists = zones.ContainsKey(route.ToZoneId);
                if (!fromExists)
                    AddMissing("RouteFromZoneNotFound", path + ".fromZoneId", issues);
                if (!toExists)
                    AddMissing("RouteToZoneNotFound", path + ".toZoneId", issues);
                if (fromExists && toExists && string.Equals(route.FromZoneId, route.ToZoneId, StringComparison.Ordinal))
                {
                    issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                        "RouteSelfLoop", path, "A scene route must connect two different zones."));
                }
                if (route.WidthMillimeters <= 0)
                {
                    issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                        "InvalidRouteWidth", path + ".widthMillimeters", "Route width must be positive."));
                }
                string edgeKey = string.CompareOrdinal(route.FromZoneId, route.ToZoneId) <= 0
                    ? route.FromZoneId + "\n" + route.ToZoneId
                    : route.ToZoneId + "\n" + route.FromZoneId;
                if (fromExists && toExists && !edges.Add(edgeKey))
                {
                    issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                        "DuplicateZoneRoute", path, "Only one canonical route may connect the same zone pair."));
                }
                if (fromExists && toExists)
                {
                    adjacency[route.FromZoneId].Add(route.ToZoneId);
                    adjacency[route.ToZoneId].Add(route.FromZoneId);
                }
                for (int waypointIndex = 0; waypointIndex < route.Waypoints.Count; waypointIndex++)
                {
                    ValidatePointRef(
                        route.Waypoints[waypointIndex],
                        owners,
                        issues,
                        path + ".waypoints[" + waypointIndex + "]",
                        true);
                }
            }

            if (plan.LayoutPolicy.RequireConnectedZones && zones.Count > 1)
                ValidateConnected(zones, adjacency, issues);
            return zones;
        }

        public static void ValidateStationSpatialProgram(
            CharacterTestGenesisValidationStation station,
            Dictionary<string, CharacterTestGenesisValidationZone> zones,
            CharacterTestGenesisValidationOwnerCatalog owners,
            List<CharacterTestGenesisValidationAuthoringIssue> issues,
            string path)
        {
            CharacterTestGenesisValidationStationSpatialProgram program = station.SpatialProgram;
            if (!station.Enabled && string.IsNullOrWhiteSpace(program.TemplateId))
                return;
            if (!CharacterTestGenesisValidationStationTemplateRegistry.TryGet(
                    program.TemplateId,
                    out ICharacterTestGenesisValidationStationTemplateDescriptor descriptor))
            {
                issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                    "UnknownStationTemplate", path + ".templateId", "Station template is not registered."));
                return;
            }
            if (!string.Equals(station.Scenario.TypeId, descriptor.ScenarioTypeId, StringComparison.Ordinal))
            {
                issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                    "StationTemplateScenarioMismatch",
                    path + ".templateId",
                    "Station template and typed Scenario do not describe the same station kind."));
            }
            if (!zones.TryGetValue(program.ZoneId, out CharacterTestGenesisValidationZone zone))
            {
                AddMissing("StationZoneNotFound", path + ".zoneId", issues);
            }
            else if (!string.Equals(zone.GroupId, station.GroupId, StringComparison.Ordinal))
            {
                issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                    "StationZoneGroupMismatch",
                    path + ".zoneId",
                    "Station and Zone must belong to the same logical group."));
            }
            ValidateRegionRef(program.FootprintRegion, owners, issues, path + ".footprintRegion", true);
            ValidatePointRef(program.EntryPoint, owners, issues, path + ".entryPoint", true);
            ValidatePointRef(program.ObservationPoint, owners, issues, path + ".observationPoint", true);
            ValidatePointRef(program.ExitPoint, owners, issues, path + ".exitPoint", true);
            if (program.MinimumApproachLengthMillimeters < descriptor.MinimumApproachLengthMillimeters)
            {
                issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                    "StationApproachTooShort",
                    path + ".minimumApproachLengthMillimeters",
                    "Station approach contract is shorter than the selected template minimum."));
            }
            if (program.MinimumClearanceMillimeters < descriptor.MinimumClearanceMillimeters)
            {
                issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                    "StationClearanceTooSmall",
                    path + ".minimumClearanceMillimeters",
                    "Station clearance contract is smaller than the selected template minimum."));
            }
            int sampleCount = station.Bindings?.SampleStaticColliders?.Count ?? 0;
            if (sampleCount < descriptor.MinimumSampleStaticCount)
            {
                issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                    "StationSampleLaneCountTooSmall",
                    path + ".templateId",
                    "Station does not provide enough ordered static sample lanes for the selected template."));
            }
        }

        private static void ValidateConnected(
            Dictionary<string, CharacterTestGenesisValidationZone> zones,
            Dictionary<string, HashSet<string>> adjacency,
            List<CharacterTestGenesisValidationAuthoringIssue> issues)
        {
            string first = null;
            foreach (string zoneId in zones.Keys)
            {
                first = zoneId;
                break;
            }
            if (first == null)
                return;
            var visited = new HashSet<string>(StringComparer.Ordinal) { first };
            var queue = new Queue<string>();
            queue.Enqueue(first);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                foreach (string next in adjacency[current])
                {
                    if (visited.Add(next))
                        queue.Enqueue(next);
                }
            }
            if (visited.Count != zones.Count)
            {
                issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                    "DisconnectedScenePlan",
                    "scenePlan.routes",
                    "All authored zones must be connected by the canonical route graph."));
            }
        }

        private static void ValidatePointRef(
            CharacterTestGenesisValidationPointRef reference,
            CharacterTestGenesisValidationOwnerCatalog owners,
            List<CharacterTestGenesisValidationAuthoringIssue> issues,
            string path,
            bool required)
        {
            if (reference == null || string.IsNullOrWhiteSpace(reference.StableId))
            {
                if (required)
                    AddMissing("MissingSpatialPointRef", path, issues);
                return;
            }
            if (!owners.ContainsPoint(reference.StableId))
                AddMissing("SpatialPointRefNotFound", path, issues);
        }

        private static void ValidateRegionRef(
            CharacterTestGenesisValidationRegionRef reference,
            CharacterTestGenesisValidationOwnerCatalog owners,
            List<CharacterTestGenesisValidationAuthoringIssue> issues,
            string path,
            bool required)
        {
            if (reference == null || string.IsNullOrWhiteSpace(reference.StableId))
            {
                if (required)
                    AddMissing("MissingSpatialRegionRef", path, issues);
                return;
            }
            if (!owners.ContainsRegion(reference.StableId))
                AddMissing("SpatialRegionRefNotFound", path, issues);
        }

        private static void AddMissing(
            string code,
            string path,
            List<CharacterTestGenesisValidationAuthoringIssue> issues)
        {
            issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                code,
                path,
                "The typed spatial reference cannot be resolved in its owner document."));
        }
    }

    internal sealed class CharacterTestGenesisValidationZoneComparer :
        IComparer<CharacterTestGenesisValidationZone>
    {
        public static readonly CharacterTestGenesisValidationZoneComparer Instance =
            new CharacterTestGenesisValidationZoneComparer();

        public int Compare(
            CharacterTestGenesisValidationZone left,
            CharacterTestGenesisValidationZone right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            int order = left.SortOrder.CompareTo(right.SortOrder);
            return order != 0 ? order : string.CompareOrdinal(left.StableId, right.StableId);
        }
    }

    internal sealed class CharacterTestGenesisValidationRouteComparer :
        IComparer<CharacterTestGenesisValidationRoute>
    {
        public static readonly CharacterTestGenesisValidationRouteComparer Instance =
            new CharacterTestGenesisValidationRouteComparer();

        public int Compare(
            CharacterTestGenesisValidationRoute left,
            CharacterTestGenesisValidationRoute right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            int from = string.CompareOrdinal(left.FromZoneId, right.FromZoneId);
            if (from != 0) return from;
            int to = string.CompareOrdinal(left.ToZoneId, right.ToZoneId);
            return to != 0 ? to : string.CompareOrdinal(left.StableId, right.StableId);
        }
    }
}
