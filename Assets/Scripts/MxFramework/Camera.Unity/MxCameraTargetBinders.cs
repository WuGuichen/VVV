using UnityEngine;

namespace MxFramework.Camera.Unity
{
    public interface IMxCameraUnityTargetBinder
    {
        MxCameraTargetSnapshot CreateSnapshot(long frame);
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Camera/Transform Target Binder")]
    public sealed class MxCameraTransformTargetBinder : MonoBehaviour, IMxCameraUnityTargetBinder
    {
        [SerializeField] private string _targetRef = "target";
        [SerializeField] private Vector3 _boundsExtents = new Vector3(0.5f, 0.5f, 0.5f);
        [SerializeField] private float _weight = 1f;
        [SerializeField] private bool _isPrimary;
        [SerializeField] private bool _isValid = true;

        public MxCameraTargetSnapshot CreateSnapshot(long frame)
        {
            return new MxCameraTargetSnapshot(
                new MxCameraTargetRef(string.IsNullOrWhiteSpace(_targetRef) ? name : _targetRef),
                MxCameraUnityConversions.ToCameraVector(transform.position),
                MxCameraUnityConversions.ToCameraVector(transform.forward),
                MxCameraUnityConversions.ToCameraVector(transform.up),
                MxCameraVector3.Zero,
                MxCameraUnityConversions.ToCameraVector(transform.position),
                MxCameraUnityConversions.ToCameraVector(_boundsExtents),
                _weight,
                _isPrimary,
                _isValid,
                frame);
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    [AddComponentMenu("MxFramework/Camera/Renderer Bounds Target Binder")]
    public sealed class MxCameraRendererBoundsTargetBinder : MonoBehaviour, IMxCameraUnityTargetBinder
    {
        [SerializeField] private string _targetRef = "target";
        [SerializeField] private float _weight = 1f;
        [SerializeField] private bool _isPrimary;
        [SerializeField] private bool _isValid = true;

        public MxCameraTargetSnapshot CreateSnapshot(long frame)
        {
            Renderer rendererComponent = GetComponent<Renderer>();
            Bounds bounds = rendererComponent != null ? rendererComponent.bounds : new Bounds(transform.position, Vector3.one);
            return new MxCameraTargetSnapshot(
                new MxCameraTargetRef(string.IsNullOrWhiteSpace(_targetRef) ? name : _targetRef),
                MxCameraUnityConversions.ToCameraVector(transform.position),
                MxCameraUnityConversions.ToCameraVector(transform.forward),
                MxCameraUnityConversions.ToCameraVector(transform.up),
                MxCameraVector3.Zero,
                MxCameraUnityConversions.ToCameraVector(bounds.center),
                MxCameraUnityConversions.ToCameraVector(bounds.extents),
                _weight,
                _isPrimary,
                _isValid && rendererComponent != null,
                frame);
        }
    }
}
