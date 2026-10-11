using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.Items;
using UnityEngine;
using UnityEngine.InputSystem;
using Fgui = global::FairyGUI;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal enum TacticalPreparationDrawerMode { Collapsed, Characters, Cards }

    /// <summary>Unity view/input adapter for low-frequency battle preparation. Persistent authority stays in the preparation service.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(111)]
    internal sealed class TacticalBattlePreparationController : MonoBehaviour
    {
        private GameManager _manager;
        private TacticalBattlePreparationService _boundPreparation;
        private CharacterTacticalPlayerService _boundPlayer;
        private InventoryOwnerId _boundInventoryOwner;
        private CharacterTacticalRoleBindingQueue _roleBindings;
        private TacticalBattlePreparationService PreparationSource => _boundPreparation ?? CharacterTacticalServices.Preparation;
        private CharacterTacticalPlayerService PlayerSource => _boundPlayer ?? CharacterTacticalServices.Player;

        internal void BindPreparationSources(TacticalBattlePreparationService preparation,
            CharacterTacticalPlayerService player, InventoryOwnerId inventoryOwner = default, CharacterTacticalRoleBindingQueue roleBindings = null)
        {
            if (ReferenceEquals(_boundPreparation, preparation) && ReferenceEquals(_boundPlayer, player)
                && _boundInventoryOwner.Equals(inventoryOwner) && ReferenceEquals(_roleBindings, roleBindings)) return;
            _boundPreparation = preparation; _boundPlayer = player; _boundInventoryOwner = inventoryOwner; _roleBindings = roleBindings;
            _validationRevision = _validationPlayerRevision = _poseRevision = -1;
        }
        private TacticalCardSliceCoordinator _flow;
        private bool _pointerDown;
        private bool _dragging;
        private string _pointerEntityId = string.Empty;
        private Vector2 _pointerStartScreen;
        private Vector3 _dragOrigin;
        private Vector3 _previewPosition;
        private bool _cameraCaptured;
        private Vector3 _cameraPosition;
        private Quaternion _cameraRotation;
        private bool _cameraOrthographic;
        private float _cameraOrthographicSize;
        private float _cameraFieldOfView;
        private float _preparationViewDistance;
        private Vector3 _preparationViewCenter;
        private Vector2 _uiFrameTarget;
        private Vector2 _uiFrameCurrent;
        internal void SetUiOcclusion(float left, float right, float bottom, float top)
            => _uiFrameTarget = new Vector2(Mathf.Clamp(left - right, -0.4f, 0.4f), Mathf.Clamp(bottom - top, -0.4f, 0.4f));
        private TacticalPreparationDrawerMode _lastDrawerMode = TacticalPreparationDrawerMode.Characters;
        private float _drawerCollapseAt = -1f;
        private float _topCollapseAt = -1f;
        private bool _drawerInteraction;
        private bool _popupInteraction;
        private long _poseRevision = -1;
        private long _validationRevision = -1, _validationCatalogRevision = -1, _validationPlayerRevision = -1;
        private string _configurationIssue = string.Empty;
        private TacticalBattlePreparationService _validationOwner;
        private CharacterTacticalPlayerService _validationPlayer;
        internal string ConfigurationIssue
        {
            get
            {
                var preparation = PreparationSource;
                var player = PlayerSource;
                if (preparation == null || player == null) return "整备服务尚未准备就绪。";
                if (!ReferenceEquals(_validationOwner, preparation) || !ReferenceEquals(_validationPlayer, player)
                    || _validationRevision != preparation.Revision || _validationCatalogRevision != player.CatalogRevision
                    || _validationPlayerRevision != player.Revision)
                {
                    _validationOwner = preparation; _validationPlayer = player;
                    _validationRevision = preparation.Revision; _validationCatalogRevision = player.CatalogRevision; _validationPlayerRevision = player.Revision;
                    preparation.ValidateForBattle(out _configurationIssue);
                }
                return _configurationIssue;
            }
        }
        private TacticalBattleSetup _poseSetup;
        private TacticalBattlePreparationService _poseOwner;

        internal string SelectedEntityId { get; private set; } = string.Empty;
        internal TacticalPreparationDrawerMode DrawerMode { get; private set; }
        internal bool TopExpanded { get; private set; }
        internal bool DrawerPinned { get; private set; }
        internal bool TopPinned { get; private set; }
        internal bool TemplateDragActive { get; set; }
        internal string LastIssue { get; private set; } = string.Empty;
        internal event Action Changed;

        private void Awake()
        {
            _manager = GetComponent<GameManager>();
            _flow = GetComponent<TacticalCardSliceCoordinator>();
        }

        private void OnEnable() => CharacterTestFairyGuiCursor.BindScene(this);

        internal string ResolveSceneCursor(Vector2 screen, bool pointerOverUi)
        {
            if (!isActiveAndEnabled || _flow == null || _flow.State != TacticalPreparationState.Preparation
                || PreparationSource == null || _popupInteraction || TemplateDragActive)
                return null;
            if (_pointerDown && _dragging && _pointerEntityId.Length > 0)
                return IsValidPlacement(_previewPosition, _pointerEntityId)
                    ? CharacterTestFairyGuiCursor.Grabbing : CharacterTestFairyGuiCursor.Unavailable;
            if (pointerOverUi || !Application.isFocused || screen.x < 0 || screen.y < 0
                || screen.x >= Screen.width || screen.y >= Screen.height) return null;
            return TryPickSceneEntity(screen, out _, out _)
                ? CharacterTestFairyGuiCursor.Grab : null;
        }

        private void Update()
        {
            bool preparation = _flow != null && _flow.State == TacticalPreparationState.Preparation;
            bool battlefieldOverview = _flow != null && _flow.UsesBattlefieldOverviewPresentation;
            if (!battlefieldOverview || PreparationSource == null)
            {
                RestoreCamera();
                return;
            }
            if (!preparation)
            {
                // The existing MultiTargetAuto camera owns automatic-battle framing. Keep the
                // current preparation pose as its transition origin instead of restoring the
                // scene's third-person camera and fighting the framework camera bridge.
                _cameraCaptured = false;
                return;
            }
            ApplyPreparationCamera(false);
            bool pointerOverUi = PointerOverFairyGui();
            if (!pointerOverUi && !_popupInteraction && !TemplateDragActive) HandleCamera();
            ApplyPreparedEntityPoses();
            UpdateAutoCollapse();
            _manager?.RuntimeContext?.UiRuntime?.FairyGuiCombatHudHost?.Close();
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            if (TemplateDragActive) return;
            if (_popupInteraction) return;
            if (_pointerDown)
            {
                if (mouse.leftButton.isPressed) UpdateScenePointer();
                if (mouse.leftButton.wasReleasedThisFrame) EndScenePointer();
                return;
            }
            if (pointerOverUi) return;
            if (mouse.leftButton.wasPressedThisFrame) BeginScenePointer();
        }

        private void LateUpdate()
        {
            if (_flow != null && _flow.State == TacticalPreparationState.Preparation
                && PreparationSource != null)
                ApplyPreparationCamera();
        }

        internal void ToggleCharacterDrawer()
        {
            DrawerMode = DrawerMode == TacticalPreparationDrawerMode.Characters
                ? TacticalPreparationDrawerMode.Collapsed
                : TacticalPreparationDrawerMode.Characters;
            _drawerCollapseAt = -1f;
            Changed?.Invoke();
        }

        internal void OpenCardDrawer()
        {
            if (SelectedEntityId.Length == 0) return;
            DrawerMode = _lastDrawerMode = TacticalPreparationDrawerMode.Cards;
            _drawerCollapseAt = -1f;
            Changed?.Invoke();
        }

        internal void RevealDrawerFromPointer()
        {
            if (DrawerMode != TacticalPreparationDrawerMode.Collapsed) return;
            DrawerMode = _lastDrawerMode == TacticalPreparationDrawerMode.Cards && SelectedEntityId.Length > 0
                ? TacticalPreparationDrawerMode.Cards : TacticalPreparationDrawerMode.Characters;
            HoldDrawer(true); Changed?.Invoke();
        }

        internal void CollapseDrawer()
        {
            if (DrawerMode != TacticalPreparationDrawerMode.Collapsed) _lastDrawerMode = DrawerMode;
            DrawerMode = TacticalPreparationDrawerMode.Collapsed;
            _drawerCollapseAt = -1f;
            Changed?.Invoke();
        }
        internal void ToggleTop()
        {
            TopExpanded = !TopExpanded;
            _topCollapseAt = -1f;
            Changed?.Invoke();
        }
        internal void HoldDrawer(bool hold)
        {
            _drawerInteraction = hold;
            if (hold || DrawerPinned) _drawerCollapseAt = -1f;
            else if (_drawerCollapseAt < 0f) _drawerCollapseAt = Time.unscaledTime + 0.35f;
        }
        internal void HoldTop(bool hold)
        {
            if (hold || TopPinned) _topCollapseAt = -1f;
            else if (_topCollapseAt < 0f) _topCollapseAt = Time.unscaledTime + 0.35f;
        }
        internal void ToggleDrawerPin() { DrawerPinned = !DrawerPinned; _drawerCollapseAt = -1f; Changed?.Invoke(); }
        internal void ToggleTopPin() { TopPinned = !TopPinned; _topCollapseAt = -1f; Changed?.Invoke(); }
        internal bool CanPlaceTemplate(Vector2 screenPosition) => IsValidPlacement(GroundPoint(screenPosition), string.Empty);
        internal void HoldPopup(bool hold)
        {
            _popupInteraction = hold;
            if (!hold) { HoldDrawer(false); HoldTop(false); }
        }
        internal void Select(string entityId)
        {
            SelectedEntityId = entityId ?? string.Empty;
            if (SelectedEntityId.Length == 0 && DrawerMode == TacticalPreparationDrawerMode.Cards)
                DrawerMode = TacticalPreparationDrawerMode.Collapsed;
            Changed?.Invoke();
        }

        internal bool AddTemplate(int characterId)
        {
            UnityCamera camera = UnityCamera.main;
            Vector3 position = camera != null ? GroundPoint(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)) : Vector3.zero;
            return AddTemplateAtScreen(characterId, position);
        }

        internal bool AddTemplateAtScreen(int characterId, Vector2 screenPosition)
        {
            return AddTemplateAtScreen(characterId, GroundPoint(screenPosition));
        }

        private bool AddTemplateAtScreen(int characterId, Vector3 position)
        {
            TacticalBattlePreparationService service = PreparationSource;
            if (!IsValidPlacement(position, string.Empty))
            {
                LastIssue = "该位置不可放置角色。"; Changed?.Invoke(); return false;
            }
            int team = service.Working.Entities.Count == 0 ? 1 : Math.Max(1, service.Working.Entities.Last().TeamId);
            if (!service.AddEntity(characterId, position, 0f, team, out string id, out string issue))
            { LastIssue = issue; Changed?.Invoke(); return false; }
            SelectedEntityId = id; LastIssue = string.Empty;
            RebuildPreview("已添加角色到战场。"); return true;
        }

        internal bool FocusCharacterItem(ItemInstanceId id, InventoryOwnerId owner)
        {
            var service = PreparationSource;
            if (service == null || !owner.IsValid || !owner.Equals(_boundInventoryOwner)
                || PlayerSource?.TryReadCharacterItemLoadout(id, owner, out _, out _, out _) != true) return false;
            if (!service.TryCaptureWorking(out var setup, out _)) return false;
            var entity = setup.Entities.FirstOrDefault(value => value.CharacterItemInstanceId == id.Value
                && value.InventoryOwnerId == owner.Value);
            if (entity == null) return false;
            LastIssue = string.Empty; Select(entity.EntityId); return true;
        }

        internal bool AddCharacterItemAtScreen(ItemInstanceId id, InventoryOwnerId owner, Vector2 screenPosition,
            long expectedPreparationRevision, long expectedPlayerRevision)
            => AddCharacterItemAtPosition(id, owner, GroundPoint(screenPosition), expectedPreparationRevision, expectedPlayerRevision);

        internal bool AddCharacterItemAtPosition(ItemInstanceId id, InventoryOwnerId owner, Vector3 position,
            long expectedPreparationRevision, long expectedPlayerRevision)
        {
            var service = PreparationSource; var player = PlayerSource;
            if (service == null || player == null || !owner.IsValid || !owner.Equals(_boundInventoryOwner))
            { ReportBattleIssue("库存归属或整备服务尚未配置。"); return false; }
            if (_flow != null && _flow.State != TacticalPreparationState.Preparation)
            { ReportBattleIssue("请先返回整备再选择角色。"); return false; }
            if (service.Revision != expectedPreparationRevision || player.Revision != expectedPlayerRevision)
            { ReportBattleIssue("角色或整备已变化，请重新选择。"); return false; }
            if (FocusCharacterItem(id, owner)) return true;
            if (!service.TryCaptureWorking(out var setup, out string issue))
            { ReportBattleIssue(issue); return false; }
            if (!IsValidPlacement(position, string.Empty))
            { ReportBattleIssue("该位置不可放置角色。"); return false; }
            int team = setup.Entities.Count == 0 ? 1 : Math.Max(1, setup.Entities.Last().TeamId);
            if (!service.AddCharacterItemEntity(id, owner, position, 0f, team, expectedPreparationRevision, out string entity, out issue))
            { ReportBattleIssue(issue); return false; }
            SelectedEntityId = entity; LastIssue = string.Empty;
            RebuildPreview("已选择角色到战场。"); return true;
        }

        internal bool SetSelectedTeam(int teamId)
        {
            TacticalBattleSetup setup = PreparationSource.Working;
            int index = FindEntityIndex(SelectedEntityId);
            TacticalBattleEntitySetup configured = index < 0 ? null : setup.Entities[index];
            var entities = _manager?.RuntimeContext?.Slice?.Services.CharacterEntities;
            CharacterTestCharacterEntity runtime = ResolveRuntimeEntity(configured, index, entities);
            if (!PreparationSource.SetTeam(SelectedEntityId, teamId, out string issue))
            { LastIssue = issue; Changed?.Invoke(); return false; }
            if (runtime != null)
                runtime.SetTeam(new CharacterTestCharacterTeamComponent(teamId, runtime.Team.DefaultTeamId));
            LastIssue = string.Empty;
            Changed?.Invoke();
            return true;
        }

        internal bool SetSelectedPose(Vector3 position, float yaw)
        {
            if (!PreparationSource.SetPose(SelectedEntityId, position, yaw, out string issue))
            { LastIssue = issue; Changed?.Invoke(); return false; }
            LastIssue = string.Empty;
            ApplyPreparedEntityPoses();
            Changed?.Invoke();
            return true;
        }

        internal bool RotateSelected(float degrees)
        {
            TacticalBattleEntitySetup selected = PreparationSource.Find(SelectedEntityId);
            return selected != null && SetSelectedPose(selected.Position, selected.YawDegrees + degrees);
        }

        internal bool StartBattle()
        {
            string issue = "整备服务尚未准备就绪。";
            bool result = _flow != null && _flow.TryDeployPreparedBattle(out issue);
            ReportBattleIssue(issue); return result;
        }

        internal void ReportBattleIssue(string issue)
        { LastIssue = issue ?? string.Empty; Changed?.Invoke(); }

        internal bool CanEditRoleBindings => _roleBindings != null
            && _roleBindings.Matches(PlayerSource, PreparationSource, _boundInventoryOwner);
        internal bool QueueRoleBinding(string entityId, ItemInstanceId item, InventoryOwnerId owner, ulong card,
            string slot, string action, long roleRevision, long playerRevision, long preparationRevision, long catalogRevision,
            Action<bool, string> completion, out string issue)
        {
            var player = PlayerSource; var preparation = PreparationSource; var queue = _roleBindings;
            if (!CanEditRoleBindings || SelectedEntityId != entityId || !_boundInventoryOwner.Equals(owner)
                || _flow != null && _flow.State != TacticalPreparationState.Preparation)
            { issue = "实例装配入口不可用或角色选择已变化。"; return false; }
            return queue.Enqueue(entityId, item, owner, card, slot, action, roleRevision, playerRevision,
                preparationRevision, catalogRevision,
                () => this != null && ReferenceEquals(_roleBindings, queue) && ReferenceEquals(PlayerSource, player)
                    && ReferenceEquals(PreparationSource, preparation) && _boundInventoryOwner.Equals(owner)
                    && (_flow == null || _flow.State == TacticalPreparationState.Preparation), completion, out issue);
        }
        internal bool CanStartBattle => _flow != null && _flow.State == TacticalPreparationState.Preparation && (_roleBindings?.PendingCount ?? 0) == 0;

        internal bool CanRefreshPublishedCatalog => _flow != null && _flow.State == TacticalPreparationState.Preparation;

        internal bool RefreshPublishedCatalog()
        {
            string message = string.Empty;
            bool success = _flow != null && CharacterTacticalServices.RefreshPublishedCatalog(_flow.State, out message);
            if (_flow == null) message = "整备场景尚未就绪。";
            LastIssue = message ?? string.Empty;
            Changed?.Invoke();
            return success;
        }

        internal void ClearIssue()
        {
            if (LastIssue.Length == 0) return;
            LastIssue = string.Empty;
            Changed?.Invoke();
        }

        internal static float ScreenBodyPickDistance(UnityCamera camera, Vector3 bottom, Vector3 top,
            float radius, Vector2 pointer)
        {
            Vector3 a = camera.WorldToScreenPoint(bottom), b = camera.WorldToScreenPoint(top);
            if (a.z <= camera.nearClipPlane || b.z <= camera.nearClipPlane) return float.PositiveInfinity;
            Vector2 start = new Vector2(a.x, a.y), end = new Vector2(b.x, b.y);
            Vector2 axis = end - start;
            float t = axis.sqrMagnitude > .01f ? Mathf.Clamp01(Vector2.Dot(pointer-start, axis) / axis.sqrMagnitude) : 0f;
            Vector3 center = Vector3.Lerp(bottom, top, t);
            var side = camera.WorldToScreenPoint(center + camera.transform.right * Mathf.Max(.1f, radius));
            var projected = camera.WorldToScreenPoint(center);
            float pixels = Mathf.Max(12f * Screen.height / 1080f, Mathf.Abs(side.x-projected.x)) + 6f;
            return (pointer - (start + axis*t)).sqrMagnitude / (pixels*pixels);
        }

        private bool TryPickSceneEntity(Vector2 screen, out string selected, out Vector3 selectedPosition)
        {
            selected = string.Empty;
            selectedPosition = default;
            TacticalBattleSetup setup = GetPoseSetup();
            var entities = _manager?.RuntimeContext?.Slice?.Services.CharacterEntities;
            if (setup == null || entities == null) return false;
            float best = 1f;
            UnityCamera camera = UnityCamera.main;
            for (int i = 0; i < setup.Entities.Count; i++)
            {
                TacticalBattleEntitySetup candidate = setup.Entities[i];
                CharacterTestCharacterEntity runtime = ResolveRuntimeEntity(candidate, i, entities);
                if (runtime == null || camera == null) continue;
                var position = new Vector3(runtime.Transform.X, runtime.Transform.Y, runtime.Transform.Z);
                var physics = runtime.Physics;
                float halfHeight = physics.EffectiveHalfHeightMeters;
                var center = position + Vector3.up * physics.EffectiveCenterY;
                float distance = ScreenBodyPickDistance(camera, center - Vector3.up * halfHeight,
                    center + Vector3.up * halfHeight, physics.EffectiveRadiusMeters, screen);
                if (distance >= best) continue;
                best = distance; selected = candidate.EntityId;
                selectedPosition = new Vector3(runtime.Transform.X, runtime.Transform.Y, runtime.Transform.Z);
            }
            return selected.Length > 0;
        }

        private void BeginScenePointer()
        {
            Vector2 screen = Mouse.current.position.ReadValue();
            TryPickSceneEntity(screen, out string selected, out Vector3 selectedPosition);
            _pointerDown = true;
            _dragging = false;
            _pointerEntityId = selected;
            _pointerStartScreen = screen;
            _dragOrigin = selectedPosition;
            _previewPosition = selectedPosition;
        }

        private void UpdateScenePointer()
        {
            if (_pointerEntityId.Length == 0) return;
            if (!_dragging)
            {
                Vector2 delta = Mouse.current.position.ReadValue() - _pointerStartScreen;
                if (delta.sqrMagnitude < 36f) return;
                _dragging = true;
            }
            UpdateDragPreview();
        }

        private void EndScenePointer()
        {
            string entityId = _pointerEntityId;
            bool wasDragging = _dragging;
            _pointerDown = false;
            _dragging = false;
            _pointerEntityId = string.Empty;
            if (wasDragging) CommitDrag(entityId);
            else Select(entityId);
        }

        private void UpdateDragPreview()
        {
            _previewPosition = GroundPoint(Mouse.current.position.ReadValue());
            int index = FindEntityIndex(_pointerEntityId);
            TacticalBattleEntitySetup configured = index < 0 ? null : PreparationSource.Working.Entities[index];
            var runtime = configured == null ? null : ResolveRuntimeEntity(configured, index,
                _manager?.RuntimeContext?.Slice?.Services.CharacterEntities);
            if (runtime != null)
                _manager?.RuntimeContext?.Slice?.Services.CharacterRuntime?.ViewBridge
                    ?.TryRestoreRuntimeAiLabViewPose(runtime.Id, _previewPosition, Quaternion.Euler(0f,
                        PreparationSource.Find(_pointerEntityId)?.YawDegrees ?? 0f, 0f));
        }

        private void CommitDrag(string entityId)
        {
            TacticalBattleEntitySetup selected = PreparationSource.Find(entityId);
            if (selected == null) return;
            if (!IsValidPlacement(_previewPosition, entityId)) _previewPosition = _dragOrigin;
            SetEntityPose(entityId, _previewPosition, selected.YawDegrees);
        }

        private bool IsValidPlacement(Vector3 position, string ignoredEntityId)
        {
            if (!TacticalBattlefieldLayout.ContainsPlacement(position)) return false;
            TacticalBattleSetup setup = GetPoseSetup();
            if (setup == null) return false;
            foreach (TacticalBattleEntitySetup entity in setup.Entities)
                if (entity.EntityId != ignoredEntityId && Vector2.Distance(new Vector2(position.x, position.z), new Vector2(entity.X, entity.Z)) < 0.8f)
                    return false;
            return true;
        }

        internal void RemoveSelected()
        {
            if (!PreparationSource.RemoveEntity(SelectedEntityId, out string issue))
            { LastIssue = issue; Changed?.Invoke(); return; }
            SelectedEntityId = string.Empty; LastIssue = string.Empty;
            RebuildPreview("已移除场景实体。");
        }

        private int FindEntityIndex(string entityId)
        {
            TacticalBattleSetup setup = PreparationSource.Working;
            for (int i = 0; i < setup.Entities.Count; i++) if (setup.Entities[i].EntityId == entityId) return i;
            return -1;
        }

        private bool SetEntityPose(string entityId, Vector3 position, float yaw)
        {
            if (!PreparationSource.SetPose(entityId, position, yaw, out string issue))
            { LastIssue = issue; Changed?.Invoke(); return false; }
            LastIssue = string.Empty;
            ApplyPreparedEntityPoses();
            Changed?.Invoke();
            return true;
        }

        internal void RefreshLoadedPreset()
        {
            Select(string.Empty);
            _poseRevision = -1;
            RebuildPreview("已加载战术整备预设。");
        }

        private void RebuildPreview(string message)
        {
            Changed?.Invoke();
            _manager?.RuntimeContext?.SessionBuilder?.RebuildTacticalPreparationSession(message);
        }

        private TacticalBattleSetup GetPoseSetup()
        {
            TacticalBattlePreparationService preparation = PreparationSource;
            if (preparation == null) return null;
            if (!ReferenceEquals(_poseOwner, preparation) || _poseRevision != preparation.Revision)
            {
                _poseOwner = preparation;
                _poseRevision = preparation.Revision;
                _poseSetup = preparation.TryCaptureWorking(out var setup, out _) ? setup : null;
            }
            return _poseSetup;
        }

        private void ApplyPreparedEntityPoses()
        {
            TacticalBattleSetup setup = GetPoseSetup();
            var entities = _manager?.RuntimeContext?.Slice?.Services.CharacterEntities;
            if (setup?.Entities == null || entities == null) return;
            for (int i = 0; i < setup.Entities.Count; i++)
            {
                TacticalBattleEntitySetup configured = setup.Entities[i];
                CharacterTestCharacterEntity runtime = ResolveRuntimeEntity(configured, i, entities);
                if (runtime == null) continue;
                if (Mathf.Abs(runtime.Transform.X - configured.X) < 0.001f
                    && Mathf.Abs(runtime.Transform.Z - configured.Z) < 0.001f
                    && Mathf.Abs(Mathf.DeltaAngle(runtime.Transform.YawDegrees, configured.YawDegrees)) < 0.01f)
                    continue;
                runtime.SetTransform(new CharacterTestCharacterTransformComponent(
                    configured.X, runtime.Transform.Y, configured.Z, configured.YawDegrees));
                _manager.RuntimeContext.Slice.Services.CharacterRuntime.ViewBridge.TryRestoreRuntimeAiLabViewPose(
                    runtime.Id, new Vector3(configured.X, runtime.Transform.Y, configured.Z),
                    Quaternion.Euler(0f, configured.YawDegrees, 0f));
            }
        }

        private static CharacterTestCharacterEntity ResolveRuntimeEntity(TacticalBattleEntitySetup configured, int setupIndex,
            IReadOnlyList<CharacterTestCharacterEntity> entities)
        {
            if (configured == null || entities == null) return null;
            for (int i = 0; i < entities.Count; i++)
                if (entities[i].Control.RuntimeAiPlannerProfileSlot == setupIndex + 1)
                    return entities[i];
            CharacterTestCharacterEntity nearest = null;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity candidate = entities[i];
                if (candidate.Identity.CharacterId != configured.CharacterId || candidate.Team.TeamId != configured.TeamId)
                    continue;
                float dx = candidate.Transform.X - configured.X;
                float dz = candidate.Transform.Z - configured.Z;
                float distance = dx * dx + dz * dz;
                if (distance >= nearestDistance) continue;
                nearestDistance = distance; nearest = candidate;
            }
            return nearest;
        }

        private void UpdateAutoCollapse()
        {
            if (_popupInteraction || TemplateDragActive) return;
            float now = Time.unscaledTime;
            if (!_drawerInteraction && _drawerCollapseAt >= 0f && now >= _drawerCollapseAt)
            {
                _drawerCollapseAt = -1f;
                if (DrawerMode != TacticalPreparationDrawerMode.Collapsed) CollapseDrawer();
            }
            if (_topCollapseAt >= 0f && now >= _topCollapseAt)
            {
                _topCollapseAt = -1f;
                if (TopExpanded) ToggleTop();
            }
        }

        private static Vector3 GroundPoint(Vector2 screen)
        {
            UnityCamera camera = UnityCamera.main;
            if (camera == null) return Vector3.zero;
            Ray ray = camera.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, Vector3.zero);
            return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : Vector3.zero;
        }

        private static bool PointerOverFairyGui()
        {
            if (Fgui.Stage.inst == null) return false;
            Fgui.DisplayObject hit = Fgui.Stage.inst.HitTest(Fgui.Stage.inst.touchPosition, true);
            return hit != null && hit != Fgui.Stage.inst;
        }

        private void HandleCamera()
        {
            UnityCamera camera = UnityCamera.main;
            if (camera == null) return;
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            float wheel = mouse.scroll.ReadValue().y / 120f;
            if (!Mathf.Approximately(wheel, 0f))
            {
                _preparationViewDistance = Mathf.Clamp(_preparationViewDistance * Mathf.Exp(-wheel * .12f), 7f, 120f);
            }
            if (mouse.middleButton.isPressed)
            {
                float worldPerPixel = 2f * _preparationViewDistance * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f) / Mathf.Max(1, Screen.height);
                Vector2 delta = mouse.delta.ReadValue() * worldPerPixel;
                Vector3 movement = camera.transform.right * (-delta.x)
                    + Vector3.Cross(camera.transform.right, Vector3.up).normalized * (-delta.y);
                _preparationViewCenter.x = Mathf.Clamp(_preparationViewCenter.x + movement.x,
                    -TacticalBattlefieldLayout.HalfWidth, TacticalBattlefieldLayout.HalfWidth);
                _preparationViewCenter.z = Mathf.Clamp(_preparationViewCenter.z + movement.z,
                    -TacticalBattlefieldLayout.HalfDepth, TacticalBattlefieldLayout.HalfDepth);
                camera.transform.position = _preparationViewCenter - camera.transform.forward * _preparationViewDistance;
            }
        }

        // Frame the local deployment rather than shrinking every character to fit the arena.
        internal static Vector3 ResolvePreparationFocus(TacticalBattleSetup setup)
        {
            if (setup?.Entities == null || setup.Entities.Count == 0) return Vector3.up;
            var anchor = setup.Entities.Find(entity => entity.EntityId == setup.PlayerControlledEntityId) ?? setup.Entities[0];
            Vector3 center = Vector3.zero; int count = 0;
            foreach (var entity in setup.Entities)
            {
                float x = entity.X - anchor.X, z = entity.Z - anchor.Z;
                if (x*x + z*z > 100f) continue;
                center += new Vector3(entity.X, entity.Y + 1f, entity.Z); count++;
            }
            return center / Mathf.Max(1, count);
        }

        private void ApplyPreparationCamera(bool animate = true)
        {
            UnityCamera camera = UnityCamera.main;
            if (camera == null) return;
            if (!_cameraCaptured)
            {
                _cameraCaptured = true;
                _cameraPosition = camera.transform.position;
                _cameraRotation = camera.transform.rotation;
                _cameraOrthographic = camera.orthographic;
                _cameraOrthographicSize = camera.orthographicSize;
                _cameraFieldOfView = camera.fieldOfView;
                camera.fieldOfView = 45f;
                _preparationViewCenter = ResolvePreparationFocus(PreparationSource?.Working);
                _preparationViewDistance = 14f;
                _uiFrameCurrent = _uiFrameTarget;
            }
            camera.orthographic = false;
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, 300f);
            camera.fieldOfView = 45f;
            Quaternion preparationRotation = Quaternion.Euler(35f, 0f, 0f);
            if (Quaternion.Angle(camera.transform.rotation, preparationRotation) > 0.1f)
            {
                camera.transform.rotation = preparationRotation;
            }
            if (animate && !TemplateDragActive && !_pointerDown)
                _uiFrameCurrent = Vector2.Lerp(_uiFrameCurrent, _uiFrameTarget, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 9f));
            float halfView = _preparationViewDistance * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            camera.transform.position = _preparationViewCenter - preparationRotation * Vector3.forward * _preparationViewDistance
                - camera.transform.right * (_uiFrameCurrent.x * halfView * camera.aspect)
                - camera.transform.up * (_uiFrameCurrent.y * halfView);
        }

        private void RestoreCamera()
        {
            if (!_cameraCaptured) return;
            UnityCamera camera = UnityCamera.main;
            if (camera != null)
            {
                camera.transform.SetPositionAndRotation(_cameraPosition, _cameraRotation);
                camera.orthographic = _cameraOrthographic;
                camera.orthographicSize = _cameraOrthographicSize;
                camera.fieldOfView = _cameraFieldOfView;
            }
            _cameraCaptured = false;
        }

        private void OnDisable()
        {
            CharacterTestFairyGuiCursor.UnbindScene(this);
            RestoreCamera();
        }
    }
}
