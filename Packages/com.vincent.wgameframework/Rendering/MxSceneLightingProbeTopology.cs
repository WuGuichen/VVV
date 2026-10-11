using System;
using System.IO;
using UnityEngine;

namespace MxFramework.Rendering
{
    /// <summary>Offline probe quality inputs. Enable explicitly invalidates legacy transport.</summary>
    [Serializable]
    public sealed class MxSceneLightingProbeQuality
    {
        public bool Enabled;
        public Bounds[] LocalRefinementVolumes = Array.Empty<Bounds>();
        public bool UseManualRefinement = true;
        public Bounds[] RefinementExclusionVolumes = Array.Empty<Bounds>();
        public bool AutomaticRefinement;
        public bool AutomaticReceivers = true;
        public bool AutomaticNavigation = true;
        public Bounds[] ActivityVolumes = Array.Empty<Bounds>();
        [Range(.1f,2f)] public float ReceiverDistanceFraction = .75f;
        [Range(0,1)] public float AutomaticBudgetFraction = .35f;
        [Min(.01f)] public float MinimumAutomaticScore = 2f;
        // Frozen scene-medium inputs supplied by the editor; no live camera/lighting inputs.
        [HideInInspector] public bool MediumDemandEnabled;
        [HideInInspector] public float MediumDensity, MediumBaseHeight, MediumHeightFalloff;
        [HideInInspector] public Bounds[] MediumVolumes = Array.Empty<Bounds>();
        public bool RelocateNearGeometry = true;
        [Min(.025f)] public float MinimumClearance = .15f;
        [Range(0,.45f)] public float MaximumRelocationFraction = .4f;
        public bool VisibilityConstrainedDilation = true;
        [Range(100,120000)] public int MaximumProbeCount = 120000;
    }

    /// <summary>Base lattice plus indexed half-spacing bricks. Actual relocated positions never change
    /// these interpolation coordinates. Bricks share probe IDs; the base lattice remains the first N IDs.</summary>
    public sealed class MxSceneLightingProbeTopology
    {
        public Vector3Int CellDimensions;
        public int[] CellBricks = Array.Empty<int>();
        public int[] BrickIndices = Array.Empty<int>();
        public int BrickCount => BrickIndices.Length / 27;

        public void Validate(Vector3Int dimensions,int probes)
        {
            long baseCount=(long)dimensions.x*dimensions.y*dimensions.z;
            if (dimensions.x<2 || dimensions.y<2 || dimensions.z<2 || baseCount>probes ||
                CellDimensions!=dimensions-Vector3Int.one || CellBricks==null || BrickIndices==null ||
                (long)CellDimensions.x*CellDimensions.y*CellDimensions.z!=CellBricks.Length || BrickIndices.Length%27!=0)
                throw new InvalidDataException("Invalid probe brick topology dimensions.");
            foreach(int brick in CellBricks) if(brick < -1 || brick>=BrickCount)
                throw new InvalidDataException("Probe cell references an invalid refinement brick.");
            foreach(int id in BrickIndices) if(id<0 || id>=probes)
                throw new InvalidDataException("Refinement brick references an invalid probe.");
        }

        /// <summary>CPU counterpart of the shader lookup for deterministic verification.</summary>
        public int RefinedCorner(Vector3 grid,int corner,out Vector3 fraction,out float blend)
        {
            var lower=new Vector3Int(Mathf.Min(Mathf.FloorToInt(grid.x),CellDimensions.x-1),
                Mathf.Min(Mathf.FloorToInt(grid.y),CellDimensions.y-1),Mathf.Min(Mathf.FloorToInt(grid.z),CellDimensions.z-1));
            fraction=Vector3.zero;blend=0;
            if(lower.x<0 || lower.y<0 || lower.z<0 || grid.x>CellDimensions.x || grid.y>CellDimensions.y || grid.z>CellDimensions.z) return -1;
            int brick=CellBricks[lower.x+CellDimensions.x*(lower.y+CellDimensions.y*lower.z)];
            if(brick<0)return -1;
            Vector3 local=grid-(Vector3)lower;
            float boundary=Mathf.Min(local.x,Mathf.Min(local.y,Mathf.Min(local.z,
                Mathf.Min(1-local.x,Mathf.Min(1-local.y,1-local.z)))));
            blend=Mathf.SmoothStep(0,1,Mathf.Clamp01(boundary*4));
            Vector3 fine=local*2;
            var sub=new Vector3Int(Mathf.Min(1,Mathf.FloorToInt(fine.x)),Mathf.Min(1,Mathf.FloorToInt(fine.y)),Mathf.Min(1,Mathf.FloorToInt(fine.z)));
            fraction=fine-(Vector3)sub;
            int x=sub.x+(corner&1),y=sub.y+((corner>>1)&1),z=sub.z+((corner>>2)&1);
            return BrickIndices[brick*27+x+3*(y+3*z)];
        }
    }
}
