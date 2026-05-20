namespace MxFramework.Camera.Unity
{
    public enum MxUiCameraRigKind
    {
        None,
        Overlay3D,
        PreviewTexture
    }

    public readonly struct MxUiCameraLayerPolicyResult
    {
        public MxUiCameraLayerPolicyResult(
            bool success,
            string code,
            string message,
            int uiLayer,
            int mainCameraMask,
            int uiCameraMask,
            bool mainExcludesUiLayer,
            bool uiIncludesUiLayer,
            bool uiOnlyUiLayer)
        {
            Success = success;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            UiLayer = uiLayer;
            MainCameraMask = mainCameraMask;
            UiCameraMask = uiCameraMask;
            MainExcludesUiLayer = mainExcludesUiLayer;
            UiIncludesUiLayer = uiIncludesUiLayer;
            UiOnlyUiLayer = uiOnlyUiLayer;
        }

        public bool Success { get; }
        public string Code { get; }
        public string Message { get; }
        public int UiLayer { get; }
        public int MainCameraMask { get; }
        public int UiCameraMask { get; }
        public bool MainExcludesUiLayer { get; }
        public bool UiIncludesUiLayer { get; }
        public bool UiOnlyUiLayer { get; }
    }

    public readonly struct MxUiCameraDebugSummary
    {
        public MxUiCameraDebugSummary(
            string rigId,
            MxUiCameraRigKind kind,
            bool available,
            bool stackBound,
            bool layerPolicyValid,
            string uiLayerName,
            bool targetTextureAssigned,
            string code,
            string message)
        {
            RigId = rigId ?? string.Empty;
            Kind = kind;
            Available = available;
            StackBound = stackBound;
            LayerPolicyValid = layerPolicyValid;
            UiLayerName = uiLayerName ?? string.Empty;
            TargetTextureAssigned = targetTextureAssigned;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string RigId { get; }
        public MxUiCameraRigKind Kind { get; }
        public bool Available { get; }
        public bool StackBound { get; }
        public bool LayerPolicyValid { get; }
        public string UiLayerName { get; }
        public bool TargetTextureAssigned { get; }
        public string Code { get; }
        public string Message { get; }
    }

    public static class MxUiCameraLayerPolicy
    {
        public const string DefaultOverlayLayerName = "MxUi3D";
        public const string DefaultPreviewLayerName = "MxUiPreview3D";
        public const string InvalidLayerCode = "CAM_UI_LAYER_INVALID";
        public const string MainCameraIncludesUiLayerCode = "CAM_UI_MAIN_MASK_INCLUDES_UI";
        public const string UiCameraExcludesUiLayerCode = "CAM_UI_CAMERA_MASK_EXCLUDES_UI";
        public const string UiCameraIncludesExtraLayersCode = "CAM_UI_CAMERA_MASK_INCLUDES_EXTRA";

        public static int MakeLayerMask(int layerIndex)
        {
            return IsValidLayer(layerIndex) ? unchecked(1 << layerIndex) : 0;
        }

        public static MxUiCameraLayerPolicyResult ValidateOverlayMasks(
            int uiLayer,
            int mainCameraMask,
            int uiCameraMask,
            bool requireUiCameraOnlyUiLayer = true)
        {
            if (!IsValidLayer(uiLayer))
            {
                return CreateResult(
                    false,
                    InvalidLayerCode,
                    "UI camera layer index must be between 0 and 31.",
                    uiLayer,
                    mainCameraMask,
                    uiCameraMask);
            }

            int uiLayerMask = MakeLayerMask(uiLayer);
            bool mainExcludesUiLayer = (mainCameraMask & uiLayerMask) == 0;
            bool uiIncludesUiLayer = (uiCameraMask & uiLayerMask) != 0;
            bool uiOnlyUiLayer = uiCameraMask == uiLayerMask;

            if (!mainExcludesUiLayer)
            {
                return new MxUiCameraLayerPolicyResult(
                    false,
                    MainCameraIncludesUiLayerCode,
                    "Main camera culling mask must exclude the UI 3D layer.",
                    uiLayer,
                    mainCameraMask,
                    uiCameraMask,
                    mainExcludesUiLayer,
                    uiIncludesUiLayer,
                    uiOnlyUiLayer);
            }

            if (!uiIncludesUiLayer)
            {
                return new MxUiCameraLayerPolicyResult(
                    false,
                    UiCameraExcludesUiLayerCode,
                    "UI camera culling mask must include the UI 3D layer.",
                    uiLayer,
                    mainCameraMask,
                    uiCameraMask,
                    mainExcludesUiLayer,
                    uiIncludesUiLayer,
                    uiOnlyUiLayer);
            }

            if (requireUiCameraOnlyUiLayer && !uiOnlyUiLayer)
            {
                return new MxUiCameraLayerPolicyResult(
                    false,
                    UiCameraIncludesExtraLayersCode,
                    "UI camera culling mask must include only the UI 3D layer.",
                    uiLayer,
                    mainCameraMask,
                    uiCameraMask,
                    mainExcludesUiLayer,
                    uiIncludesUiLayer,
                    uiOnlyUiLayer);
            }

            return new MxUiCameraLayerPolicyResult(
                true,
                string.Empty,
                string.Empty,
                uiLayer,
                mainCameraMask,
                uiCameraMask,
                mainExcludesUiLayer,
                uiIncludesUiLayer,
                uiOnlyUiLayer);
        }

        private static bool IsValidLayer(int layerIndex)
        {
            return layerIndex >= 0 && layerIndex <= 31;
        }

        private static MxUiCameraLayerPolicyResult CreateResult(
            bool success,
            string code,
            string message,
            int uiLayer,
            int mainCameraMask,
            int uiCameraMask)
        {
            return new MxUiCameraLayerPolicyResult(
                success,
                code,
                message,
                uiLayer,
                mainCameraMask,
                uiCameraMask,
                false,
                false,
                false);
        }
    }
}
