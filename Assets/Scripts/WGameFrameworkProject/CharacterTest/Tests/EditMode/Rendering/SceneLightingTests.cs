using System;
using System.IO;
using MxFramework.Rendering;
using MxFramework.Rendering.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Rendering.Universal;

namespace MxFramework.Tests.Rendering
{
    public sealed class SceneLightingTests
    {
        [Test] public void ProbeRelocationRecapturesFinalPositionsWithoutWritingAnySceneOrCache()
        {
            var root=new GameObject("Owned probe relocation fixture");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            IDisposable job=null;
            try
            {
                wall.transform.SetParent(root.transform);wall.transform.position=new Vector3(0,1,0);wall.transform.localScale=new Vector3(.1f,2,4);
                material.SetColor("_BaseColor",Color.white);wall.GetComponent<MeshRenderer>().sharedMaterial=material;
                var layout=new MxSceneLightingSourceLayout {BaseColorProperty="_ProbeFixtureTextureAbsent",SurfaceDataProperty="_ProbeFixtureDataAbsent",SolidColorProperty="_BaseColor"};
                var quality=new MxSceneLightingProbeQuality {Enabled=true,RelocateNearGeometry=true,MinimumClearance=.15f};
                var type=typeof(MxSceneLightingBuilder).GetNestedType("BuildJob",System.Reflection.BindingFlags.NonPublic);
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                job=(IDisposable)Activator.CreateInstance(type,new object[]{root.transform,"Assets/__ProbeQualityUnused",layout,null,3f,4f,
                    (Bounds?)new Bounds(new Vector3(4,3,4),new Vector3(8,4,8)),quality});
                type.GetMethod("Prepare",flags).Invoke(job,null);
                var positions=(MxSceneLightingProbeLayout)type.GetField("_probeLayout",flags).GetValue(job);
                // Find a lattice point exactly on the wall's midplane at y=1.
                int id=Array.FindIndex(positions.Positions,p=>Mathf.Abs(p.x)<.001f && Mathf.Abs(p.y-1)<.001f && Mathf.Abs(p.z)<.001f);
                Assert.GreaterOrEqual(id,0);
                type.GetMethod("BakeProbe",flags).Invoke(job,new object[]{id});
                var payload=(MxSceneLightingPayload)type.GetField("_payload",flags).GetValue(job);
                Vector3 final=payload.Probes[id].PositionValid;
                Assert.Greater(payload.ProbeQuality[id].x,.15f);Assert.GreaterOrEqual(payload.ProbeQuality[id].y,.15f);
                Assert.AreEqual(1,payload.Probes[id].PositionValid.w);
                Assert.AreNotEqual(positions.Positions[id],final);
                float nearest=payload.ProbeClearDistances.Skip(id*96).Take(96).Min();
                Assert.Greater(nearest,.1f,"Fresh final-position capture must replace the interior near-plane distances.");
                Assert.IsFalse(UnityEditor.AssetDatabase.IsValidFolder("Assets/__ProbeQualityUnused"));
            }
            finally {job?.Dispose();UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(material);}
        }

        [Test] public void ProbeGeometryRejectsThinWallsButPreservesDoorwayVisibilityAndNearestClearance()
        {
            // Two separated wall panels leave a real doorway at z in (-1,1).
            var vertices=new List<Vector3>();
            void Panel(float z0,float z1){vertices.AddRange(new[]{new Vector3(0,-2,z0),new Vector3(0,2,z0),new Vector3(0,2,z1),
                new Vector3(0,-2,z0),new Vector3(0,2,z1),new Vector3(0,-2,z1)});}
            Panel(-4,-1);Panel(1,4);var geometry=new MxSceneLightingProbeGeometry(vertices);
            Assert.IsTrue(geometry.SegmentClear(new Vector3(-2,0,0),new Vector3(2,0,0)));
            Assert.IsFalse(geometry.SegmentClear(new Vector3(-2,0,2),new Vector3(2,0,2)));
            Assert.IsTrue(geometry.Nearest(new Vector3(.05f,0,2),.2f,out var closest,out _,out float distance));
            Assert.AreEqual(.05f,distance,.00001f);Assert.That(Vector3.Distance(new Vector3(0,0,2),closest),Is.LessThan(.00001f));
            Assert.IsFalse(geometry.Nearest(new Vector3(0,0,0),.2f,out _,out _,out _));
            var payload=new MxSceneLightingPayload {Version=2,Probes=new[]{new MxSceneLightingProbe {PositionValid=new Vector4(-2,0,0,0)},new MxSceneLightingProbe {PositionValid=new Vector4(2,0,0,1)}},
                ProbeQuality=new Vector4[2],ProbeClearDistances=Enumerable.Repeat(10f,192).ToArray()};
            Assert.IsTrue(MxSceneLightingBuilder.VisibleDilationSegment(payload,0,1,geometry));
            payload.Probes[0].PositionValid.z=2;payload.Probes[1].PositionValid.z=2;
            Assert.IsFalse(MxSceneLightingBuilder.VisibleDilationSegment(payload,0,1,geometry));
            payload.Probes[0].PositionValid.z=0;payload.Probes[1].PositionValid.z=0;
            Array.Fill(payload.ProbeClearDistances,1f);
            Assert.IsFalse(MxSceneLightingBuilder.VisibleDilationSegment(payload,0,1,geometry));
        }

        [Test] public void LocalProbeBricksShareIdsAndKeepTheirCorrectInterpolationCoordinates()
        {
            var grid=new MxSceneLightingProbeGrid(default,Vector3.zero,new Vector3Int(3,3,3),4);
            var quality=new MxSceneLightingProbeQuality {Enabled=true,LocalRefinementVolumes=new[]{
                new Bounds(new Vector3(2,2,2),Vector3.one*2),new Bounds(new Vector3(6,2,2),Vector3.one*2)}};
            var layout=MxSceneLightingProbeLayout.Create(grid,quality);
            Assert.AreEqual(2,layout.Topology.BrickCount);
            Assert.AreEqual(layout.Positions.Length,layout.Positions.Distinct().Count());
            for(int id=0;id<grid.Count;id++)Assert.AreEqual(grid.Position(id),layout.Positions[id]);
            for(int brick=0;brick<2;brick++)for(int z=0;z<3;z++)for(int y=0;y<3;y++)for(int x=0;x<3;x++)
            {
                int id=layout.Topology.BrickIndices[brick*27+x+3*(y+3*z)];
                Assert.AreEqual(new Vector3(brick*4+x*2,y*2,z*2),layout.Positions[id]);
            }
            for(int z=0;z<3;z++)for(int y=0;y<3;y++)Assert.AreEqual(
                layout.Topology.BrickIndices[2+3*(y+3*z)],layout.Topology.BrickIndices[27+3*(y+3*z)]);
            int first=layout.Topology.RefinedCorner(new Vector3(.25f,.25f,.25f),0,out var fraction,out float blend);
            Assert.AreEqual(Vector3.one*.5f,fraction);Assert.AreEqual(1,blend);Assert.AreEqual(Vector3.zero,layout.Positions[first]);
            layout.Topology.RefinedCorner(new Vector3(1,.25f,.25f),0,out _,out blend);Assert.AreEqual(0,blend);
            layout.Topology.RefinedCorner(new Vector3(.9999f,.25f,.25f),0,out _,out blend);Assert.Less(blend,.00001f);
            quality.MaximumProbeCount=100;quality.LocalRefinementVolumes=new[]{new Bounds(Vector3.one*4,Vector3.one*8)};
            Assert.Throws<InvalidOperationException>(()=>MxSceneLightingProbeLayout.Create(grid,quality));
        }

