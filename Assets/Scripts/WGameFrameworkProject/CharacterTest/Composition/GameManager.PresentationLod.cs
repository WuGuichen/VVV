using System;
using System.Collections.Generic;
using MxFramework.Rendering;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class GameManager
    {
        private CharacterTestPresentationLod _boundPresentationLod;
        private MxSceneRenderingSession _lodRenderingSession;
        private MxSceneRenderingConfiguration _lodRenderingConfiguration;
        private Func<Renderer, bool> _lodShadowDemand;

        private void PreparePresentationLod()
        {
            var lod = _context?.Slice?.Services.Modules.CharacterRuntime?.ViewBridge.PresentationLod;
            PrepareShallowWaterPresentation(lod);
            if (!ReferenceEquals(lod, _boundPresentationLod) || !ReferenceEquals(_sceneRenderingSession, _lodRenderingSession)
                || !ReferenceEquals(_sceneRendering, _lodRenderingConfiguration))
            {
                _boundPresentationLod?.BindShadowPoseDemand(null);
                if (_lodRenderingConfiguration != null) _lodRenderingConfiguration.ReadDirectionalShadowPosePublication = null;
                _boundPresentationLod = lod; _lodRenderingSession = _sceneRenderingSession; _lodRenderingConfiguration = _sceneRendering;
                _lodShadowDemand = _lodRenderingSession != null ? _lodRenderingSession.NeedsCurrentDirectionalShadowPose : (Func<Renderer, bool>)null;
                lod?.BindShadowPoseDemand(_lodShadowDemand);
                if (lod != null && _sceneRendering != null)
                {
                    _sceneRendering.ReadDirectionalShadowPosePublication = lod.ReadPosePublication;
                    _sceneRendering.CollectDirectionalShadowReceiverCameras = CollectLodShadowReceivers;
                }
            }
            _sceneRenderingSession?.PrepareDirectionalShadowPoseDemand();
        }

        private bool CollectLodShadowReceivers(List<UnityEngine.Camera> cameras)
        {
            if (_boundPresentationLod == null || !_boundPresentationLod.CollectShadowReceiverCameras(cameras)) return false;
            if (_sceneRendering != null && _sceneRendering.PreviewDirectionalShadowsInSceneView && Application.isEditor)
                return CharacterTestDirectionalShadowPolicy.CollectPreviewReceiverCameras != null
                    && CharacterTestDirectionalShadowPolicy.CollectPreviewReceiverCameras(cameras);
            return true;
        }
    }
}
