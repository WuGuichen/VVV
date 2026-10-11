using UnityEngine;
using UnityEngine.Rendering;
using MxFramework.Rendering;

namespace MxFramework.Game.CharacterTest
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterTestRuntimeAiPlannerDevSceneController))]
    [AddComponentMenu("MxFramework/Game/Character Test/Runtime AI Lab Preview Overlay")]
    public sealed class CharacterTestRuntimeAiPlannerDevPreviewOverlay : MonoBehaviour
    {
        private const int CircleSegments = 72;
        private const float LineWidth = 0.032f;
        private const float RangeLineWidth = 0.022f;
        private const float GroundPlaneOffsetY = 0.035f;

        private readonly Vector3[] _circlePoints = new Vector3[CircleSegments + 1];

        private CharacterTestRuntimeAiPlannerDevSceneController _controller;
        private Material _lineMaterial;
        private LineRenderer _targetLine;
        private LineRenderer _attackRange;
        private LineRenderer _moveLine;
        private LineRenderer _moveHeadLeft;
        private LineRenderer _moveHeadRight;
        private const int MaximumCameraScopes = 8;
        private readonly Renderer[] _cameraRenderers = new Renderer[5];
        private readonly CameraVisibilityScope[] _cameraScopes = new CameraVisibilityScope[MaximumCameraScopes];
        private int _cameraScopeCount;
        private bool _cameraHooksBound;
        private struct CameraVisibilityScope
        {
            internal UnityEngine.Camera Camera;
            internal int PreviousForceOff;
        }

        private void OnEnable()
        {
            if (_cameraHooksBound) return;
            _cameraHooksBound = true;
            RenderPipelineManager.beginContextRendering += RestoreCameraVisibility;
            RenderPipelineManager.endContextRendering += RestoreCameraVisibility;
            RenderPipelineManager.beginCameraRendering += BeginCameraVisibility;
            RenderPipelineManager.endCameraRendering += EndCameraVisibility;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
            EnsureVisuals();
            SetVisible(false);
        }

        private void OnDisable()
        {
            ReleaseCameraHooks();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            ReleaseCameraHooks();
            if (_lineMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(_lineMaterial);
            else
                DestroyImmediate(_lineMaterial);
            _lineMaterial = null;
        }

        private void LateUpdate()
        {
            if (!HasVisuals())
                EnsureVisuals();
            if (!HasVisuals())
                return;

            HandleAgentSelection();

            if (_controller == null || !_controller.TryBuildSelectedPreview(out var preview) || !preview.Available)
            {
                SetVisible(false);
                return;
            }

            Draw(preview);
        }

        private void HandleAgentSelection()
        {
            if (_controller == null
                || _controller.Phase != CharacterTestRuntimeAiPlannerDevScenePhase.Running
                || !CharacterTestRuntimeAiLabInput.IsMouseButtonDown(0)
                || CharacterTestRuntimeAiLabInput.IsPointerBlockedByUi()
                || !CharacterTestRuntimeAiLabCameraResolver.TryGetSceneCamera(out UnityEngine.Camera camera))
            {
                return;
            }

            Vector3 pointer = CharacterTestRuntimeAiLabInput.MousePosition;
            _controller.TrySelectAgentByScreenPosition(
                camera,
                new Vector2(pointer.x, pointer.y));
        }

        private void EnsureVisuals()
        {
            if (_lineMaterial == null)
            {
                Shader shader = ResolvePreviewShader();
                if (shader == null)
                    return;

                _lineMaterial = new Material(shader)
                {
                    name = "Runtime AI Lab Preview Line Material",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            _targetLine = CreateLine("Runtime AI Lab Target Line", LineWidth);
            _attackRange = CreateLine("Runtime AI Lab Attack Range", RangeLineWidth);
            _moveLine = CreateLine("Runtime AI Lab Move Arrow", LineWidth);
            _moveHeadLeft = CreateLine("Runtime AI Lab Move Arrow Head L", LineWidth);
            _moveHeadRight = CreateLine("Runtime AI Lab Move Arrow Head R", LineWidth);
            _cameraRenderers[0] = _targetLine;
            _cameraRenderers[1] = _attackRange;
            _cameraRenderers[2] = _moveLine;
            _cameraRenderers[3] = _moveHeadLeft;
            _cameraRenderers[4] = _moveHeadRight;
        }

        // Only this owner's five debug renderers participate. Never change layers or camera masks.
        private void BeginCameraVisibility(ScriptableRenderContext context, UnityEngine.Camera camera)
        {
            if (_cameraScopeCount == MaximumCameraScopes) RestoreCameraVisibility(context, null);
            int previous = ReadForceOffMask();
            _cameraScopes[_cameraScopeCount++] = new CameraVisibilityScope
                { Camera = camera, PreviousForceOff = previous };
            bool excluded = camera != null && camera.gameObject.scene == gameObject.scene
                && MxSceneRenderingAuxiliaryCamera.TryGet(camera, out var auxiliary)
                && auxiliary.Kind == MxCameraRenderKind.Reflection
                && MxSceneRenderingAuxiliaryCamera.Matches(camera, auxiliary.SourceCamera);
            // A nested ordinary view sees the original state, then its end resumes the outer scope.
            WriteForceOffMask(excluded ? (1 << _cameraRenderers.Length) - 1
                : _cameraScopes[0].PreviousForceOff);
        }

        private void EndCameraVisibility(ScriptableRenderContext context, UnityEngine.Camera camera)
        {
            int match = _cameraScopeCount - 1;
            while (match >= 0 && _cameraScopes[match].Camera != camera) match--;
            if (match < 0) return;
            while (_cameraScopeCount > match)
            {
                var scope = _cameraScopes[--_cameraScopeCount];
                WriteForceOffMask(scope.PreviousForceOff);
                _cameraScopes[_cameraScopeCount] = default;
            }
        }

        private void RestoreCameraVisibility(ScriptableRenderContext context, System.Collections.Generic.List<UnityEngine.Camera> cameras)
        {
            if (_cameraScopeCount == 0) return;
            WriteForceOffMask(_cameraScopes[0].PreviousForceOff);
            System.Array.Clear(_cameraScopes, 0, _cameraScopeCount);
            _cameraScopeCount = 0;
        }

        private int ReadForceOffMask()
        {
            int value = 0;
            for (int i = 0; i < _cameraRenderers.Length; i++)
                if (_cameraRenderers[i] != null && _cameraRenderers[i].forceRenderingOff) value |= 1 << i;
            return value;
        }

        private void WriteForceOffMask(int value)
        {
            for (int i = 0; i < _cameraRenderers.Length; i++)
                if (_cameraRenderers[i] != null) _cameraRenderers[i].forceRenderingOff = (value & (1 << i)) != 0;
        }

        private void ReleaseCameraHooks()
        {
            RestoreCameraVisibility(default, null);
            if (!_cameraHooksBound) return;
            _cameraHooksBound = false;
            RenderPipelineManager.beginContextRendering -= RestoreCameraVisibility;
            RenderPipelineManager.endContextRendering -= RestoreCameraVisibility;
            RenderPipelineManager.beginCameraRendering -= BeginCameraVisibility;
            RenderPipelineManager.endCameraRendering -= EndCameraVisibility;
        }

        private LineRenderer CreateLine(string objectName, float width)
        {
            var lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(transform, worldPositionStays: false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.material = _lineMaterial;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.useWorldSpace = true;
            line.startWidth = width;
            line.endWidth = width;
            line.positionCount = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void Draw(CharacterTestRuntimeAiPlannerDevPreviewItem preview)
        {
            SetVisible(true);

            DrawTargetLine(preview);
            DrawAttackRange(preview);
            DrawMoveArrow(preview);
        }

        private void DrawTargetLine(CharacterTestRuntimeAiPlannerDevPreviewItem preview)
        {
            if (!preview.HasTargetLine || !preview.HasGeometry)
            {
                _targetLine.positionCount = 0;
                return;
            }

            Color mainColor = preview.TargetLineColor;
            Color brightColor = mainColor;
            brightColor.a = 0.9f;

            // Exact locked-Hurtbox witnesses are preferred. Preview items retain
            // frozen actor endpoints while the precise sample is unavailable so
            // the single target indicator never disappears between valid frames.
            _targetLine.startColor = brightColor;
            _targetLine.endColor = brightColor;
            _targetLine.positionCount = 2;
            _targetLine.SetPosition(0, preview.DistanceSourcePoint);
            _targetLine.SetPosition(1, preview.DistanceTargetPoint);
        }

        private void DrawAttackRange(CharacterTestRuntimeAiPlannerDevPreviewItem preview)
        {
            if (!preview.HasAttackRange || !preview.HasGeometry)
            {
                _attackRange.positionCount = 0;
                return;
            }

            Color color = preview.AttackRangeColor;
            color.a = 0.8f;
            _attackRange.startColor = color;
            _attackRange.endColor = color;
            _attackRange.positionCount = _circlePoints.Length;
            Vector3 center = preview.SourceCenter;
            float radius = preview.AttackRange;
            float groundY = ResolveGroundY(center);
            for (int i = 0; i <= CircleSegments; i++)
            {
                float angle = (Mathf.PI * 2f * i) / CircleSegments;
                _circlePoints[i] = new Vector3(
                    center.x + Mathf.Cos(angle) * radius,
                    groundY,
                    center.z + Mathf.Sin(angle) * radius);
            }

            _attackRange.SetPositions(_circlePoints);
        }

        private void DrawMoveArrow(CharacterTestRuntimeAiPlannerDevPreviewItem preview)
        {
            if (!preview.HasMoveArrow || !preview.HasGeometry)
            {
                _moveLine.positionCount = 0;
                _moveHeadLeft.positionCount = 0;
                _moveHeadRight.positionCount = 0;
                return;
            }

            Color color = preview.MoveArrowColor;
            color.a = 0.85f;
            SetLineColor(_moveLine, color);
            SetLineColor(_moveHeadLeft, color);
            SetLineColor(_moveHeadRight, color);
            float groundY = ResolveGroundY(preview.SourceCenter) + 0.015f;
            Vector3 start = new Vector3(preview.SourceCenter.x, groundY, preview.SourceCenter.z);
            Vector3 end = new Vector3(preview.MoveEnd.x, groundY, preview.MoveEnd.z);
            _moveLine.positionCount = 2;
            _moveLine.SetPosition(0, start);
            _moveLine.SetPosition(1, end);

            Vector3 direction = end - start;
            if (direction.sqrMagnitude < 0.0001f)
            {
                _moveHeadLeft.positionCount = 0;
                _moveHeadRight.positionCount = 0;
                return;
            }

            direction.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
            Vector3 headBase = end - direction * 0.28f;
            Vector3 left = headBase + side * 0.18f;
            Vector3 right = headBase - side * 0.18f;
            _moveHeadLeft.positionCount = 2;
            _moveHeadLeft.SetPosition(0, end);
            _moveHeadLeft.SetPosition(1, left);
            _moveHeadRight.positionCount = 2;
            _moveHeadRight.SetPosition(0, end);
            _moveHeadRight.SetPosition(1, right);
        }

        private void SetVisible(bool visible)
        {
            if (_targetLine != null)
                _targetLine.gameObject.SetActive(visible);
            if (_attackRange != null)
                _attackRange.gameObject.SetActive(visible);
            if (_moveLine != null)
                _moveLine.gameObject.SetActive(visible);
            if (_moveHeadLeft != null)
                _moveHeadLeft.gameObject.SetActive(visible);
            if (_moveHeadRight != null)
                _moveHeadRight.gameObject.SetActive(visible);
        }

        private static float ResolveGroundY(Vector3 center)
        {
            return Mathf.Max(
                GroundPlaneOffsetY,
                center.y > 0.6f
                    ? center.y - 0.95f + GroundPlaneOffsetY
                    : GroundPlaneOffsetY);
        }

        private static void SetLineColor(LineRenderer line, Color color)
        {
            line.startColor = color;
            line.endColor = color;
        }

        private bool HasVisuals()
        {
            return _targetLine != null
                && _attackRange != null
                && _moveLine != null
                && _moveHeadLeft != null
                && _moveHeadRight != null;
        }

        private static Shader ResolvePreviewShader()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
                return shader;

            shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
                return shader;

            return Shader.Find("Hidden/Internal-Colored");
        }
    }
}
