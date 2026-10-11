using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(95)]
    [AddComponentMenu("MxFramework/Game/Character Test/Runtime AI Lab Setup Overlay")]
    internal sealed class CharacterTestRuntimeAiLabSetupOverlay : MonoBehaviour
    {
        private const float PreviewY = 0.95f;
        private const float RingY = 0.035f;
        private const float RingScale = 1.45f;
        private const float PreviewGroundPlaneY = 0f;
        private const float PreviewScreenPickPadding = 18f;
        private const float PreviewScreenPickMinRadius = 34f;
        private const float PreviewScreenPickMaxRadius = 90f;
        private const float DragStartScreenThreshold = 6f;

        private static readonly Color Team1Color = new Color(0.15f, 0.45f, 1f, 0.72f);
        private static readonly Color Team2Color = new Color(1f, 0.18f, 0.15f, 0.72f);
        private static readonly Color WarningColor = new Color(1f, 0.74f, 0.12f, 0.78f);
        private static readonly Color ErrorColor = new Color(1f, 0.12f, 0.08f, 0.86f);
        private static readonly Color DisabledColor = new Color(0.35f, 0.35f, 0.35f, 0.35f);

        private readonly SetupSlotView[] _slotViews =
            new SetupSlotView[CharacterTestArenaMatchConfig.SlotsPerTeam * 2];

        private CharacterTestRuntimeAiPlannerDevSceneController _controller;
        private Transform _root;
        private Material _team1Material;
        private Material _team2Material;
        private Material _warningMaterial;
        private Material _errorMaterial;
        private Material _disabledMaterial;
        private SetupSlotView _draggingView;
        private SetupSlotView _pendingDragView;
        private SetupSlotView _hoverView;
        private LineRenderer _distanceLine;
        private TextMesh _distanceLabel;
        private float _lastDistance = -1f;
        private Vector3 _pendingDragMousePosition;
        private Vector2 _dragOffset;

        public bool IsDraggingSlot => _draggingView != null || _pendingDragView != null;

        private void Awake()
        {
            _controller = GetComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
            CreateMaterials();
        }

        private void OnDestroy()
        {
            DestroyMaterial(_team1Material);
            DestroyMaterial(_team2Material);
            DestroyMaterial(_warningMaterial);
            DestroyMaterial(_errorMaterial);
            DestroyMaterial(_disabledMaterial);
        }

        private void Update()
        {
            if (!IsSetupActive() || CharacterTestRuntimeAiLabBehaviorCardPreviewHost.ActiveHost?.HasSetupCharacters == true)
            {
                SetRootVisible(false);
                _draggingView = null;
                _pendingDragView = null;
                _hoverView = null;
                _pendingDragMousePosition = default;
                _dragOffset = default;
                return;
            }

            EnsureViews();
            SetRootVisible(true);
            RefreshViews();
            HandleInput();
        }

        private bool IsSetupActive()
        {
            if (_controller == null)
                _controller = GetComponent<CharacterTestRuntimeAiPlannerDevSceneController>();

            return _controller != null
                && _controller.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Setup;
        }

        private void EnsureViews()
        {
            if (_root == null)
            {
                var root = new GameObject("Runtime AI Lab Setup Preview Slots");
                root.transform.SetParent(transform, worldPositionStays: false);
                _root = root.transform;
            }

            int index = 0;
            for (int i = 0; i < CharacterTestArenaMatchConfig.SlotsPerTeam; i++)
                EnsureView(ref index, CharacterTestArenaMatchConfig.Team1Id, i);
            for (int i = 0; i < CharacterTestArenaMatchConfig.SlotsPerTeam; i++)
                EnsureView(ref index, CharacterTestArenaMatchConfig.Team2Id, i);
            EnsureDistanceIndicator();
        }

        private void EnsureView(ref int index, int teamId, int slotIndex)
        {
            if (_slotViews[index] == null)
                _slotViews[index] = SetupSlotView.Create(_root, teamId, slotIndex);
            index++;
        }

        private void RefreshViews()
        {
            CharacterTestRuntimeAiLabSpawnValidationResult validation =
                _controller.ValidateSetupSpawns();
            for (int i = 0; i < _slotViews.Length; i++)
            {
                SetupSlotView view = _slotViews[i];
                if (view == null)
                    continue;

                if (!_controller.MatchConfig.TryGetSpawnPosition(
                        view.TeamId,
                        view.SlotIndex,
                        out Vector2 position))
                {
                    view.Root.SetActive(false);
                    continue;
                }

                CharacterTestArenaSlotConfig slot =
                    _controller.MatchConfig.TryGetSlot(view.TeamId, view.SlotIndex);
                bool enabled = slot != null && slot.Enabled;
                validation.TryGetSlot(
                    view.TeamId,
                    view.SlotIndex,
                    out CharacterTestRuntimeAiLabSpawnSlotValidation slotValidation);

                view.SetPresentation(position, enabled, slotValidation);
                view.BodyRenderer.sharedMaterial = ResolveBodyMaterial(view.TeamId, slotValidation);
                view.RingRenderer.sharedMaterial = ResolveRingMaterial(view.TeamId, slotValidation);
                view.FacingRenderer.sharedMaterial = ResolveRingMaterial(view.TeamId, slotValidation);
                bool selected = _controller.SelectedSetupTeamId == view.TeamId
                    && _controller.SelectedSetupSlotIndex == view.SlotIndex;
                view.SetHighlighted(selected || view == _hoverView || view == _draggingView);
            }
            RefreshDistanceIndicator();
        }

        private void EnsureDistanceIndicator()
        {
            if (_distanceLine != null)
                return;

            var lineObject = new GameObject("RuntimeAiLab_SetupSpawnDistance");
            lineObject.transform.SetParent(_root, worldPositionStays: false);
            _distanceLine = lineObject.AddComponent<LineRenderer>();
            _distanceLine.useWorldSpace = true;
            _distanceLine.positionCount = 2;
            _distanceLine.startWidth = 0.045f;
            _distanceLine.endWidth = 0.045f;
            _distanceLine.sharedMaterial = _team1Material;
            _distanceLine.startColor = new Color(0.55f, 0.78f, 1f, 0.8f);
            _distanceLine.endColor = new Color(1f, 0.55f, 0.52f, 0.8f);

            var labelObject = new GameObject("DistanceLabel");
            labelObject.transform.SetParent(lineObject.transform, worldPositionStays: false);
            _distanceLabel = labelObject.AddComponent<TextMesh>();
            _distanceLabel.anchor = TextAnchor.MiddleCenter;
            _distanceLabel.alignment = TextAlignment.Center;
            _distanceLabel.characterSize = 0.14f;
            _distanceLabel.fontSize = 34;
            _distanceLabel.color = new Color(0.9f, 0.95f, 1f, 0.92f);
        }

        private void RefreshDistanceIndicator()
        {
            SetupSlotView team1 = FindFirstActiveSlot(CharacterTestArenaMatchConfig.Team1Id);
            SetupSlotView team2 = FindFirstActiveSlot(CharacterTestArenaMatchConfig.Team2Id);
            bool visible = team1 != null && team2 != null;
            if (_distanceLine != null)
                _distanceLine.gameObject.SetActive(visible);
            if (!visible)
                return;

            Vector3 start = team1.Root.transform.position + Vector3.up * 0.12f;
            Vector3 end = team2.Root.transform.position + Vector3.up * 0.12f;
            _distanceLine.SetPosition(0, start);
            _distanceLine.SetPosition(1, end);
            _distanceLabel.transform.position = (start + end) * 0.5f + Vector3.up * 0.25f;
            float distance = Vector3.Distance(start, end);
            if (Mathf.Abs(_lastDistance - distance) > 0.01f)
            {
                _lastDistance = distance;
                string distanceText = distance.ToString("0.#") + "m";
                _distanceLabel.text = distanceText;
            }
        }

        private SetupSlotView FindFirstActiveSlot(int teamId)
        {
            for (int i = 0; i < _slotViews.Length; i++)
            {
                SetupSlotView view = _slotViews[i];
                if (view != null && view.TeamId == teamId && view.Root.activeSelf)
                    return view;
            }

            return null;
        }

        private void HandleInput()
        {
            if (_draggingView != null)
            {
                if (CharacterTestRuntimeAiLabInput.IsMouseButtonPressed(0))
                    DragSelectedSlot();

                if (CharacterTestRuntimeAiLabInput.IsMouseButtonUp(0))
                {
                    _draggingView = null;
                    _dragOffset = default;
                }
                return;
            }

            if (_pendingDragView != null)
            {
                if (!CharacterTestRuntimeAiLabInput.IsMouseButtonPressed(0))
                {
                    _pendingDragView = null;
                    _pendingDragMousePosition = default;
                    _dragOffset = default;
                    return;
                }

                Vector3 mousePosition = CharacterTestRuntimeAiLabInput.MousePosition;
                Vector2 delta = new Vector2(
                    mousePosition.x - _pendingDragMousePosition.x,
                    mousePosition.y - _pendingDragMousePosition.y);
                if (delta.sqrMagnitude >= DragStartScreenThreshold * DragStartScreenThreshold)
                {
                    if (TryProjectPointerToGroundPlane(out Vector2 groundPosition))
                    {
                        _draggingView = _pendingDragView;
                        _dragOffset = GetSlotPosition(_draggingView) - groundPosition;
                        _pendingDragView = null;
                        _pendingDragMousePosition = default;
                        DragSelectedSlot();
                        return;
                    }

                    _controller.SetSetupInteractionStatus("Drag target missed the setup ground plane.");
                    _pendingDragView = null;
                    _pendingDragMousePosition = default;
                    _dragOffset = default;
                    return;
                }

                return;
            }

            bool pointerBlocked = CharacterTestRuntimeAiLabInput.IsPointerBlockedByUi();
            if (pointerBlocked)
            {
                _hoverView = null;
                if (CharacterTestRuntimeAiLabInput.IsMouseButtonDown(0))
                    _controller.SetSetupInteractionStatus("Setup placement ignored: pointer is over a Runtime AI Lab UI control.");
                return;
            }

            TryPickSlotByScreenPosition(out _hoverView, out _);

            if (CharacterTestRuntimeAiLabInput.IsMouseButtonDown(0))
                BeginPointerInteraction();
        }

        private void BeginPointerInteraction()
        {
            if (TryPickSlotByScreenPosition(out SetupSlotView view, out _))
            {
                _pendingDragView = view;
                _pendingDragMousePosition = CharacterTestRuntimeAiLabInput.MousePosition;
                _controller.SelectSetupSpawnSlot(view.TeamId, view.SlotIndex);
                return;
            }

            if (TryProjectPointerToGroundPlane(out _))
            {
                _controller.SetSetupInteractionStatus(
                    "Spawn selection retained. Drag a character preview to move it.");
                return;
            }

            _controller.SetSetupInteractionStatus(
                "Setup placement missed. The current spawn selection was retained.");
        }

        private bool TryPickSlotByScreenPosition(
            out SetupSlotView view,
            out Vector2 groundPosition)
        {
            view = null;
            groundPosition = default;
            if (!CharacterTestRuntimeAiLabCameraResolver.TryGetSceneCamera(out UnityEngine.Camera camera))
            {
                _controller.SetSetupInteractionStatus("Setup placement failed: no active scene camera found.");
                return false;
            }

            Vector3 mousePosition = CharacterTestRuntimeAiLabInput.MousePosition;
            var pointer = new Vector2(mousePosition.x, mousePosition.y);
            float bestScore = float.MaxValue;
            for (int i = 0; i < _slotViews.Length; i++)
            {
                SetupSlotView candidate = _slotViews[i];
                if (candidate == null || !candidate.Root.activeInHierarchy)
                    continue;

                Vector3 projected = camera.WorldToScreenPoint(candidate.ScreenPickCenter);
                if (projected.z <= 0f)
                    continue;

                float radius = CalculateScreenPickRadius(camera, candidate.ScreenPickCenter);
                float dx = Mathf.Abs(pointer.x - projected.x) / radius;
                float dy = Mathf.Abs(pointer.y - projected.y) / (radius * 1.45f);
                float score = dx * dx + dy * dy;
                if (score > 1f || score >= bestScore)
                    continue;

                bestScore = score;
                view = candidate;
            }

            if (view == null)
                return false;

            if (!TryProjectPointerToGroundPlane(out groundPosition))
                groundPosition = GetSlotPosition(view);

            return true;
        }

        private void DragSelectedSlot()
        {
            if (_draggingView == null)
                return;

            if (TryProjectPointerToGroundPlane(out Vector2 best))
            {
                _controller.SetSetupSpawnPosition(
                    _draggingView.TeamId,
                    _draggingView.SlotIndex,
                    best + _dragOffset);
                return;
            }

            _controller.SetSetupInteractionStatus("Drag target missed the setup ground plane.");
        }

        private bool TryProjectPointerToGroundPlane(out Vector2 position)
        {
            position = default;
            if (!CharacterTestRuntimeAiLabCameraResolver.TryGetSceneCamera(out UnityEngine.Camera camera))
            {
                _controller.SetSetupInteractionStatus("Setup placement failed: no active scene camera found.");
                return false;
            }

            Ray ray = camera.ScreenPointToRay(CharacterTestRuntimeAiLabInput.MousePosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, PreviewGroundPlaneY, 0f));
            if (!plane.Raycast(ray, out float distance))
                return false;

            Vector3 point = ray.GetPoint(distance);
            position = new Vector2(point.x, point.z);
            return true;
        }

        private static float CalculateScreenPickRadius(UnityEngine.Camera camera, Vector3 center)
        {
            Vector3 rightPoint = center + camera.transform.right * 0.9f;
            Vector3 centerScreen = camera.WorldToScreenPoint(center);
            Vector3 rightScreen = camera.WorldToScreenPoint(rightPoint);
            float radius = Vector2.Distance(
                new Vector2(centerScreen.x, centerScreen.y),
                new Vector2(rightScreen.x, rightScreen.y)) + PreviewScreenPickPadding;
            return Mathf.Clamp(radius, PreviewScreenPickMinRadius, PreviewScreenPickMaxRadius);
        }

        private static Vector2 GetSlotPosition(SetupSlotView view)
        {
            Vector3 position = view.Root.transform.position;
            return new Vector2(position.x, position.z);
        }

        private Material ResolveBodyMaterial(
            int teamId,
            CharacterTestRuntimeAiLabSpawnSlotValidation validation)
        {
            if (validation.Severity == CharacterTestRuntimeAiLabSpawnSeverity.Error)
                return _errorMaterial;
            if (validation.Severity == CharacterTestRuntimeAiLabSpawnSeverity.Warning)
                return _warningMaterial;
            if (!validation.Enabled)
                return _disabledMaterial;
            return teamId == CharacterTestArenaMatchConfig.Team2Id ? _team2Material : _team1Material;
        }

        private Material ResolveRingMaterial(
            int teamId,
            CharacterTestRuntimeAiLabSpawnSlotValidation validation)
        {
            return ResolveBodyMaterial(teamId, validation);
        }

        private void SetRootVisible(bool visible)
        {
            if (_root != null && _root.gameObject.activeSelf != visible)
                _root.gameObject.SetActive(visible);
        }

        private void CreateMaterials()
        {
            _team1Material = CreateMaterial("Runtime AI Lab Team 1 Preview", Team1Color);
            _team2Material = CreateMaterial("Runtime AI Lab Team 2 Preview", Team2Color);
            _warningMaterial = CreateMaterial("Runtime AI Lab Warning Preview", WarningColor);
            _errorMaterial = CreateMaterial("Runtime AI Lab Error Preview", ErrorColor);
            _disabledMaterial = CreateMaterial("Runtime AI Lab Disabled Preview", DisabledColor);
        }

        private static Material CreateMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            var material = new Material(shader)
            {
                name = name,
                color = color
            };
            return material;
        }

        private static void DestroyMaterial(Material material)
        {
            if (material != null)
                Destroy(material);
        }

        private static string FormatLabel(
            int teamId,
            int slotIndex,
            CharacterTestRuntimeAiLabSpawnSlotValidation validation)
        {
            string team = teamId == CharacterTestArenaMatchConfig.Team2Id ? "T2·" : "T1·";
            return team + (slotIndex + 1).ToString();
        }

        private sealed class SetupSlotView
        {
            private bool _hasPresentation;
            private bool _lastEnabled;
            private CharacterTestRuntimeAiLabSpawnSeverity _lastSeverity;
            private string _lastMessage = string.Empty;

            private SetupSlotView(
                GameObject root,
                int teamId,
                int slotIndex,
                MeshRenderer bodyRenderer,
                MeshRenderer ringRenderer,
                MeshRenderer facingRenderer,
                TextMesh label)
            {
                Root = root;
                TeamId = teamId;
                SlotIndex = slotIndex;
                BodyRenderer = bodyRenderer;
                RingRenderer = ringRenderer;
                FacingRenderer = facingRenderer;
                Label = label;
            }

            public GameObject Root { get; }
            public int TeamId { get; }
            public int SlotIndex { get; }
            public MeshRenderer BodyRenderer { get; }
            public MeshRenderer RingRenderer { get; }
            public MeshRenderer FacingRenderer { get; }
            public TextMesh Label { get; }
            public Vector3 ScreenPickCenter => Root.transform.position + new Vector3(0f, PreviewY, 0f);

            public static SetupSlotView Create(Transform parent, int teamId, int slotIndex)
            {
                var root = new GameObject(
                    "RuntimeAiLab_SetupSlot_"
                    + teamId.ToString()
                    + "_"
                    + slotIndex.ToString());
                root.transform.SetParent(parent, worldPositionStays: false);

                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "PreviewBody";
                body.transform.SetParent(root.transform, worldPositionStays: false);
                body.transform.localPosition = new Vector3(0f, PreviewY, 0f);
                body.transform.localScale = new Vector3(0.72f, 0.9f, 0.72f);
                Collider bodyCollider = body.GetComponent<Collider>();
                if (bodyCollider != null)
                    UnityEngine.Object.Destroy(bodyCollider);
                MeshRenderer bodyRenderer = body.GetComponent<MeshRenderer>();

                GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "PreviewRing";
                ring.transform.SetParent(root.transform, worldPositionStays: false);
                ring.transform.localPosition = new Vector3(0f, RingY, 0f);
                ring.transform.localScale = new Vector3(RingScale, 0.025f, RingScale);
                Collider ringCollider = ring.GetComponent<Collider>();
                if (ringCollider != null)
                    UnityEngine.Object.Destroy(ringCollider);
                MeshRenderer ringRenderer = ring.GetComponent<MeshRenderer>();

                GameObject facing = GameObject.CreatePrimitive(PrimitiveType.Cube);
                facing.name = "FacingArrow";
                facing.transform.SetParent(root.transform, worldPositionStays: false);
                float facingSign = teamId == CharacterTestArenaMatchConfig.Team2Id ? -1f : 1f;
                facing.transform.localPosition = new Vector3(0f, 0.09f, facingSign * 1.05f);
                facing.transform.localScale = new Vector3(0.16f, 0.06f, 0.72f);
                if (facingSign < 0f)
                    facing.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Collider facingCollider = facing.GetComponent<Collider>();
                if (facingCollider != null)
                    UnityEngine.Object.Destroy(facingCollider);
                MeshRenderer facingRenderer = facing.GetComponent<MeshRenderer>();

                var labelObject = new GameObject("PreviewLabel");
                labelObject.transform.SetParent(root.transform, worldPositionStays: false);
                labelObject.transform.localPosition = new Vector3(0f, 2.25f, 0f);
                TextMesh label = labelObject.AddComponent<TextMesh>();
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.characterSize = 0.18f;
                label.fontSize = 34;
                label.text = string.Empty;

                return new SetupSlotView(
                    root,
                    teamId,
                    slotIndex,
                    bodyRenderer,
                    ringRenderer,
                    facingRenderer,
                    label);
            }

            public void SetPresentation(
                Vector2 position,
                bool enabled,
                CharacterTestRuntimeAiLabSpawnSlotValidation validation)
            {
                Root.SetActive(enabled);
                Vector3 worldPosition = new Vector3(position.x, 0f, position.y);
                if (Root.transform.position != worldPosition)
                    Root.transform.position = worldPosition;

                if (!_hasPresentation
                    || _lastEnabled != enabled
                    || _lastSeverity != validation.Severity
                    || !string.Equals(_lastMessage, validation.Message, System.StringComparison.Ordinal))
                {
                    _hasPresentation = true;
                    _lastEnabled = enabled;
                    _lastSeverity = validation.Severity;
                    _lastMessage = validation.Message;
                    Label.text = FormatLabel(TeamId, SlotIndex, validation);
                }
            }

            public void SetHighlighted(bool highlighted)
            {
                if (RingRenderer == null)
                    return;

                RingRenderer.enabled = highlighted;
                if (!highlighted)
                    return;

                RingRenderer.transform.localScale =
                    new Vector3(RingScale * 1.22f, 0.025f, RingScale * 1.22f);
            }
        }
    }
}
