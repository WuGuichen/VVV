using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MxFramework.Rendering
{
    [Serializable]
    public sealed class MxSceneLightingSourceLayout
    {
        public string BaseColorProperty;
        public string SurfaceDataProperty;
        /// <summary>Optional material scalar multiplying surface-data emission. Empty or absent uses 1.</summary>
        public string EmissionIntensityProperty;
        public string SolidColorProperty;
        public string SecondaryColorProperty;
        public string SecondaryBlendProperty;
        public float[] DiffuseFractions = {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1};
    }
    /// <summary>Versioned, light-independent scene transport. Created by the Editor builder.</summary>
    public sealed class MxSceneLightingData : ScriptableObject
    {
        public const int FormatVersion = 2;
        [SerializeField] private TextAsset _payload;
        [SerializeField] private Mesh _occluders;
        [SerializeField] private string _sourceFingerprint;
        [SerializeField] private Bounds _bounds;
        [SerializeField] private Vector3 _probeOrigin;
        [SerializeField] private Vector3Int _probeDimensions;
        [SerializeField] private float _probeSpacing;
        [SerializeField] private float _requestedProbeSpacing;
        [SerializeField] private int _surfaceCount;
        [SerializeField] private int _nodeCount;
        [SerializeField] private int _probeCount;
        [SerializeField] private float _retainedTransport;
        [SerializeField] private string _probeSettingsFingerprint;

        public TextAsset Payload => _payload;
        public Mesh Occluders => _occluders;
        public string SourceFingerprint => _sourceFingerprint;
        public Bounds Bounds => _bounds;
        public Vector3 ProbeOrigin => _probeOrigin;
        public Vector3Int ProbeDimensions => _probeDimensions;
        public float ProbeSpacing => _probeSpacing;
        public float RequestedProbeSpacing => _requestedProbeSpacing > 0 ? _requestedProbeSpacing : _probeSpacing;
        public int SurfaceCount => _surfaceCount;
        public int NodeCount => _nodeCount;
        public int ProbeCount => _probeCount;
        public float RetainedTransport => _retainedTransport;
        public string ProbeSettingsFingerprint => _probeSettingsFingerprint ?? string.Empty;

        /// <summary>Low-frequency authoring operation; does not persist the asset itself.</summary>
        public void Initialize(TextAsset payload, Mesh occluders, string fingerprint, Bounds bounds,
            Vector3 probeOrigin, Vector3Int dimensions, float spacing, int surfaces, int nodes,
            int probes, float retainedTransport, string probeSettingsFingerprint = "", float requestedProbeSpacing = 0)
        {
            _payload = payload; _occluders = occluders; _sourceFingerprint = fingerprint;
            _bounds = bounds; _probeOrigin = probeOrigin; _probeDimensions = dimensions;
            _probeSpacing = spacing; _surfaceCount = surfaces; _nodeCount = nodes;
            _probeCount = probes; _retainedTransport = retainedTransport;
            _probeSettingsFingerprint = probeSettingsFingerprint;
            _requestedProbeSpacing = requestedProbeSpacing;
        }

        /// <summary>Allocates once when a scene-lighting provider is created.</summary>
        public MxSceneLightingPayload ReadPayload()
        {
            if (_payload == null) throw new InvalidDataException("Scene lighting payload is missing.");
            var result=MxSceneLightingPayload.Read(_payload.bytes);
            if(_occluders==null || _probeSpacing<=0 || float.IsNaN(_probeSpacing) || float.IsInfinity(_probeSpacing) || result.SurfaceCount!=_surfaceCount || result.Nodes.Length!=_nodeCount ||
                result.Probes.Length!=_probeCount || (result.Version==1 && (long)_probeDimensions.x*_probeDimensions.y*_probeDimensions.z!=_probeCount))
                throw new InvalidDataException("Scene lighting metadata does not match its payload.");
            if(result.Version==2)result.Topology.Validate(_probeDimensions,_probeCount);
            return result;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MxSceneLightingNode
    {
        public Vector4 PositionArea, NormalParent, Albedo, Emission, Sky;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MxSceneLightingRow
    {
        public int Start, Count;
        public MxSceneLightingRow(int start, int count) { Start = start; Count = count; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MxSceneLightingEdge
    {
        public int Target;
        public float Weight;
        public MxSceneLightingEdge(int target, float weight) { Target = target; Weight = weight; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MxSceneLightingProbeLink
    {
        public Vector4 DirectionWeight;
        public int Target, Padding0, Padding1, Padding2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MxSceneLightingProbe
    {
        public Vector4 PositionValid, SkyUp, SkyEquator, SkyGround;
    }

    /// <summary>Portable binary CPU representation; immutable after upload to GPU buffers.</summary>
    public sealed class MxSceneLightingPayload
    {
        private const int Magic = 0x4D584C54;
        public int Version = 1;
        public int SurfaceCount;
        public MxSceneLightingNode[] Nodes;
        public MxSceneLightingRow[] Rows, ProbeRows;
        public MxSceneLightingEdge[] Edges;
        public MxSceneLightingProbe[] Probes;
        public MxSceneLightingProbeLink[] ProbeLinks;
        public Vector2[] ProbeVisibility;
        public MxSceneLightingProbeTopology Topology;
        // Relocation length, minimum geometry distance, back-face fraction, dilation contribution.
        public Vector4[] ProbeQuality;
        public float[] ProbeClearDistances;
        public MxSceneLightingRow[] DilationRows;
        public MxSceneLightingEdge[] DilationLinks;

        public void Validate()
        {
            if(Version!=1 && Version!=2)throw new InvalidDataException("Unsupported scene-lighting payload version.");
            if (Nodes == null || Rows == null || Rows.Length != Nodes.Length || SurfaceCount < 1 ||
                SurfaceCount > Nodes.Length || Probes == null || Probes.Length<1 || ProbeRows == null ||
                ProbeRows.Length != Probes.Length || ProbeVisibility == null ||
                ProbeVisibility.Length != Probes.Length * 96 || Edges == null || ProbeLinks == null)
                throw new InvalidDataException("Inconsistent scene-lighting buffer dimensions.");
            for (int i = 0; i < Rows.Length; i++)
            {
                CheckRow(Rows[i], Edges.Length);
                float sum = 0;
                for (int j = 0; j < Rows[i].Count; j++)
                {
                    MxSceneLightingEdge edge = Edges[Rows[i].Start + j];
                    if (edge.Target < 0 || edge.Target >= Nodes.Length || !Finite(edge.Weight) || edge.Weight < 0)
                        throw new InvalidDataException("Invalid light transport edge.");
                    sum += edge.Weight;
                }
                if (sum > 1.002f) throw new InvalidDataException("Light transport creates energy.");
            }
            for (int i = 0; i < ProbeRows.Length; i++)
            {
                for(int axis=0;axis<4;axis++)if(!Finite(Probes[i].PositionValid[axis]))
                    throw new InvalidDataException("Non-finite light probe position or validity.");
                CheckRow(ProbeRows[i], ProbeLinks.Length);
                for (int j = 0; j < ProbeRows[i].Count; j++)
                {
                    var link = ProbeLinks[ProbeRows[i].Start + j];
                    if (link.Target < 0 || link.Target >= Nodes.Length || !Finite(link.DirectionWeight.w) || link.DirectionWeight.w < 0)
                        throw new InvalidDataException("Invalid light probe link.");
                }
            }
            if(Version==2)
            {
                if(Topology==null || ProbeQuality==null || ProbeQuality.Length!=Probes.Length ||
                    ProbeClearDistances==null || ProbeClearDistances.Length!=Probes.Length*96 ||
                    DilationRows==null || DilationRows.Length!=Probes.Length || DilationLinks==null)
                    throw new InvalidDataException("Inconsistent v2 probe quality dimensions.");
                Topology.Validate(Topology.CellDimensions+Vector3Int.one,Probes.Length);
                foreach(float distance in ProbeClearDistances)if(!Finite(distance) || distance<0)
                    throw new InvalidDataException("Invalid conservative probe distance.");
                foreach(var quality in ProbeQuality)for(int axis=0;axis<4;axis++)if(!Finite(quality[axis]) || quality[axis]<0)
                    throw new InvalidDataException("Invalid probe quality diagnostic.");
                for(int i=0;i<DilationRows.Length;i++)
                {
                    CheckRow(DilationRows[i],DilationLinks.Length);float sum=0;
                    for(int j=0;j<DilationRows[i].Count;j++)
                    {
                        var edge=DilationLinks[DilationRows[i].Start+j];
                        if(edge.Target<0 || edge.Target>=Probes.Length || edge.Target==i || Probes[edge.Target].PositionValid.w!=1 ||
                            !Finite(edge.Weight) || edge.Weight<0)throw new InvalidDataException("Invalid visibility-constrained dilation source.");
                        sum+=edge.Weight;
                    }
                    if(sum>1.002f)throw new InvalidDataException("Probe dilation creates energy.");
                }
            }
        }

        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        private static void CheckRow(MxSceneLightingRow row, int length)
        {
            if (row.Start < 0 || row.Count < 0 || row.Start > length - row.Count)
                throw new InvalidDataException("Invalid scene-lighting row.");
        }

        public byte[] Write()
        {
            Validate();
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream))
            {
                w.Write(Magic); w.Write(Version); w.Write(SurfaceCount);
                w.Write(Nodes.Length); w.Write(Edges.Length); w.Write(Probes.Length); w.Write(ProbeLinks.Length);
                foreach (var n in Nodes) { V(w,n.PositionArea); V(w,n.NormalParent); V(w,n.Albedo); V(w,n.Emission); V(w,n.Sky); }
                foreach (var r in Rows) { w.Write(r.Start); w.Write(r.Count); }
                foreach (var e in Edges) { w.Write(e.Target); w.Write(e.Weight); }
                foreach (var p in Probes) { V(w,p.PositionValid); V(w,p.SkyUp); V(w,p.SkyEquator); V(w,p.SkyGround); }
                foreach (var r in ProbeRows) { w.Write(r.Start); w.Write(r.Count); }
                foreach (var p in ProbeLinks) { V(w,p.DirectionWeight); w.Write(p.Target); }
                foreach (var v in ProbeVisibility) { w.Write(v.x); w.Write(v.y); }
                if(Version==2)
                {
                    w.Write(Topology.CellDimensions.x);w.Write(Topology.CellDimensions.y);w.Write(Topology.CellDimensions.z);
                    w.Write(Topology.CellBricks.Length);w.Write(Topology.BrickIndices.Length);w.Write(DilationLinks.Length);
                    foreach(int brick in Topology.CellBricks)w.Write(brick);
                    foreach(int id in Topology.BrickIndices)w.Write(id);
                    foreach(var quality in ProbeQuality)V(w,quality);
                    foreach(float distance in ProbeClearDistances)w.Write(distance);
                    foreach(var row in DilationRows){w.Write(row.Start);w.Write(row.Count);}
                    foreach(var edge in DilationLinks){w.Write(edge.Target);w.Write(edge.Weight);}
                }
                return stream.ToArray();
            }
        }

        public static MxSceneLightingPayload Read(byte[] bytes)
        {
            if(bytes==null || bytes.Length<28)throw new InvalidDataException("Truncated scene-lighting header.");
            using (var r = new BinaryReader(new MemoryStream(bytes, false)))
            {
                if (r.ReadInt32() != Magic)
                    throw new InvalidDataException("Unsupported scene-lighting payload.");
                int version=r.ReadInt32();
                if(version!=1 && version!=MxSceneLightingData.FormatVersion)throw new InvalidDataException("Unsupported scene-lighting payload version.");
                int surfaces=r.ReadInt32(), nodes=Count(r), edges=Count(r), probes=Count(r), links=Count(r);
                long expected = 28L + nodes * 88L + edges * 8L + probes * 840L + links * 20L;
                if ((version==1 && expected!=bytes.LongLength) || (version==2 && expected+24L+probes*408L>bytes.LongLength)) throw new InvalidDataException("Truncated scene-lighting payload.");
                var p = new MxSceneLightingPayload { Version=version,SurfaceCount=surfaces,
                    Nodes=new MxSceneLightingNode[nodes], Rows=new MxSceneLightingRow[nodes],
                    Edges=new MxSceneLightingEdge[edges], Probes=new MxSceneLightingProbe[probes],
                    ProbeRows=new MxSceneLightingRow[probes], ProbeLinks=new MxSceneLightingProbeLink[links],
                    ProbeVisibility=new Vector2[probes*96] };
                for(int i=0;i<nodes;i++) p.Nodes[i]=new MxSceneLightingNode { PositionArea=V(r),NormalParent=V(r),Albedo=V(r),Emission=V(r),Sky=V(r) };
                for(int i=0;i<nodes;i++) p.Rows[i]=new MxSceneLightingRow(r.ReadInt32(),r.ReadInt32());
                for(int i=0;i<edges;i++) p.Edges[i]=new MxSceneLightingEdge(r.ReadInt32(),r.ReadSingle());
                for(int i=0;i<probes;i++) p.Probes[i]=new MxSceneLightingProbe { PositionValid=V(r),SkyUp=V(r),SkyEquator=V(r),SkyGround=V(r) };
                for(int i=0;i<probes;i++) p.ProbeRows[i]=new MxSceneLightingRow(r.ReadInt32(),r.ReadInt32());
                for(int i=0;i<links;i++) p.ProbeLinks[i]=new MxSceneLightingProbeLink { DirectionWeight=V(r),Target=r.ReadInt32() };
                for(int i=0;i<p.ProbeVisibility.Length;i++) p.ProbeVisibility[i]=new Vector2(r.ReadSingle(),r.ReadSingle());
                if(version==2)
                {
                    var dimensions=new Vector3Int(r.ReadInt32(),r.ReadInt32(),r.ReadInt32());
                    int cells=Count(r),brickIndices=Count(r),dilationLinks=Count(r);
                    if(expected+24L+cells*4L+brickIndices*4L+probes*408L+dilationLinks*8L!=bytes.LongLength)
                        throw new InvalidDataException("Truncated v2 probe topology.");
                    p.Topology=new MxSceneLightingProbeTopology {CellDimensions=dimensions,CellBricks=new int[cells],BrickIndices=new int[brickIndices]};
                    p.ProbeQuality=new Vector4[probes];p.ProbeClearDistances=new float[probes*96];
                    p.DilationRows=new MxSceneLightingRow[probes];p.DilationLinks=new MxSceneLightingEdge[dilationLinks];
                    for(int i=0;i<cells;i++)p.Topology.CellBricks[i]=r.ReadInt32();
                    for(int i=0;i<brickIndices;i++)p.Topology.BrickIndices[i]=r.ReadInt32();
                    for(int i=0;i<probes;i++)p.ProbeQuality[i]=V(r);
                    for(int i=0;i<p.ProbeClearDistances.Length;i++)p.ProbeClearDistances[i]=r.ReadSingle();
                    for(int i=0;i<probes;i++)p.DilationRows[i]=new MxSceneLightingRow(r.ReadInt32(),r.ReadInt32());
                    for(int i=0;i<dilationLinks;i++)p.DilationLinks[i]=new MxSceneLightingEdge(r.ReadInt32(),r.ReadSingle());
                }
                p.Validate(); return p;
            }
        }
        private static int Count(BinaryReader r) { int n=r.ReadInt32(); if(n<0 || n>16000000) throw new InvalidDataException("Unreasonable lighting allocation."); return n; }
        private static void V(BinaryWriter w,Vector4 v) { w.Write(v.x);w.Write(v.y);w.Write(v.z);w.Write(v.w); }
        private static Vector4 V(BinaryReader r) => new Vector4(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
    }
}
