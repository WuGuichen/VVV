using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Config.Levels;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed partial class CharacterTestGenesisValidationLevelEditorModule
    {
        internal void CreateFoundationSceneScaffoldForTests() =>
            CreateFoundationSceneScaffold();

        private void CreateFoundationSceneScaffold()
        {
            EnsureLoaded();
            EnsureUndoSession();
            RefreshOwnerDocuments();
            if (_worldObjectModule == null)
            {
                _status = "当前创世验证模块没有连接世界对象 owner，无法创建场景骨架。";
                return;
            }
            if (_document.ScenePlan.Zones.Count > 0 || _document.Stations.Count > 0)
            {
                _status = "当前文档已经包含 Zone 或 Station；为避免重复布局，本次未创建。";
                return;
            }
            string subjectCharacterId = ResolveFirstCharacterStableId();
            if (string.IsNullOrWhiteSpace(subjectCharacterId))
            {
                _status = "场景中没有可绑定的角色实例；请先通过角色 owner 创建角色。";
                return;
            }
            GameManager manager = FindGameManager();
            Transform worldRoot = manager?.WorldObjectSceneRoot;
            if (worldRoot == null)
            {
                _status = "当前场景中未找到 CharacterTest 世界对象根节点。";
                return;
            }

            Vector3 anchor = ResolveScaffoldAnchor(worldRoot);
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("创建创世验证场景骨架");
            try
            {
                using CharacterTestLevelEditorDocumentUndoSession.Transaction transaction =
                    _undoSession.BeginJoinedTransaction("创建创世验证场景骨架", undoGroup);
                if (!_worldObjectModule.TryCreateGenesisValidationScaffold(
                        anchor,
                        undoGroup,
                        out CharacterTestGenesisValidationWorldScaffoldResult scaffold,
                        out string failureMessage))
                {
                    _status = failureMessage;
                    return;
                }

                PopulateFoundationScenePlan(scaffold, subjectCharacterId);
                _document = _document.Normalize();
                transaction.Commit();
            }
            catch (Exception exception)
            {
                _status = "创建基础场景框架失败，已回滚：" + exception.Message;
                return;
            }
            Undo.CollapseUndoOperations(undoGroup);
            InvalidateSaveCompletionForEdit();
            _dirty = true;
            RefreshOwnerDocuments();
            _report = ValidateDocument();
            _selectedStation = _document.Stations.Count == 0 ? null : _document.Stations[0];
            RefreshAll(
                "已创建基础场景框架：3 个 Zone、2 条 Route、3 个模板 Station；尚未保存。 ");
            SceneView.RepaintAll();
        }

        private void PopulateFoundationScenePlan(
            CharacterTestGenesisValidationWorldScaffoldResult scaffold,
            string subjectCharacterId)
        {
            _document.ScenePlan = new CharacterTestGenesisValidationScenePlan
            {
                SceneBounds = scaffold.SceneBounds,
                OriginPoint = scaffold.OriginPoint,
                LayoutPolicy = new CharacterTestGenesisValidationSceneLayoutPolicy(),
                Zones = new List<CharacterTestGenesisValidationZone>(),
                Routes = new List<CharacterTestGenesisValidationRoute>()
            };
            for (int i = 0; i < scaffold.Stations.Count; i++)
            {
                CharacterTestGenesisValidationWorldStationScaffold source = scaffold.Stations[i];
                _document.ScenePlan.Zones.Add(new CharacterTestGenesisValidationZone
                {
                    StableId = source.ZoneId,
                    DisplayName = source.ZoneDisplayName,
                    GroupId = source.GroupId,
                    SortOrder = source.SortOrder,
                    LayoutPattern = source.LayoutPattern,
                    FootprintRegion = source.FootprintRegion,
                    EntryPoint = source.EntryPoint,
                    ExitPoint = source.ExitPoint
                });
                _document.Stations.Add(CreateStationFromScaffold(source, subjectCharacterId));
            }
            for (int i = 0; i + 1 < scaffold.Stations.Count; i++)
            {
                CharacterTestGenesisValidationWorldStationScaffold from = scaffold.Stations[i];
                CharacterTestGenesisValidationWorldStationScaffold to = scaffold.Stations[i + 1];
                _document.ScenePlan.Routes.Add(new CharacterTestGenesisValidationRoute
                {
                    StableId = "route." + from.ZoneId + ".to." + to.ZoneId,
                    FromZoneId = from.ZoneId,
                    ToZoneId = to.ZoneId,
                    Bidirectional = true,
                    WidthMillimeters = _document.ScenePlan.LayoutPolicy.DefaultRouteWidthMillimeters
                });
            }
        }

        private static CharacterTestGenesisValidationStation CreateStationFromScaffold(
            CharacterTestGenesisValidationWorldStationScaffold source,
            string subjectCharacterId)
        {
            CharacterTestGenesisValidationStationTemplateRegistry.TryGet(
                source.TemplateId,
                out ICharacterTestGenesisValidationStationTemplateDescriptor template);
            CharacterTestGenesisValidationAuthoringScenarioRegistry.TryGet(
                template.ScenarioTypeId,
                1,
                out ICharacterTestGenesisValidationAuthoringScenarioDescriptor scenarioDescriptor);
            var station = new CharacterTestGenesisValidationStation
            {
                StableId = "station." + source.ZoneId,
                DisplayName = source.ZoneDisplayName,
                Enabled = true,
                GroupId = source.GroupId,
                SortOrder = source.SortOrder,
                Intent = ResolveTemplateIntent(source.TemplateId),
                CanonTrace = new CharacterTestGenesisValidationCanonTrace
                {
                    SourcePath = "StorySource/Projects/WGame/genesis_stage_overview.md",
                    SectionTitle = "场景验证框架",
                    DesignNote = "由基础场景模板生成；Canon 仅用于追溯，不复制领域事实。"
                },
                Bindings = new CharacterTestGenesisValidationBindings
                {
                    SubjectCharacter = new CharacterTestGenesisValidationCharacterRef
                    {
                        StableId = subjectCharacterId
                    },
                    TargetMovableObject = source.TargetMovable,
                    TargetStaticCollider = source.TargetStatic,
                    SampleStaticColliders = new List<CharacterTestGenesisValidationStaticColliderRef>(
                        source.SampleStatics),
                    StartPoint = source.ApproachStart,
                    ResetPoint = source.ResetPoint,
                    ObservationRegion = source.FootprintRegion
                },
                SpatialProgram = new CharacterTestGenesisValidationStationSpatialProgram
                {
                    TemplateId = source.TemplateId,
                    ZoneId = source.ZoneId,
                    FootprintRegion = source.FootprintRegion,
                    EntryPoint = source.EntryPoint,
                    ObservationPoint = source.ObservationPoint,
                    ExitPoint = source.ExitPoint,
                    MinimumApproachLengthMillimeters = template.MinimumApproachLengthMillimeters,
                    MinimumClearanceMillimeters = template.MinimumClearanceMillimeters
                },
                Scenario = scenarioDescriptor.CreateDefaultScenario(),
                Observation = new CharacterTestGenesisValidationObservation
                {
                    BodyPrompt = ResolveTemplateBodyPrompt(source.TemplateId),
                    RequiredSignalIds = CreateDefaultSignals(scenarioDescriptor)
                },
                ResetPolicy = CharacterTestGenesisValidationResetPolicy.SessionRebuild,
                Notes = "基础模板生成；实际身体结论必须人工记录。"
            };
            return station;
        }

        private static List<string> CreateDefaultSignals(
            ICharacterTestGenesisValidationAuthoringScenarioDescriptor descriptor)
        {
            var result = new List<string>();
            int count = Math.Min(2, descriptor.AllowedSignalIds.Count);
            for (int i = 0; i < count; i++)
                result.Add(descriptor.AllowedSignalIds[i]);
            return result;
        }

        private static string ResolveTemplateIntent(string templateId)
        {
            switch (templateId)
            {
                case CharacterTestGenesisValidationStationTemplateIds.SurfaceComparisonLane:
                    return "在相同接近条件下比较硬滑、粗糙与弹性表面的身体接触响应。";
                case CharacterTestGenesisValidationStationTemplateIds.BoundaryApproachBay:
                    return "以可重复接近路径观察边界碰撞后的身体响应与落地状态。";
                case CharacterTestGenesisValidationStationTemplateIds.MovableObjectSandbox:
                    return "在安全占地区域内观察身体推动可动自然物后的双方运动变化。";
                default:
                    return "验证模板所声明的身体事实。";
            }
        }

        private static string ResolveTemplateBodyPrompt(string templateId)
        {
            switch (templateId)
            {
                case CharacterTestGenesisValidationStationTemplateIds.SurfaceComparisonLane:
                    return "依次走过三条样本跑道，记录摩擦、弹性与动作连续性的可感知差异。";
                case CharacterTestGenesisValidationStationTemplateIds.BoundaryApproachBay:
                    return "从 ApproachStart 接近边界，记录接触、停步、滑动与离地反馈。";
                case CharacterTestGenesisValidationStationTemplateIds.MovableObjectSandbox:
                    return "从 ApproachStart 推动目标，记录身体受阻感与目标移动响应。";
                default:
                    return "记录身体反馈与 typed 读数，不把人工体感伪装成自动结论。";
            }
        }

        private string ResolveFirstCharacterStableId()
        {
            if (_characters?.Characters == null)
                return string.Empty;
            for (int i = 0; i < _characters.Characters.Count; i++)
            {
                CharacterTestSceneCharacterDefinition character = _characters.Characters[i];
                if (character.Enabled && !string.IsNullOrWhiteSpace(character.StableId))
                    return character.StableId;
            }
            return string.Empty;
        }

        private static Vector3 ResolveScaffoldAnchor(Transform worldRoot)
        {
            Transform selected = Selection.activeTransform;
            if (selected != null && selected.IsChildOf(worldRoot))
                return CharacterTestLevelEditorPlacementUtility.ApplyHorizontalGrid(
                    selected.position,
                    CharacterTestLevelEditorPlacementUtility.GridSnapEnabled);

            Transform[] existing = worldRoot.GetComponentsInChildren<Transform>(includeInactive: true);
            bool hasExistingContent = false;
            float maximumX = worldRoot.position.x;
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] == worldRoot)
                    continue;
                hasExistingContent = true;
                maximumX = Mathf.Max(maximumX, existing[i].position.x);
            }
            Vector3 candidate = hasExistingContent
                ? new Vector3(maximumX + 12f, worldRoot.position.y, worldRoot.position.z)
                : worldRoot.position;
            return CharacterTestLevelEditorPlacementUtility.ApplyHorizontalGrid(
                candidate,
                CharacterTestLevelEditorPlacementUtility.GridSnapEnabled);
        }

        private void BuildScenePlanInspector(VisualElement root)
        {
            CharacterTestGenesisValidationScenePlan plan = _document.ScenePlan;
            root.Add(new HelpBox(
                "Scene Plan 是场景设计基础层：定义边界、原点、Zone、Route 和全局布局策略；具体 GameObject 仍归世界对象 owner。",
                HelpBoxMessageType.Info));
            root.Add(CreateStringPicker(
                "场景边界 Region",
                CollectRegionIds(),
                plan.SceneBounds?.StableId,
                value => plan.SceneBounds = CreateRegionRef(value, plan.SceneBounds),
                "修改场景边界"));
            root.Add(CreateStringPicker(
                "场景原点 Point",
                CollectPointIds(),
                plan.OriginPoint?.StableId,
                value => plan.OriginPoint = CreatePointRef(value, plan.OriginPoint),
                "修改场景原点"));

            Foldout policy = CreateFoldout(root, "全局布局策略", true);
            policy.Add(CreateIntegerField(
                "Zone 最小间距（mm）",
                plan.LayoutPolicy.MinimumZoneClearanceMillimeters,
                value => plan.LayoutPolicy.MinimumZoneClearanceMillimeters = value,
                "修改 Zone 最小间距"));
            policy.Add(CreateIntegerField(
                "默认 Route 宽度（mm）",
                plan.LayoutPolicy.DefaultRouteWidthMillimeters,
                value => plan.LayoutPolicy.DefaultRouteWidthMillimeters = value,
                "修改 Route 宽度"));
            policy.Add(CreateToggle(
                "要求 Zone 连通",
                plan.LayoutPolicy.RequireConnectedZones,
                value => plan.LayoutPolicy.RequireConnectedZones = value,
                "修改 Zone 连通策略"));
            policy.Add(CreateToggle(
                "空间角色必须位于 footprint 内",
                plan.LayoutPolicy.RequireSpatialRolesInsideFootprint,
                value => plan.LayoutPolicy.RequireSpatialRolesInsideFootprint = value,
                "修改角色局部性策略"));

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.Add(CreateButton("新增 Zone", AddZone));
            actions.Add(CreateButton("新增 Route", AddRoute));
            root.Add(actions);

            for (int i = 0; i < plan.Zones.Count; i++)
                root.Add(BuildZoneInspector(plan.Zones[i], i));
            for (int i = 0; i < plan.Routes.Count; i++)
                root.Add(BuildRouteInspector(plan.Routes[i], i));
        }

        private VisualElement BuildZoneInspector(
            CharacterTestGenesisValidationZone zone,
            int index)
        {
            var foldout = new Foldout
            {
                text = "Zone " + (index + 1) + " · " + zone.DisplayName,
                value = false
            };
            foldout.Add(CreateTextField("Stable ID", zone.StableId, value => zone.StableId = value, "修改 Zone Stable ID"));
            foldout.Add(CreateTextField("名称", zone.DisplayName, value => zone.DisplayName = value, "修改 Zone 名称"));
            foldout.Add(CreateStringPicker("分组", CollectGroupIds(), zone.GroupId, value => zone.GroupId = value, "修改 Zone 分组", false));
            foldout.Add(CreateIntegerField("排序", zone.SortOrder, value => zone.SortOrder = value, "修改 Zone 排序"));
            var patternNames = new List<string>(Enum.GetNames(typeof(CharacterTestGenesisValidationZoneLayoutPattern)));
            foldout.Add(CreateStringPicker(
                "布局模式",
                patternNames,
                zone.LayoutPattern.ToString(),
                value => zone.LayoutPattern = Enum.TryParse(
                    value,
                    out CharacterTestGenesisValidationZoneLayoutPattern parsed)
                        ? parsed
                        : CharacterTestGenesisValidationZoneLayoutPattern.Unknown,
                "修改 Zone 布局模式",
                false));
            foldout.Add(CreateStringPicker("Footprint Region", CollectRegionIds(), zone.FootprintRegion?.StableId, value => zone.FootprintRegion = CreateRegionRef(value, zone.FootprintRegion), "绑定 Zone footprint"));
            foldout.Add(CreateStringPicker("Entry Point", CollectPointIds(), zone.EntryPoint?.StableId, value => zone.EntryPoint = CreatePointRef(value, zone.EntryPoint), "绑定 Zone Entry"));
            foldout.Add(CreateStringPicker("Exit Point", CollectPointIds(), zone.ExitPoint?.StableId, value => zone.ExitPoint = CreatePointRef(value, zone.ExitPoint), "绑定 Zone Exit"));
            foldout.Add(CreateButton("删除 Zone", () => Mutate(
                "删除 Scene Plan Zone",
                () => _document.ScenePlan.Zones.Remove(zone))));
            return foldout;
        }

        private VisualElement BuildRouteInspector(
            CharacterTestGenesisValidationRoute route,
            int index)
        {
            var foldout = new Foldout
            {
                text = "Route " + (index + 1) + " · " + route.FromZoneId + " → " + route.ToZoneId,
                value = false
            };
            List<string> zoneIds = CollectZoneIds();
            foldout.Add(CreateTextField("Stable ID", route.StableId, value => route.StableId = value, "修改 Route Stable ID"));
            foldout.Add(CreateStringPicker("起点 Zone", zoneIds, route.FromZoneId, value => route.FromZoneId = value, "修改 Route 起点", false));
            foldout.Add(CreateStringPicker("终点 Zone", zoneIds, route.ToZoneId, value => route.ToZoneId = value, "修改 Route 终点", false));
            foldout.Add(CreateToggle("双向", route.Bidirectional, value => route.Bidirectional = value, "修改 Route 方向"));
            foldout.Add(CreateIntegerField("宽度（mm）", route.WidthMillimeters, value => route.WidthMillimeters = value, "修改 Route 宽度"));
            for (int i = 0; i < route.Waypoints.Count; i++)
            {
                int waypointIndex = i;
                CharacterTestGenesisValidationPointRef waypoint = route.Waypoints[i];
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                VisualElement picker = CreateStringPicker(
                    "Waypoint " + (i + 1),
                    CollectPointIds(),
                    waypoint?.StableId,
                    value => route.Waypoints[waypointIndex] = CreatePointRef(value, waypoint),
                    "修改 Route Waypoint");
                picker.style.flexGrow = 1f;
                row.Add(picker);
                row.Add(CreateButton("删除", () => Mutate(
                    "删除 Route Waypoint",
                    () => route.Waypoints.RemoveAt(waypointIndex))));
                foldout.Add(row);
            }
            foldout.Add(CreateButton("新增 Waypoint", () =>
            {
                List<string> pointIds = CollectPointIds();
                if (pointIds.Count == 0)
                {
                    _status = "当前世界对象文档没有可绑定的 Point。";
                    return;
                }
                Mutate(
                    "新增 Route Waypoint",
                    () => route.Waypoints.Add(CreatePointRef(pointIds[0], null)));
            }));
            foldout.Add(CreateButton("删除 Route", () => Mutate(
                "删除 Scene Plan Route",
                () => _document.ScenePlan.Routes.Remove(route))));
            return foldout;
        }

        private void BuildSpatialProgramInspector(
            VisualElement root,
            CharacterTestGenesisValidationStation station)
        {
            CharacterTestGenesisValidationStationSpatialProgram program = station.SpatialProgram;
            Foldout spatial = CreateFoldout(root, "Station Spatial Program", true);
            var templateIds = new List<string>();
            for (int i = 0; i < CharacterTestGenesisValidationStationTemplateRegistry.All.Count; i++)
                templateIds.Add(CharacterTestGenesisValidationStationTemplateRegistry.All[i].TemplateId);
            spatial.Add(CreateStringPicker(
                "场景模板",
                templateIds,
                program.TemplateId,
                value => ApplyStationTemplate(station, value),
                "修改 Station 场景模板",
                false));
            spatial.Add(CreateStringPicker("所属 Zone", CollectZoneIds(), program.ZoneId, value => program.ZoneId = value, "修改 Station Zone", false));
            spatial.Add(CreateStringPicker("Footprint Region", CollectRegionIds(), program.FootprintRegion?.StableId, value => program.FootprintRegion = CreateRegionRef(value, program.FootprintRegion), "绑定 Station footprint"));
            spatial.Add(CreateStringPicker("Entry Point", CollectPointIds(), program.EntryPoint?.StableId, value => program.EntryPoint = CreatePointRef(value, program.EntryPoint), "绑定 Station Entry"));
            spatial.Add(CreateStringPicker("Observation Point", CollectPointIds(), program.ObservationPoint?.StableId, value => program.ObservationPoint = CreatePointRef(value, program.ObservationPoint), "绑定 Station Observation"));
            spatial.Add(CreateStringPicker("Exit Point", CollectPointIds(), program.ExitPoint?.StableId, value => program.ExitPoint = CreatePointRef(value, program.ExitPoint), "绑定 Station Exit"));
            spatial.Add(CreateIntegerField("最短接近距离（mm）", program.MinimumApproachLengthMillimeters, value => program.MinimumApproachLengthMillimeters = value, "修改接近距离合同"));
            spatial.Add(CreateIntegerField("最小净空（mm）", program.MinimumClearanceMillimeters, value => program.MinimumClearanceMillimeters = value, "修改净空合同"));
        }

        private static void ApplyStationTemplate(
            CharacterTestGenesisValidationStation station,
            string templateId)
        {
            if (!CharacterTestGenesisValidationStationTemplateRegistry.TryGet(
                    templateId,
                    out ICharacterTestGenesisValidationStationTemplateDescriptor template))
                return;
            station.SpatialProgram.TemplateId = template.TemplateId;
            station.SpatialProgram.MinimumApproachLengthMillimeters =
                template.MinimumApproachLengthMillimeters;
            station.SpatialProgram.MinimumClearanceMillimeters =
                template.MinimumClearanceMillimeters;
            station.GroupId = template.DefaultGroupId;
            if (CharacterTestGenesisValidationAuthoringScenarioRegistry.TryGet(
                    template.ScenarioTypeId,
                    1,
                    out ICharacterTestGenesisValidationAuthoringScenarioDescriptor descriptor))
            {
                station.Scenario = descriptor.CreateDefaultScenario();
                station.Observation.RequiredSignalIds = CreateDefaultSignals(descriptor);
            }
        }

        private void AddZone()
        {
            int next = _document.ScenePlan.Zones.Count + 1;
            Mutate("新增 Scene Plan Zone", () => _document.ScenePlan.Zones.Add(
                new CharacterTestGenesisValidationZone
                {
                    StableId = "zone.new-" + next.ToString("00", CultureInfo.InvariantCulture),
                    DisplayName = "新 Zone " + next,
                    GroupId = _document.Groups.Count == 0 ? string.Empty : _document.Groups[0].StableId,
                    SortOrder = next * 10,
                    LayoutPattern = CharacterTestGenesisValidationZoneLayoutPattern.LinearApproach
                }));
        }

        private void AddRoute()
        {
            if (_document.ScenePlan.Zones.Count < 2)
            {
                _status = "至少需要两个 Zone 才能创建 Route。";
                return;
            }
            int next = _document.ScenePlan.Routes.Count + 1;
            Mutate("新增 Scene Plan Route", () => _document.ScenePlan.Routes.Add(
                new CharacterTestGenesisValidationRoute
                {
                    StableId = "route.new-" + next.ToString("00", CultureInfo.InvariantCulture),
                    FromZoneId = _document.ScenePlan.Zones[0].StableId,
                    ToZoneId = _document.ScenePlan.Zones[1].StableId,
                    WidthMillimeters = _document.ScenePlan.LayoutPolicy.DefaultRouteWidthMillimeters
                }));
        }

        private List<string> CollectGroupIds()
        {
            var result = new List<string>();
            for (int i = 0; i < _document.Groups.Count; i++)
                result.Add(_document.Groups[i].StableId);
            return result;
        }

        private List<string> CollectZoneIds()
        {
            var result = new List<string>();
            for (int i = 0; i < _document.ScenePlan.Zones.Count; i++)
                result.Add(_document.ScenePlan.Zones[i].StableId);
            return result;
        }

        private void DrawScenePlan(SceneView sceneView)
        {
            if (_document?.ScenePlan == null || sceneView == null)
                return;
            Transform root = FindGameManager()?.WorldObjectSceneRoot;
            if (root == null)
                return;
            if (TryResolveRegionTransform(
                    root,
                    _document.ScenePlan.SceneBounds?.StableId,
                    out Transform sceneBounds))
            {
                DrawRegionBox(sceneBounds, new Color(1f, 0.76f, 0.2f, 0.75f));
                Handles.Label(sceneBounds.position + Vector3.up * 0.8f, "Scene Bounds");
            }
            for (int i = 0; i < _document.ScenePlan.Zones.Count; i++)
            {
                CharacterTestGenesisValidationZone zone = _document.ScenePlan.Zones[i];
                if (!TryResolveRegionTransform(root, zone.FootprintRegion?.StableId, out Transform footprint))
                    continue;
                Color color = IsSelectedZone(zone.StableId)
                    ? new Color(0.15f, 0.9f, 1f, 0.95f)
                    : new Color(0.2f, 0.65f, 1f, 0.65f);
                DrawRegionBox(footprint, color);
                Handles.Label(
                    footprint.position + Vector3.up * 0.55f,
                    zone.DisplayName + "\n" + zone.LayoutPattern);
                DrawRolePoint(root, zone.EntryPoint?.StableId, "Entry", new Color(0.2f, 1f, 0.45f, 0.9f));
                DrawRolePoint(root, zone.ExitPoint?.StableId, "Exit", new Color(1f, 0.55f, 0.2f, 0.9f));
            }
            for (int i = 0; i < _document.ScenePlan.Routes.Count; i++)
                DrawRoute(root, _document.ScenePlan.Routes[i]);
        }

        private bool IsSelectedZone(string zoneId)
        {
            return _selectedStation != null
                && string.Equals(
                    _selectedStation.SpatialProgram?.ZoneId,
                    zoneId,
                    StringComparison.Ordinal);
        }

        private static void DrawRegionBox(Transform transform, Color color)
        {
            using (new Handles.DrawingScope(color, transform.localToWorldMatrix))
                Handles.DrawWireCube(Vector3.zero, Vector3.one);
        }

        private void DrawRolePoint(
            Transform root,
            string stableId,
            string label,
            Color color)
        {
            if (!TryResolvePointTransform(root, stableId, out Transform point))
                return;
            Handles.color = color;
            Handles.DrawWireDisc(point.position, Vector3.up, 0.35f);
            Handles.Label(point.position + Vector3.up * 0.35f, label);
        }

        private void DrawRoute(Transform root, CharacterTestGenesisValidationRoute route)
        {
            CharacterTestGenesisValidationZone from = FindZone(route.FromZoneId);
            CharacterTestGenesisValidationZone to = FindZone(route.ToZoneId);
            if (from == null
                || to == null
                || !TryResolvePointTransform(root, from.ExitPoint?.StableId, out Transform start)
                || !TryResolvePointTransform(root, to.EntryPoint?.StableId, out Transform end))
                return;
            var positions = new List<Vector3> { start.position };
            for (int i = 0; i < route.Waypoints.Count; i++)
            {
                if (TryResolvePointTransform(root, route.Waypoints[i]?.StableId, out Transform waypoint))
                    positions.Add(waypoint.position);
            }
            positions.Add(end.position);
            Handles.color = new Color(0.78f, 0.56f, 1f, 0.9f);
            Handles.DrawAAPolyLine(4f, positions.ToArray());
            Vector3 arrowStart = positions[positions.Count - 2];
            Vector3 arrowEnd = positions[positions.Count - 1];
            Vector3 direction = arrowEnd - arrowStart;
            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                Handles.ArrowHandleCap(
                    0,
                    Vector3.Lerp(arrowStart, arrowEnd, 0.65f),
                    rotation,
                    1.2f,
                    EventType.Repaint);
            }
            if (route.Bidirectional)
            {
                Vector3 reverseStart = positions[1];
                Vector3 reverseEnd = positions[0];
                Vector3 reverseDirection = reverseEnd - reverseStart;
                if (reverseDirection.sqrMagnitude > 0.001f)
                {
                    Handles.ArrowHandleCap(
                        0,
                        Vector3.Lerp(reverseStart, reverseEnd, 0.65f),
                        Quaternion.LookRotation(reverseDirection.normalized, Vector3.up),
                        1.2f,
                        EventType.Repaint);
                }
            }
            int labelSegment = (positions.Count - 1) / 2;
            Handles.Label(
                Vector3.Lerp(positions[labelSegment], positions[labelSegment + 1], 0.5f),
                route.StableId);
        }

        private CharacterTestGenesisValidationZone FindZone(string stableId)
        {
            for (int i = 0; i < _document.ScenePlan.Zones.Count; i++)
            {
                if (string.Equals(_document.ScenePlan.Zones[i].StableId, stableId, StringComparison.Ordinal))
                    return _document.ScenePlan.Zones[i];
            }
            return null;
        }

        private bool TryResolvePointTransform(Transform root, string stableId, out Transform transform)
        {
            transform = null;
            if (string.IsNullOrWhiteSpace(stableId) || _worldObjects?.Points == null)
                return false;
            for (int i = 0; i < _worldObjects.Points.Count; i++)
            {
                LevelWorldObjectPointDefinition item = _worldObjects.Points[i];
                if (string.Equals(item.StableId, stableId, StringComparison.Ordinal)
                    && CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(
                        root,
                        item.TransformRef,
                        out transform))
                    return true;
            }
            return false;
        }

        private bool TryResolveRegionTransform(Transform root, string stableId, out Transform transform)
        {
            transform = null;
            if (string.IsNullOrWhiteSpace(stableId) || _worldObjects?.Regions == null)
                return false;
            for (int i = 0; i < _worldObjects.Regions.Count; i++)
            {
                LevelWorldObjectRegionDefinition item = _worldObjects.Regions[i];
                if (string.Equals(item.StableId, stableId, StringComparison.Ordinal)
                    && CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(
                        root,
                        item.TransformRef,
                        out transform))
                    return true;
            }
            return false;
        }
    }

    internal static class CharacterTestGenesisValidationEditorLayoutValidator
    {
        public static IReadOnlyList<CharacterTestGenesisValidationAuthoringIssue> Validate(
            CharacterTestGenesisValidationAuthoringDocument document,
            LevelWorldObjectAuthoringDocument worldObjects,
            Transform root,
            out string resolvedSpatialInputHash)
        {
            var issues = new List<CharacterTestGenesisValidationAuthoringIssue>();
            resolvedSpatialInputHash = string.Empty;
            if (document?.ScenePlan == null
                || document.ScenePlan.Zones.Count == 0
                || worldObjects == null
                || root == null)
                return issues;

            var points = ResolvePoints(worldObjects, root);
            var regions = ResolveRegions(worldObjects, root);
            var spatialInputs = new SortedDictionary<string, Transform>(StringComparer.Ordinal);
            AddSpatialInput(spatialInputs, "scene.bounds", document.ScenePlan.SceneBounds?.StableId, regions);
            AddSpatialInput(spatialInputs, "scene.origin", document.ScenePlan.OriginPoint?.StableId, points);
            ValidateZones(document, regions, points, spatialInputs, issues);
            ValidateStations(document, worldObjects, root, regions, points, spatialInputs, issues);
            ValidateRouteCrossings(document, points, spatialInputs, issues);
            resolvedSpatialInputHash = ComputeSpatialHash(spatialInputs);
            return issues;
        }

        private static void ValidateZones(
            CharacterTestGenesisValidationAuthoringDocument document,
            Dictionary<string, Transform> regions,
            Dictionary<string, Transform> points,
            SortedDictionary<string, Transform> inputs,
            List<CharacterTestGenesisValidationAuthoringIssue> issues)
        {
            CharacterTestGenesisValidationScenePlan plan = document.ScenePlan;
            regions.TryGetValue(plan.SceneBounds?.StableId ?? string.Empty, out Transform sceneBounds);
            for (int i = 0; i < plan.Zones.Count; i++)
            {
                CharacterTestGenesisValidationZone zone = plan.Zones[i];
                string path = "scenePlan.zones[" + i + "]";
                if (!regions.TryGetValue(zone.FootprintRegion?.StableId ?? string.Empty, out Transform footprint))
                    continue;
                inputs["zone." + zone.StableId + ".footprint"] = footprint;
                AddSpatialInput(inputs, "zone." + zone.StableId + ".entry", zone.EntryPoint?.StableId, points);
                AddSpatialInput(inputs, "zone." + zone.StableId + ".exit", zone.ExitPoint?.StableId, points);
                if (sceneBounds != null && !ContainsBox(sceneBounds, footprint))
                {
                    issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                        "ZoneOutsideSceneBounds",
                        path + ".footprintRegion",
                        "Zone footprint is not fully contained by Scene Bounds."));
                }
            }

            float clearance = plan.LayoutPolicy.MinimumZoneClearanceMillimeters / 1000f;
            for (int leftIndex = 0; leftIndex < plan.Zones.Count; leftIndex++)
            {
                CharacterTestGenesisValidationZone left = plan.Zones[leftIndex];
                if (!regions.TryGetValue(left.FootprintRegion?.StableId ?? string.Empty, out Transform leftTransform))
                    continue;
                Bounds2D leftBounds = Bounds2D.FromTransform(leftTransform);
                for (int rightIndex = leftIndex + 1; rightIndex < plan.Zones.Count; rightIndex++)
                {
                    CharacterTestGenesisValidationZone right = plan.Zones[rightIndex];
                    if (!regions.TryGetValue(right.FootprintRegion?.StableId ?? string.Empty, out Transform rightTransform))
                        continue;
                    Bounds2D rightBounds = Bounds2D.FromTransform(rightTransform);
                    float distance = leftBounds.DistanceTo(rightBounds);
                    if (leftBounds.Overlaps(rightBounds))
                    {
                        issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                            "ZoneFootprintsOverlap",
                            "scenePlan.zones[" + rightIndex + "].footprintRegion",
                            "Zone footprint overlaps " + left.StableId + "."));
                    }
                    else if (distance + 0.0001f < clearance)
                    {
                        issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                            "ZoneClearanceTooSmall",
                            "scenePlan.zones[" + rightIndex + "].footprintRegion",
                            "Zone clearance is smaller than the global layout policy."));
                    }
                }
            }
        }

        private static void ValidateStations(
            CharacterTestGenesisValidationAuthoringDocument document,
            LevelWorldObjectAuthoringDocument worldObjects,
            Transform root,
            Dictionary<string, Transform> regions,
            Dictionary<string, Transform> points,
            SortedDictionary<string, Transform> inputs,
            List<CharacterTestGenesisValidationAuthoringIssue> issues)
        {
            for (int i = 0; i < document.Stations.Count; i++)
            {
                CharacterTestGenesisValidationStation station = document.Stations[i];
                CharacterTestGenesisValidationStationSpatialProgram program = station.SpatialProgram;
                string path = "stations[" + i + "].spatialProgram";
                if (!regions.TryGetValue(program.FootprintRegion?.StableId ?? string.Empty, out Transform footprint))
                    continue;
                inputs["station." + station.StableId + ".footprint"] = footprint;
                var roles = new[]
                {
                    new SpatialRole("entryPoint", program.EntryPoint?.StableId),
                    new SpatialRole("approachStart", station.Bindings.StartPoint?.StableId),
                    new SpatialRole("resetPoint", station.Bindings.ResetPoint?.StableId),
                    new SpatialRole("observationPoint", program.ObservationPoint?.StableId),
                    new SpatialRole("exitPoint", program.ExitPoint?.StableId)
                };
                for (int roleIndex = 0; roleIndex < roles.Length; roleIndex++)
                {
                    SpatialRole role = roles[roleIndex];
                    if (!points.TryGetValue(role.StableId ?? string.Empty, out Transform point))
                        continue;
                    inputs["station." + station.StableId + "." + role.Name] = point;
                    if (document.ScenePlan.LayoutPolicy.RequireSpatialRolesInsideFootprint
                        && !ContainsPoint(footprint, point.position))
                    {
                        issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                            "StationSpatialRoleOutsideFootprint",
                            path + "." + role.Name,
                            "Station spatial role is outside its footprint."));
                    }
                }

                Transform target = ResolvePrimaryTarget(station, worldObjects, root);
                if (target != null)
                {
                    inputs["station." + station.StableId + ".target"] = target;
                    if (!ContainsPoint(footprint, target.position))
                    {
                        issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                            "StationTargetOutsideFootprint",
                            path + ".footprintRegion",
                            "Primary station target is outside its footprint."));
                    }
                    if (points.TryGetValue(
                            station.Bindings.StartPoint?.StableId ?? string.Empty,
                            out Transform start))
                    {
                        float distance = HorizontalDistance(start.position, target.position);
                        if (distance * 1000f + 0.5f < program.MinimumApproachLengthMillimeters)
                        {
                            issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                                "ResolvedApproachLengthTooShort",
                                path + ".minimumApproachLengthMillimeters",
                                "Resolved ApproachStart-to-target distance is shorter than the station contract."));
                        }
                    }
                }
                ValidateSampleLaneClearance(station, worldObjects, root, path, inputs, issues);
            }
        }

        private static void ValidateSampleLaneClearance(
            CharacterTestGenesisValidationStation station,
            LevelWorldObjectAuthoringDocument worldObjects,
            Transform root,
            string path,
            SortedDictionary<string, Transform> inputs,
            List<CharacterTestGenesisValidationAuthoringIssue> issues)
        {
            List<CharacterTestGenesisValidationStaticColliderRef> samples =
                station.Bindings.SampleStaticColliders;
            if (samples == null || samples.Count < 2)
                return;
            var transforms = new List<Transform>();
            for (int i = 0; i < samples.Count; i++)
            {
                Transform transform = ResolveStatic(samples[i], worldObjects, root);
                if (transform == null)
                    continue;
                transforms.Add(transform);
                inputs["station." + station.StableId + ".sample." + i] = transform;
            }
            float minimum = station.SpatialProgram.MinimumClearanceMillimeters / 1000f;
            for (int left = 0; left < transforms.Count; left++)
            {
                for (int right = left + 1; right < transforms.Count; right++)
                {
                    if (HorizontalDistance(transforms[left].position, transforms[right].position) + 0.0001f < minimum)
                    {
                        issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                            "SampleLaneClearanceTooSmall",
                            path + ".minimumClearanceMillimeters",
                            "Resolved sample lane centers violate the station clearance contract."));
                        return;
                    }
                }
            }
        }

        private static void ValidateRouteCrossings(
            CharacterTestGenesisValidationAuthoringDocument document,
            Dictionary<string, Transform> points,
            SortedDictionary<string, Transform> inputs,
            List<CharacterTestGenesisValidationAuthoringIssue> issues)
        {
            var segments = new List<RouteSegment>();
            for (int i = 0; i < document.ScenePlan.Routes.Count; i++)
            {
                CharacterTestGenesisValidationRoute route = document.ScenePlan.Routes[i];
                CharacterTestGenesisValidationZone from = FindZone(document, route.FromZoneId);
                CharacterTestGenesisValidationZone to = FindZone(document, route.ToZoneId);
                if (from == null
                    || to == null
                    || !points.TryGetValue(from.ExitPoint?.StableId ?? string.Empty, out Transform start)
                    || !points.TryGetValue(to.EntryPoint?.StableId ?? string.Empty, out Transform end))
                    continue;
                var routePoints = new List<Vector3> { start.position };
                for (int waypointIndex = 0; waypointIndex < route.Waypoints.Count; waypointIndex++)
                {
                    string stableId = route.Waypoints[waypointIndex]?.StableId ?? string.Empty;
                    if (!points.TryGetValue(stableId, out Transform waypoint))
                        continue;
                    routePoints.Add(waypoint.position);
                    inputs[
                        "route."
                        + route.StableId
                        + ".waypoint."
                        + waypointIndex.ToString(CultureInfo.InvariantCulture)] = waypoint;
                }
                routePoints.Add(end.position);
                for (int segmentIndex = 0; segmentIndex + 1 < routePoints.Count; segmentIndex++)
                {
                    segments.Add(new RouteSegment(
                        i,
                        route,
                        routePoints[segmentIndex],
                        routePoints[segmentIndex + 1]));
                }
            }
            for (int left = 0; left < segments.Count; left++)
            {
                for (int right = left + 1; right < segments.Count; right++)
                {
                    RouteSegment a = segments[left];
                    RouteSegment b = segments[right];
                    if (a.Index == b.Index
                        || a.SharesZone(b)
                        || !SegmentsIntersect(a.Start, a.End, b.Start, b.End))
                        continue;
                    issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                        "SceneRoutesCross",
                        "scenePlan.routes[" + b.Index + "]",
                        "Route crosses " + a.Route.StableId + " without an authored junction Zone."));
                }
            }
        }

        private static Transform ResolvePrimaryTarget(
            CharacterTestGenesisValidationStation station,
            LevelWorldObjectAuthoringDocument worldObjects,
            Transform root)
        {
            if (station.Bindings.SampleStaticColliders != null
                && station.Bindings.SampleStaticColliders.Count > 0)
                return ResolveStatic(station.Bindings.SampleStaticColliders[0], worldObjects, root);
            Transform targetStatic = ResolveStatic(station.Bindings.TargetStaticCollider, worldObjects, root);
            if (targetStatic != null)
                return targetStatic;
            if (!string.IsNullOrWhiteSpace(station.Bindings.TargetMovableObject?.InstanceStableId))
            {
                for (int i = 0; i < worldObjects.MovableInstances.Count; i++)
                {
                    LevelWorldObjectInstanceDefinition item = worldObjects.MovableInstances[i];
                    if (string.Equals(
                            item.InstanceStableId,
                            station.Bindings.TargetMovableObject.InstanceStableId,
                            StringComparison.Ordinal)
                        && CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(
                            root,
                            item.TransformRef,
                            out Transform transform))
                        return transform;
                }
            }
            return null;
        }

        private static Transform ResolveStatic(
            CharacterTestGenesisValidationStaticColliderRef reference,
            LevelWorldObjectAuthoringDocument worldObjects,
            Transform root)
        {
            if (reference == null)
                return null;
            for (int i = 0; i < worldObjects.SingleStaticColliders.Count; i++)
            {
                LevelWorldObjectInstanceDefinition item = worldObjects.SingleStaticColliders[i];
                if (item.BodyId == reference.BodyId
                    && (item.ColliderId > 0 ? item.ColliderId : 1) == reference.ColliderId
                    && CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(
                        root,
                        item.TransformRef,
                        out Transform transform))
                    return transform;
            }
            return null;
        }

        private static Dictionary<string, Transform> ResolvePoints(
            LevelWorldObjectAuthoringDocument worldObjects,
            Transform root)
        {
            var result = new Dictionary<string, Transform>(StringComparer.Ordinal);
            for (int i = 0; i < worldObjects.Points.Count; i++)
            {
                LevelWorldObjectPointDefinition point = worldObjects.Points[i];
                if (point.Enabled
                    && !string.IsNullOrWhiteSpace(point.StableId)
                    && CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(
                        root,
                        point.TransformRef,
                        out Transform transform))
                    result[point.StableId] = transform;
            }
            return result;
        }

        private static Dictionary<string, Transform> ResolveRegions(
            LevelWorldObjectAuthoringDocument worldObjects,
            Transform root)
        {
            var result = new Dictionary<string, Transform>(StringComparer.Ordinal);
            for (int i = 0; i < worldObjects.Regions.Count; i++)
            {
                LevelWorldObjectRegionDefinition region = worldObjects.Regions[i];
                if (region.Enabled
                    && !string.IsNullOrWhiteSpace(region.StableId)
                    && CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(
                        root,
                        region.TransformRef,
                        out Transform transform))
                    result[region.StableId] = transform;
            }
            return result;
        }

        private static void AddSpatialInput(
            SortedDictionary<string, Transform> output,
            string key,
            string stableId,
            Dictionary<string, Transform> values)
        {
            if (!string.IsNullOrWhiteSpace(stableId)
                && values.TryGetValue(stableId, out Transform transform))
                output[key] = transform;
        }

        private static string ComputeSpatialHash(SortedDictionary<string, Transform> inputs)
        {
            if (inputs.Count == 0)
                return string.Empty;
            var canonical = new StringBuilder();
            foreach (KeyValuePair<string, Transform> pair in inputs)
            {
                Transform transform = pair.Value;
                Vector3 position = transform.position;
                Vector3 rotation = transform.rotation.eulerAngles;
                Vector3 scale = transform.lossyScale;
                canonical.Append(pair.Key).Append('|')
                    .Append(Quantize(position.x, 1000f)).Append(',')
                    .Append(Quantize(position.y, 1000f)).Append(',')
                    .Append(Quantize(position.z, 1000f)).Append('|')
                    .Append(Quantize(rotation.x, 1000f)).Append(',')
                    .Append(Quantize(rotation.y, 1000f)).Append(',')
                    .Append(Quantize(rotation.z, 1000f)).Append('|')
                    .Append(Quantize(scale.x, 1000f)).Append(',')
                    .Append(Quantize(scale.y, 1000f)).Append(',')
                    .Append(Quantize(scale.z, 1000f)).Append('\n');
            }
            using SHA256 sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
            var result = new StringBuilder(16);
            for (int i = 0; i < 8; i++)
                result.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
            return result.ToString();
        }

        private static long Quantize(float value, float scale)
        {
            return (long)Math.Round(value * scale, MidpointRounding.AwayFromZero);
        }

        private static bool ContainsPoint(Transform region, Vector3 point)
        {
            Vector3 local = region.InverseTransformPoint(point);
            return Mathf.Abs(local.x) <= 0.5001f && Mathf.Abs(local.z) <= 0.5001f;
        }

        private static bool ContainsBox(Transform outer, Transform inner)
        {
            Vector3[] corners =
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, 0.5f)
            };
            for (int i = 0; i < corners.Length; i++)
            {
                if (!ContainsPoint(outer, inner.TransformPoint(corners[i])))
                    return false;
            }
            return true;
        }

        private static float HorizontalDistance(Vector3 left, Vector3 right)
        {
            float x = left.x - right.x;
            float z = left.z - right.z;
            return Mathf.Sqrt(x * x + z * z);
        }

        private static CharacterTestGenesisValidationZone FindZone(
            CharacterTestGenesisValidationAuthoringDocument document,
            string stableId)
        {
            for (int i = 0; i < document.ScenePlan.Zones.Count; i++)
            {
                if (string.Equals(document.ScenePlan.Zones[i].StableId, stableId, StringComparison.Ordinal))
                    return document.ScenePlan.Zones[i];
            }
            return null;
        }

        private static bool SegmentsIntersect(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            float o1 = Orientation(a, b, c);
            float o2 = Orientation(a, b, d);
            float o3 = Orientation(c, d, a);
            float o4 = Orientation(c, d, b);
            return o1 * o2 < -0.000001f && o3 * o4 < -0.000001f;
        }

        private static float Orientation(Vector3 a, Vector3 b, Vector3 c)
        {
            return (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x);
        }

        private readonly struct SpatialRole
        {
            public SpatialRole(string name, string stableId)
            {
                Name = name;
                StableId = stableId;
            }

            public string Name { get; }
            public string StableId { get; }
        }

        private readonly struct RouteSegment
        {
            public RouteSegment(
                int index,
                CharacterTestGenesisValidationRoute route,
                Vector3 start,
                Vector3 end)
            {
                Index = index;
                Route = route;
                Start = start;
                End = end;
            }

            public int Index { get; }
            public CharacterTestGenesisValidationRoute Route { get; }
            public Vector3 Start { get; }
            public Vector3 End { get; }

            public bool SharesZone(RouteSegment other)
            {
                return string.Equals(Route.FromZoneId, other.Route.FromZoneId, StringComparison.Ordinal)
                    || string.Equals(Route.FromZoneId, other.Route.ToZoneId, StringComparison.Ordinal)
                    || string.Equals(Route.ToZoneId, other.Route.FromZoneId, StringComparison.Ordinal)
                    || string.Equals(Route.ToZoneId, other.Route.ToZoneId, StringComparison.Ordinal);
            }
        }

        private readonly struct Bounds2D
        {
            private Bounds2D(float minX, float maxX, float minZ, float maxZ)
            {
                MinX = minX;
                MaxX = maxX;
                MinZ = minZ;
                MaxZ = maxZ;
            }

            public float MinX { get; }
            public float MaxX { get; }
            public float MinZ { get; }
            public float MaxZ { get; }

            public static Bounds2D FromTransform(Transform transform)
            {
                Vector3[] corners =
                {
                    transform.TransformPoint(new Vector3(-0.5f, 0f, -0.5f)),
                    transform.TransformPoint(new Vector3(-0.5f, 0f, 0.5f)),
                    transform.TransformPoint(new Vector3(0.5f, 0f, -0.5f)),
                    transform.TransformPoint(new Vector3(0.5f, 0f, 0.5f))
                };
                float minX = corners[0].x;
                float maxX = corners[0].x;
                float minZ = corners[0].z;
                float maxZ = corners[0].z;
                for (int i = 1; i < corners.Length; i++)
                {
                    minX = Mathf.Min(minX, corners[i].x);
                    maxX = Mathf.Max(maxX, corners[i].x);
                    minZ = Mathf.Min(minZ, corners[i].z);
                    maxZ = Mathf.Max(maxZ, corners[i].z);
                }
                return new Bounds2D(minX, maxX, minZ, maxZ);
            }

            public bool Overlaps(Bounds2D other)
            {
                return MinX < other.MaxX
                    && MaxX > other.MinX
                    && MinZ < other.MaxZ
                    && MaxZ > other.MinZ;
            }

            public float DistanceTo(Bounds2D other)
            {
                float x = Mathf.Max(0f, Mathf.Max(other.MinX - MaxX, MinX - other.MaxX));
                float z = Mathf.Max(0f, Mathf.Max(other.MinZ - MaxZ, MinZ - other.MaxZ));
                return Mathf.Sqrt(x * x + z * z);
            }
        }
    }
}
