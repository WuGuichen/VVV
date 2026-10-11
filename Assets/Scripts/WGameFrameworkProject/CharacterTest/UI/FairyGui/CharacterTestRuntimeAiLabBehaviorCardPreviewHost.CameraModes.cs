using System.Collections.Generic;
using MxFramework.Camera;
using MxFramework.Camera.Unity;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestPreviewCameraMode { Free, ThirdPerson, MultiTarget }

    internal sealed partial class CharacterTestRuntimeAiLabBehaviorCardPreviewHost
    {
        public bool ShowTargetHurtboxes { get; private set; }
        private Mesh _hurtboxMesh;
        private Material _hurtboxMaterial;
        private readonly List<Vector3> _hurtboxVertices = new List<Vector3>(4096);
        private readonly List<int> _hurtboxIndices = new List<int>(4096);
        private readonly Vector3[] _hurtboxCorners = new Vector3[8];

        public void ToggleTargetHurtboxes()
        {
            ShowTargetHurtboxes = !ShowTargetHurtboxes;
            if (_header?.GetChild("btnRuntimeOverlay") is global::FairyGUI.GButton button)
                button.title = ShowTargetHurtboxes ? "受击框：开" : "受击框：关";
        }

        internal void DrawTargetHurtboxes()
        {
            if (!ShowTargetHurtboxes || !_active || _previewCamera == null || !_previewCamera.enabled || _controller == null
                || !_controller.TryGetPreviewTargetHurtboxes(out var geometry, out var root)) return;
            if (_hurtboxMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) return;
                _hurtboxMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave,
                    color = new Color(1f, 0.25f, 0.65f, 1f) };
                _hurtboxMesh = new Mesh { name = "Target Hurtbox Preview", hideFlags = HideFlags.HideAndDontSave,
                    indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                _hurtboxMesh.MarkDynamic();
            }
            _hurtboxVertices.Clear(); _hurtboxIndices.Clear();
            for (int i = 0; i < geometry.ColliderCount; i++)
            {
                var shape = geometry.GetCollider(i).LocalShape;
                switch (shape.Kind)
                {
                    case MxFramework.Combat.Physics.CombatPhysicsShapeKind.Sphere:
                        HurtboxCapsule(root + HurtboxVector(shape.Center), root + HurtboxVector(shape.Center), HurtboxFloat(shape.Radius));
                        break;
                    case MxFramework.Combat.Physics.CombatPhysicsShapeKind.Capsule:
                        HurtboxCapsule(root + HurtboxVector(shape.PointA), root + HurtboxVector(shape.PointB), HurtboxFloat(shape.Radius));
                        break;
                    case MxFramework.Combat.Physics.CombatPhysicsShapeKind.Aabb:
                        var min = HurtboxVector(shape.Min); var max = HurtboxVector(shape.Max);
                        HurtboxBox(root + (min + max) * .5f, (max - min) * .5f, Vector3.right, Vector3.up, Vector3.forward);
                        break;
                    case MxFramework.Combat.Physics.CombatPhysicsShapeKind.Obb:
                        HurtboxBox(root + HurtboxVector(shape.Center), HurtboxVector(shape.HalfExtents),
                            HurtboxVector(shape.AxisX), HurtboxVector(shape.AxisY), HurtboxVector(shape.AxisZ));
                        break;
                }
            }
            _hurtboxMesh.Clear();
            _hurtboxMesh.SetVertices(_hurtboxVertices);
            _hurtboxMesh.SetIndices(_hurtboxIndices, MeshTopology.Lines, 0);
            // Camera-specific drawing keeps diagnostic geometry out of the main Game camera.
            Graphics.DrawMesh(_hurtboxMesh, Matrix4x4.identity, _hurtboxMaterial, 0, _previewCamera,
                0, null, UnityEngine.Rendering.ShadowCastingMode.Off, false);
        }

        private static Vector3 HurtboxVector(MxFramework.Core.Math.FixVector3 value) =>
            new Vector3(HurtboxFloat(value.X), HurtboxFloat(value.Y), HurtboxFloat(value.Z));

        private static float HurtboxFloat(MxFramework.Core.Math.Fix64 value) =>
            value.RawValue / (float)MxFramework.Core.Math.Fix64.Scale;

        private void HurtboxLine(Vector3 a, Vector3 b)
        {
            _hurtboxIndices.Add(_hurtboxVertices.Count); _hurtboxVertices.Add(a);
            _hurtboxIndices.Add(_hurtboxVertices.Count); _hurtboxVertices.Add(b);
        }

        private void HurtboxBox(Vector3 center, Vector3 size, Vector3 x, Vector3 y, Vector3 z)
        {
            for (int i = 0; i < 8; i++) _hurtboxCorners[i] = center
                + x * ((i & 1) == 0 ? -size.x : size.x)
                + y * ((i & 2) == 0 ? -size.y : size.y)
                + z * ((i & 4) == 0 ? -size.z : size.z);
            for (int i = 0; i < 8; i++) for (int bit = 1; bit <= 4; bit <<= 1)
                if ((i & bit) == 0) HurtboxLine(_hurtboxCorners[i], _hurtboxCorners[i | bit]);
        }

        private void HurtboxCapsule(Vector3 a, Vector3 b, float radius)
        {
            Vector3 axis = b - a;
            axis = axis.sqrMagnitude < .000001f ? Vector3.up : axis.normalized;
            Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < .9f ? Vector3.up : Vector3.right).normalized;
            Vector3 v = Vector3.Cross(axis, u);
            const int segments = 24;
            for (int i = 0; i < segments; i++)
            {
                float t0 = i * Mathf.PI * 2 / segments, t1 = (i + 1) * Mathf.PI * 2 / segments;
                Vector3 p = radius * (u * Mathf.Cos(t0) + v * Mathf.Sin(t0));
                Vector3 q = radius * (u * Mathf.Cos(t1) + v * Mathf.Sin(t1));
                HurtboxLine(a + p, a + q); HurtboxLine(b + p, b + q);
            }
            for (int side = 0; side < 4; side++)
            {
                Vector3 radial = side == 0 ? u : side == 1 ? v : side == 2 ? -u : -v;
                HurtboxLine(a + radial * radius, b + radial * radius);
                for (int i = 0; i < segments / 4; i++)
                {
                    float t0 = i * Mathf.PI * 2 / segments, t1 = (i + 1) * Mathf.PI * 2 / segments;
                    HurtboxLine(a + radius * (radial * Mathf.Cos(t0) - axis * Mathf.Sin(t0)),
                        a + radius * (radial * Mathf.Cos(t1) - axis * Mathf.Sin(t1)));
                    HurtboxLine(b + radius * (radial * Mathf.Cos(t0) + axis * Mathf.Sin(t0)),
                        b + radius * (radial * Mathf.Cos(t1) + axis * Mathf.Sin(t1)));
                }
            }
        }

        private void ReleaseTargetHurtboxes()
        {
            if (_hurtboxMesh != null) DestroyObject(_hurtboxMesh);
            if (_hurtboxMaterial != null) DestroyObject(_hurtboxMaterial);
            _hurtboxMesh = null; _hurtboxMaterial = null;
        }

        private readonly List<CharacterTestPreviewCameraSubject> _cameraSubjects = new List<CharacterTestPreviewCameraSubject>(8);
        private readonly List<MxCameraTargetSnapshot> _cameraTargets = new List<MxCameraTargetSnapshot>(8);
        private readonly Dictionary<int, MxCameraTargetRef> _cameraTargetRefs = new Dictionary<int, MxCameraTargetRef>();
        private readonly MxCameraProfileDefinition[] _cameraProfiles = { new MxCameraProfileDefinition {
            ProfileId = new MxCameraProfileId("runtime-ai-lab.preview"), FieldOfView = 45f,
            MinFieldOfView = 20f, MaxFieldOfView = 80f, MinDistance = 3f, MaxDistance = 250f,
            TargetPadding = 1f, MaxTargetRadius = 1000f, TargetLostGraceFrames = 0,
            BoundsPolicy = MxCameraBoundsPolicy.NoClamp } };
        private MxCameraUnityRig _automaticRig;
        internal CharacterTestPreviewCameraMode CameraMode { get; private set; } = CharacterTestPreviewCameraMode.MultiTarget;
        internal int CameraSubjectCount { get; private set; }
        internal int CameraSourceFrame { get; private set; }
        public string CameraModeLabel => CameraMode switch {
            CharacterTestPreviewCameraMode.Free => "自由观察",
            CharacterTestPreviewCameraMode.ThirdPerson => "第三人称跟随",
            _ => "多角色自动取景" };

        public void CycleCameraMode() => SetCameraMode(CameraMode switch {
            CharacterTestPreviewCameraMode.Free => CharacterTestPreviewCameraMode.ThirdPerson,
            CharacterTestPreviewCameraMode.ThirdPerson => CharacterTestPreviewCameraMode.MultiTarget,
            _ => CharacterTestPreviewCameraMode.Free });

        internal void SetCameraMode(CharacterTestPreviewCameraMode mode)
        {
            CameraMode = mode;
            _fitRequested = mode != CharacterTestPreviewCameraMode.Free;
            UpdateCameraModeCaption();
            if (_active) UpdateCameraFraming();
        }

        private void UpdateCameraModeCaption()
        {
            var button = _header?.GetChild("btnRuntimeFollow")?.asButton;
            if (button == null) return;
            string caption = CameraMode switch {
                CharacterTestPreviewCameraMode.Free => "自由观察 ↻",
                CharacterTestPreviewCameraMode.ThirdPerson => "第三人称跟随 ↻",
                _ => "多角色自动取景 ↻" };
            if (button.title != caption) button.title = caption;
            button.tooltips = "点击切换：自由观察 → 第三人称跟随 → 多角色自动取景。拖动或滚轮切换为自由观察。";
        }

        internal void TickCameraPresentation()
        {
            if (!_active || !_result.Ready || _previewCamera == null
                || !_previewCamera.isActiveAndEnabled || CameraMode == CharacterTestPreviewCameraMode.Free
                || !IsComponentAlive(_header) || !_header.onStage)
                return;

            // Only advance camera presentation here; mounting and model binding remain throttled.
            UpdateAutomaticCamera();
        }

        private void UpdateAutomaticCamera()
        {
            if (_controller == null || !_controller.CollectPreviewCameraSubjects(_cameraSubjects,
                    CameraMode == CharacterTestPreviewCameraMode.MultiTarget, out int frame))
            {
                CameraSubjectCount = 0;
                if (_header?.GetChild("btnRuntimeFollow") is global::FairyGUI.GObject button)
                    button.tooltips = "当前帧没有可取景角色；保留视角，角色可用后自动恢复。点击可切换模式。";
                return;
            }
            CameraSourceFrame = frame; CameraSubjectCount = _cameraSubjects.Count;
            if (_automaticRig == null) _automaticRig = _cameraObject.AddComponent<MxCameraUnityRig>();
            var profile = _cameraProfiles[0];
            ConfigureAutomaticProfile(profile, CameraMode, _cameraSubjects, _previewCamera.aspect);
            _cameraTargets.Clear();
            int count = CameraMode == CharacterTestPreviewCameraMode.ThirdPerson ? 1 : _cameraSubjects.Count;
            for (int i = 0; i < count; i++)
            {
                var subject = _cameraSubjects[i];
                if (!_cameraTargetRefs.TryGetValue(subject.EntityId, out var id))
                {
                    id = new MxCameraTargetRef("preview.entity." + subject.EntityId);
                    _cameraTargetRefs.Add(subject.EntityId, id);
                }
                var position = MxCameraUnityConversions.ToCameraVector(subject.Center);
                _cameraTargets.Add(new MxCameraTargetSnapshot(id, position, MxCameraVector3.Forward,
                    MxCameraVector3.Up, MxCameraVector3.Zero, position, new MxCameraVector3(0.6f, 1.1f, 0.6f),
                    1f, subject.Primary, true, frame));
            }
            var result = _automaticRig.EvaluateAndApplyLateNoAlloc(frame, 0f, _cameraProfiles, _cameraTargets);
            if (!result.Success) return;
            _navigationPivot = _automaticRig.HasLastNoAllocEvaluation
                ? MxCameraUnityConversions.ToUnityVector(_automaticRig.LastNoAllocEvaluation.State.FocusCenter)
                : MxCameraUnityConversions.ToUnityVector(_automaticRig.LastEvaluationResult.State.FocusCenter);
            _previewCamera.farClipPlane = Mathf.Max(_previewCamera.farClipPlane,
                Vector3.Distance(_previewCamera.transform.position, _navigationPivot) + 100f);
            _fitRequested = false;
        }

        internal static void ConfigureAutomaticProfile(MxCameraProfileDefinition profile, CharacterTestPreviewCameraMode mode,
            IReadOnlyList<CharacterTestPreviewCameraSubject> subjects, float aspect)
        {
            profile.Mode = mode == CharacterTestPreviewCameraMode.MultiTarget ? MxCameraMode.GroupFollowPerspective : MxCameraMode.Follow;
            profile.WorldOffset = MxCameraVector3.Zero;
            profile.LocalOffset = MxCameraVector3.Zero;
            profile.Yaw = 0f; profile.Pitch = 22f; profile.Distance = 6f;
            profile.MinDistance = 3f; profile.MaxDistance = 250f;
            if (mode == CharacterTestPreviewCameraMode.MultiTarget && subjects.Count > 0)
            {
                // Supply a viewport-safe distance floor to the existing group solver.
                // A sphere must fit inside both frustum planes, including depth and body bounds.
                Vector3 center = Vector3.zero;
                for (int i = 0; i < subjects.Count; i++) center += subjects[i].Center;
                center /= subjects.Count;
                float radius = 0f;
                for (int i = 0; i < subjects.Count; i++)
                    radius = Mathf.Max(radius, Vector3.Distance(center, subjects[i].Center) + 1.4f);
                float halfAngle = Mathf.Atan(Mathf.Tan(profile.FieldOfView * Mathf.Deg2Rad * 0.5f)
                    * Mathf.Min(1f, Mathf.Max(0.1f, aspect)));
                profile.MinDistance = Mathf.Max(3f, radius / Mathf.Sin(halfAngle));
                profile.MaxDistance = Mathf.Max(250f, profile.MinDistance);
            }
            if (mode != CharacterTestPreviewCameraMode.ThirdPerson || subjects.Count < 2) return;
            var player = Presentation(subjects[0].Center);
            var target = Presentation(subjects[1].Center);
            var settings = new CharacterTestLockedThirdPersonCameraCompositionSettings(6f, 3f, 80f, profile.FieldOfView,
                aspect, 0.6f, 0.6f, 0f, 22f);
            var composition = CharacterTestLockedThirdPersonCameraComposer.Compose(player, target, settings);
            profile.Yaw = composition.Yaw; profile.Pitch = composition.Pitch;
            profile.Distance = profile.MinDistance = profile.MaxDistance = composition.Distance;
            profile.WorldOffset = MxCameraUnityConversions.ToCameraVector(composition.WorldOffset);
            // Snapshot centers are not the live rig's authored bone anchors. Keep its composition,
            // but fit the preview body envelope so a close primary does not fall below the viewport.
            float safeDistance = CalculateFramingDistance(subjects[0].Center, subjects[1].Center,
                subjects[0].Center + composition.WorldOffset, Quaternion.Euler(composition.Pitch, composition.Yaw, 0),
                profile.FieldOfView, aspect);
            profile.Distance = profile.MinDistance = profile.MaxDistance = Mathf.Max(composition.Distance, safeDistance);

            static CharacterTestCameraTargetPresentation Presentation(Vector3 center) =>
                new CharacterTestCameraTargetPresentation(null, center, Vector3.forward, center,
                    center, new Vector3(0.6f, 1.1f, 0.6f), default);
        }
    }
}
