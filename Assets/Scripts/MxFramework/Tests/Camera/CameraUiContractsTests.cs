using MxFramework.Camera.Unity;
using NUnit.Framework;

namespace MxFramework.Tests.Camera
{
    public sealed class CameraUiContractsTests
    {
        [Test]
        public void LayerPolicy_ValidOverlayMasks_Succeeds()
        {
            const int uiLayer = 12;
            int uiMask = MxUiCameraLayerPolicy.MakeLayerMask(uiLayer);
            int mainMask = ~uiMask;

            MxUiCameraLayerPolicyResult result = MxUiCameraLayerPolicy.ValidateOverlayMasks(uiLayer, mainMask, uiMask);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.MainExcludesUiLayer);
            Assert.IsTrue(result.UiIncludesUiLayer);
            Assert.IsTrue(result.UiOnlyUiLayer);
            Assert.AreEqual(string.Empty, result.Code);
        }

        [Test]
        public void LayerPolicy_MainCameraIncludesUiLayer_Fails()
        {
            const int uiLayer = 12;
            int uiMask = MxUiCameraLayerPolicy.MakeLayerMask(uiLayer);

            MxUiCameraLayerPolicyResult result = MxUiCameraLayerPolicy.ValidateOverlayMasks(uiLayer, uiMask, uiMask);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MxUiCameraLayerPolicy.MainCameraIncludesUiLayerCode, result.Code);
            Assert.IsFalse(result.MainExcludesUiLayer);
        }

        [Test]
        public void LayerPolicy_UiCameraExcludesUiLayer_Fails()
        {
            const int uiLayer = 12;
            int uiMask = MxUiCameraLayerPolicy.MakeLayerMask(uiLayer);
            int mainMask = ~uiMask;

            MxUiCameraLayerPolicyResult result = MxUiCameraLayerPolicy.ValidateOverlayMasks(uiLayer, mainMask, 0);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MxUiCameraLayerPolicy.UiCameraExcludesUiLayerCode, result.Code);
            Assert.IsFalse(result.UiIncludesUiLayer);
        }

        [Test]
        public void LayerPolicy_UiCameraIncludesExtraLayers_FailsWhenStrict()
        {
            const int uiLayer = 12;
            int uiMask = MxUiCameraLayerPolicy.MakeLayerMask(uiLayer);
            int extraMask = MxUiCameraLayerPolicy.MakeLayerMask(13);
            int mainMask = ~(uiMask | extraMask);

            MxUiCameraLayerPolicyResult result = MxUiCameraLayerPolicy.ValidateOverlayMasks(uiLayer, mainMask, uiMask | extraMask);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MxUiCameraLayerPolicy.UiCameraIncludesExtraLayersCode, result.Code);
            Assert.IsFalse(result.UiOnlyUiLayer);
        }

        [Test]
        public void DebugSummary_StoresUiCameraState()
        {
            var summary = new MxUiCameraDebugSummary(
                "ui.presentation",
                MxUiCameraRigKind.Overlay3D,
                true,
                true,
                true,
                MxUiCameraLayerPolicy.DefaultOverlayLayerName,
                false,
                string.Empty,
                string.Empty);

            Assert.AreEqual("ui.presentation", summary.RigId);
            Assert.AreEqual(MxUiCameraRigKind.Overlay3D, summary.Kind);
            Assert.IsTrue(summary.Available);
            Assert.IsTrue(summary.StackBound);
            Assert.IsTrue(summary.LayerPolicyValid);
            Assert.IsFalse(summary.TargetTextureAssigned);
        }
    }
}