        [Test] public void ProbeV2RoundTripRejectsInvalidTopologyAndUnknownVersions()
        {
            var p=Fixture();p.Version=2;
            p.Probes=Enumerable.Repeat(p.Probes[0],8).ToArray();p.ProbeRows=new MxSceneLightingRow[8];
            p.ProbeVisibility=Enumerable.Repeat(new Vector2(2,4),8*96).ToArray();
            p.Topology=new MxSceneLightingProbeTopology {CellDimensions=Vector3Int.one,CellBricks=new[]{-1}};
            p.ProbeQuality=new Vector4[8];p.ProbeClearDistances=Enumerable.Repeat(2f,8*96).ToArray();
            p.DilationRows=new MxSceneLightingRow[8];p.DilationLinks=Array.Empty<MxSceneLightingEdge>();
            byte[] bytes=p.Write();var read=MxSceneLightingPayload.Read(bytes);
            Assert.AreEqual(2,read.Version);Assert.AreEqual(Vector3Int.one,read.Topology.CellDimensions);
            CollectionAssert.AreEqual(p.ProbeClearDistances,read.ProbeClearDistances);
            byte[] unknown=(byte[])bytes.Clone();Array.Copy(BitConverter.GetBytes(999),0,unknown,4,4);
            Assert.Throws<InvalidDataException>(()=>MxSceneLightingPayload.Read(unknown));
            Array.Resize(ref bytes,bytes.Length-4);Assert.Throws<InvalidDataException>(()=>MxSceneLightingPayload.Read(bytes));
            p.Topology.CellBricks[0]=0;Assert.Throws<InvalidDataException>(()=>p.Write());
            Assert.AreEqual(1,MxSceneLightingPayload.Read(Fixture().Write()).Version);
        }

        [Test] public void IndependentProbeCoveragePreservesCaptureGeometryAndDefaultSpacing()
        {
            var capture=new Bounds(new Vector3(2,3,4),Vector3.one*2);
            var coverage=new Bounds(new Vector3(40,12,-20),new Vector3(24,16,32));
            var grid=MxSceneLightingBuilder.ProbeGrid(capture,4,coverage);
            Assert.AreEqual(capture,grid.CaptureBounds);
            Assert.AreEqual(coverage.min-Vector3.one*4,grid.Origin);
            Assert.AreEqual(new Vector3Int(9,7,11),grid.Dimensions);
            Assert.IsTrue(grid.GridBounds.Contains(coverage.min));
            Assert.IsTrue(grid.GridBounds.Contains(coverage.max));
            Assert.AreEqual(4,new MxSceneRenderingConfiguration().ProbeSpacing);
            Assert.Throws<ArgumentException>(()=>MxSceneLightingBuilder.ProbeGrid(capture,4,new Bounds(Vector3.zero,Vector3.zero)));
        }

        [Test] public void CoverageChangeInvalidatesCacheWithoutChangingGeometryFingerprint()
        {
            var root=new GameObject("Coverage fixture");var data=ScriptableObject.CreateInstance<MxSceneLightingData>();
            try
            {
                var c=new MxSceneRenderingConfiguration {StaticRoot=root.transform,Data=data};
                string geometry=MxSceneLightingBuilder.Fingerprint(root.transform,c.SourceLayout);
                data.Initialize(null,null,geometry,default,Vector3.zero,Vector3Int.one,4,0,0,1,0);
                Assert.IsTrue(MxSceneLightingBuilder.CacheMatchesSource(c,out _));
                c.UseProbeCoverageBounds=true;
                Assert.IsFalse(MxSceneLightingBuilder.CacheMatchesSource(c,out string reason));
                Assert.That(reason,Does.Contain("coverage"));
                string settings=MxSceneLightingBuilder.ProbeSettingsFingerprint(c);
                data.Initialize(null,null,geometry,default,Vector3.zero,Vector3Int.one,4,0,0,1,0,settings);
                Assert.IsTrue(MxSceneLightingBuilder.CacheMatchesSource(c,out _));
                c.ProbeCoverageBounds=new Bounds(c.ProbeCoverageBounds.center+Vector3.right,c.ProbeCoverageBounds.size);
                Assert.IsFalse(MxSceneLightingBuilder.CacheMatchesSource(c,out _));
                Assert.AreEqual(geometry,MxSceneLightingBuilder.Fingerprint(root.transform,c.SourceLayout));
            }
            finally {UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(data);}
        }

        [Test] public void ProbeSliceIncludesBothFloatingPointBoundaryLayersAndReportsThinning()
        {
            var data=UnityEditor.AssetDatabase.LoadAssetAtPath<MxSceneLightingData>("Assets/Config/MxFramework/Rendering/SceneLighting/TacticalCardDemoScene/SceneLighting.asset");
            using var preview=new MxSceneLightingProbePreview();preview.Refresh(new MxSceneRenderingConfiguration {Data=data});
            var ids=new List<int>();
            float center=data.ProbeOrigin.y+data.ProbeSpacing*3.5f;
            int count=preview.CollectVisible(true,true,center+0.000001f,2,false,Vector3.zero,0,0,2000,ids);
            Assert.AreEqual(data.ProbeDimensions.x*data.ProbeDimensions.z*2,count);
            Assert.AreEqual(count/2,ids.Count);
            Assert.AreEqual(0,preview.CollectVisible(true,true,center,1.999f,false,Vector3.zero,0,0,2000,ids));
            Assert.That(preview.FilterDiagnostics,Does.Contain("高度 0"));
            Assert.That(preview.CacheDiagnostics,Does.Contain("边界层有效"));
            Assert.Greater(preview.OuterValidCount,0);
            Assert.IsTrue(preview.ActualCacheBounds.Contains(preview.Position(true,0)));
        }

