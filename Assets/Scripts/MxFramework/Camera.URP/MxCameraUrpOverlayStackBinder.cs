using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Camera.URP
{
    public readonly struct MxCameraUrpStackResult
    {
        public MxCameraUrpStackResult(bool success, string code, string message, int stackCount)
        {
            Success = success;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            StackCount = stackCount;
        }

        public bool Success { get; }
        public string Code { get; }
        public string Message { get; }
        public int StackCount { get; }

        public static MxCameraUrpStackResult Ok(int stackCount, string message = "")
        {
            return new MxCameraUrpStackResult(true, string.Empty, message, stackCount);
        }

        public static MxCameraUrpStackResult Failed(string code, string message, int stackCount = 0)
        {
            return new MxCameraUrpStackResult(false, code, message, stackCount);
        }
    }

    public static class MxCameraUrpDiagnosticCodes
    {
        public const string MissingBaseCamera = "CAM_UI_STACK_BASE_MISSING";
        public const string MissingOverlayCamera = "CAM_UI_STACK_OVERLAY_MISSING";
        public const string MissingBaseData = "CAM_UI_STACK_BASE_DATA_MISSING";
        public const string MissingOverlayData = "CAM_UI_STACK_OVERLAY_DATA_MISSING";
        public const string StackUnavailable = "CAM_UI_STACK_UNAVAILABLE";
        public const string InvalidBaseRenderType = "CAM_UI_STACK_BASE_NOT_BASE";
        public const string InvalidOverlayRenderType = "CAM_UI_STACK_OVERLAY_NOT_OVERLAY";
    }

    public static class MxCameraUrpOverlayStackBinder
    {
        public static MxCameraUrpStackResult Bind(
            UnityCamera baseCamera,
            UnityCamera overlayCamera,
            bool setRenderTypes = true)
        {
            MxCameraUrpStackResult validation = ValidateInputs(baseCamera, overlayCamera, out UniversalAdditionalCameraData baseData, out UniversalAdditionalCameraData overlayData);
            if (!validation.Success)
                return validation;

            if (setRenderTypes)
            {
                baseData.renderType = CameraRenderType.Base;
                overlayData.renderType = CameraRenderType.Overlay;
            }
            else
            {
                if (baseData.renderType != CameraRenderType.Base)
                    return MxCameraUrpStackResult.Failed(MxCameraUrpDiagnosticCodes.InvalidBaseRenderType, "Base camera render type must be Base.");
                if (overlayData.renderType != CameraRenderType.Overlay)
                    return MxCameraUrpStackResult.Failed(MxCameraUrpDiagnosticCodes.InvalidOverlayRenderType, "Overlay camera render type must be Overlay.");
            }

            List<UnityCamera> stack = baseData.cameraStack;
            if (stack == null)
                return MxCameraUrpStackResult.Failed(MxCameraUrpDiagnosticCodes.StackUnavailable, "URP camera stack is unavailable for this base camera.");

            RemoveNullsAndDuplicates(stack, overlayCamera);
            stack.Add(overlayCamera);
            return MxCameraUrpStackResult.Ok(stack.Count);
        }

        public static MxCameraUrpStackResult Unbind(UnityCamera baseCamera, UnityCamera overlayCamera)
        {
            MxCameraUrpStackResult validation = ValidateInputs(baseCamera, overlayCamera, out UniversalAdditionalCameraData baseData, out _);
            if (!validation.Success)
                return validation;

            List<UnityCamera> stack = baseData.cameraStack;
            if (stack == null)
                return MxCameraUrpStackResult.Failed(MxCameraUrpDiagnosticCodes.StackUnavailable, "URP camera stack is unavailable for this base camera.");

            for (int i = stack.Count - 1; i >= 0; i--)
            {
                UnityCamera candidate = stack[i];
                if (candidate == null || candidate == overlayCamera)
                    stack.RemoveAt(i);
            }

            return MxCameraUrpStackResult.Ok(stack.Count);
        }

        public static MxCameraUrpStackResult ValidateBound(UnityCamera baseCamera, UnityCamera overlayCamera)
        {
            MxCameraUrpStackResult validation = ValidateInputs(baseCamera, overlayCamera, out UniversalAdditionalCameraData baseData, out UniversalAdditionalCameraData overlayData);
            if (!validation.Success)
                return validation;

            if (baseData.renderType != CameraRenderType.Base)
                return MxCameraUrpStackResult.Failed(MxCameraUrpDiagnosticCodes.InvalidBaseRenderType, "Base camera render type must be Base.");
            if (overlayData.renderType != CameraRenderType.Overlay)
                return MxCameraUrpStackResult.Failed(MxCameraUrpDiagnosticCodes.InvalidOverlayRenderType, "Overlay camera render type must be Overlay.");

            List<UnityCamera> stack = baseData.cameraStack;
            if (stack == null)
                return MxCameraUrpStackResult.Failed(MxCameraUrpDiagnosticCodes.StackUnavailable, "URP camera stack is unavailable for this base camera.");

            bool found = false;
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                UnityCamera candidate = stack[i];
                if (candidate == null)
                    continue;

                if (candidate == overlayCamera)
                    found = true;
            }

            return found
                ? MxCameraUrpStackResult.Ok(stack.Count)
                : MxCameraUrpStackResult.Failed(MxCameraUrpDiagnosticCodes.MissingOverlayCamera, "Overlay camera is not in the base camera stack.", stack.Count);
        }

        private static MxCameraUrpStackResult ValidateInputs(
            UnityCamera baseCamera,
            UnityCamera overlayCamera,
            out UniversalAdditionalCameraData baseData,
            out UniversalAdditionalCameraData overlayData)
        {
            baseData = null;
            overlayData = null;
            if (baseCamera == null)
                return MxCameraUrpStackResult.Failed(MxCameraUrpDiagnosticCodes.MissingBaseCamera, "Base camera is missing.");
            if (overlayCamera == null)
                return MxCameraUrpStackResult.Failed(MxCameraUrpDiagnosticCodes.MissingOverlayCamera, "Overlay camera is missing.");

            baseData = baseCamera.GetUniversalAdditionalCameraData();
            overlayData = overlayCamera.GetUniversalAdditionalCameraData();
            if (baseData == null)
                return MxCameraUrpStackResult.Failed(MxCameraUrpDiagnosticCodes.MissingBaseData, "Base camera URP data is missing.");
            if (overlayData == null)
                return MxCameraUrpStackResult.Failed(MxCameraUrpDiagnosticCodes.MissingOverlayData, "Overlay camera URP data is missing.");

            return MxCameraUrpStackResult.Ok(0);
        }

        private static void RemoveNullsAndDuplicates(List<UnityCamera> stack, UnityCamera overlayCamera)
        {
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                UnityCamera candidate = stack[i];
                if (candidate == null || candidate == overlayCamera)
                    stack.RemoveAt(i);
            }
        }
    }
}
