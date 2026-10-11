using System;
using UnityEngine;

namespace MxFramework.Rendering
{
    /// <summary>Persisted reference-pose directional visibility, independent of scene GI.</summary>
    public sealed class MxDirectionalShadowData : ScriptableObject
    {
        public const int FormatVersion = 3;
        [SerializeField] private int _version = FormatVersion;
        // 0: raw centre depth; 1: six low mantissa bits hold fixed-plane coverage.
        [SerializeField] private int _sampleEncoding;
        [SerializeField] private float _coveragePlaneY, _coveragePlaneTolerance, _coverageBias;
        public bool HasPlaneCoverage => _version == FormatVersion && _sampleEncoding == 1;
        /// <summary>Encoding, horizontal plane Y, allowed Y deviation, baked world-space bias.</summary>
        public Vector4 PlaneCoverage => HasPlaneCoverage
            ? new Vector4(1, _coveragePlaneY, _coveragePlaneTolerance, _coverageBias) : Vector4.zero;
        public static bool IsSupportedPlaneCoverage(Vector4 settings)
        {
            for (int i=0;i<4;i++) if (float.IsNaN(settings[i]) || float.IsInfinity(settings[i])) return false;
            return settings.x == 0 ? settings == Vector4.zero :
                settings.x == 1 && settings.z > 0 && settings.z <= .01f && settings.w >= 0;
        }
        /// <summary>Disables consumers before an in-place format-changing asset write.</summary>
        public void InvalidateForRebuild() { _version = 0; }
        [SerializeField] private Texture2D _depth;
        [SerializeField] private Texture2D _peripheralDepth;
        [SerializeField] private Vector3 _direction, _basisX, _basisY;
        [SerializeField] private Vector4 _rectangle, _depthRange;
        [SerializeField] private Vector4 _peripheralRectangle;
        [SerializeField] private Bounds _worldBounds;
        [SerializeField] private Bounds _centerBounds;
        [SerializeField] private int _textureBudgetResolution;
        [SerializeField] private float _transitionWidth;
        [SerializeField] private string _fingerprint;
        [SerializeField] private int _triangles, _sources;
        public Texture2D Depth => _depth;
        public Texture2D PeripheralDepth => _peripheralDepth;
        public Vector4 PeripheralRectangle => _peripheralRectangle;
        public Bounds CenterBounds => _centerBounds;
        public int TextureBudgetResolution => _textureBudgetResolution;
        /// <summary>Width of the center/periphery blend band in light-space metres.</summary>
        public float TransitionWidth => _transitionWidth;
        public Vector3 Direction => _direction;
        public Vector3 BasisX => _basisX;
        public Vector3 BasisY => _basisY;
        /// <summary>Light-space XY minimum and positive XY size, in world metres.</summary>
        public Vector4 Rectangle => _rectangle;
        /// <summary>Minimum -dot(position, lightDirection), positive depth span, reserved zeroes.</summary>
        public Vector4 DepthRange => _depthRange;
        public Bounds WorldBounds => _worldBounds;
        public string SourceFingerprint => _fingerprint ?? string.Empty;
        public int TriangleCount => _triangles;
        public int SourceCount => _sources;
        public long TextureBytes => (_depth == null ? 0 : (long)_depth.width * _depth.height * 4)+
            (_peripheralDepth==null||_peripheralDepth==_depth?0:(long)_peripheralDepth.width*_peripheralDepth.height*4);
        private bool ValidEncoding =>
            (_version == 2 && _sampleEncoding == 0) ||
            (_version == FormatVersion && (_sampleEncoding == 0 ||
                _sampleEncoding == 1 && IsSupportedPlaneCoverage(PlaneCoverage)));
        public bool IsValid => ValidEncoding && _depth != null && _depth.format == TextureFormat.RFloat &&
            _peripheralDepth!=null&&_peripheralDepth.format==TextureFormat.RFloat&&
            _rectangle.z > 0 && _rectangle.w > 0 && _peripheralRectangle.z>0&&_peripheralRectangle.w>0&&
            _depthRange.y > 0 && _direction.sqrMagnitude > .99f && _transitionWidth>=0&&_textureBudgetResolution>0;
        public void Initialize(Texture2D depth, Vector3 direction, Vector3 basisX, Vector3 basisY,
            Vector4 rectangle, Vector4 depthRange, Bounds worldBounds, string fingerprint, int triangles, int sources)
        {
            Initialize(depth,depth,direction,basisX,basisY,rectangle,rectangle,depthRange,worldBounds,worldBounds,
                depth==null?0:depth.width,0,fingerprint,triangles,sources);
        }
        /// <summary>Initializes two fixed receiver regions sharing light axes and a common depth slab.</summary>
        public void Initialize(Texture2D depth,Texture2D peripheralDepth,Vector3 direction,Vector3 basisX,Vector3 basisY,
            Vector4 rectangle,Vector4 peripheralRectangle,Vector4 depthRange,Bounds worldBounds,Bounds centerBounds,
            int textureBudgetResolution,float transitionWidth,string fingerprint,int triangles,int sources)
            => Initialize(depth, peripheralDepth, direction, basisX, basisY, rectangle, peripheralRectangle,
                depthRange, worldBounds, centerBounds, textureBudgetResolution, transitionWidth, fingerprint,
                triangles, sources, Vector4.zero);
        /// <summary>Initializes versioned fixed-plane coverage alongside the original centre depth.</summary>
        public void Initialize(Texture2D depth, Texture2D peripheralDepth, Vector3 direction, Vector3 basisX, Vector3 basisY,
            Vector4 rectangle, Vector4 peripheralRectangle, Vector4 depthRange, Bounds worldBounds, Bounds centerBounds,
            int textureBudgetResolution, float transitionWidth, string fingerprint, int triangles, int sources,
            Vector4 planeCoverage)
        {
            if (!IsSupportedPlaneCoverage(planeCoverage)) throw new ArgumentException("Invalid fixed-plane coverage contract.");
            _version = FormatVersion; _sampleEncoding=(int)planeCoverage.x;
            _coveragePlaneY=planeCoverage.y; _coveragePlaneTolerance=planeCoverage.z; _coverageBias=planeCoverage.w;
            _depth = depth; _direction = direction.normalized;
            _peripheralDepth=peripheralDepth;_peripheralRectangle=peripheralRectangle;
            _centerBounds=centerBounds;_textureBudgetResolution=textureBudgetResolution;_transitionWidth=transitionWidth;
            _basisX = basisX; _basisY = basisY; _rectangle = rectangle; _depthRange = depthRange;
            _worldBounds = worldBounds; _fingerprint = fingerprint; _triangles = triangles; _sources = sources;
            if (!IsValid) throw new ArgumentException("Invalid persistent directional-shadow data.");
        }
        public static void Basis(Vector3 direction, out Vector3 x, out Vector3 y)
        {
            direction = direction.normalized;
            x = Vector3.Cross(Mathf.Abs(direction.y) > .99f ? Vector3.forward : Vector3.up, direction).normalized;
            y = Vector3.Cross(direction, x).normalized;
        }
    }
    internal static class MxDirectionalShadowIds
    {
        internal static readonly int Enabled = Shader.PropertyToID("_MxDirectionalShadowEnabled");
        internal static readonly int StaticCoverage = Shader.PropertyToID("_MxDirectionalShadowStaticCoverage");
        internal static readonly int StaticDepth = Shader.PropertyToID("_MxDirectionalShadowStaticDepth");
        internal static readonly int PeripheralDepth = Shader.PropertyToID("_MxDirectionalShadowPeripheralDepth");
        internal static readonly int DynamicDepth = Shader.PropertyToID("_MxDirectionalShadowDynamicDepth");
        internal static readonly int BasisX = Shader.PropertyToID("_MxDirectionalShadowBasisX");
        internal static readonly int BasisY = Shader.PropertyToID("_MxDirectionalShadowBasisY");
        internal static readonly int Direction = Shader.PropertyToID("_MxDirectionalShadowDirection");
        internal static readonly int StaticRect = Shader.PropertyToID("_MxDirectionalShadowStaticRect");
        internal static readonly int PeripheralRect = Shader.PropertyToID("_MxDirectionalShadowPeripheralRect");
        internal static readonly int PeripheralSize = Shader.PropertyToID("_MxDirectionalShadowPeripheralSize");
        internal static readonly int DynamicRect = Shader.PropertyToID("_MxDirectionalShadowDynamicRect");
        internal static readonly int Range = Shader.PropertyToID("_MxDirectionalShadowRange");
        internal static readonly int Size = Shader.PropertyToID("_MxDirectionalShadowSize");
    }
}
