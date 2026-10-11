using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.Rendering;
using MxFramework.Rendering.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Rendering
{
    public sealed class SceneLightingAutomaticProbeTests
    {
        private static Vector3[] ShelteredFloor(float ceiling, Vector3 offset)
        {
            var floor = Floor().Select(p => p + Vector3.up * 2.5f + offset).ToArray();
            var roof = Floor().Select(p => p + Vector3.up * ceiling + offset).ToArray();
            for (int i = 0; i < roof.Length; i += 3) { var swap = roof[i+1]; roof[i+1] = roof[i+2]; roof[i+2] = swap; }
            return floor.Concat(roof).ToArray();
        }
        private static float SupportRisk(Vector3[] triangles, Vector3 offset)
        {
            var demand = new MxSceneLightingProbeDemand(triangles);
            var method = typeof(MxSceneLightingProbeDemand).GetMethod("ReceiverSupportRisk",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (float)method.Invoke(demand, new object[] { new Bounds(new Vector3(4,6,4)+offset,Vector3.one*8),
                new Vector3(4,2.5f,4)+offset, Vector3.up, .15f });
        }
        [Test] public void ShelteredFlatReceiverDetectsUsefulNearFineSupport()
        {
            var floor = Floor().Select(p => p + Vector3.up * 2.5f).ToArray();
            Assert.AreEqual(0,SupportRisk(floor,Vector3.zero));
            Assert.Greater(SupportRisk(ShelteredFloor(7.5f,Vector3.zero),Vector3.zero),.5f);
        }
        [Test] public void ReceiverSupportRiskIsInvariantToWorldTranslation()
        {
            var offset = new Vector3(123,-17,81);
            Assert.AreEqual(SupportRisk(ShelteredFloor(7.5f,Vector3.zero),Vector3.zero),
                SupportRisk(ShelteredFloor(7.5f,offset),offset),.00001f);
        }
        [Test] public void ReceiverSupportRiskDoesNotRewardAnAlsoOccludedFineLattice()
            => Assert.AreEqual(0,SupportRisk(ShelteredFloor(3.5f,Vector3.zero),Vector3.zero));

        private static MxSceneLightingProbeQuality Quality(int budget=2000)=>new MxSceneLightingProbeQuality
        {Enabled=true,AutomaticRefinement=true,AutomaticNavigation=false,MaximumProbeCount=budget,AutomaticBudgetFraction=1};
        private static MxSceneLightingProbeGrid Grid()=>MxSceneLightingBuilder.ProbeGrid(new Bounds(Vector3.zero,Vector3.one*8),4);
        private static Vector3[] Floor()=>new[]{new Vector3(-20,0,-20),new Vector3(-20,0,20),new Vector3(20,0,20),
            new Vector3(-20,0,-20),new Vector3(20,0,20),new Vector3(20,0,-20)};
        private static Vector3[] Wall()=>new[]{new Vector3(.25f,-20,-20),new Vector3(.25f,20,-20),new Vector3(.25f,20,20),
            new Vector3(.25f,-20,-20),new Vector3(.25f,20,20),new Vector3(.25f,-20,20)};

        [Test] public void NoDemandKeepsCompleteCoarseGrid()
        {
            var grid=Grid();var layout=MxSceneLightingProbeLayout.Create(grid,Quality(),new MxSceneLightingProbeDemand(null));
            Assert.AreEqual(grid.Count,layout.Positions.Length);Assert.AreEqual(0,layout.AutomaticBricks);
            layout.Topology.Validate(layout.Grid.Dimensions,layout.Positions.Length);
        }
        [Test] public void GeometricCornerScoresAboveOpenPlanarReceiver()
        {
            var quality=Quality();var cell=new Bounds(new Vector3(1,1,1),Vector3.one*4);
            float open=new MxSceneLightingProbeDemand(Floor()).Score(cell,quality);
            float complex=new MxSceneLightingProbeDemand(Floor().Concat(Wall()).ToArray()).Score(cell,quality);
            Assert.Greater(complex,open);Assert.Greater(complex,quality.MinimumAutomaticScore);
        }
        [Test] public void GeometryAloneWithoutReceiverActivityOrMediumDoesNotRefine()
        {
            var quality=Quality();quality.AutomaticReceivers=false;
            var layout=MxSceneLightingProbeLayout.Create(Grid(),quality,new MxSceneLightingProbeDemand(Floor().Concat(Wall()).ToArray()));
            Assert.AreEqual(0,layout.AutomaticBricks);
        }
        [Test] public void ThinWallSelectsCellsOnBothSidesWithoutRemovingBaseNodes()
        {
            var grid=Grid();var layout=MxSceneLightingProbeLayout.Create(grid,Quality(),new MxSceneLightingProbeDemand(Wall()));
            var dims=layout.Topology.CellDimensions;bool left=false,right=false;
            for(int i=0;i<layout.CellKinds.Length;i++)if(layout.CellKinds[i]==2)
            {float x=grid.Origin.x+(i%dims.x+.5f)*grid.Spacing;left|=x<.25f;right|=x>.25f;}
            Assert.IsTrue(left&&right);for(int i=0;i<grid.Count;i++)Assert.AreEqual(grid.Position(i),layout.Positions[i]);
            Assert.IsFalse(new MxSceneLightingProbeGeometry(Wall()).SegmentClear(new Vector3(-1,0,0),new Vector3(1,0,0)));
        }
        [Test] public void AuthoredActivityCanRefineWithoutStaticReceivers()
        {
            var quality=Quality();quality.ActivityVolumes=new[]{new Bounds(Vector3.zero,Vector3.one*3)};
            var layout=MxSceneLightingProbeLayout.Create(Grid(),quality,new MxSceneLightingProbeDemand(null));
            Assert.Greater(layout.AutomaticBricks,0);
            quality.ActivityVolumes=Array.Empty<Bounds>();Assert.AreEqual(0,MxSceneLightingProbeLayout.Create(Grid(),quality,new MxSceneLightingProbeDemand(null)).AutomaticBricks);
        }
        [Test] public void NavigationActivityIsOptInAndDoesNotUseCameraOrActors()
        {
            var quality=Quality();var demand=new MxSceneLightingProbeDemand(null,new[]{new Bounds(Vector3.zero,Vector3.one*3)});
            Assert.AreEqual(0,MxSceneLightingProbeLayout.Create(Grid(),quality,demand).AutomaticBricks);
            quality.AutomaticNavigation=true;Assert.Greater(MxSceneLightingProbeLayout.Create(Grid(),quality,demand).AutomaticBricks,0);
        }
        [Test] public void MediumDemandHasPositiveAndDisabledNegativeCases()
        {
            var quality=Quality();quality.MediumDemandEnabled=true;quality.MediumDensity=.3f;quality.MediumHeightFalloff=.2f;
            var demand=new MxSceneLightingProbeDemand(null);
            Assert.Greater(MxSceneLightingProbeLayout.Create(Grid(),quality,demand).AutomaticBricks,0);
            quality.MediumDemandEnabled=false;Assert.AreEqual(0,MxSceneLightingProbeLayout.Create(Grid(),quality,demand).AutomaticBricks);
        }
        [Test] public void ExclusionOverridesManualAndAutomaticButKeepsBaseCoverage()
        {
            var grid=Grid();var quality=Quality();quality.LocalRefinementVolumes=new[]{grid.GridBounds};quality.RefinementExclusionVolumes=new[]{grid.GridBounds};
            var layout=MxSceneLightingProbeLayout.Create(grid,quality,new MxSceneLightingProbeDemand(Floor()));
            Assert.AreEqual(grid.Count,layout.Positions.Length);Assert.AreEqual(0,layout.Topology.BrickCount);
        }
        [Test] public void ManualRegionsWinLimitedBudgetBeforeAutomaticCandidates()
        {
            var grid=Grid();var quality=Quality((int)grid.Count+20);
            quality.LocalRefinementVolumes=new[]{new Bounds(new Vector3(-6,-6,-6),Vector3.one*.2f)};
            quality.ActivityVolumes=new[]{grid.GridBounds};
            var layout=MxSceneLightingProbeLayout.Create(grid,quality,new MxSceneLightingProbeDemand(null));
            Assert.AreEqual(1,layout.ManualBricks);Assert.AreEqual(0,layout.AutomaticBricks);
            Assert.LessOrEqual(layout.Positions.Length,quality.MaximumProbeCount);Assert.Greater(layout.RejectedForBudget,0);
        }
        [Test] public void BaseBudgetShortageCoarsensWithoutTruncatingStencil()
        {
            var grid=MxSceneLightingBuilder.ProbeGrid(new Bounds(Vector3.zero,Vector3.one*50),2);
            var quality=Quality(100);var layout=MxSceneLightingProbeLayout.Create(grid,quality,new MxSceneLightingProbeDemand(null));
            Assert.LessOrEqual(layout.Positions.Length,100);Assert.Greater(layout.Grid.Spacing,grid.Spacing);
            Assert.AreEqual(layout.Grid.Count,layout.Positions.Length);layout.Topology.Validate(layout.Grid.Dimensions,layout.Positions.Length);
            Assert.IsTrue(layout.Grid.GridBounds.Contains(new Vector3(25,25,25)));Assert.IsTrue(layout.Grid.GridBounds.Contains(new Vector3(-25,-25,-25)));
        }
        [Test] public void ExactBaseBudgetProducesStableGracefulDegradation()
        {
            var grid=Grid();var quality=Quality((int)grid.Count);quality.ActivityVolumes=new[]{grid.GridBounds};
            var layout=MxSceneLightingProbeLayout.Create(grid,quality,new MxSceneLightingProbeDemand(null));
            Assert.AreEqual(grid.Count,layout.Positions.Length);Assert.Greater(layout.RejectedForBudget,0);
        }
        [Test] public void RepeatedPlansShareStablePositionsTopologyAndSelectedCells()
        {
            var quality=Quality(600);var demand=new MxSceneLightingProbeDemand(Floor().Concat(Wall()).ToArray());
            var first=MxSceneLightingProbeLayout.Create(Grid(),quality,demand);var second=MxSceneLightingProbeLayout.Create(Grid(),quality,demand);
            CollectionAssert.AreEqual(first.Positions,second.Positions);CollectionAssert.AreEqual(first.Topology.BrickIndices,second.Topology.BrickIndices);
            CollectionAssert.AreEqual(first.CellKinds,second.CellKinds);
        }
        [Test] public void OverlappingManualVolumesShareFinePointsRatherThanStackDensity()
        {
            var quality=Quality();var bounds=new Bounds(Vector3.zero,Vector3.one*3);quality.LocalRefinementVolumes=new[]{bounds};
            var first=MxSceneLightingProbeLayout.Create(Grid(),quality,new MxSceneLightingProbeDemand(null));quality.LocalRefinementVolumes=new[]{bounds,bounds};
            var second=MxSceneLightingProbeLayout.Create(Grid(),quality,new MxSceneLightingProbeDemand(null));
            CollectionAssert.AreEqual(first.Positions,second.Positions);Assert.AreEqual(first.Topology.BrickCount,second.Topology.BrickCount);
        }
        [Test] public void ManualOverridesCanBeDisabledWithoutDeletingTheirDefinitions()
        {
            var quality=Quality();quality.LocalRefinementVolumes=new[]{Grid().GridBounds};quality.UseManualRefinement=false;
            var layout=MxSceneLightingProbeLayout.Create(Grid(),quality,new MxSceneLightingProbeDemand(null));
            Assert.AreEqual(0,layout.ManualBricks);Assert.AreEqual(1,quality.LocalRefinementVolumes.Length);
        }
        [Test] public void DisabledAutomaticModePreservesLegacyManualLayout()
        {
            var quality=Quality();quality.AutomaticRefinement=false;quality.LocalRefinementVolumes=new[]{new Bounds(Vector3.zero,Vector3.one*3)};
            var first=MxSceneLightingProbeLayout.Create(Grid(),quality);quality.ActivityVolumes=new[]{Grid().GridBounds};quality.MediumDemandEnabled=true;quality.MediumDensity=4;
            var second=MxSceneLightingProbeLayout.Create(Grid(),quality);CollectionAssert.AreEqual(first.Positions,second.Positions);
        }
        [Test] public void MediumAndAlgorithmControlsInvalidateAutomaticCacheIdentity()
        {
            var configuration=new MxSceneRenderingConfiguration {ProbeQuality=Quality(),HeightFogEnabled=true};
            var first=MxSceneLightingBuilder.ProbeSettingsFingerprint(configuration);configuration.FogDensity+=.1f;
            Assert.AreNotEqual(first,MxSceneLightingBuilder.ProbeSettingsFingerprint(configuration));
            configuration.ProbeQuality.MinimumAutomaticScore+=1;Assert.AreNotEqual(first,MxSceneLightingBuilder.ProbeSettingsFingerprint(configuration));
        }
        [Test] public void LegacyCacheIdentityIgnoresNewDisabledAutomaticDefaults()
        {
            var configuration=new MxSceneRenderingConfiguration {ProbeQuality=Quality()};configuration.ProbeQuality.AutomaticRefinement=false;
            string first=MxSceneLightingBuilder.ProbeSettingsFingerprint(configuration);configuration.ProbeQuality.AutomaticBudgetFraction=.6f;configuration.HeightFogEnabled=true;
            Assert.AreEqual(first,MxSceneLightingBuilder.ProbeSettingsFingerprint(configuration));
        }
        [Test] public void MalformedDemandVolumeIsRejectedBeforeAllocationOfRefinedPoints()
        {
            var quality=Quality();quality.ActivityVolumes=new[]{new Bounds(new Vector3(float.NaN,0,0),Vector3.one)};
            Assert.Throws<ArgumentException>(()=>MxSceneLightingProbeLayout.Create(Grid(),quality,new MxSceneLightingProbeDemand(null)));
        }
    }
}
