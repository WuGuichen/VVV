using System;
using System.Reflection;
using MxFramework.Rendering;
using MxFramework.Rendering.Editor;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Rendering
{
    public sealed class DirectionalShadowRegressionTests
    {
        [Test]
        public void StaticPlaneCoverage_MetadataRecognizesLegacyAndRejectsUnknownEncoding()
        {
            var texture=new Texture2D(16,16,TextureFormat.RFloat,false,true){filterMode=FilterMode.Point};
            var data=ScriptableObject.CreateInstance<MxDirectionalShadowData>();
            var bounds=new Bounds(Vector3.zero,Vector3.one*4);
            void Init(Vector4 plane)=>data.Initialize(texture,texture,Vector3.up,Vector3.right,Vector3.forward,
                new Vector4(-2,-2,4,4),new Vector4(-2,-2,4,4),new Vector4(-4,8,0,0),bounds,bounds,16,0,"fixture",1,1,plane);
            void Set(string name,object value)=>typeof(MxDirectionalShadowData).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(data,value);
            try
            {
                Init(Vector4.zero);Assert.IsTrue(data.IsValid);Assert.IsFalse(data.HasPlaneCoverage);
                Set("_version",2);Assert.IsTrue(data.IsValid);Assert.AreEqual(Vector4.zero,data.PlaneCoverage);
                Set("_sampleEncoding",1);Assert.IsFalse(data.IsValid,"Legacy assets cannot advertise a new payload.");
                Init(new Vector4(1,0,.001f,.015f));Assert.IsTrue(data.IsValid);Assert.IsTrue(data.HasPlaneCoverage);
                Set("_sampleEncoding",9);Assert.IsFalse(data.IsValid);
                Init(new Vector4(1,0,.001f,.015f));Set("_version",99);Assert.IsFalse(data.IsValid);
                Assert.Throws<ArgumentException>(()=>Init(new Vector4(1,float.NaN,.001f,.015f)));
                Assert.Throws<ArgumentException>(()=>Init(new Vector4(1,0,0,.015f)));
                Init(new Vector4(1,0,.001f,.015f));data.InvalidateForRebuild();Assert.IsFalse(data.IsValid);
            }
            finally {Object.DestroyImmediate(data);Object.DestroyImmediate(texture);}
        }
        [Test]
        public void StaticPlaneCoverage_FingerprintPreservesRawAndTracksPlaneBias()
        {
            var sources=Array.Empty<MxShadowBakeSource>();var bounds=new Bounds(Vector3.zero,Vector3.one*4);
            string Hash(Vector4 plane)=>MxDirectionalShadowBaker.Fingerprint(sources,bounds,bounds,Vector3.up,128,128,plane);
            Assert.AreEqual(MxDirectionalShadowBaker.Fingerprint(sources,bounds,bounds,Vector3.up,128,128),Hash(Vector4.zero));
            string encoded=Hash(new Vector4(1,0,.001f,.015f));Assert.AreNotEqual(Hash(Vector4.zero),encoded);
            Assert.AreNotEqual(encoded,Hash(new Vector4(1,.1f,.001f,.015f)));
            Assert.AreNotEqual(encoded,Hash(new Vector4(1,0,.002f,.015f)));
            Assert.AreNotEqual(encoded,Hash(new Vector4(1,0,.001f,.02f)));
        }
        [Test]
        public void StaticPlaneCoverage_EncodingPreservesCoverageAndBoundsDepthError()
        {
            var encode=typeof(MxDirectionalShadowBaker).GetMethod("EncodePlaneCoverage",BindingFlags.Static|BindingFlags.NonPublic);
            var random=new System.Random(1443);
            for(int i=0;i<1002;i++)
            {
                float depth=i==0?0:i==1?1:(float)random.NextDouble();int count=random.Next(65);
                ulong mask=count==64?ulong.MaxValue:(1UL<<count)-1;
                float packed=(float)encode.Invoke(null,new object[]{depth,mask});
                uint bits=BitConverter.ToUInt32(BitConverter.GetBytes(packed),0);
                float decoded=BitConverter.ToSingle(BitConverter.GetBytes(bits&0xffffffc0u),0);
                Assert.IsFalse(float.IsNaN(packed)||float.IsInfinity(packed));Assert.That(packed,Is.GreaterThanOrEqualTo(1.17549435E-38f));
                Assert.That(Mathf.Abs(decoded-depth),Is.LessThanOrEqualTo(1.9073486328125E-06f));
                Assert.That(Mathf.Abs((bits&63u)/63f-count/64f),Is.LessThanOrEqualTo(1f/126+1e-7f));
                Assert.AreEqual(count>0,(bits&63u)>0);
            }
        }
        [Test]
        public void StaticPlaneCoverage_RFloatAssetAndGpuRoundTripPreservePayloadBits()
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)Assert.Ignore("Requires graphical Editor and GPU readback.");
            var encode=typeof(MxDirectionalShadowBaker).GetMethod("EncodePlaneCoverage",BindingFlags.Static|BindingFlags.NonPublic);
            var values=new float[256];
            for(int i=0;i<values.Length;i++)
            {int n=i%65;values[i]=(float)encode.Invoke(null,new object[]{i<65?0f:i<130?1f:(i-130)/126f,n==64?ulong.MaxValue:(1UL<<n)-1});}
            string path=UnityEditor.AssetDatabase.GenerateUniqueAssetPath("Assets/__GrassCoverageBitRoundTrip.asset");
            var texture=new Texture2D(256,1,TextureFormat.RFloat,false,true){filterMode=FilterMode.Point};
            var readback=new Texture2D(256,1,TextureFormat.RFloat,false,true);
            RenderTexture target=null;Shader shader=null;Material material=null;var previous=RenderTexture.active;
            try
            {
                texture.SetPixelData(values,0);texture.Apply(false,true);
                UnityEditor.AssetDatabase.CreateAsset(texture,path);UnityEditor.AssetDatabase.SaveAssetIfDirty(texture);
                UnityEngine.Resources.UnloadAsset(texture);texture=null;
                UnityEditor.AssetDatabase.ImportAsset(path,UnityEditor.ImportAssetOptions.ForceUpdate);
                texture=UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                shader=UnityEditor.ShaderUtil.CreateShaderAsset(@"Shader ""Hidden/MxCoverageBitRoundTrip"" { Properties { _MainTex (""Source"", 2D) = ""white"" {} } SubShader { Pass { ZTest Always ZWrite Off Cull Off
CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 4.5
#include ""UnityCG.cginc""
Texture2D<float> _MainTex;
float4 frag(v2f_img i):SV_Target { float v=_MainTex.Load(int3((int2)i.pos.xy,0)); return v.xxxx; }
ENDCG
} } }");
                Assert.IsTrue(shader.isSupported,"The RFloat round-trip shader must compile on the target graphics backend.");
                material=new Material(shader);material.SetTexture("_MainTex",texture);
                UnityEditor.ShaderUtil.CompilePass(material,0,true);
                Assert.IsTrue(UnityEditor.ShaderUtil.IsPassCompiled(material,0));
                target=new RenderTexture(256,1,0,RenderTextureFormat.RFloat,RenderTextureReadWrite.Linear){filterMode=FilterMode.Point};
                Assert.IsTrue(target.Create());Graphics.Blit(texture,target,material);
                RenderTexture.active=target;readback.ReadPixels(new Rect(0,0,256,1),0,0);readback.Apply();
                var actual=readback.GetRawTextureData<float>();
                for(int i=0;i<values.Length;i++)Assert.AreEqual(BitConverter.ToInt32(BitConverter.GetBytes(values[i]),0),
                    BitConverter.ToInt32(BitConverter.GetBytes(actual[i]),0),"RFloat payload at "+i);
            }
            finally
            {
                RenderTexture.active=previous;if(target!=null){target.Release();Object.DestroyImmediate(target);}
                if(material!=null)Object.DestroyImmediate(material);if(shader!=null)Object.DestroyImmediate(shader);
                Object.DestroyImmediate(readback);
                if(UnityEditor.AssetDatabase.LoadMainAssetAtPath(path)!=null)UnityEditor.AssetDatabase.DeleteAsset(path);
                else if(texture!=null)Object.DestroyImmediate(texture);
            }
        }


        [Test]
        public void CasterAdmission_RejectsMissingTransparentAndDisabledPasses_AndPreservesExplicitFoliageCoverage()
        {
            var opaque=new Material(Shader.Find("WGame/PropStylizedLit"));
            var foliage=new Material(Shader.Find("WGame/FoliageMossLit"));
            try
            {
                Assert.IsFalse(MxDirectionalShadowCoverage.CanCast(null));
                Assert.IsTrue(MxDirectionalShadowCoverage.CanCast(opaque));
                opaque.renderQueue=3000;
                Assert.IsFalse(MxDirectionalShadowCoverage.CanCast(opaque));
                opaque.renderQueue=2450;
                Assert.IsTrue(MxDirectionalShadowCoverage.CanCast(opaque),"Cutout remains eligible.");
                opaque.SetShaderPassEnabled("ShadowCaster",false);
                Assert.IsFalse(MxDirectionalShadowCoverage.CanCast(opaque));
                Assert.IsFalse(MxDirectionalShadowCoverage.CanCast(opaque,true),"Explicit coverage must not override a disabled pass.");
                opaque.SetShaderPassEnabled("ShadowCaster",true);
                Assert.IsTrue(MxDirectionalShadowCoverage.CanCast(opaque));
                Assert.That(foliage.FindPass("ShadowCaster"),Is.LessThan(0));
                Assert.IsFalse(MxDirectionalShadowCoverage.CanCast(foliage),"Ordinary renderer sources require a mesh pass.");
                Assert.IsTrue(MxDirectionalShadowCoverage.CanCast(foliage,true),"Authored vegetation can supply its known reference coverage.");
                foliage.SetShaderPassEnabled("ShadowCaster",false);
                Assert.IsFalse(MxDirectionalShadowCoverage.CanCast(foliage,true));
            }
            finally {Object.DestroyImmediate(opaque);Object.DestroyImmediate(foliage);}
        }

        [Test]
        public void DynamicAdmission_ExcludedLargeSubmeshDoesNotConsumeBudget_AndSnapshotTracksNextUpdate()
        {
            var go=new GameObject("Mixed shadow submeshes"){hideFlags=HideFlags.HideAndDontSave};
            var mesh=Triangle();
            var opaque=new Material(Shader.Find("WGame/PropStylizedLit"));
            var transparent=new Material(opaque){renderQueue=3000};
            var noPass=new Material(Shader.Find("WGame/FoliageMossLit"));
            var disabled=new Material(opaque);disabled.SetShaderPassEnabled("ShadowCaster",false);
            IDisposable actor=null;
            try
            {
                mesh.subMeshCount=5;
                var oversized=new int[(MxDirectionalShadowProvider.MaximumTrianglesPerFrame+1)*3];
                for(int i=0;i<oversized.Length;i++)oversized[i]=i%3;
                mesh.SetIndices(new[]{0,1,2},UnityEngine.MeshTopology.Triangles,0);
                mesh.SetIndices(oversized,UnityEngine.MeshTopology.Triangles,1);
                mesh.SetIndices(new[]{0,1,2},UnityEngine.MeshTopology.Triangles,2);
                mesh.SetIndices(new[]{0,1,2},UnityEngine.MeshTopology.Triangles,3);
                mesh.SetIndices(new[]{0,1,2},UnityEngine.MeshTopology.Triangles,4);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials=new[]{opaque,transparent,noPass,disabled};
                var type=typeof(MxDirectionalShadowProvider).GetNestedType("Actor",BindingFlags.NonPublic);
                actor=(IDisposable)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{renderer,false},null);
                var snapshot=type.GetMethod("SnapshotCastingSubMeshes",BindingFlags.Instance|BindingFlags.NonPublic);
                int Count()=>(int)snapshot.Invoke(actor,null);
                bool[] Ranges()=>(bool[])type.GetField("CastingSubMeshes",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(actor);
                Assert.AreEqual(1,Count(),"Transparent/no-pass/disabled/missing slots contribute no triangles.");
                CollectionAssert.AreEqual(new[]{true,false,false,false,false},Ranges());
                transparent.renderQueue=2000;
                Assert.IsFalse(Ranges()[1],"An existing update retains its material admission snapshot.");
                Assert.AreEqual(MxDirectionalShadowProvider.MaximumTrianglesPerFrame+2,Count());
                transparent.SetShaderPassEnabled("ShadowCaster",false);
                Assert.AreEqual(1,Count());
                renderer.sharedMaterials=new[]{opaque};
                Assert.AreEqual(1,Count(),"Removing material slots drops their previous admission.");
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                Assert.AreEqual(0,Count());
                CollectionAssert.AreEqual(new[]{false,false,false,false,false},Ranges());
            }
            finally
            {
                actor?.Dispose();Object.DestroyImmediate(go);Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(opaque);Object.DestroyImmediate(transparent);Object.DestroyImmediate(noPass);Object.DestroyImmediate(disabled);
            }
        }

        [Test]
        public void StaticFingerprint_UsesSameCasterAdmission_AndObservesLivePassDisable()
        {
            var mesh=Triangle();var opaque=new Material(Shader.Find("WGame/PropStylizedLit"));
            var noPass=new Material(Shader.Find("WGame/FoliageMossLit"));
            try
            {
                var bounds=new Bounds(Vector3.zero,Vector3.one*4);
                string Hash(params MxShadowBakeSource[] sources)=>MxDirectionalShadowBaker.Fingerprint(sources,bounds,Vector3.up,128);
                string empty=Hash();
                var valid=new MxShadowBakeSource(mesh,opaque,Matrix4x4.identity,0);
                Assert.AreNotEqual(empty,Hash(valid));
                Assert.AreEqual(empty,Hash(new MxShadowBakeSource(mesh,noPass,Matrix4x4.identity,0)));
                opaque.SetShaderPassEnabled("ShadowCaster",false);
                Assert.AreEqual(empty,Hash(valid),"A source snapshot must not bypass a newly disabled material pass.");
                opaque.SetShaderPassEnabled("ShadowCaster",true);
                opaque.renderQueue=3000;
                Assert.AreEqual(empty,Hash(valid));
                opaque.renderQueue=2000;
                Assert.AreNotEqual(empty,Hash(valid));
                Assert.AreEqual(empty,Hash(new MxShadowBakeSource(mesh,opaque,Matrix4x4.identity,0,null,null,UnityEngine.Rendering.ShadowCastingMode.Off)));
            }
            finally {Object.DestroyImmediate(mesh);Object.DestroyImmediate(opaque);Object.DestroyImmediate(noPass);}
        }

        [Test]
        public void StaticFingerprint_ExplicitCoveragePreservesReferenceFoliage_WithoutOverridingCastOff()
        {
            var mesh=Triangle();var foliage=new Material(Shader.Find("WGame/FoliageMossLit"));
            try
            {
                var bounds=new Bounds(Vector3.zero,Vector3.one*4);
                string Hash(params MxShadowBakeSource[] sources)=>MxDirectionalShadowBaker.Fingerprint(sources,bounds,Vector3.up,128);
                string empty=Hash();
                var explicitSource=new MxShadowBakeSource(mesh,foliage,Matrix4x4.identity,0,null,null,UnityEngine.Rendering.ShadowCastingMode.On,true);
                Assert.AreNotEqual(empty,Hash(explicitSource));
                Assert.AreEqual(empty,Hash(new MxShadowBakeSource(mesh,foliage,Matrix4x4.identity,0,null,null,UnityEngine.Rendering.ShadowCastingMode.Off,true)));
                foliage.SetShaderPassEnabled("ShadowCaster",false);
                Assert.AreEqual(empty,Hash(explicitSource));
                foliage.SetShaderPassEnabled("ShadowCaster",true);foliage.renderQueue=3000;
                Assert.AreEqual(empty,Hash(explicitSource));
            }
            finally {Object.DestroyImmediate(mesh);Object.DestroyImmediate(foliage);}
        }


        [Test]
        public void MaterialBlock_ReplacesRendererBlock_ForAllCoverageInputs()
        {
            var material=new Material(Shader.Find("WGame/CharacterStylizedLit"));
            var objectBlock=new MaterialPropertyBlock();
            var subBlock=new MaterialPropertyBlock();
            var read=typeof(MxFramework.Game.CharacterTest.GameManager).Assembly
                .GetType("MxFramework.Game.CharacterTest.CharacterTestDirectionalShadowPolicy")
                .GetMethod("Read",BindingFlags.Static|BindingFlags.Public);
            try
            {
                material.SetFloat("_WgDissolveProgress",0);
                objectBlock.SetFloat("_WgDissolveProgress",1);
                objectBlock.SetFloat("_Cutoff",.8f);
                objectBlock.SetTexture("_BaseMap",Texture2D.blackTexture);
                objectBlock.SetVector("_BaseMap_ST",new Vector4(5,6,7,8));
                subBlock.SetColor("_Color",new Color(1,1,1,.4f));
                var baseline=(MxDirectionalShadowCoverage)read.Invoke(null,new object[]{material,null,null});
                var actual=(MxDirectionalShadowCoverage)read.Invoke(null,new object[]{material,objectBlock,subBlock});
                Assert.AreEqual(.4f,actual.Alpha);
                Assert.AreEqual(0,actual.Dissolve,"A missing material-block property must fall back to the material.");
                Assert.AreEqual(baseline.Cutoff,actual.Cutoff);
                Assert.AreEqual(baseline.Mask,actual.Mask);
                Assert.AreEqual(baseline.BaseMapST,actual.BaseMapST);
                subBlock.Clear();
                actual=(MxDirectionalShadowCoverage)read.Invoke(null,new object[]{material,objectBlock,subBlock});
                Assert.AreEqual(1,actual.Dissolve,"Clearing the material block restores renderer overrides.");
                Assert.AreEqual(new Vector4(5,6,7,8),actual.BaseMapST);
            }
            finally {Object.DestroyImmediate(material);}
        }

        [Test]
        public void DefaultCoverage_UsesTextureTransform_AndWholeBlockPrecedence()
        {
            var resolver=MxDirectionalShadowCoverage.Resolver;
            var overrides=MxDirectionalShadowCoverage.OverrideResolver;
            var material=new Material(Shader.Find("WGame/PropStylizedLit"));
            try
            {
                MxDirectionalShadowCoverage.Resolver=null;
                MxDirectionalShadowCoverage.OverrideResolver=null;
                material.SetTextureScale("_BaseMap",new Vector2(2,3));
                material.SetTextureOffset("_BaseMap",new Vector2(.1f,.2f));
                var objectBlock=new MaterialPropertyBlock();
                objectBlock.SetVector("_BaseMap_ST",new Vector4(5,6,7,8));
                objectBlock.SetFloat("_Cutoff",.8f);
                var subBlock=new MaterialPropertyBlock();subBlock.SetFloat("_Unrelated",1);
                var actual=MxDirectionalShadowCoverage.Read(material,objectBlock,subBlock);
                Assert.AreEqual(new Vector4(2,3,.1f,.2f),actual.BaseMapST);
                Assert.AreEqual(0,actual.Cutoff);
                subBlock.Clear();actual=MxDirectionalShadowCoverage.Read(material,objectBlock,subBlock);
                Assert.AreEqual(new Vector4(5,6,7,8),actual.BaseMapST);
                Assert.AreEqual(.8f,actual.Cutoff);
            }
            finally
            {
                MxDirectionalShadowCoverage.Resolver=resolver;MxDirectionalShadowCoverage.OverrideResolver=overrides;
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void Fingerprint_DistinguishesSubassetsInTheSameFile()
        {
            var cube=UnityEngine.Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var sphere=UnityEngine.Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            Assert.NotNull(cube);Assert.NotNull(sphere);
            Assert.AreEqual(UnityEditor.AssetDatabase.GetAssetPath(cube),UnityEditor.AssetDatabase.GetAssetPath(sphere));
            var material=new Material(Shader.Find("WGame/PropStylizedLit"));
            try {Assert.AreNotEqual(Fingerprint(cube,material),Fingerprint(sphere,material));}
            finally {Object.DestroyImmediate(material);}
        }

        [Test]
        public void Fingerprint_DetectsProceduralMeshEditsWithoutReplacingTheObject()
        {
            var mesh=Triangle();var material=new Material(Shader.Find("WGame/PropStylizedLit"));
            try
            {
                string before=Fingerprint(mesh,material);
                var vertices=mesh.vertices;vertices[1]=new Vector3(2,0,0);mesh.vertices=vertices;
                Assert.AreNotEqual(before,Fingerprint(mesh,material));
            }
            finally {Object.DestroyImmediate(mesh);Object.DestroyImmediate(material);}
        }

        [Test]
        public void Fingerprint_IsStableWhenEquivalentProceduralMaterialsAreRecreated()
        {
            var mesh=Triangle();
            var first=new Material(Shader.Find("WGame/PropStylizedLit"));
            var second=new Material(first);
            try
            {
                Assert.AreEqual(Fingerprint(mesh,first),Fingerprint(mesh,second));
                second.SetColor("_Color",new Color(1,1,1,.25f));
                Assert.AreNotEqual(Fingerprint(mesh,first),Fingerprint(mesh,second));
            }
            finally {Object.DestroyImmediate(mesh);Object.DestroyImmediate(first);Object.DestroyImmediate(second);}
        }

        [Test]
        public void CpuPose_UsesOwnedRawStorage_RendererSpace_AndUpdatedBonePose()
        {
            var root=new GameObject("Shadow pose fixture"){hideFlags=HideFlags.HideAndDontSave};
            var mesh=Triangle();IDisposable actor=null;
            try
            {
                root.transform.position=new Vector3(3,4,5);
                root.transform.localScale=new Vector3(2,3,4);
                var body=new GameObject("Body");body.transform.SetParent(root.transform,false);
                body.transform.localPosition=new Vector3(1,0,0);
                var bone=new GameObject("Bone");bone.transform.SetParent(root.transform,false);
                var skin=body.AddComponent<SkinnedMeshRenderer>();
                mesh.bindposes=new[]{bone.transform.worldToLocalMatrix*body.transform.localToWorldMatrix};
                mesh.boneWeights=new[]{Weight(),Weight(),Weight()};
                skin.sharedMesh=mesh;skin.bones=new[]{bone.transform};skin.rootBone=bone.transform;
                var originalTarget=skin.vertexBufferTarget;
                var type=typeof(MxDirectionalShadowProvider).GetNestedType("Actor",BindingFlags.NonPublic);
                actor=(IDisposable)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{skin,false},null);
                var poseMethod=type.GetMethod("GetVertexBuffer",BindingFlags.Instance|BindingFlags.NonPublic);
                var buffer=(GraphicsBuffer)poseMethod.Invoke(actor,null);
                Assert.AreEqual(originalTarget,skin.vertexBufferTarget,"Editor previews must leave native skin buffer targets alone.");
                Assert.AreEqual(0,type.GetProperty("PoseBufferAcquisitions",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(actor,null));
                Assert.IsTrue((buffer.target&GraphicsBuffer.Target.Raw)!=0);
                Assert.AreEqual(4,buffer.stride);Assert.AreEqual(9,buffer.count);
                var before=new float[9];buffer.GetData(before);
                Assert.AreEqual(body.transform.localToWorldMatrix,type.GetField("PoseToWorld",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(actor));
                Assert.That(before[0],Is.EqualTo(0).Within(.0001f));
                Assert.That(before[3],Is.EqualTo(1).Within(.0001f));
                bone.transform.localPosition+=Vector3.up;
                var next=(GraphicsBuffer)poseMethod.Invoke(actor,null);
                Assert.AreSame(buffer,next,"Fallback storage must be reused.");
                var after=new float[9];next.GetData(after);
                Assert.That(after[1]-before[1],Is.EqualTo(1).Within(.0001f));
            }
            finally {actor?.Dispose();Object.DestroyImmediate(root);Object.DestroyImmediate(mesh);}
        }

        [Test]
        public void UnreadableDynamicMesh_IsRejectedBeforeCpuMetadataAccess()
        {
            var go=new GameObject("Unreadable shadow fixture"){hideFlags=HideFlags.HideAndDontSave};
            var mesh=Triangle();
            try
            {
                go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();
                mesh.UploadMeshData(true);
                var type=typeof(MxDirectionalShadowProvider).GetNestedType("Actor",BindingFlags.NonPublic);
                var error=Assert.Throws<TargetInvocationException>(()=>Activator.CreateInstance(type,
                    BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{renderer,false},null));
                Assert.That(error.InnerException,Is.TypeOf<NotSupportedException>());
                StringAssert.Contains("Read/Write Enabled",error.InnerException.Message);
            }
            finally {Object.DestroyImmediate(go);Object.DestroyImmediate(mesh);}
        }

        [Test]
        public void ReceiverFit_UsesLessArea_AndPreservesEveryReceiverAndLightRay()
        {
            var receivers=new Bounds(new Vector3(0,8.922486f,-15.5f),new Vector3(148,18.4649734f,151));
            var direction=new Vector3(-.48642045f,.52991927f,.69468033f).normalized;
            var rectangle=FitReceivers(receivers,direction,out var x,out var y);
            float determinant=Vector3.Dot(Vector3.Cross(x,y),direction);
            Assert.That(determinant,Is.GreaterThanOrEqualTo(.5f));
            Assert.That(Mathf.Abs(Vector3.Dot(x,direction)),Is.LessThan(.00001f));
            Assert.That(Mathf.Abs(Vector3.Dot(y,direction)),Is.LessThan(.00001f));
            Assert.That(rectangle.z*rectangle.w/determinant,Is.LessThan(213.41545f*139.31912f*.6f));
            for(int corner=0;corner<8;corner++)
            {
                var p=receivers.center+Vector3.Scale(receivers.extents,new Vector3(
                    (corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                var uv=new Vector2(Vector3.Dot(p,x),Vector3.Dot(p,y));
                Assert.That(uv.x,Is.InRange(rectangle.x,rectangle.x+rectangle.z));
                Assert.That(uv.y,Is.InRange(rectangle.y,rectangle.y+rectangle.w));
                var caster=p+direction*100;
                Assert.That(Vector3.Dot(caster,x),Is.EqualTo(uv.x).Within(.0001f));
                Assert.That(Vector3.Dot(caster,y),Is.EqualTo(uv.y).Within(.0001f));
            }
        }

        [TestCase(.015f)]
        [TestCase(.1f)]
        [TestCase(.53f)]
        [TestCase(1f)]
        public void ReceiverFit_KeepsAxesWellConditionedAtDifferentSunElevations(float elevation)
        {
            var direction=new Vector3(.3f,elevation,.7f).normalized;
            var rectangle=FitReceivers(new Bounds(Vector3.zero,new Vector3(80,12,60)),direction,out var x,out var y);
            Assert.That(x.magnitude,Is.EqualTo(1).Within(.00001f));
            Assert.That(y.magnitude,Is.EqualTo(1).Within(.00001f));
            Assert.That(Vector3.Dot(Vector3.Cross(x,y),direction),Is.GreaterThanOrEqualTo(.49999f));
            Assert.That(Mathf.Abs(Vector3.Dot(x,direction)),Is.LessThan(.00001f));
            Assert.That(Mathf.Abs(Vector3.Dot(y,direction)),Is.LessThan(.00001f));
            Assert.Greater(rectangle.z,0);Assert.Greater(rectangle.w,0);
        }

        [Test]
        public void DynamicFit_DoesNotPadAThinCoverageToASquare_AndSnapsToTexels()
        {
            var lo=new Vector2(-5,-1);var hi=new Vector2(7,1);
            var rectangle=MxDirectionalShadowProvider.FitCoverageRectangle(lo,hi,1024,Vector4.zero,.1f);
            Assert.Greater(rectangle.z,rectangle.w*4);
            Assert.Less(rectangle.z*rectangle.w,40);
            Assert.LessOrEqual(rectangle.x,lo.x-.1f);Assert.GreaterOrEqual(rectangle.x+rectangle.z,hi.x+.1f);
            Assert.LessOrEqual(rectangle.y,lo.y-.1f);Assert.GreaterOrEqual(rectangle.y+rectangle.w,hi.y+.1f);
            float cellX=rectangle.x/(rectangle.z/1024),cellY=rectangle.y/(rectangle.w/1024);
            Assert.That(cellX,Is.EqualTo(Mathf.Round(cellX)).Within(.001f));
            Assert.That(cellY,Is.EqualTo(Mathf.Round(cellY)).Within(.001f));
        }

        [Test]
        public void DynamicFit_HoldsScaleForSmallPoseChanges_ButReclaimsLargeEmptyAreas()
        {
            var lo=new Vector2(-10,-4);var hi=new Vector2(10,4);
            var first=MxDirectionalShadowProvider.FitCoverageRectangle(lo,hi,1024,Vector4.zero,.1f);
            var small=MxDirectionalShadowProvider.FitCoverageRectangle(lo+Vector2.one*.02f,hi-Vector2.one*.02f,1024,first,.1f);
            Assert.That(small.z,Is.EqualTo(first.z).Within(.00001f));
            Assert.That(small.w,Is.EqualTo(first.w).Within(.00001f));
            var tight=MxDirectionalShadowProvider.FitCoverageRectangle(new Vector2(-2,-1),new Vector2(2,1),1024,first,.1f);
            Assert.Less(tight.z*tight.w,first.z*first.w*.2f);
            var grown=MxDirectionalShadowProvider.FitCoverageRectangle(new Vector2(-30,-20),new Vector2(30,20),1024,tight,.1f);
            Assert.LessOrEqual(grown.x,-30.1f);Assert.GreaterOrEqual(grown.x+grown.z,30.1f);
            Assert.LessOrEqual(grown.y,-20.1f);Assert.GreaterOrEqual(grown.y+grown.w,20.1f);
        }

        [Test]
        public void ReceiverClip_KeepsFilterBorder_ReclaimsUnusedCoverage_AndRetainsUnboundedScopes()
        {
            var region=new MxDirectionalShadowReceiverRegion{Minimum=new Vector2(-2,-1),Maximum=new Vector2(2,1)};
            var casterLo=new Vector2(-100,-50);var casterHi=new Vector2(100,50);
            Assert.IsTrue(MxDirectionalShadowInfluence.TryClipProjected(casterLo,casterHi,region,.25f,out var lo,out var hi));
            Assert.AreEqual(new Vector2(-2.25f,-1.25f),lo);Assert.AreEqual(new Vector2(2.25f,1.25f),hi);
            Assert.IsFalse(MxDirectionalShadowInfluence.TryClipProjected(new Vector2(10,10),new Vector2(11,11),region,.25f,out _,out _));
            region.Unbounded=true;
            Assert.IsTrue(MxDirectionalShadowInfluence.TryClipProjected(casterLo,casterHi,region,.25f,out lo,out hi));
            Assert.AreEqual(casterLo,lo);Assert.AreEqual(casterHi,hi);
        }

        [TestCase(128)]
        [TestCase(512)]
        [TestCase(4096)]
        public void SplitRegions_AllocateMoreTexelsToCenter_WithinTheOriginalBudget(int budget)
        {
            var method=typeof(MxDirectionalShadowBaker).GetMethod("RegionResolutions",BindingFlags.Static|BindingFlags.NonPublic);
            object[] args={budget,0,0};method.Invoke(null,args);
            int center=(int)args[1],peripheral=(int)args[2];
            Assert.Greater(center,peripheral);
            Assert.LessOrEqual((long)center*center+(long)peripheral*peripheral,(long)budget*budget);
            if(budget==4096){Assert.AreEqual(3072,center);Assert.AreEqual(2048,peripheral);}
        }

        [Test]
        public void SplitFingerprint_DetectsAChangedCenterRegion()
        {
            var mesh=Triangle();var material=new Material(Shader.Find("WGame/PropStylizedLit"));
            try
            {
                var sources=new[]{new MxShadowBakeSource(mesh,material,Matrix4x4.identity,0)};
                var full=new Bounds(Vector3.zero,Vector3.one*20);
                var center=new Bounds(Vector3.zero,Vector3.one*5);
                string before=MxDirectionalShadowBaker.Fingerprint(sources,full,center,Vector3.up,128);
                center.center=Vector3.right;
                Assert.AreNotEqual(before,MxDirectionalShadowBaker.Fingerprint(sources,full,center,Vector3.up,128));
            }
            finally{Object.DestroyImmediate(mesh);Object.DestroyImmediate(material);}
        }

        [Test]
        public void SplitData_RequiresBothRegions_AndAccountsForBothTextures()
        {
            var center=new Texture2D(32,32,TextureFormat.RFloat,false,true);
            var peripheral=new Texture2D(16,16,TextureFormat.RFloat,false,true);
            var data=ScriptableObject.CreateInstance<MxDirectionalShadowData>();
            try
            {
                MxDirectionalShadowData.Basis(Vector3.up,out var x,out var y);
                data.Initialize(center,peripheral,Vector3.up,x,y,new Vector4(-2,-2,4,4),new Vector4(-10,-10,20,20),
                    new Vector4(-10,20,0,0),new Bounds(Vector3.zero,Vector3.one*20),new Bounds(Vector3.zero,Vector3.one*4),
                    64,1,"regions",1,1);
                Assert.IsTrue(data.IsValid);Assert.AreEqual(5120,data.TextureBytes);
                Assert.AreSame(peripheral,data.PeripheralDepth);Assert.AreEqual(64,data.TextureBudgetResolution);
                Assert.AreEqual(1,data.TransitionWidth);
                Object.DestroyImmediate(peripheral);
                Assert.IsFalse(data.IsValid,"Missing peripheral data must fail closed instead of exposing a bright seam.");
            }
            finally{Object.DestroyImmediate(data);Object.DestroyImmediate(center);if(peripheral!=null)Object.DestroyImmediate(peripheral);}
        }

        [Test]
        public void CenterProjection_PlacesAuthoredReceiversInsideTheFullQualityPartOfTheBlendBand()
        {
            var full=new Bounds(new Vector3(0,8,-15),new Vector3(148,18,151));
            var center=new Bounds(new Vector3(0,8,0),new Vector3(80,18,60));
            var direction=new Vector3(-.48642045f,.52991927f,.69468033f).normalized;
            FitReceivers(full,direction,out var x,out var y);
            var method=typeof(MxDirectionalShadowBaker).GetMethod("FitReceiverRectangle",BindingFlags.Static|BindingFlags.NonPublic);
            var rect=(Vector4)method.Invoke(null,new object[]{center,3072,x,y,1.1f});
            for(int corner=0;corner<8;corner++)
            {
                var p=center.center+Vector3.Scale(center.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                var uv=new Vector2(Vector3.Dot(p,x),Vector3.Dot(p,y));
                float edge=Mathf.Min(Mathf.Min(uv.x-rect.x,uv.y-rect.y),Mathf.Min(rect.x+rect.z-uv.x,rect.y+rect.w-uv.y));
                Assert.GreaterOrEqual(edge,1,"The high-detail region must cover the entire authored battlefield before fading.");
            }
        }

        private static Vector4 FitReceivers(Bounds receivers,Vector3 direction,out Vector3 x,out Vector3 y)
        {
            var method=typeof(MxDirectionalShadowBaker).GetMethod("FitReceiverProjection",BindingFlags.Static|BindingFlags.NonPublic);
            object[] args={receivers,direction,4096,Vector3.zero,Vector3.zero};
            var rectangle=(Vector4)method.Invoke(null,args);x=(Vector3)args[3];y=(Vector3)args[4];return rectangle;
        }
        private static Mesh Triangle()=>new Mesh
        {
            vertices=new[]{Vector3.zero,Vector3.right,Vector3.up},triangles=new[]{0,1,2},
            hideFlags=HideFlags.HideAndDontSave
        };
        private static BoneWeight Weight()=>new BoneWeight{boneIndex0=0,weight0=1};
        private static string Fingerprint(Mesh mesh,Material material)=>MxDirectionalShadowBaker.Fingerprint(
            new[]{new MxShadowBakeSource(mesh,material,Matrix4x4.identity,0)},
            new Bounds(Vector3.zero,Vector3.one*10),Vector3.up,128);
    }
}
