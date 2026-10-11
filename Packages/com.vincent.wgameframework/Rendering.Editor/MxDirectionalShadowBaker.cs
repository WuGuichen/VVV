using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MxFramework.Rendering.Editor
{
    public readonly struct MxShadowBakeSource
    {
        public readonly Mesh Mesh;
        public readonly Material Material;
        public readonly Matrix4x4 LocalToWorld;
        public readonly int SubMesh;
        public readonly MxDirectionalShadowCoverage Coverage;
        public readonly ShadowCastingMode CastingMode;
        public readonly bool HasExplicitCoverage;
        internal bool CanCast => CastingMode!=ShadowCastingMode.Off && MxDirectionalShadowCoverage.CanCast(Material,HasExplicitCoverage);
        public MxShadowBakeSource(Mesh mesh, Material material, Matrix4x4 matrix, int subMesh,
            MaterialPropertyBlock objectProperties = null, MaterialPropertyBlock subProperties = null)
            : this(mesh,material,matrix,subMesh,objectProperties,subProperties,ShadowCastingMode.On) { }
        public MxShadowBakeSource(Mesh mesh, Material material, Matrix4x4 matrix, int subMesh,
            MaterialPropertyBlock objectProperties, MaterialPropertyBlock subProperties, ShadowCastingMode castingMode)
            : this(mesh,material,matrix,subMesh,objectProperties,subProperties,castingMode,false) { }
        public MxShadowBakeSource(Mesh mesh, Material material, Matrix4x4 matrix, int subMesh,
            MaterialPropertyBlock objectProperties, MaterialPropertyBlock subProperties, ShadowCastingMode castingMode,
            bool hasExplicitCoverage)
        {
            Mesh=mesh;Material=material;LocalToWorld=matrix;SubMesh=subMesh;CastingMode=castingMode;HasExplicitCoverage=hasExplicitCoverage;
            Coverage=material!=null?MxDirectionalShadowCoverage.Read(material,objectProperties,subProperties):default;
        }
    }

    /// <summary>Reference geometry and alpha are rasterized offline; no runtime capture or mesh draw.</summary>
    public static class MxDirectionalShadowBaker
    {
        public static string Fingerprint(IReadOnlyList<MxShadowBakeSource> sources, Bounds receiverBounds, Vector3 direction, int resolution)
            =>Fingerprint(sources,receiverBounds,DefaultCenterBounds(receiverBounds),direction,resolution);
        public static string Fingerprint(IReadOnlyList<MxShadowBakeSource> sources,Bounds receiverBounds,Bounds centerBounds,
            Vector3 direction,int resolution)
            =>Fingerprint(sources,receiverBounds,centerBounds,direction,resolution,0);
        public static string Fingerprint(IReadOnlyList<MxShadowBakeSource> sources,Bounds receiverBounds,Bounds centerBounds,
            Vector3 direction,int resolution,int centerResolution)
            => Fingerprint(sources,receiverBounds,centerBounds,direction,resolution,centerResolution,Vector4.zero);
        public static string Fingerprint(IReadOnlyList<MxShadowBakeSource> sources,Bounds receiverBounds,Bounds centerBounds,
            Vector3 direction,int resolution,int centerResolution,Vector4 planeCoverage)
        {
            if (!MxDirectionalShadowData.IsSupportedPlaneCoverage(planeCoverage)) throw new ArgumentException("Invalid plane coverage.");
            direction.Normalize();
            var text = new StringBuilder("DirectionalShadow:v6:caster-admission:center-periphery:3of4-1of2:blend:");
            var identities=new Dictionary<UnityEngine.Object,string>();
            void Float(float value) => text.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append(';');
            Float(receiverBounds.center.x); Float(receiverBounds.center.y); Float(receiverBounds.center.z);
            Float(receiverBounds.size.x); Float(receiverBounds.size.y); Float(receiverBounds.size.z);
            Float(centerBounds.center.x);Float(centerBounds.center.y);Float(centerBounds.center.z);
            Float(centerBounds.size.x);Float(centerBounds.size.y);Float(centerBounds.size.z);
            Float(direction.x); Float(direction.y); Float(direction.z); text.Append(resolution).Append(';');
            // Preserve legacy fingerprints when the actual center still uses the
            // original split. Only an explicit differing center size extends it.
            if(centerResolution>0 && centerResolution!=resolution*3/4)
                text.Append("center-resolution:").Append(centerResolution).Append(';');
            if (planeCoverage.x == 1)
            {
                text.Append("plane-coverage:v1:8x8:mantissa6:");
                Float(planeCoverage.y); Float(planeCoverage.z); Float(planeCoverage.w);
            }
            foreach (var source in sources)
            {
                if(!source.CanCast)continue;
                text.Append((int)source.CastingMode).Append(';').Append(source.HasExplicitCoverage ? 1 : 0).Append(';');
                Asset(source.Mesh); Asset(source.Material); text.Append(source.SubMesh).Append(';');
                for (int i=0;i<16;i++) Float(source.LocalToWorld[i]);
                var c = source.Coverage; text.Append(c.Mode).Append(';').Append(c.DissolveMode).Append(';'); Float(c.Alpha); Float(c.Cutoff); Float(c.Dissolve);
                for (int i=0;i<4;i++) Float(c.BaseMapST[i]); Asset(c.Mask); Asset(c.Noise);
            }
            void Asset(UnityEngine.Object asset)
            {
                if(asset==null){text.Append("null;");return;}
                if(!identities.TryGetValue(asset,out string identity))
                {
                    string path=AssetDatabase.GetAssetPath(asset);
                    string builtin=BuiltInTextureIdentity(asset);
                    if(builtin!=null)identity=builtin;
                    else if(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset,out string guid,out long localId))
                        identity=guid+":"+localId.ToString(CultureInfo.InvariantCulture)+":"+AssetDatabase.GetAssetDependencyHash(path);
                    else if(asset is Mesh mesh)identity="mesh:"+MeshContentFingerprint(mesh);
                    else if(asset is Material material && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material.shader,out string shaderGuid,out long shaderId))
                        // All material values used by the bake are already in Coverage.
                        // Recreating an equivalent procedural material must be stable.
                        identity="material:"+shaderGuid+":"+shaderId.ToString(CultureInfo.InvariantCulture)+":"+
                            AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(material.shader));
                    else throw new NotSupportedException("Persist the shadow input as an asset before baking: "+asset.name);
                    identities.Add(asset,identity);
                }
                text.Append(identity.Length).Append(':').Append(identity).Append(';');
            }
            return Hash128.Compute(text.ToString()).ToString();
        }
        private static string BuiltInTextureIdentity(UnityEngine.Object asset)
        {
            // These textures have no GUID. EntityId is process-local and invalidates
            // an otherwise unchanged bake after restarting the Editor.
            if(asset==Texture2D.whiteTexture)return "builtin:white";
            if(asset==Texture2D.blackTexture)return "builtin:black";
            if(asset==Texture2D.redTexture)return "builtin:red";
            if(asset==Texture2D.grayTexture)return "builtin:gray";
            if(asset==Texture2D.linearGrayTexture)return "builtin:linear-gray";
            if(asset==Texture2D.normalTexture)return "builtin:normal";
            return null;
        }
        private static string MeshContentFingerprint(Mesh mesh)
        {
            // Procedural meshes have no persistent subasset ID. Detect edits made to
            // the same instance; cache this content hash once per Fingerprint call.
            var snapshot=new MeshSnapshot(mesh);
            var text=new StringBuilder();
            void Float(float value)=>text.Append(value.ToString("R",CultureInfo.InvariantCulture)).Append(';');
            text.Append(snapshot.Positions.Length).Append(';');
            for(int i=0;i<snapshot.Positions.Length;i++)
            {
                var p=snapshot.Positions[i];Float(p.x);Float(p.y);Float(p.z);
                var a=snapshot.Attributes[i];Float(a.x);Float(a.y);Float(a.z);Float(a.w);
            }
            text.Append(snapshot.Indices.Length).Append(';');
            for(int sub=0;sub<snapshot.Indices.Length;sub++)
            {
                text.Append((int)mesh.GetTopology(sub)).Append(';').Append(snapshot.Indices[sub].Length).Append(';');
                foreach(int index in snapshot.Indices[sub])text.Append(index).Append(';');
            }
            return Hash128.Compute(text.ToString()).ToString();
        }
        internal static Vector4 FitReceiverProjection(Bounds receivers,Vector3 direction,int resolution,
            out Vector3 basisX,out Vector3 basisY)
        {
            direction.Normalize();
            MxDirectionalShadowData.Basis(direction,out var bestX,out var bestY);
            double bestArea=double.PositiveInfinity;
            void Consider(Vector3 x,Vector3 y)
            {
                if(x.sqrMagnitude<1e-8f||y.sqrMagnitude<1e-8f)return;
                x.Normalize();y.Normalize();
                float determinant=Vector3.Dot(Vector3.Cross(x,y),direction);
                // Unit axes with a bounded angle keep projection/filter arithmetic
                // well conditioned. Score real light-plane area, not skewed UV area.
                if(Mathf.Abs(determinant)<.5f)return;
                if(determinant<0)y=-y;
                var e=receivers.extents;
                double width=2*(Math.Abs(x.x)*e.x+Math.Abs(x.y)*e.y+Math.Abs(x.z)*e.z);
                double height=2*(Math.Abs(y.x)*e.x+Math.Abs(y.y)*e.y+Math.Abs(y.z)*e.z);
                double area=Math.Max(.01,width)*Math.Max(.01,height)/Math.Abs(determinant);
                if(double.IsNaN(area)||double.IsInfinity(area)||area>=bestArea*.999999)return;
                bestArea=area;bestX=x;bestY=y;
            }
            Consider(bestX,bestY);
            var axes=new[]{Vector3.right,Vector3.up,Vector3.forward};
            for(int i=0;i<3;i++)
            {
                // Projected box edges contain the minimum-area orthographic fit.
                var x=axes[i]-direction*Vector3.Dot(axes[i],direction);
                Consider(x,Vector3.Cross(direction,x));
                // A bounded affine shear can straighten the footprint of a wide
                // receiver plane. Both axes still annihilate the light direction:
                // p and p+t*direction retain identical UV, for every 3D receiver.
                var n=axes[i];float divisor=Vector3.Dot(n,direction);
                if(Mathf.Abs(divisor)<.05f)continue;
                var a=axes[(i+1)%3];var b=axes[(i+2)%3];
                Consider(a-n*(Vector3.Dot(a,direction)/divisor),b-n*(Vector3.Dot(b,direction)/divisor));
            }
            if(double.IsInfinity(bestArea))throw new ArgumentException("Receiver projection is not finite.");
            basisX=bestX;basisY=bestY;
            return FitReceiverRectangle(receivers,resolution,bestX,bestY,.1f);
        }
        internal static Vector4 FitReceiverRectangle(Bounds receivers,int resolution,Vector3 basisX,Vector3 basisY,float border)
        {
            var centre=new Vector2(Vector3.Dot(receivers.center,basisX),Vector3.Dot(receivers.center,basisY));
            var extents=receivers.extents;
            var extent=new Vector2(Mathf.Abs(basisX.x)*extents.x+Mathf.Abs(basisX.y)*extents.y+Mathf.Abs(basisX.z)*extents.z,
                Mathf.Abs(basisY.x)*extents.x+Mathf.Abs(basisY.y)*extents.y+Mathf.Abs(basisY.z)*extents.z);
            // Authored receivers, plus a small world/PCF border, define XY coverage.
            // Remote casters must affect depth, but must not enlarge this rectangle.
            extent+=Vector2.one*Mathf.Max(0,border);
            var span=Vector2.Max(extent*2,Vector2.one*.01f);
            var step=span/Mathf.Max(1,resolution-6);
            var minimum=centre-extent-step*2;
            minimum=new Vector2(Mathf.Floor(minimum.x/step.x)*step.x,Mathf.Floor(minimum.y/step.y)*step.y);
            return new Vector4(minimum.x,minimum.y,step.x*resolution,step.y*resolution);
        }
        private static Bounds DefaultCenterBounds(Bounds receivers)=>new Bounds(receivers.center,
            new Vector3(receivers.size.x*.5f,receivers.size.y,receivers.size.z*.5f));
        internal static void RegionResolutions(int budget,out int center,out int peripheral)
        {center=budget*3/4;peripheral=budget/2;}
        public static MxDirectionalShadowData Bake(IReadOnlyList<MxShadowBakeSource> sources, Bounds receiverBounds,
            Vector3 lightDirection, string folder, int resolution = 4096)
            =>Bake(sources,receiverBounds,DefaultCenterBounds(receiverBounds),lightDirection,folder,resolution);
        public static MxDirectionalShadowData Bake(IReadOnlyList<MxShadowBakeSource> sources,Bounds receiverBounds,
            Bounds centerBounds,Vector3 lightDirection,string folder,int resolution=4096)
            =>Bake(sources,receiverBounds,centerBounds,lightDirection,folder,resolution,0,null);
        /// <summary>Optionally rebuilds only the center of an unchanged, validated source field.</summary>
        public static MxDirectionalShadowData Bake(IReadOnlyList<MxShadowBakeSource> sources,Bounds receiverBounds,
            Bounds centerBounds,Vector3 lightDirection,string folder,int resolution,int centerResolutionOverride,
            MxDirectionalShadowData reusePeripheral)
            => Bake(sources,receiverBounds,centerBounds,lightDirection,folder,resolution,centerResolutionOverride,reusePeripheral,Vector4.zero);
        public static MxDirectionalShadowData Bake(IReadOnlyList<MxShadowBakeSource> sources,Bounds receiverBounds,
            Bounds centerBounds,Vector3 lightDirection,string folder,int resolution,int centerResolutionOverride,
            MxDirectionalShadowData reusePeripheral,Vector4 planeCoverage)
        {
            if (!MxDirectionalShadowData.IsSupportedPlaneCoverage(planeCoverage)) throw new ArgumentException("Invalid plane coverage.");
            if (Application.isPlaying) throw new InvalidOperationException("Bake reference shadows in Edit Mode.");
            if (!folder.StartsWith("Assets/", StringComparison.Ordinal)) throw new ArgumentException("Shadow assets must be under Assets.");
            if (sources == null || sources.Count == 0) throw new ArgumentException("No static shadow sources.");
            resolution = Mathf.Clamp(Mathf.NextPowerOfTwo(resolution),128,8192);
            if(!receiverBounds.Contains(centerBounds.min)||!receiverBounds.Contains(centerBounds.max))
                throw new ArgumentException("The high-detail center must be inside the full receiver coverage.");
            RegionResolutions(resolution,out int centerResolution,out int peripheralResolution);
            if(centerResolutionOverride!=0)
            {
                if(centerResolutionOverride<128 || centerResolutionOverride>8192 || !Mathf.IsPowerOfTwo(centerResolutionOverride))
                    throw new ArgumentException("Center shadow resolution must be a power of two between 128 and 8192.");
                centerResolution=centerResolutionOverride;
            }
            lightDirection.Normalize();
            if(reusePeripheral!=null && (!reusePeripheral.IsValid ||
                AssetDatabase.GetAssetPath(reusePeripheral)!=folder+"/DirectionalShadows.asset" ||
                reusePeripheral.PlaneCoverage != planeCoverage ||
                reusePeripheral.TextureBudgetResolution!=resolution || reusePeripheral.PeripheralDepth.width!=peripheralResolution ||
                reusePeripheral.SourceFingerprint!=Fingerprint(sources,receiverBounds,reusePeripheral.CenterBounds,
                    lightDirection,resolution,reusePeripheral.Depth.width,planeCoverage)))
                throw new InvalidOperationException("Peripheral shadow inputs changed; a center-only bake cannot reuse this field.");
            if (lightDirection.y <= .01f) throw new ArgumentException("The fixed directional sun must be above the receiver plane.");
            Vector4 peripheralRectangle=FitReceiverProjection(receiverBounds,lightDirection,peripheralResolution,out Vector3 basisX,out Vector3 basisY);
            float transitionWidth=Mathf.Min(1f,Mathf.Max(.01f,Mathf.Min(centerBounds.size.x,centerBounds.size.z)*.05f));
            if(reusePeripheral!=null)
            {
                basisX=reusePeripheral.BasisX;basisY=reusePeripheral.BasisY;
                peripheralRectangle=reusePeripheral.PeripheralRectangle;transitionWidth=reusePeripheral.TransitionWidth;
            }
            // Keep the authored center fully in the high-detail part; fade in a
            // padded band outside its projected receiver bounds.
            Vector4 rectangle=FitReceiverRectangle(centerBounds,centerResolution,basisX,basisY,transitionWidth+.1f);
            Bounds worldBounds = receiverBounds;
            int castingSources=0;
            foreach (var source in sources)
            {
                if(!source.CanCast)continue;
                castingSources++;
                Bounds b = source.Mesh.bounds;
                for(int k=0;k<8;k++) worldBounds.Encapsulate(source.LocalToWorld.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents,
                    new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1))));
            }
            if(castingSources==0)throw new ArgumentException("No sources with Cast Shadows enabled.");
            // Include reference receivers and practical actor motion above the flat floor.
            worldBounds.Expand(new Vector3(4,12,4));
            Vector3 minimum = new Vector3(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity), maximum = -minimum;
            for(int k=0;k<8;k++)
            {
                Vector3 p=worldBounds.center+Vector3.Scale(worldBounds.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1));
                Vector3 projected=new Vector3(Vector3.Dot(p,basisX),Vector3.Dot(p,basisY),-Vector3.Dot(p,lightDirection));
                minimum=Vector3.Min(minimum,projected);maximum=Vector3.Max(maximum,projected);
            }
            // Retain the conservative common depth slab (including offscreen
            // casters and actor motion). Only the XY atlas footprint is tightened.
            float depthSpan=maximum.z-minimum.z;
            var depthRange=new Vector4(minimum.z,depthSpan,0,0);
            if(reusePeripheral!=null)
            {
                depthRange=reusePeripheral.DepthRange;minimum.z=depthRange.x;depthSpan=depthRange.y;
                worldBounds=reusePeripheral.WorldBounds;
            }
            var depth=new float[centerResolution*centerResolution];for(int i=0;i<depth.Length;i++)depth[i]=1;
            var peripheralDepth=reusePeripheral==null?new float[peripheralResolution*peripheralResolution]:null;
            if(peripheralDepth!=null)for(int i=0;i<peripheralDepth.Length;i++)peripheralDepth[i]=1;
            var meshes=new Dictionary<Mesh,MeshSnapshot>();
            var masks=new Dictionary<Texture,MaskSnapshot>();
            ulong[] coverageScratch=null;
            int triangles=0;
            try
            {
                for(int sourceIndex=0;sourceIndex<sources.Count;sourceIndex++)
                {
                    if ((sourceIndex&31)==0 && EditorUtility.DisplayCancelableProgressBar("真实轮廓静态阴影烘焙",
                        "参考几何与 alpha："+sourceIndex+" / "+sources.Count,(float)sourceIndex/sources.Count)) throw new OperationCanceledException();
                    var source=sources[sourceIndex];
                    if(!source.CanCast)continue;
                    if(source.Mesh.GetTopology(source.SubMesh)!=UnityEngine.MeshTopology.Triangles)throw new NotSupportedException("Static shadow sources must use triangles.");
                    if(!meshes.TryGetValue(source.Mesh,out var mesh))meshes.Add(source.Mesh,mesh=new MeshSnapshot(source.Mesh));
                    var coverage=source.Coverage;
                    MaskSnapshot mask=coverage.Mode==0?null:GetMask(coverage.Mask,masks);
                    MaskSnapshot noise=coverage.Dissolve>0 && coverage.DissolveMode!=0?GetMask(coverage.Noise,masks):null;
                    int[] indices=mesh.Indices[source.SubMesh];
                    for(int t=0;t<indices.Length;t+=3)
                    {
                        int i0=indices[t],i1=indices[t+1],i2=indices[t+2];
                        Vector3 a=Project(source.LocalToWorld.MultiplyPoint3x4(mesh.Positions[i0]));
                        Vector3 b=Project(source.LocalToWorld.MultiplyPoint3x4(mesh.Positions[i1]));
                        Vector3 c=Project(source.LocalToWorld.MultiplyPoint3x4(mesh.Positions[i2]));
                        // Every source participates in both regions. A caster's
                        // world position does not determine which region it shadows.
                        Raster(ToMap(a,rectangle,centerResolution),ToMap(b,rectangle,centerResolution),ToMap(c,rectangle,centerResolution),
                            mesh.Attributes[i0],mesh.Attributes[i1],mesh.Attributes[i2],coverage,mask,noise,depth,centerResolution);
                        if(peripheralDepth!=null)Raster(ToMap(a,peripheralRectangle,peripheralResolution),ToMap(b,peripheralRectangle,peripheralResolution),ToMap(c,peripheralRectangle,peripheralResolution),
                            mesh.Attributes[i0],mesh.Attributes[i1],mesh.Attributes[i2],coverage,mask,noise,peripheralDepth,peripheralResolution);
                        triangles++;
                    }
                    Vector3 Project(Vector3 p)=>new Vector3(Vector3.Dot(p,basisX),Vector3.Dot(p,basisY),
                        (-Vector3.Dot(p,lightDirection)-minimum.z)/depthSpan);
                }
                // Coverage is evaluated at the declared fixed receiver, not at an arbitrary
                // nearest occluder. Process each map separately so at most one ulong mask
                // array (128 MiB at 4096) is allocated in addition to the original depth arrays.
                if (planeCoverage.x == 1)
                {
                    BakePlaneCoverage(depth, centerResolution, rectangle);
                    if (peripheralDepth != null) BakePlaneCoverage(peripheralDepth, peripheralResolution, peripheralRectangle);
                }
                EnsureFolder(folder);
                string dataPath=folder+"/DirectionalShadows.asset";
                var data=AssetDatabase.LoadAssetAtPath<MxDirectionalShadowData>(dataPath);
                // A failed in-place second texture write must not leave old metadata
                // advertising a valid mixed raw/packed field.
                if (data!=null) { data.InvalidateForRebuild(); EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data); }
                var existingTexture=SaveDepth(folder+"/StaticDepth.asset","DirectionalShadows.CenterDepth",centerResolution,depth);
                var existingPeripheral=reusePeripheral!=null?reusePeripheral.PeripheralDepth:
                    SaveDepth(folder+"/PeripheralDepth.asset","DirectionalShadows.PeripheralDepth",peripheralResolution,peripheralDepth);
                if(data==null){data=ScriptableObject.CreateInstance<MxDirectionalShadowData>();AssetDatabase.CreateAsset(data,dataPath);}
                data.Initialize(existingTexture,existingPeripheral,lightDirection,basisX,basisY,rectangle,peripheralRectangle,
                    depthRange,worldBounds,centerBounds,resolution,transitionWidth,
                    Fingerprint(sources,receiverBounds,centerBounds,lightDirection,resolution,centerResolution,planeCoverage),triangles,castingSources,planeCoverage);
                EditorUtility.SetDirty(data);AssetDatabase.SaveAssetIfDirty(data);
                return data;
            }
            finally{EditorUtility.ClearProgressBar();}

            void BakePlaneCoverage(float[] target,int mapResolution,Vector4 mapRectangle)
            {
                if(coverageScratch==null)coverageScratch=new ulong[Math.Max(depth.Length,peripheralDepth==null?0:peripheralDepth.Length)];
                var samples=coverageScratch;Array.Clear(samples,0,target.Length);
                var receiverPlane=ReceiverPlaneDepth(mapRectangle,mapResolution,basisX,basisY,lightDirection,depthRange,planeCoverage.y);
                float normalizedBias=planeCoverage.w/depthRange.y;
                for(int sourceIndex=0;sourceIndex<sources.Count;sourceIndex++)
                {
                    if ((sourceIndex&15)==0 && EditorUtility.DisplayCancelableProgressBar("固定地面阴影覆盖烘焙",
                        "8x8 子像素覆盖："+sourceIndex+" / "+sources.Count,(float)sourceIndex/sources.Count)) throw new OperationCanceledException();
                    var source=sources[sourceIndex]; if(!source.CanCast)continue;
                    var mesh=meshes[source.Mesh]; var coverage=source.Coverage;
                    var mask=coverage.Mode==0?null:GetMask(coverage.Mask,masks);
                    var noise=coverage.Dissolve>0&&coverage.DissolveMode!=0?GetMask(coverage.Noise,masks):null;
                    int[] indices=mesh.Indices[source.SubMesh];
                    for(int t=0;t<indices.Length;t+=3)
                    {
                        int i0=indices[t],i1=indices[t+1],i2=indices[t+2];
                        RasterPlaneCoverage(Map(i0),Map(i1),Map(i2),mesh.Attributes[i0],mesh.Attributes[i1],mesh.Attributes[i2],
                            coverage,mask,noise,samples,mapResolution,receiverPlane,normalizedBias);
                    }
                    Vector3 Map(int index)
                    {
                        Vector3 p=source.LocalToWorld.MultiplyPoint3x4(mesh.Positions[index]);
                        return ToMap(new Vector3(Vector3.Dot(p,basisX),Vector3.Dot(p,basisY),
                            (-Vector3.Dot(p,lightDirection)-depthRange.x)/depthRange.y),mapRectangle,mapResolution);
                    }
                }
                for(int i=0;i<target.Length;i++)target[i]=EncodePlaneCoverage(target[i],samples[i]);
            }
        }
        [StructLayout(LayoutKind.Explicit)]
        private struct DepthBits
        {
            [FieldOffset(0)] public float Float;
            [FieldOffset(0)] public uint UInt;
        }
        internal static float EncodePlaneCoverage(float centreDepth,ulong sampleMask)
        {
            if(float.IsNaN(centreDepth)||float.IsInfinity(centreDepth)||centreDepth<0||centreDepth>1)
                throw new ArgumentOutOfRangeException(nameof(centreDepth));
            int count=0;while(sampleMask!=0){sampleMask&=sampleMask-1;count++;}
            uint bits=new DepthBits{Float=centreDepth}.UInt;
            uint rounded=(bits+32u)&0xffffffc0u;if(rounded<0x00800000u)rounded=0x00800000u;
            return new DepthBits{UInt=rounded|(uint)(count*63+32)/64}.Float;
        }
        internal static Vector3 ReceiverPlaneDepth(Vector4 rectangle,int resolution,Vector3 basisX,Vector3 basisY,
            Vector3 direction,Vector4 range,float planeY)
        {
            double determinant=(double)basisX.x*basisY.z-(double)basisX.z*basisY.x;
            if(Math.Abs(determinant)<1e-8)throw new ArgumentException("The shadow projection is singular on the selected receiver plane.");
            double Depth(double x,double y)
            {
                double u=rectangle.x+x*rectangle.z/resolution-basisX.y*planeY;
                double v=rectangle.y+y*rectangle.w/resolution-basisY.y*planeY;
                double wx=(u*basisY.z-v*basisX.z)/determinant;
                double wz=(basisX.x*v-basisY.x*u)/determinant;
                return (-wx*direction.x-planeY*direction.y-wz*direction.z-range.x)/range.y;
            }
            double origin=Depth(0,0);
            return new Vector3((float)(Depth(1,0)-origin),(float)(Depth(0,1)-origin),(float)origin);
        }
        private static void RasterPlaneCoverage(Vector3 a, Vector3 b, Vector3 c,
            Vector4 aa, Vector4 ab, Vector4 ac, MxDirectionalShadowCoverage coverage,
            MaskSnapshot mask, MaskSnapshot noise, ulong[] sampleMasks, int resolution,
            Vector3 planeDepth, float normalizedBias)
        {
            float area = Cross(b-a,c-a);
            if (Mathf.Abs(area) < .000001f) return;
            // Receiver minus caster depth is affine over a triangle. If all vertices
            // lie on/behind the biased receiver, no sub-sample can occlude the plane.
            // This rejects the large ground triangles before the 64-sample loop.
            if (a.x*planeDepth.x+a.y*planeDepth.y+planeDepth.z-a.z<=normalizedBias &&
                b.x*planeDepth.x+b.y*planeDepth.y+planeDepth.z-b.z<=normalizedBias &&
                c.x*planeDepth.x+c.y*planeDepth.y+planeDepth.z-c.z<=normalizedBias) return;
            int minX=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x))));
            int minY=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y))));
            int maxX=Mathf.Min(resolution-1,Mathf.FloorToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))));
            int maxY=Mathf.Min(resolution-1,Mathf.FloorToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))));
            for (int y=minY;y<=maxY;y++) for (int x=minX;x<=maxX;x++)
            {
                int index=y*resolution+x;
                ulong bits=sampleMasks[index];
                if (bits==ulong.MaxValue) continue;
                for (int sy=0;sy<8;sy++) for (int sx=0;sx<8;sx++)
                {
                    ulong bit=1UL<<(sy*8+sx);
                    if ((bits&bit)!=0) continue;
                    float px=x+(sx+.5f)/8, py=y+(sy+.5f)/8;
                    var p=new Vector3(px-a.x,py-a.y,0);
                    float wb=Cross(p,c-a)/area,wc=Cross(b-a,p)/area,wa=1-wb-wc;
                    if (wa<0||wb<0||wc<0) continue;
                    float depth=a.z*wa+b.z*wb+c.z*wc;
                    float receiver=px*planeDepth.x+py*planeDepth.y+planeDepth.z;
                    if (receiver<0||receiver>1||receiver<=depth+normalizedBias) continue;
                    Vector4 attribute=aa*wa+ab*wb+ac*wc;
                    if (!Covered(coverage,new Vector2(attribute.x,attribute.y),attribute.z,mask,noise)) continue;
                    bits|=bit;
                }
                sampleMasks[index]=bits;
            }
        }
        private static Vector3 ToMap(Vector3 p,Vector4 rectangle,int resolution)=>new Vector3(
            (p.x-rectangle.x)/rectangle.z*resolution,(p.y-rectangle.y)/rectangle.w*resolution,p.z);
        private static Texture2D SaveDepth(string path,string name,int resolution,float[] depth)
        {
            var texture=new Texture2D(resolution,resolution,TextureFormat.RFloat,false,true)
                {name=name,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixelData(depth,0);texture.Apply(false,true);
            var asset=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(asset==null){AssetDatabase.CreateAsset(texture,path);asset=texture;}
            else{EditorUtility.CopySerialized(texture,asset);UnityEngine.Object.DestroyImmediate(texture);EditorUtility.SetDirty(asset);}
            AssetDatabase.SaveAssetIfDirty(asset);return asset;
        }
        private static void Raster(Vector3 a,Vector3 b,Vector3 c,Vector4 aa,Vector4 ab,Vector4 ac,
            MxDirectionalShadowCoverage coverage,MaskSnapshot mask,MaskSnapshot noise,float[] depth,int resolution)
        {
            float area=Cross(b-a,c-a);if(Mathf.Abs(area)<.000001f)return;
            int minX=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x)))),minY=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y))));
            int maxX=Mathf.Min(resolution-1,Mathf.FloorToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x)))),maxY=Mathf.Min(resolution-1,Mathf.FloorToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))));
            for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)
            {
                var p=new Vector3(x+.5f-a.x,y+.5f-a.y,0);
                float wb=Cross(p,c-a)/area,wc=Cross(b-a,p)/area,wa=1-wb-wc;
                if(wa<0||wb<0||wc<0)continue;
                float z=a.z*wa+b.z*wb+c.z*wc;int index=y*resolution+x;if(z>=depth[index])continue;
                Vector4 attribute=aa*wa+ab*wb+ac*wc;
                if(!Covered(coverage,new Vector2(attribute.x,attribute.y),attribute.z,mask,noise))continue;
                depth[index]=Mathf.Clamp01(z);
            }
        }
        private static float Cross(Vector3 a,Vector3 b)=>a.x*b.y-a.y*b.x;
        private static bool Covered(MxDirectionalShadowCoverage c,Vector2 uv,float vertexAlpha,MaskSnapshot mask,MaskSnapshot noise)
        {
            if(c.Mode==1 && Mathf.Clamp01(mask.Sample(uv).r*c.Alpha*vertexAlpha)<c.Cutoff)return false;
            if(c.Mode==2 && Mathf.Clamp01(mask.Sample(new Vector2(uv.x*c.BaseMapST.x+c.BaseMapST.z,uv.y*c.BaseMapST.y+c.BaseMapST.w)).a*c.Alpha)<c.Cutoff)return false;
            if(c.Dissolve>=1)return false;
            if(c.Dissolve>.00001f && (c.DissolveMode==0?ReferenceNoise(uv):noise.Sample(uv).r)<Mathf.Clamp01(c.Dissolve))return false;
            return true;
        }
        private static float Frac(float x)=>x-Mathf.Floor(x);
        private static float Hash(Vector2 cell)
        {
            Vector3 p=new Vector3(Frac(cell.x*.1031f),Frac(cell.y*.1031f),Frac(cell.x*.1031f));
            float d=Vector3.Dot(p,new Vector3(p.y,p.z,p.x)+Vector3.one*33.33f);p+=Vector3.one*d;return Frac((p.x+p.y)*p.z);
        }
        private static float ReferenceNoise(Vector2 uv)
        {
            Vector2 point=uv*32,cell=new Vector2(Mathf.Floor(point.x),Mathf.Floor(point.y)),blend=point-cell;
            blend=new Vector2(blend.x*blend.x*(3-2*blend.x),blend.y*blend.y*(3-2*blend.y));
            return Mathf.Lerp(Mathf.Lerp(Hash(cell),Hash(cell+Vector2.right),blend.x),Mathf.Lerp(Hash(cell+Vector2.up),Hash(cell+Vector2.one),blend.x),blend.y);
        }
        private static void EnsureFolder(string folder)
        {
            string[] segments=folder.Split('/');string current=segments[0];
            for(int i=1;i<segments.Length;i++){string next=current+"/"+segments[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,segments[i]);current=next;}
        }
        private sealed class MeshSnapshot
        {
            internal readonly Vector3[] Positions;internal readonly Vector4[] Attributes;internal readonly int[][] Indices;
            internal MeshSnapshot(Mesh mesh)
            {
                Positions=mesh.vertices;Vector2[] uv=mesh.uv;Color[] colors=mesh.colors;Attributes=new Vector4[Positions.Length];
                for(int i=0;i<Positions.Length;i++)Attributes[i]=new Vector4(i<uv.Length?uv[i].x:0,i<uv.Length?uv[i].y:0,i<colors.Length?colors[i].a:1,0);
                Indices=new int[mesh.subMeshCount][];for(int sub=0;sub<Indices.Length;sub++)Indices[sub]=mesh.GetIndices(sub,true);
            }
        }
        private static MaskSnapshot GetMask(Texture texture,Dictionary<Texture,MaskSnapshot> masks)
        {
            if(!masks.TryGetValue(texture,out var mask))masks.Add(texture,mask=new MaskSnapshot(texture));return mask;
        }
        private sealed class MaskSnapshot
        {
            private readonly Color[] _pixels;private readonly int _width,_height;private readonly TextureWrapMode _wrapU,_wrapV;private readonly bool _point;
            internal MaskSnapshot(Texture source)
            {
                _width=source.width;_height=source.height;_wrapU=source.wrapModeU;_wrapV=source.wrapModeV;_point=source.filterMode==FilterMode.Point;
                var target=RenderTexture.GetTemporary(_width,_height,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
                RenderTexture previous=RenderTexture.active;var readable=new Texture2D(_width,_height,TextureFormat.RGBAFloat,false,true);
                try{Graphics.Blit(source,target);RenderTexture.active=target;readable.ReadPixels(new Rect(0,0,_width,_height),0,0);readable.Apply();_pixels=readable.GetPixels();}
                finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(readable);}
            }
            internal Color Sample(Vector2 uv)
            {
                float x=uv.x*_width-.5f,y=uv.y*_height-.5f;
                if(_point)return Pixel(Mathf.FloorToInt(x+.5f),Mathf.FloorToInt(y+.5f));
                int ix=Mathf.FloorToInt(x),iy=Mathf.FloorToInt(y);float fx=x-ix,fy=y-iy;
                return Color.LerpUnclamped(Color.LerpUnclamped(Pixel(ix,iy),Pixel(ix+1,iy),fx),Color.LerpUnclamped(Pixel(ix,iy+1),Pixel(ix+1,iy+1),fx),fy);
            }
            private Color Pixel(int x,int y)=>_pixels[Wrap(y,_height,_wrapV)*_width+Wrap(x,_width,_wrapU)];
            private static int Wrap(int i,int size,TextureWrapMode wrap)
            {
                if(wrap==TextureWrapMode.Clamp)return Mathf.Clamp(i,0,size-1);
                if(wrap==TextureWrapMode.MirrorOnce)return Mathf.Clamp(i<0?-i-1:i,0,size-1);
                if(wrap==TextureWrapMode.Mirror){int j=((i%(size*2))+size*2)%(size*2);return j<size?j:size*2-1-j;}
                return ((i%size)+size)%size;
            }
        }
    }
}