        [Test] public void ProbePreviewUsesCaptureTrianglesAndExistingSourceSelectionRules()
        {
            var root = new GameObject("ProbePreviewFixture");
            Mesh mesh = new Mesh(); GameObject excluded = null;
            try
            {
                // Unreferenced and degenerate far vertices must not expand the capture bounds.
                mesh.vertices = new[] { Vector3.zero, new Vector3(4,0,0), new Vector3(0,0,4),
                    new Vector3(1000,0,0), new Vector3(1001,0,0), new Vector3(1002,0,0), new Vector3(-9999,0,0) };
                mesh.triangles = new[] { 0,1,2, 3,4,5 };
                var child = new GameObject("NonStaticSource"); child.transform.SetParent(root.transform);
                child.transform.position = new Vector3(10,1,4); child.layer = 31; child.isStatic = false;
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                var source = child.AddComponent<MeshRenderer>();
                var disabled = new GameObject("Disabled"); disabled.transform.SetParent(root.transform);
                disabled.transform.position = Vector3.one * 500;
                disabled.AddComponent<MeshFilter>().sharedMesh = mesh; disabled.AddComponent<MeshRenderer>().enabled = false;
                var grid = MxSceneLightingBuilder.PlanProbeGrid(root.transform);
                Assert.AreEqual(new Vector3(10,1,4), grid.CaptureBounds.min);
                Assert.AreEqual(new Vector3(14,1,8), grid.CaptureBounds.max);
                Assert.AreEqual(new Vector3(6,-3,0), grid.Origin);
                Assert.AreEqual(new Vector3Int(4,3,4), grid.Dimensions);
                Assert.AreEqual(48, grid.Count);
                Assert.AreEqual(new Vector3(18,5,12), grid.Position(47));
                // Feed the compiler's real AddTriangle path without Prepare/capture/build/save.
                // This catches a planner mismatch at subdivision or degenerate rejection boundaries.
                Type jobType = typeof(MxSceneLightingBuilder).GetNestedType("BuildJob",System.Reflection.BindingFlags.NonPublic);
                var job = (IDisposable)Activator.CreateInstance(jobType,new object[] { root.transform,"Assets/__ProbePreviewUnused",new MxSceneLightingSourceLayout(),null,3f,4f });
                var color = new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);
                var surface = new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);
                try
                {
                    color.SetPixel(0,0,Color.white); surface.SetPixel(0,0,new Color(1,0,0,.03125f));
                    var add = jobType.GetMethod("AddTriangle",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                    for(int t=0;t<mesh.triangles.Length;t+=3)
                    {
                        int[] indices=mesh.triangles; Vector3[] vertices=mesh.vertices;
                        add.Invoke(job,new object[] {child.transform.TransformPoint(vertices[indices[t]]),
                            child.transform.TransformPoint(vertices[indices[t+1]]),child.transform.TransformPoint(vertices[indices[t+2]]),
                            Vector2.zero,Vector2.zero,Vector2.zero,color,surface,1f,0});
                    }
                    var captured=(List<Vector3>)jobType.GetField("_vertices",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(job);
                    Assert.Greater(captured.Count,0);
                    Vector3 min=captured[0],max=min;
                    foreach(var point in captured){min=Vector3.Min(min,point);max=Vector3.Max(max,point);}
                    Assert.AreEqual(grid.CaptureBounds.min,min); Assert.AreEqual(grid.CaptureBounds.max,max);
                }
                finally { job.Dispose(); UnityEngine.Object.DestroyImmediate(color); UnityEngine.Object.DestroyImmediate(surface); }
                var explicitGrid = MxSceneLightingBuilder.PlanProbeGrid(root.transform, new[] { source });
                Assert.AreEqual(grid.CaptureBounds, explicitGrid.CaptureBounds);
                excluded = new GameObject("OutsideRoot"); excluded.AddComponent<MeshFilter>().sharedMesh = mesh;
                var outside = excluded.AddComponent<MeshRenderer>();
                Assert.Throws<InvalidOperationException>(() => MxSceneLightingBuilder.PlanProbeGrid(root.transform, new[] { outside }));
                Assert.Throws<InvalidOperationException>(() => MxSceneLightingBuilder.PlanProbeGrid(root.transform, new[] { source, source }));
                child.SetActive(false);
                Assert.Throws<InvalidOperationException>(() => MxSceneLightingBuilder.PlanProbeGrid(root.transform));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(excluded); UnityEngine.Object.DestroyImmediate(mesh); }
        }

