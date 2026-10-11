using System;
using UnityEngine;

namespace MxFramework.Combat.Authoring
{
    [CreateAssetMenu(
        fileName = "CombatActionAuthoringAsset",
        menuName = "MxFramework/Combat/Action Authoring Asset")]
    public sealed class CombatActionAuthoringAsset : ScriptableObject
    {
        public const string CurrentSchemaVersion = "0.1.0";

        [SerializeField] private int actionId = 1;
        [SerializeField] private int totalFrames = 1;
        [SerializeField] private string schemaVersion = CurrentSchemaVersion;
        [SerializeField] private CombatAuthoringFrameRange startup = new CombatAuthoringFrameRange(0, 0);
        [SerializeField] private CombatAuthoringFrameRange active = CombatAuthoringFrameRange.Empty;
        [SerializeField] private CombatAuthoringFrameRange recovery = CombatAuthoringFrameRange.Empty;
        [SerializeField] private CombatShapeAuthoringData[] hitboxes = Array.Empty<CombatShapeAuthoringData>();
        [SerializeField] private CombatShapeAuthoringData[] hurtboxes = Array.Empty<CombatShapeAuthoringData>();
        [SerializeField] private CombatWeaponTraceAuthoringData[] weaponTraces = Array.Empty<CombatWeaponTraceAuthoringData>();

        public int ActionId
        {
            get => actionId;
            set => actionId = value;
        }

        public int TotalFrames
        {
            get => totalFrames;
            set => totalFrames = value;
        }

        public string SchemaVersion
        {
            get => schemaVersion;
            set => schemaVersion = value;
        }

        public CombatAuthoringFrameRange Startup
        {
            get => startup;
            set => startup = value;
        }

        public CombatAuthoringFrameRange Active
        {
            get => active;
            set => active = value;
        }

        public CombatAuthoringFrameRange Recovery
        {
            get => recovery;
            set => recovery = value;
        }

        public CombatShapeAuthoringData[] Hitboxes
        {
            get => hitboxes;
            set => hitboxes = value ?? Array.Empty<CombatShapeAuthoringData>();
        }

        public CombatShapeAuthoringData[] Hurtboxes
        {
            get => hurtboxes;
            set => hurtboxes = value ?? Array.Empty<CombatShapeAuthoringData>();
        }

        public CombatWeaponTraceAuthoringData[] WeaponTraces
        {
            get => weaponTraces;
            set => weaponTraces = value ?? Array.Empty<CombatWeaponTraceAuthoringData>();
        }
    }

    [Serializable]
    public struct CombatAuthoringFrameRange
    {
        public static readonly CombatAuthoringFrameRange Empty = new CombatAuthoringFrameRange(0, -1);

        [SerializeField] private int startFrame;
        [SerializeField] private int endFrame;

        public CombatAuthoringFrameRange(int startFrame, int endFrame)
        {
            this.startFrame = startFrame;
            this.endFrame = endFrame;
        }

        public int StartFrame
        {
            get => startFrame;
            set => startFrame = value;
        }

        public int EndFrame
        {
            get => endFrame;
            set => endFrame = value;
        }

        public bool IsEmpty => endFrame < startFrame;
    }

    public enum CombatAuthoringShapeKind
    {
        Sphere = 0,
        Capsule = 1,
        Aabb = 2,
        Sector = 3,
    }

    [Serializable]
    public struct CombatShapeAuthoringData
    {
        [SerializeField] private int trackId;
        [SerializeField] private CombatAuthoringShapeKind shapeKind;
        [SerializeField] private CombatAuthoringFrameRange frameRange;
        [SerializeField] private string markerId;
        [SerializeField] private Vector3 localCenter;
        [SerializeField] private int radiusRaw;
        [SerializeField] private int heightRaw;
        [SerializeField] private int sourceOrder;

        public int TrackId
        {
            get => trackId;
            set => trackId = value;
        }

        public CombatAuthoringShapeKind ShapeKind
        {
            get => shapeKind;
            set => shapeKind = value;
        }

        public CombatAuthoringFrameRange FrameRange
        {
            get => frameRange;
            set => frameRange = value;
        }

        public string MarkerId
        {
            get => markerId;
            set => markerId = value;
        }

        public Vector3 LocalCenter
        {
            get => localCenter;
            set => localCenter = value;
        }

        public int RadiusRaw
        {
            get => radiusRaw;
            set => radiusRaw = value;
        }

        public int HeightRaw
        {
            get => heightRaw;
            set => heightRaw = value;
        }

        public int SourceOrder
        {
            get => sourceOrder;
            set => sourceOrder = value;
        }
    }

    [Serializable]
    public struct CombatWeaponTraceAuthoringData
    {
        [SerializeField] private int traceId;
        [SerializeField] private CombatAuthoringFrameRange frameRange;
        [SerializeField] private string rootMarkerId;
        [SerializeField] private string tipMarkerId;
        [SerializeField] private int radiusRaw;
        [SerializeField] private int sourceOrder;

        public int TraceId
        {
            get => traceId;
            set => traceId = value;
        }

        public CombatAuthoringFrameRange FrameRange
        {
            get => frameRange;
            set => frameRange = value;
        }

        public string RootMarkerId
        {
            get => rootMarkerId;
            set => rootMarkerId = value;
        }

        public string TipMarkerId
        {
            get => tipMarkerId;
            set => tipMarkerId = value;
        }

        public int RadiusRaw
        {
            get => radiusRaw;
            set => radiusRaw = value;
        }

        public int SourceOrder
        {
            get => sourceOrder;
            set => sourceOrder = value;
        }
    }
}
