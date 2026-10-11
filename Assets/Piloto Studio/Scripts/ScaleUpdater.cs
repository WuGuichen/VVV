using UnityEngine;

[ExecuteAlways]
public class ScreenSpacePlaneAutoScale_FOV : MonoBehaviour
{
    [Header("Camera")]
    public Camera targetCamera;

    [Header("Coverage")]
    [Tooltip("1 = exactly fills the camera frustum at this distance. >1 overscales.")]
    public float overScaling = 10.0f;

    [Tooltip("If true and this object is parented to the camera, uses localPosition.z as distance.")]
    public bool useLocalZDistanceIfCameraParent = true;

    [Header("Axis Mapping")]
    [Tooltip("If your plane is rotated (e.g., lies on XZ), enable this to swap Y/Z usage.")]
    public bool planeUsesXZInsteadOfXY = false;

    float _lastFov = -1f;
    float _lastAspect = -1f;
    float _lastDist = -1f;

    void OnEnable()
    {
        ResolveCamera();
        Apply();
    }

    void LateUpdate()
    {
        ResolveCamera();
        if (targetCamera == null) return;

        float fov = targetCamera.fieldOfView;
        float aspect = targetCamera.aspect;
        float dist = GetDistanceFromCamera();

        // Only recompute when something relevant changes
        if (!Mathf.Approximately(fov, _lastFov) ||
            !Mathf.Approximately(aspect, _lastAspect) ||
            !Mathf.Approximately(dist, _lastDist))
        {
            Apply();
        }
    }

    void ResolveCamera()
    {
        if (targetCamera != null) return;

        // If we are under a camera hierarchy, prefer that
        targetCamera = GetComponentInParent<Camera>();

        // Fallback
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    float GetDistanceFromCamera()
    {
        if (targetCamera == null) return 1f;

        if (useLocalZDistanceIfCameraParent && transform.IsChildOf(targetCamera.transform))
        {
            // You said Z is always 1; this is the ideal case.
            return Mathf.Max(0.0001f, transform.localPosition.z);
        }

        // Otherwise compute real world distance
        float d = Vector3.Distance(transform.position, targetCamera.transform.position);
        return Mathf.Max(0.0001f, d);
    }

    void Apply()
    {
        if (targetCamera == null) return;

        float dist = GetDistanceFromCamera();

        float fovRad = targetCamera.fieldOfView * Mathf.Deg2Rad;

        // --- Stolen algorithm (from your script): ---
        float frustumHeight = 2f * Mathf.Tan(0.5f * fovRad) * dist;
        float frustumWidth = frustumHeight * targetCamera.aspect;

        frustumHeight *= overScaling;
        frustumWidth *= overScaling;

        // Apply to plane scale.
        // Default Unity plane/quad is in XY; some people use XZ.
        if (!planeUsesXZInsteadOfXY)
            transform.localScale = new Vector3(frustumWidth, frustumHeight, transform.localScale.z);
        else
            transform.localScale = new Vector3(frustumWidth, transform.localScale.y, frustumHeight);

        _lastFov = targetCamera.fieldOfView;
        _lastAspect = targetCamera.aspect;
        _lastDist = dist;
    }
}
