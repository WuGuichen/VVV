using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestVfxAnchorResolver : IUnityVfxAnchorResolver
    {
        internal const int ControlStatusHeadSocketId = 2147483601;

        private readonly CharacterTestCharacterUnityViewBridge _viewBridge;

        public CharacterTestVfxAnchorResolver(CharacterTestCharacterUnityViewBridge viewBridge)
        {
            _viewBridge = viewBridge;
        }

        public bool TryResolve(in VfxAnchor anchor, out UnityVfxResolvedAnchor resolvedAnchor)
        {
            resolvedAnchor = default;
            if (HasExplicitTargetHitPose(anchor))
            {
                resolvedAnchor = new UnityVfxResolvedAnchor(ToVector3(anchor.Pose.Position), ToVector3(anchor.Pose.Forward));
                return true;
            }

            if (_viewBridge == null)
                return false;

            int entityId = ResolveEntityId(anchor);
            if (entityId <= 0)
                return false;

            if (anchor.SocketId == ControlStatusHeadSocketId)
            {
                if (!_viewBridge.TryResolveControlStatusVfxAnchor(
                        entityId,
                        out Vector3 statusPosition,
                        out Vector3 statusForward))
                {
                    return false;
                }

                resolvedAnchor = new UnityVfxResolvedAnchor(statusPosition, statusForward);
                return true;
            }

            if (!_viewBridge.TryResolveVfxAnchor(
                    entityId,
                    anchor.SocketId,
                    out Vector3 position,
                    out Vector3 forward))
            {
                return false;
            }

            Vector3 offset = ToVector3(anchor.Pose.Position);
            if (anchor.Kind == VfxAnchorKind.Attached)
            {
                Vector3 safeForward = forward.sqrMagnitude > 0.000001f ? forward.normalized : Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, safeForward).normalized;
                offset = right * offset.x + Vector3.up * offset.y + safeForward * offset.z;
            }
            resolvedAnchor = new UnityVfxResolvedAnchor(position + offset, forward);
            return true;
        }

        private static int ResolveEntityId(VfxAnchor anchor)
        {
            switch (anchor.Kind)
            {
                case VfxAnchorKind.SourceEntity:
                case VfxAnchorKind.Attached:
                    return anchor.SourceId;
                case VfxAnchorKind.TargetEntity:
                    return anchor.TargetId > 0 ? anchor.TargetId : anchor.SourceId;
                default:
                    return 0;
            }
        }

        private static bool HasExplicitTargetHitPose(VfxAnchor anchor)
        {
            // Status and environment damage may have no source entity, while the
            // target pose is still an authoritative world-space hit position.
            return anchor.Kind == VfxAnchorKind.TargetEntity
                && anchor.TargetId > 0
                && anchor.TargetId != anchor.SourceId
                && !anchor.Pose.Equals(VfxPose.Identity);
        }

        private static Vector3 ToVector3(VfxVector3 value)
        {
            return new Vector3(
                ToFloat(value.RawX),
                ToFloat(value.RawY),
                ToFloat(value.RawZ));
        }

        private static float ToFloat(long raw)
        {
            return raw / (float)VfxVector3.Scale;
        }
    }
}