        [Test] public void ProbeGridCeilingPaddingAndLimitsStayExplicit()
        {
            Assert.AreEqual(4, new MxSceneRenderingConfiguration().ProbeSpacing);
            var grid = MxSceneLightingBuilder.ProbeGrid(new Bounds(new Vector3(0,2,0), new Vector3(9,1,7)), 4);
            Assert.AreEqual(new Vector3Int(6,4,5), grid.Dimensions);
            Assert.AreEqual(grid.GridBounds.max, grid.Position((int)grid.Count-1));
            Assert.AreEqual(1, MxSceneLightingBuilder.ProbeGrid(new Bounds(Vector3.zero, Vector3.one), .5f).Spacing);
            Assert.Throws<ArgumentOutOfRangeException>(() => MxSceneLightingBuilder.ProbeGrid(default, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => grid.Position((int)grid.Count));
            Assert.Greater(MxSceneLightingBuilder.ProbeGrid(new Bounds(Vector3.zero, Vector3.one * 1000), 4).Count, 120000);
        }

        [Test] public void RealProbeCachePreviewReusesPayloadAndDistinguishesPlanFromCache()
        {
            var data = UnityEditor.AssetDatabase.LoadAssetAtPath<MxSceneLightingData>("Assets/Config/MxFramework/Rendering/SceneLighting/TacticalCardDemoScene/SceneLighting.asset");
            var c = new MxSceneRenderingConfiguration { Data = data };
            using var preview = new MxSceneLightingProbePreview();
            preview.Refresh(c);
            Assert.IsFalse(preview.HasPlan); Assert.IsTrue(preview.HasCache);
            Assert.IsFalse(preview.CacheCurrent, "Missing authoring source must not claim current cache.");
            Assert.AreEqual(data.ProbeCount, preview.PointCount(true));
            Assert.AreEqual(0, preview.PointCount(false));
            var payload = preview.Payload;
            preview.Refresh(c);
            Assert.AreSame(payload, preview.Payload); Assert.AreEqual(1, preview.DecodeCount);
            Assert.AreEqual((Vector3)payload.Probes[0].PositionValid, preview.Position(true, 0));
            var ids = new List<int>();
            int valid = preview.CollectVisible(true, false,0,0,false,Vector3.zero,0,1,17,ids);
            Assert.AreEqual(preview.ValidCount, valid); Assert.LessOrEqual(ids.Count,17);
            Assert.IsTrue(ids.All(preview.IsValid));
            int invalid = preview.CollectVisible(true,false,0,0,false,Vector3.zero,0,2,17,ids);
            Assert.AreEqual(data.ProbeCount-preview.ValidCount,invalid); Assert.IsTrue(ids.All(i=>!preview.IsValid(i)));
            Vector3 position = preview.Position(true,0);
            int local = preview.CollectVisible(true,true,position.y,0,true,position,.01f,0,8,ids);
            Assert.GreaterOrEqual(local,1); Assert.IsTrue(ids.Contains(0));
            preview.Dispose(); Assert.IsFalse(preview.HasCache); Assert.AreEqual(0,preview.PointCount(true));
        }

        [Test] public void MissingAndCorruptProbeCacheRemainSafeAcrossRefreshes()
        {
            using var preview = new MxSceneLightingProbePreview();
            var ids = new List<int>();
            preview.Refresh(null);
            Assert.IsFalse(preview.HasCache); Assert.IsFalse(preview.HasPlan);
            Assert.AreEqual(0,preview.CollectVisible(true,false,0,0,false,Vector3.zero,0,0,1,ids));
            var data = ScriptableObject.CreateInstance<MxSceneLightingData>();
            try
            {
                preview.Refresh(new MxSceneRenderingConfiguration { Data = data });
                Assert.IsFalse(preview.HasCache); Assert.That(preview.CacheIssue,Does.Contain("缓存无法读取"));
                preview.Refresh(new MxSceneRenderingConfiguration { Data = data });
                Assert.IsFalse(preview.CacheCurrent); Assert.That(preview.CacheIssue,Does.Contain("缓存无法读取"));
            }
            finally { UnityEngine.Object.DestroyImmediate(data); }
        }

        [Test] public void ProbeCacheSpacingInvalidationPreservesLegacyFingerprint()
        {
            var root = new GameObject("Source"); var data = ScriptableObject.CreateInstance<MxSceneLightingData>();
            try
            {
                var c = new MxSceneRenderingConfiguration { StaticRoot = root.transform, Data = data };
                string fingerprint = MxSceneLightingBuilder.Fingerprint(root.transform,c.SourceLayout);
                data.Initialize(null,null,fingerprint,default,Vector3.zero,Vector3Int.one,4,0,0,1,0);
                Assert.IsTrue(MxSceneLightingBuilder.CacheMatchesSource(c,out _));
                c.ProbeSpacing = 6;
                Assert.IsFalse(MxSceneLightingBuilder.CacheMatchesSource(c,out string reason));
                Assert.That(reason,Does.Contain("spacing"));
                Assert.AreEqual(fingerprint,MxSceneLightingBuilder.Fingerprint(root.transform,c.SourceLayout));
                c.ProbeSpacing = 4;
                Assert.IsTrue(MxSceneLightingBuilder.CacheMatchesSource(c,out _));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(data); }
        }

        [Test] public void ProbeVisibilityBinRepresentativeDirectionsMatchAllPersistedFaces()
        {
            for(int bin=0;bin<96;bin++)
            {
                Vector3 d=MxSceneLightingProbePreview.VisibilityDirection(bin), a=new Vector3(Mathf.Abs(d.x),Mathf.Abs(d.y),Mathf.Abs(d.z));
                int face;Vector2 uv;
                if(a.x>=a.y && a.x>=a.z){face=d.x>=0?0:1;uv=new Vector2(d.x>=0?-d.z:d.z,d.y)/a.x;}
                else if(a.y>=a.z){face=d.y>=0?2:3;uv=new Vector2(d.x,d.y>=0?-d.z:d.z)/a.y;}
                else{face=d.z>=0?4:5;uv=new Vector2(d.z>=0?d.x:-d.x,d.y)/a.z;}
                int x=Mathf.Clamp((int)((uv.x*.5f+.5f)*4),0,3), y=Mathf.Clamp((int)((uv.y*.5f+.5f)*4),0,3);
                Assert.AreEqual(bin,face*16+y*4+x);
                Assert.That(d.magnitude,Is.EqualTo(1).Within(.00001f));
            }
        }

        private static MxSceneLightingPayload Fixture()
        {
            var visibility=new Vector2[96];for(int i=0;i<visibility.Length;i++)visibility[i]=new Vector2(2,4);
            return new MxSceneLightingPayload {SurfaceCount=2,
                Nodes=new[]{new MxSceneLightingNode {PositionArea=new Vector4(1,2,3,4),Albedo=new Vector4(.3f,.4f,.5f,0)},new MxSceneLightingNode()},
                Rows=new[]{new MxSceneLightingRow(0,1),new MxSceneLightingRow(1,0)},
                Edges=new[]{new MxSceneLightingEdge(1,.25f)},
                Probes=new[]{new MxSceneLightingProbe {PositionValid=new Vector4(0,1,0,1),SkyUp=new Vector4(0,.25f,0,.5f)}},
                ProbeRows=new[]{new MxSceneLightingRow(0,1)},
                ProbeLinks=new[]{new MxSceneLightingProbeLink {Target=0,DirectionWeight=new Vector4(.1f,.2f,.1f,.3f)}},ProbeVisibility=visibility};
        }
        [Test] public void BinaryRoundTripPreservesTransferAndVisibility()
        {
            var p=Fixture();var read=MxSceneLightingPayload.Read(p.Write());
            Assert.AreEqual(p.Nodes[0].PositionArea,read.Nodes[0].PositionArea);
            Assert.AreEqual(.25f,read.Edges[0].Weight);
            Assert.AreEqual(p.Probes[0].SkyUp,read.Probes[0].SkyUp);
            CollectionAssert.AreEqual(p.ProbeVisibility,read.ProbeVisibility);
        }
        [TestCase(-.1f)] [TestCase(1.01f)] [TestCase(float.NaN)]
        public void InvalidTransportEnergyIsRejected(float weight)
        {var p=Fixture();p.Edges[0]=new MxSceneLightingEdge(1,weight);Assert.Throws<InvalidDataException>(()=>p.Write());}
        [Test] public void TruncatedPayloadCannotAllocateOrBeAdopted()
        {byte[] bytes=Fixture().Write();Array.Resize(ref bytes,bytes.Length-8);Assert.Throws<InvalidDataException>(()=>MxSceneLightingPayload.Read(bytes));}
        [Test] public void BadTargetAndRowAreRejected()
        {
            var p=Fixture();p.Edges[0]=new MxSceneLightingEdge(2,.25f);Assert.Throws<InvalidDataException>(()=>p.Validate());
            p=Fixture();p.Rows[0]=new MxSceneLightingRow(1,4);Assert.Throws<InvalidDataException>(()=>p.Validate());
        }
        [Test] public void VisibilityShapeIsPartOfContract()
        {var p=Fixture();p.ProbeVisibility=new Vector2[95];Assert.Throws<InvalidDataException>(()=>p.Validate());}
        [Test] public void RealScenePayloadConservesEnergy()
        {
            var data=UnityEditor.AssetDatabase.LoadAssetAtPath<MxSceneLightingData>("Assets/Config/MxFramework/Rendering/SceneLighting/TacticalCardDemoScene/SceneLighting.asset");
            Assert.NotNull(data,"Build the scene lighting before acceptance.");
            var payload=data.ReadPayload();
            Assert.DoesNotThrow(()=>payload.Validate());
            Assert.Greater(payload.Edges.Length,payload.SurfaceCount,"A sky-only capture is not a transport solution.");
            Assert.Greater(data.RetainedTransport,.85f);
        }

        [Test] public void MotionInputIsOptInAndCameraRetargetPreservesWorldPasses()
        {
            var data=UnityEditor.AssetDatabase.LoadAssetAtPath<MxSceneLightingData>("Assets/Config/MxFramework/Rendering/SceneLighting/TacticalCardDemoScene/SceneLighting.asset");
            var compute=UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>("Packages/com.vincent.wgameframework/Rendering/SceneLighting.compute");
            var sunObject=new GameObject("Lighting test sun");
            var firstObject=new GameObject("Lighting test camera A");
            var secondObject=new GameObject("Lighting test camera B");
            var look=ScriptableObject.CreateInstance<MxSceneLightingLook>();
            MxSceneLightingProvider provider=null;
            try
            {
                var sun=sunObject.AddComponent<Light>();sun.type=LightType.Directional;
                var first=firstObject.AddComponent<UnityEngine.Camera>();first.enabled=false;
                var second=secondObject.AddComponent<UnityEngine.Camera>();second.enabled=false;
                provider=new MxSceneLightingProvider(data,look,sun,first,compute,
                    Shader.Find("Hidden/MxFramework/SceneLightingCapture"),Shader.Find("Hidden/MxFramework/SceneLightingResolve"),false);
                var pipeline=new MxRenderPipeline();pipeline.RegisterProvider(provider);
                var a=new MxCameraRenderContextDescriptor(MxCameraRenderKind.Game,first,Vector3.zero);
                var b=new MxCameraRenderContextDescriptor(MxCameraRenderKind.Game,second,Vector3.zero);
                var passes=pipeline.CollectPasses(a).ToArray();
                Assert.AreEqual(2,passes.Length);
                ScriptableRenderPassInput Inputs() => pipeline.CollectPasses(a).Cast<IMxRenderGraphPass>()
                    .Aggregate(ScriptableRenderPassInput.None,(mask,pass)=>mask|pass.InputRequirements);
                Assert.AreEqual(ScriptableRenderPassInput.Depth|ScriptableRenderPassInput.Normal,Inputs());
                provider.SetMotionVectorsRequired(true);
                Assert.IsTrue((Inputs()&ScriptableRenderPassInput.Motion)!=0);
                provider.SetMotionVectorsRequired(false);
                Assert.IsFalse((Inputs()&ScriptableRenderPassInput.Motion)!=0);
                look.DebugView=3;
                Assert.IsTrue((Inputs()&ScriptableRenderPassInput.Motion)!=0);
                look.DebugView=0;
                Assert.IsFalse((Inputs()&ScriptableRenderPassInput.Motion)!=0);
                long bytes=provider.PersistentBytes;
                provider.SetTargetCamera(second,false);
                Assert.AreEqual(0,pipeline.CollectPasses(a).Count);
                var switched=pipeline.CollectPasses(b).ToArray();
                CollectionAssert.AreEqual(passes,switched,"Camera switches must retain the objects owning the world cache.");
                Assert.AreEqual(bytes,provider.PersistentBytes);
                provider.SetTargetCamera(null,false);
                Assert.AreEqual(0,pipeline.CollectPasses(b).Count);
                provider.SetTargetCamera(first,false);
                CollectionAssert.AreEqual(passes,pipeline.CollectPasses(a).ToArray());
                provider.Dispose();
                Assert.AreEqual(0,pipeline.CollectPasses(a).Count);
                Assert.Throws<ObjectDisposedException>(()=>provider.SetTargetCamera(second,false));
            }
            finally
            {
                provider?.Dispose();
                UnityEngine.Object.DestroyImmediate(sunObject);UnityEngine.Object.DestroyImmediate(firstObject);
                UnityEngine.Object.DestroyImmediate(secondObject);UnityEngine.Object.DestroyImmediate(look);
            }
        }

        [Test] public void RealSceneOccludersShareExactPositionsWithoutOfflineAttributes()
        {
            var data=UnityEditor.AssetDatabase.LoadAssetAtPath<MxSceneLightingData>("Assets/Config/MxFramework/Rendering/SceneLighting/TacticalCardDemoScene/SceneLighting.asset");
            Mesh mesh=data.Occluders;
            Assert.AreEqual(mesh.vertexCount,new HashSet<Vector3>(mesh.vertices).Count);
            Assert.Less(mesh.vertexCount,mesh.GetIndexCount(0));
            Assert.IsFalse(mesh.HasVertexAttribute(VertexAttribute.Color));
            Assert.IsFalse(mesh.HasVertexAttribute(VertexAttribute.Normal));
            Assert.IsFalse(mesh.HasVertexAttribute(VertexAttribute.TexCoord0));
        }

        private struct Coefficients {public Vector4 R,G,B;}

        [TestCase(10f, 0, 1f)]
        [TestCase(.5f, 0, 0f)]
        [TestCase(.5f, 8, 0f)]
        [TestCase(.5f, 15, 0f)]
        public void ProbeResolveDistinguishesOpenBackHemisphereFromOccludedBins(
            float nearDistance, int skySamples, float expectedScale)
        {
            if (!SystemInfo.supportsComputeShaders) Assert.Ignore("Structured buffer device required.");
            var owned = new List<GraphicsBuffer>();
            RenderTexture previous = RenderTexture.active;
            int invVpId = Shader.PropertyToID("unity_MatrixInvVP");
            int viewId = Shader.PropertyToID("unity_MatrixV");
            Matrix4x4 previousInvVp = Shader.GetGlobalMatrix(invVpId), previousView = Shader.GetGlobalMatrix(viewId);
            Material material = null; RenderTexture target = null;
            Texture2D readback = null, depth = null, normals = null;
            var cmd = new CommandBuffer();
            try
            {
                // A receiver just above a floor can have only upper probes: every valid probe
                // is behind a downward-facing chin/sleeve normal, without a wall between them.
                var probes = new MxSceneLightingProbe[8];
                var coefficients = new Coefficients[8];
                var moments = new Vector2[8 * 96];
                Vector2 bin = Vector2.zero;
                for (int i = 0; i < 16; i++)
                {
                    float distance = i < skySamples ? 10 : nearDistance;
                    bin += new Vector2(distance, distance * distance) / 16;
                }
                for (int i = 0; i < 8; i++)
                {
                    int x = i & 1, y = (i >> 1) & 1, z = (i >> 2) & 1;
                    probes[i].PositionValid = new Vector4(x * 2 - 1, y * 2 - 1, z * 2 - 1, y);
                    coefficients[i] = new Coefficients
                    {
                        R = new Vector4(0, 0, 0, .2f), G = new Vector4(0, 0, 0, .4f), B = new Vector4(0, 0, 0, .6f)
                    };
                    for (int cell = 0; cell < 96; cell++) moments[i * 96 + cell] = bin;
                }
                material = new Material(Shader.Find("Hidden/MxFramework/SceneLightingResolve"));
                material.SetBuffer("_MxLightingProbes", Buffer(owned, probes, 64));
                material.SetBuffer("_MxLightingCoefficients", Buffer(owned, coefficients, 48));
                material.SetBuffer("_MxLightingVisibility", Buffer(owned, moments, 8));
                material.SetVector("_MxLightingGridOrigin", new Vector4(-1, -1, -1, .5f));
                material.SetVector("_MxLightingGridSize", new Vector4(2, 2, 2, 0));
                material.SetFloat("_MxLightingCapsuleStrength", 0);
                material.SetInt("_MxLightingCapsuleCount", 0);
                depth = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
                depth.SetPixel(0, 0, new Color(.5f, 0, 0, 0)); depth.Apply();
                normals = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
                material.SetTexture("_CameraDepthTexture", depth);
                material.SetTexture("_CameraNormalsTexture", normals);
                target = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                target.Create(); readback = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
                for (int step = -10; step <= 10; step++)
                {
                    Vector3 normal = new Vector3(step * .025f, -1, 0).normalized;
                    normals.SetPixel(0, 0, new Color(normal.x, normal.y, normal.z, 0)); normals.Apply();
                    // Map the center pixel to a known world position independently of graphics API depth conventions.
                    Matrix4x4 invVp = Matrix4x4.identity;
                    invVp.m22 = 0; invVp.m03 = step * .02f;
                    cmd.Clear(); cmd.SetRenderTarget(target); cmd.SetViewport(new Rect(0, 0, 1, 1));
                    cmd.SetGlobalMatrix(invVpId, invVp);
                    cmd.SetGlobalMatrix(viewId, Matrix4x4.Translate(Vector3.back));
                    cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);
                    cmd.SetGlobalMatrix(invVpId, previousInvVp); cmd.SetGlobalMatrix(viewId, previousView);
                    Graphics.ExecuteCommandBuffer(cmd); RenderTexture.active = target;
                    readback.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); readback.Apply();
                    Color actual = readback.GetPixel(0, 0);
                    Assert.That(actual.r, Is.EqualTo(.2f * expectedScale).Within(.0001f), "Receiver step " + step);
                    Assert.That(actual.g, Is.EqualTo(.4f * expectedScale).Within(.0001f));
                    Assert.That(actual.b, Is.EqualTo(.6f * expectedScale).Within(.0001f));
                    Assert.That(actual.a, Is.EqualTo(1).Within(.0001f), "The production resolve must preserve receiver depth.");
                }
            }
            finally
            {
                Shader.SetGlobalMatrix(invVpId, previousInvVp); Shader.SetGlobalMatrix(viewId, previousView);
                RenderTexture.active = previous; cmd.Dispose(); foreach (var buffer in owned) buffer.Dispose();
                if (target != null) target.Release();
                UnityEngine.Object.DestroyImmediate(readback); UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(depth); UnityEngine.Object.DestroyImmediate(normals);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test] public void UnifiedDiffuseWithoutCacheIgnoresStaleLookAndPreservesHdr()
        {
            const string source = @"Shader ""Hidden/MxFramework/Tests/UnifiedSceneDiffuse"" {
                SubShader { Pass { ZTest Always ZWrite Off Cull Off
                HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex Vert
                #pragma fragment Frag
                #include ""Assets/Art/WGame/Shaders/ShaderLibrary/WGameSceneLighting.hlsl""
                float4 _TestLightColor;
                float4 Vert(uint id:SV_VertexID):SV_POSITION { return GetFullScreenTriangleVertexPosition(id); }
                float4 Frag():SV_Target { return float4(WgSceneDiffuse(float3(.2,.4,.6),float3(0,1,0),
                    float3(0,0,0),float2(.5,.5),float3(0,1,0),_TestLightColor.rgb,1,1),1); }
                ENDHLSL
                } } }";
            int enabledId=Shader.PropertyToID("_MxSceneLightingEnabled"),lookId=Shader.PropertyToID("_MxSceneLightingLook");
            int minId=Shader.PropertyToID("_MxSceneLightingBoundsMin"),maxId=Shader.PropertyToID("_MxSceneLightingBoundsMax");
            float oldEnabled=Shader.GetGlobalFloat(enabledId);
            Vector4 oldLook=Shader.GetGlobalVector(lookId),oldMin=Shader.GetGlobalVector(minId),oldMax=Shader.GetGlobalVector(maxId);
            RenderTexture previous=RenderTexture.active;
            Shader shader=null;Material material=null;RenderTexture target=null;Texture2D readback=null;
            var cmd=new CommandBuffer();
            try
            {
                shader=UnityEditor.ShaderUtil.CreateShaderAsset(source);material=new Material(shader);
                target=new RenderTexture(1,1,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);target.Create();
                readback=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);
                Color Capture(Vector4 light)
                {
                    material.SetVector("_TestLightColor",light);
                    cmd.Clear();cmd.SetRenderTarget(target);cmd.SetViewport(new Rect(0,0,1,1));
                    cmd.DrawProcedural(Matrix4x4.identity,material,0,MeshTopology.Triangles,3);
                    Graphics.ExecuteCommandBuffer(cmd);RenderTexture.active=target;
                    readback.ReadPixels(new Rect(0,0,1,1),0,0);readback.Apply();return readback.GetPixel(0,0);
                }
                void AssertRgb(Color actual,Vector3 expected)
                {
                    Assert.That(actual.r,Is.EqualTo(expected.x).Within(.0001f));
                    Assert.That(actual.g,Is.EqualTo(expected.y).Within(.0001f));
                    Assert.That(actual.b,Is.EqualTo(expected.z).Within(.0001f));
                }
                Shader.SetGlobalFloat(enabledId,0);
                Shader.SetGlobalVector(lookId,new Vector4(0,10000,1,1));
                AssertRgb(Capture(new Vector4(2,2,2,1)),new Vector3(.4f,.8f,1.2f));
                AssertRgb(Capture(Vector4.zero),Vector3.zero);
                Shader.SetGlobalFloat(enabledId,1);
                Shader.SetGlobalVector(lookId,new Vector4(.5f,0,0,0));
                Shader.SetGlobalVector(minId,-Vector4.one);Shader.SetGlobalVector(maxId,Vector4.one);
                AssertRgb(Capture(new Vector4(2,2,2,1)),new Vector3(.2f,.4f,.6f));
                Shader.SetGlobalVector(lookId,new Vector4(.5f,10000,1,1));
                Shader.SetGlobalVector(minId,Vector4.one*10);Shader.SetGlobalVector(maxId,Vector4.one*20);
                AssertRgb(Capture(new Vector4(2,2,2,1)),new Vector3(.2f,.4f,.6f));
            }
            finally
            {
                Shader.SetGlobalFloat(enabledId,oldEnabled);Shader.SetGlobalVector(lookId,oldLook);
                Shader.SetGlobalVector(minId,oldMin);Shader.SetGlobalVector(maxId,oldMax);
                RenderTexture.active=previous;cmd.Dispose();if(target!=null)target.Release();
                UnityEngine.Object.DestroyImmediate(readback);UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(shader);
            }
        }

        [Test] public void DarkClothRespectsLightingOnGpu()
        {
            const string source = @"Shader ""Hidden/MxFramework/Tests/DarkClothDiffuse"" {
                SubShader { Pass { ZTest Always ZWrite Off Cull Off
                HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex Vert
                #pragma fragment Frag
                #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
                #include ""Assets/Art/WGame/Shaders/ShaderLibrary/WGamePalette.hlsl""
                float4 _TestAlbedo, _TestDiffuse, _TestResponse;
                float4 Vert(uint id:SV_VertexID):SV_POSITION { return GetFullScreenTriangleVertexPosition(id); }
                float4 Frag():SV_Target {
                    return float4(WgApplyPaletteCharacterMaterialResponse(
                        _TestDiffuse.rgb, _TestAlbedo.rgb, _TestResponse.z, float3(0,1,0),
                        float3(0,1,0), float3(0,0,1), 0, 0, 2, float3(1,1,1)), 1); }
                ENDHLSL
                } } }";
            RenderTexture previous = RenderTexture.active;
            Shader shader = null; Material material = null; RenderTexture target = null; Texture2D readback = null;
            var cmd = new CommandBuffer();
            try
            {
                shader = UnityEditor.ShaderUtil.CreateShaderAsset(source);
                material = new Material(shader);
                target = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                target.Create();
                readback = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
                Vector3 Capture(Vector3 albedo, Vector3 diffuse, float profile)
                {
                    material.SetVector("_TestAlbedo", albedo);
                    material.SetVector("_TestDiffuse", diffuse);
                    material.SetVector("_TestResponse", new Vector4(0, 0, profile, 0));
                    cmd.Clear(); cmd.SetRenderTarget(target); cmd.SetViewport(new Rect(0, 0, 1, 1));
                    cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);
                    Graphics.ExecuteCommandBuffer(cmd); RenderTexture.active = target;
                    readback.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); readback.Apply();
                    Color color = readback.GetPixel(0, 0);
                    return new Vector3(color.r, color.g, color.b);
                }
                var luma = new Vector3(.2126f, .7152f, .0722f);
                foreach (float profile in new[] { 4f, 10f })
                {
                    Vector3 dim = Capture(Vector3.one * .02f, Vector3.one * .05f, profile);
                    Vector3 bright = Capture(Vector3.one * .02f, Vector3.one * .5f, profile);
                    Assert.Greater(Vector3.Dot(bright, luma), Vector3.Dot(dim, luma) * 5,
                        "Dark material response must not cap valid illumination at a fixed body value.");
                }
            }
            finally
            {
                RenderTexture.active = previous; cmd.Dispose(); if (target != null) target.Release();
                UnityEngine.Object.DestroyImmediate(readback); UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(material); UnityEngine.Object.DestroyImmediate(shader);
            }
        }

        [TestCase(1)] [TestCase(3)] [TestCase(32)]
        public void CapsuleFadeHasBoundedTotalOcclusionError(int count)
        {
            const float radius=.3f,strength=.35f;
            Vector4 range=MxSceneLightingCapsuleTiles.Range(radius,strength,count);
            Assert.Greater(range.z,range.x);
            Assert.That((range.z-range.x)*range.y,Is.EqualTo(1).Within(1e-6));
            // At and beyond the start of fading, even the sum of all omitted maxima fits the budget.
            Assert.LessOrEqual(count*strength*radius*radius/range.x,
                MxSceneLightingCapsuleTiles.MaximumOcclusionError+1e-6f);
            for(int step=0;step<=20;step++)
            {
                float distanceSquared=Mathf.Lerp(range.x,range.z,step/20f);
                float term=strength*radius*radius/distanceSquared;
                float fade=1-Mathf.SmoothStep(0,1,(distanceSquared-range.x)*range.y);
                float full=Mathf.Pow(1-term,count),bounded=Mathf.Pow(1-term*fade,count);
                Assert.That(bounded-full,Is.InRange(0f,MxSceneLightingCapsuleTiles.MaximumOcclusionError+1e-6f));
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void CapsuleScreenBoundsContainProjectedInfluence(bool topOrigin)
        {
            Matrix4x4 projection=Matrix4x4.Perspective(60,16f/9f,.1f,100);
            var start=new Vector3(1,0,-8);var end=new Vector3(1,2,-8);
            Vector4 rect=MxSceneLightingCapsuleTiles.ScreenRect(start,end,.6f,Matrix4x4.identity,projection,.1f,320,180,topOrigin);
            foreach(Vector3 center in new[]{start,end})
                for(int i=0;i<8;i++)
                {
                    Vector3 point=center+new Vector3((i&1)==0?-.6f:.6f,(i&2)==0?-.6f:.6f,(i&4)==0?-.6f:.6f);
                    Vector4 clip=projection*new Vector4(point.x,point.y,point.z,1);
                    float x=(clip.x/clip.w*.5f+.5f)*320;
                    float y=(clip.y/clip.w*(topOrigin?-.5f:.5f)+.5f)*180;
                    Assert.That(x,Is.InRange(rect.x,rect.z));Assert.That(y,Is.InRange(rect.y,rect.w));
                }
            Assert.AreEqual(new Vector4(0,0,320,180),MxSceneLightingCapsuleTiles.ScreenRect(
                new Vector3(0,0,-.1f),new Vector3(0,1,-.1f),.6f,Matrix4x4.identity,projection,.1f,320,180,topOrigin));
            Assert.AreEqual(Vector4.zero,MxSceneLightingCapsuleTiles.ScreenRect(
                Vector3.forward*3,Vector3.forward*4,.6f,Matrix4x4.identity,projection,.1f,320,180,topOrigin));
        }

        [Test] public void CapsuleScreenBoundsHandleOrthographicScreenEdges()
        {
            var projection=Matrix4x4.Ortho(-5,5,-5,5,.1f,100);
            var rect=MxSceneLightingCapsuleTiles.ScreenRect(new Vector3(-5.1f,0,-5),new Vector3(-5.1f,1,-5),
                .4f,Matrix4x4.identity,projection,.1f,320,180,false);
            Assert.AreEqual(0,rect.x);Assert.Greater(rect.z,0);Assert.Less(rect.z,32);
        }

        [Test] public void GpuCapsuleTilesPreserveEdgesAndHighestMaskBit()
        {
            if(!SystemInfo.supportsComputeShaders)Assert.Ignore("Compute shader device required.");
            var shader=UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>("Packages/com.vincent.wgameframework/Rendering/SceneLighting.compute");
            using(var masks=new GraphicsBuffer(GraphicsBuffer.Target.Structured,4,sizeof(uint)))
            {
                var rects=new Vector4[32];rects[0]=new Vector4(0,0,16,16);
                rects[1]=new Vector4(15,15,17,17);rects[31]=new Vector4(16,16,32,32);
                int kernel=shader.FindKernel("BuildCapsuleTiles");
                shader.SetInt("_CapsuleCount",32);shader.SetInt("_CapsuleTilesX",2);shader.SetInt("_CapsuleTilesY",2);
                shader.SetVectorArray("_CapsuleScreenRects",rects);shader.SetBuffer(kernel,"_CapsuleTileMasks",masks);
                shader.Dispatch(kernel,1,1,1);var actual=new uint[4];masks.GetData(actual);
                CollectionAssert.AreEqual(new uint[]{3,2,2,0x80000002},actual);
                shader.SetInt("_CapsuleCount",0);shader.Dispatch(kernel,1,1,1);masks.GetData(actual);
                CollectionAssert.AreEqual(new uint[4],actual,"Empty frames must not retain the previous camera's candidates.");
            }
        }

        private static GraphicsBuffer Buffer<T>(List<GraphicsBuffer> owned,T[] data,int stride) where T:struct
        {var b=new GraphicsBuffer(GraphicsBuffer.Target.Structured,data.Length,stride);owned.Add(b);b.SetData(data);return b;}

        [Test] public void GpuTransportMatchesTwoPatchAnalyticalSolution()
        {
            if(!SystemInfo.supportsComputeShaders)Assert.Ignore("Compute shader device required.");
            var shader=UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>("Packages/com.vincent.wgameframework/Rendering/SceneLighting.compute");
            Assert.NotNull(shader);var owned=new List<GraphicsBuffer>();
            try
            {
                var node=new MxSceneLightingNode {NormalParent=Vector3.up,Albedo=new Vector4(.5f,.5f,.5f,0)};
                var nodes=Buffer(owned,new[]{node,node},80);
                var rows=Buffer(owned,new[]{new MxSceneLightingRow(0,1),new MxSceneLightingRow(1,1)},8);
                var edges=Buffer(owned,new[]{new MxSceneLightingEdge(1,.25f),new MxSceneLightingEdge(0,.25f)},8);
                var direct=Buffer(owned,new Vector4[2],16);var bounce=Buffer(owned,new Vector4[2],16);
                var probes=Buffer(owned,new[]{new MxSceneLightingProbe {PositionValid=new Vector4(0,0,0,1)}},64);
                var probeRows=Buffer(owned,new[]{new MxSceneLightingRow(0,1)},8);
                var links=Buffer(owned,new[]{new MxSceneLightingProbeLink {Target=0,DirectionWeight=new Vector4(0,1,0,1)}},32);
                var result=Buffer(owned,new Coefficients[1],48);
                shader.SetInt("_SurfaceCount",2);shader.SetInt("_NodeCount",2);shader.SetInt("_ProbeCount",1);
                shader.SetVector("_SunDirection",Vector3.up);shader.SetVector("_SunColor",Vector4.one);
                shader.SetVector("_SkyUp",Vector4.zero);shader.SetVector("_SkyEquator",Vector4.zero);shader.SetVector("_SkyGround",Vector4.zero);
                shader.SetMatrix("_SunVP",Matrix4x4.identity);shader.SetMatrix("_SunView",Matrix4x4.identity);shader.SetVector("_SunRange",new Vector4(0,1,0,0));
                int inject=shader.FindKernel("Inject");shader.SetBuffer(inject,"_Nodes",nodes);shader.SetBuffer(inject,"_Destination",direct);shader.SetTexture(inject,"_SunVisibility",Texture2D.whiteTexture);shader.Dispatch(inject,1,1,1);
                int transport=shader.FindKernel("Bounce");shader.SetBuffer(transport,"_Nodes",nodes);shader.SetBuffer(transport,"_Rows",rows);shader.SetBuffer(transport,"_Edges",edges);
                shader.SetBuffer(transport,"_Source",direct);shader.SetBuffer(transport,"_Direct",direct);shader.SetBuffer(transport,"_Destination",bounce);shader.Dispatch(transport,1,1,1);
                int project=shader.FindKernel("Project");shader.SetBuffer(project,"_Probes",probes);shader.SetBuffer(project,"_ProbeRows",probeRows);shader.SetBuffer(project,"_ProbeLinks",links);shader.SetBuffer(project,"_Source",bounce);shader.SetBuffer(project,"_Coefficients",result);shader.Dispatch(project,1,1,1);
                var coefficients=new Coefficients[1];result.GetData(coefficients);
                // Two equal Lambert patches: 0.5 direct exitance + 0.5 * 0.25 * 0.5 reflected exitance.
                Assert.That(coefficients[0].R.w,Is.EqualTo(.5625f).Within(.0001f));
                Assert.That(coefficients[0].G.w,Is.EqualTo(.5625f).Within(.0001f));
                Assert.That(coefficients[0].B.w,Is.EqualTo(.5625f).Within(.0001f));
            }
            finally {foreach(var b in owned)b.Dispose();}
        }

        [Test] public void CaptureDepthSelectsNearestOccluderAndBlocksRearSurface()
        {
            GameObject primitive=null;Mesh mesh=null,rearMesh=null;Material material=null;RenderTexture target=null;Texture2D readback=null;CommandBuffer cmd=null;
            RenderTexture previous=RenderTexture.active;
            try
            {
                primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);primitive.hideFlags=HideFlags.HideAndDontSave;
                mesh=UnityEngine.Object.Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh);
                var colors=new Color[mesh.vertexCount];for(int i=0;i<colors.Length;i++)colors[i]=Color.red;mesh.colors=colors;
                rearMesh=UnityEngine.Object.Instantiate(mesh);
                for(int i=0;i<colors.Length;i++)colors[i]=new Color(2,0,0,1);rearMesh.colors=colors;
                // The production capture mesh is already in world space; object matrices are intentionally unused.
                var rearVertices=rearMesh.vertices;for(int i=0;i<rearVertices.Length;i++)rearVertices[i]+=Vector3.forward*3;rearMesh.vertices=rearVertices;
                material=new Material(Shader.Find("Hidden/MxFramework/SceneLightingCapture"));
                target=new RenderTexture(32,32,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);target.Create();
                readback=new Texture2D(32,32,TextureFormat.RGBAFloat,false,true);
                var origin=new Vector3(0,0,-5);
                var view=Matrix4x4.Scale(new Vector3(1,1,-1))*Matrix4x4.TRS(origin,Quaternion.identity,Vector3.one).inverse;
                var block=new MaterialPropertyBlock();block.SetMatrix("_CaptureVP",GL.GetGPUProjectionMatrix(Matrix4x4.Perspective(90,1,.1f,100),true)*view);block.SetVector("_CaptureOrigin",origin);
                cmd=new CommandBuffer();cmd.SetRenderTarget(target);cmd.ClearRenderTarget(true,true,Color.clear,1);cmd.SetViewport(new Rect(0,0,32,32));cmd.DrawMesh(mesh,Matrix4x4.identity,material,0,0,block);
                // Draw a distinct receiver behind the opaque wall after the wall. Its ID must never replace the wall ID.
                cmd.DrawMesh(rearMesh,Matrix4x4.identity,material,0,0,block);Graphics.ExecuteCommandBuffer(cmd);
                RenderTexture.active=target;readback.ReadPixels(new Rect(0,0,32,32),0,0);readback.Apply();
                Color center=readback.GetPixel(16,16);
                Assert.That(Mathf.Abs(center.r),Is.EqualTo(1).Within(.01f));
                Assert.That(center.g,Is.EqualTo(4.5f).Within(.1f),"Capture must see the front surface, not empty sky or the back wall.");
            }
            finally
            {
                RenderTexture.active=previous;cmd?.Dispose();if(target!=null)target.Release();
                UnityEngine.Object.DestroyImmediate(primitive);UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(rearMesh);UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(readback);
            }
        }
    }
}
