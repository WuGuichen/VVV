using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using UnityEngine;

namespace MxFramework.Combat.Authoring
{
    public interface ICombatAuthoringPreviewMarkerResolver
    {
        bool TryResolveMarker(string markerId, out CombatAuthoringPreviewMarkerPose pose);
    }

    public readonly struct CombatAuthoringPreviewMarkerPose
    {
        public CombatAuthoringPreviewMarkerPose(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public Vector3 Position { get; }

        public Quaternion Rotation { get; }
    }

    public readonly struct CombatAuthoringPreviewQuery : IComparable<CombatAuthoringPreviewQuery>
    {
        public CombatAuthoringPreviewQuery(
            int frame,
            int queryId,
            int entityId,
            int bodyId,
            int colliderId,
            int traceId,
            int actionId,
            int sourceOrder,
            string kind,
            string message)
        {
            Frame = frame;
            QueryId = queryId;
            EntityId = entityId;
            BodyId = bodyId;
            ColliderId = colliderId;
            TraceId = traceId;
            ActionId = actionId;
            SourceOrder = sourceOrder;
            Kind = kind ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public int Frame { get; }

        public int QueryId { get; }

        public int EntityId { get; }

        public int BodyId { get; }

        public int ColliderId { get; }

        public int TraceId { get; }

        public int ActionId { get; }

        public int SourceOrder { get; }

        public string Kind { get; }

        public string Message { get; }

        public int CompareTo(CombatAuthoringPreviewQuery other)
        {
            int compare = Frame.CompareTo(other.Frame);
            if (compare != 0)
            {
                return compare;
            }

            compare = QueryId.CompareTo(other.QueryId);
            if (compare != 0)
            {
                return compare;
            }

            compare = EntityId.CompareTo(other.EntityId);
            if (compare != 0)
            {
                return compare;
            }

            compare = BodyId.CompareTo(other.BodyId);
            if (compare != 0)
            {
                return compare;
            }

            compare = ColliderId.CompareTo(other.ColliderId);
            if (compare != 0)
            {
                return compare;
            }

            compare = TraceId.CompareTo(other.TraceId);
            if (compare != 0)
            {
                return compare;
            }

            compare = ActionId.CompareTo(other.ActionId);
            return compare != 0 ? compare : SourceOrder.CompareTo(other.SourceOrder);
        }

        public override string ToString()
        {
            return "Frame " + Frame
                + " / Query " + QueryId
                + " / Entity " + EntityId
                + " / Body " + BodyId
                + " / Collider " + ColliderId
                + " / Trace " + TraceId
                + " / Action " + ActionId
                + " / Order " + SourceOrder
                + " / " + Kind
                + " / " + Message;
        }
    }

    public readonly struct CombatAuthoringPreviewCandidate : IComparable<CombatAuthoringPreviewCandidate>
    {
        public CombatAuthoringPreviewCandidate(
            int frame,
            int queryId,
            int entityId,
            int bodyId,
            int colliderId,
            int traceId,
            int actionId,
            int sourceOrder,
            string kind,
            string message)
        {
            Frame = frame;
            QueryId = queryId;
            EntityId = entityId;
            BodyId = bodyId;
            ColliderId = colliderId;
            TraceId = traceId;
            ActionId = actionId;
            SourceOrder = sourceOrder;
            Kind = kind ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public int Frame { get; }

        public int QueryId { get; }

        public int EntityId { get; }

        public int BodyId { get; }

        public int ColliderId { get; }

        public int TraceId { get; }

        public int ActionId { get; }

        public int SourceOrder { get; }

        public string Kind { get; }

        public string Message { get; }

        public int CompareTo(CombatAuthoringPreviewCandidate other)
        {
            int compare = Frame.CompareTo(other.Frame);
            if (compare != 0)
            {
                return compare;
            }

            compare = QueryId.CompareTo(other.QueryId);
            if (compare != 0)
            {
                return compare;
            }

            compare = EntityId.CompareTo(other.EntityId);
            if (compare != 0)
            {
                return compare;
            }

            compare = BodyId.CompareTo(other.BodyId);
            if (compare != 0)
            {
                return compare;
            }

            compare = ColliderId.CompareTo(other.ColliderId);
            if (compare != 0)
            {
                return compare;
            }

            compare = TraceId.CompareTo(other.TraceId);
            if (compare != 0)
            {
                return compare;
            }

            compare = ActionId.CompareTo(other.ActionId);
            return compare != 0 ? compare : SourceOrder.CompareTo(other.SourceOrder);
        }

        public override string ToString()
        {
            return "Frame " + Frame
                + " / Query " + QueryId
                + " / Entity " + EntityId
                + " / Body " + BodyId
                + " / Collider " + ColliderId
                + " / Trace " + TraceId
                + " / Action " + ActionId
                + " / Order " + SourceOrder
                + " / " + Kind
                + " / " + Message;
        }
    }

    public readonly struct CombatAuthoringPreviewResolve : IComparable<CombatAuthoringPreviewResolve>
    {
        public CombatAuthoringPreviewResolve(
            int frame,
            int queryId,
            int entityId,
            int bodyId,
            int colliderId,
            int traceId,
            int actionId,
            int sourceOrder,
            string kind,
            string message)
        {
            Frame = frame;
            QueryId = queryId;
            EntityId = entityId;
            BodyId = bodyId;
            ColliderId = colliderId;
            TraceId = traceId;
            ActionId = actionId;
            SourceOrder = sourceOrder;
            Kind = kind ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public int Frame { get; }

        public int QueryId { get; }

        public int EntityId { get; }

        public int BodyId { get; }

        public int ColliderId { get; }

        public int TraceId { get; }

        public int ActionId { get; }

        public int SourceOrder { get; }

        public string Kind { get; }

        public string Message { get; }

        public int CompareTo(CombatAuthoringPreviewResolve other)
        {
            int compare = Frame.CompareTo(other.Frame);
            if (compare != 0)
            {
                return compare;
            }

            compare = QueryId.CompareTo(other.QueryId);
            if (compare != 0)
            {
                return compare;
            }

            compare = EntityId.CompareTo(other.EntityId);
            if (compare != 0)
            {
                return compare;
            }

            compare = BodyId.CompareTo(other.BodyId);
            if (compare != 0)
            {
                return compare;
            }

            compare = ColliderId.CompareTo(other.ColliderId);
            if (compare != 0)
            {
                return compare;
            }

            compare = TraceId.CompareTo(other.TraceId);
            if (compare != 0)
            {
                return compare;
            }

            compare = ActionId.CompareTo(other.ActionId);
            return compare != 0 ? compare : SourceOrder.CompareTo(other.SourceOrder);
        }

        public override string ToString()
        {
            return "Frame " + Frame
                + " / Query " + QueryId
                + " / Entity " + EntityId
                + " / Body " + BodyId
                + " / Collider " + ColliderId
                + " / Trace " + TraceId
                + " / Action " + ActionId
                + " / Order " + SourceOrder
                + " / " + Kind
                + " / " + Message;
        }
    }

    public readonly struct CombatAuthoringPreviewPhysicsDebug : IComparable<CombatAuthoringPreviewPhysicsDebug>
    {
        public CombatAuthoringPreviewPhysicsDebug(
            int frame,
            int queryId,
            int entityId,
            int bodyId,
            int colliderId,
            int candidateIndex,
            int layer,
            int broadphaseRawCandidateCount,
            int broadphaseCandidateCount,
            int postFilterCandidateCount,
            int filteredSourceCount,
            int filteredLayerCount,
            int broadphaseCellCount,
            int hitCount,
            string status,
            string reason)
        {
            Frame = frame;
            QueryId = queryId;
            EntityId = entityId;
            BodyId = bodyId;
            ColliderId = colliderId;
            CandidateIndex = candidateIndex;
            Layer = layer;
            BroadphaseRawCandidateCount = broadphaseRawCandidateCount;
            BroadphaseCandidateCount = broadphaseCandidateCount;
            PostFilterCandidateCount = postFilterCandidateCount;
            FilteredSourceCount = filteredSourceCount;
            FilteredLayerCount = filteredLayerCount;
            BroadphaseCellCount = broadphaseCellCount;
            HitCount = hitCount;
            Status = status ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public int Frame { get; }

        public int QueryId { get; }

        public int EntityId { get; }

        public int BodyId { get; }

        public int ColliderId { get; }

        public int CandidateIndex { get; }

        public int Layer { get; }

        public int BroadphaseRawCandidateCount { get; }

        public int BroadphaseCandidateCount { get; }

        public int PostFilterCandidateCount { get; }

        public int FilteredSourceCount { get; }

        public int FilteredLayerCount { get; }

        public int BroadphaseCellCount { get; }

        public int HitCount { get; }

        public string Status { get; }

        public string Reason { get; }

        public int CompareTo(CombatAuthoringPreviewPhysicsDebug other)
        {
            int compare = Frame.CompareTo(other.Frame);
            if (compare != 0)
            {
                return compare;
            }

            compare = QueryId.CompareTo(other.QueryId);
            if (compare != 0)
            {
                return compare;
            }

            compare = CandidateIndex.CompareTo(other.CandidateIndex);
            if (compare != 0)
            {
                return compare;
            }

            compare = EntityId.CompareTo(other.EntityId);
            if (compare != 0)
            {
                return compare;
            }

            compare = BodyId.CompareTo(other.BodyId);
            return compare != 0 ? compare : ColliderId.CompareTo(other.ColliderId);
        }

        public override string ToString()
        {
            string target = CandidateIndex < 0
                ? "Summary"
                : "Candidate " + CandidateIndex
                    + " / Entity " + EntityId
                    + " / Body " + BodyId
                    + " / Collider " + ColliderId
                    + " / Layer " + Layer;
            return "Frame " + Frame
                + " / Query " + QueryId
                + " / " + target
                + " / Broadphase raw " + BroadphaseRawCandidateCount
                + " / Broadphase candidates " + BroadphaseCandidateCount
                + " / Post-filter " + PostFilterCandidateCount
                + " / Filtered source " + FilteredSourceCount
                + " / Filtered layer " + FilteredLayerCount
                + " / Cells " + BroadphaseCellCount
                + " / Hits " + HitCount
                + " / " + Status
                + " / " + Reason;
        }
    }

    public sealed class CombatAuthoringPreviewReport
    {
        private readonly CombatAuthoringPreviewQuery[] _queries;
        private readonly CombatAuthoringPreviewCandidate[] _candidates;
        private readonly CombatAuthoringPreviewResolve[] _resolves;
        private readonly CombatAuthoringPreviewPhysicsDebug[] _physicsDebugRows;
        private readonly string[] _reasonLines;

        public CombatAuthoringPreviewReport(
            string actionName,
            int actionId,
            string bindingName,
            int frame,
            bool hasRuntimePreview,
            string previewMode,
            IEnumerable<CombatAuthoringPreviewQuery> queries,
            IEnumerable<CombatAuthoringPreviewCandidate> candidates,
            IEnumerable<CombatAuthoringPreviewResolve> resolves,
            IEnumerable<string> reasonLines,
            IEnumerable<CombatAuthoringPreviewPhysicsDebug> physicsDebugRows = null)
        {
            ActionName = actionName ?? string.Empty;
            ActionId = actionId;
            BindingName = bindingName ?? string.Empty;
            Frame = frame;
            HasRuntimePreview = hasRuntimePreview;
            PreviewMode = previewMode ?? string.Empty;
            _queries = ToSortedArray(queries);
            _candidates = ToSortedArray(candidates);
            _resolves = ToSortedArray(resolves);
            _physicsDebugRows = ToSortedArray(physicsDebugRows);
            _reasonLines = ToArray(reasonLines);
            StableHash = BuildStableHash().ToString();
        }

        public string ActionName { get; }

        public int ActionId { get; }

        public string BindingName { get; }

        public int Frame { get; }

        public bool HasRuntimePreview { get; }

        public string PreviewMode { get; }

        public string StableHash { get; }

        public int QueryCount => _queries.Length;

        public int CandidateCount => _candidates.Length;

        public int ResolveCount => _resolves.Length;

        public int PhysicsDebugCount => _physicsDebugRows.Length;

        public int ReasonLineCount => _reasonLines.Length;

        public CombatAuthoringPreviewQuery GetQuery(int index)
        {
            return _queries[index];
        }

        public CombatAuthoringPreviewCandidate GetCandidate(int index)
        {
            return _candidates[index];
        }

        public CombatAuthoringPreviewResolve GetResolve(int index)
        {
            return _resolves[index];
        }

        public CombatAuthoringPreviewPhysicsDebug GetPhysicsDebug(int index)
        {
            return _physicsDebugRows[index];
        }

        public string GetReasonLine(int index)
        {
            return _reasonLines[index];
        }

        public string ToDisplayText()
        {
            var builder = new StringBuilder(2048);
            builder.AppendLine("Combat Authoring Preview Explain");
            builder.Append("Mode: ");
            builder.AppendLine(PreviewMode);
            builder.Append("Action: ");
            builder.Append(ActionName.Length == 0 ? "(none)" : ActionName);
            builder.Append(" / ");
            builder.AppendLine(ActionId.ToString());
            builder.Append("Binding: ");
            builder.AppendLine(BindingName.Length == 0 ? "(none)" : BindingName);
            builder.Append("Frame: ");
            builder.AppendLine(Frame.ToString());
            builder.Append("HasRuntimePreview: ");
            builder.AppendLine(HasRuntimePreview.ToString());
            AppendSection(builder, "Generated Queries", _queries);
            AppendSection(builder, "Candidate Hits", _candidates);
            AppendSection(builder, "Resolve Results", _resolves);
            AppendSection(builder, "Physics Query Debug", _physicsDebugRows);
            builder.AppendLine("Reason:");
            if (_reasonLines.Length == 0)
            {
                builder.AppendLine("  - No reason lines were generated.");
            }
            else
            {
                for (int i = 0; i < _reasonLines.Length; i++)
                {
                    builder.Append("  - ");
                    builder.AppendLine(_reasonLines[i]);
                }
            }

            builder.Append("Hash: ");
            builder.AppendLine(StableHash);
            return builder.ToString();
        }

        private static void AppendSection<T>(StringBuilder builder, string title, T[] rows)
        {
            builder.Append(title);
            builder.Append(": ");
            builder.AppendLine(rows.Length.ToString());
            for (int i = 0; i < rows.Length; i++)
            {
                builder.Append("  ");
                builder.Append(i + 1);
                builder.Append(". ");
                builder.AppendLine(rows[i].ToString());
            }
        }

        private CombatHash BuildStableHash()
        {
            CombatHash hash = CombatHash.Empty
                .Add(ActionId)
                .Add(Frame)
                .Add(HasRuntimePreview ? 1 : 0);
            hash = AddString(hash, ActionName);
            hash = AddString(hash, BindingName);
            hash = AddString(hash, PreviewMode);

            for (int i = 0; i < _queries.Length; i++)
            {
                CombatAuthoringPreviewQuery query = _queries[i];
                hash = AddRowHash(hash, query.Frame, query.QueryId, query.EntityId, query.BodyId, query.ColliderId, query.TraceId, query.ActionId, query.SourceOrder, query.Kind, query.Message);
            }

            for (int i = 0; i < _candidates.Length; i++)
            {
                CombatAuthoringPreviewCandidate candidate = _candidates[i];
                hash = AddRowHash(hash, candidate.Frame, candidate.QueryId, candidate.EntityId, candidate.BodyId, candidate.ColliderId, candidate.TraceId, candidate.ActionId, candidate.SourceOrder, candidate.Kind, candidate.Message);
            }

            for (int i = 0; i < _resolves.Length; i++)
            {
                CombatAuthoringPreviewResolve resolve = _resolves[i];
                hash = AddRowHash(hash, resolve.Frame, resolve.QueryId, resolve.EntityId, resolve.BodyId, resolve.ColliderId, resolve.TraceId, resolve.ActionId, resolve.SourceOrder, resolve.Kind, resolve.Message);
            }

            for (int i = 0; i < _physicsDebugRows.Length; i++)
            {
                CombatAuthoringPreviewPhysicsDebug debug = _physicsDebugRows[i];
                hash = AddString(
                    hash.Add(debug.Frame)
                        .Add(debug.QueryId)
                        .Add(debug.EntityId)
                        .Add(debug.BodyId)
                        .Add(debug.ColliderId)
                        .Add(debug.CandidateIndex)
                        .Add(debug.Layer)
                        .Add(debug.BroadphaseRawCandidateCount)
                        .Add(debug.BroadphaseCandidateCount)
                        .Add(debug.PostFilterCandidateCount)
                        .Add(debug.FilteredSourceCount)
                        .Add(debug.FilteredLayerCount)
                        .Add(debug.BroadphaseCellCount)
                        .Add(debug.HitCount),
                    debug.Status + "|" + debug.Reason);
            }

            for (int i = 0; i < _reasonLines.Length; i++)
            {
                hash = AddString(hash, _reasonLines[i]);
            }

            return hash;
        }

        private static CombatHash AddRowHash(
            CombatHash hash,
            int frame,
            int queryId,
            int entityId,
            int bodyId,
            int colliderId,
            int traceId,
            int actionId,
            int sourceOrder,
            string kind,
            string message)
        {
            return AddString(
                hash.Add(frame)
                    .Add(queryId)
                    .Add(entityId)
                    .Add(bodyId)
                    .Add(colliderId)
                    .Add(traceId)
                    .Add(actionId)
                    .Add(sourceOrder),
                kind + "|" + message);
        }

        private static CombatHash AddString(CombatHash hash, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return hash.Add(0);
            }

            hash = hash.Add(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                hash = hash.Add(value[i]);
            }

            return hash;
        }

        private static T[] ToSortedArray<T>(IEnumerable<T> values)
            where T : IComparable<T>
        {
            if (values == null)
            {
                return Array.Empty<T>();
            }

            var list = new List<T>(values);
            list.Sort();
            return list.ToArray();
        }

        private static string[] ToArray(IEnumerable<string> values)
        {
            if (values == null)
            {
                return Array.Empty<string>();
            }

            var list = new List<string>(values);
            return list.ToArray();
        }
    }

    public static class CombatAuthoringPreviewExplainer
    {
        private const int TargetLayer = 0;
        private const int DefaultActionInstanceId = 1;
        private const int DefaultDamage = 1;
        private const int DefaultStaggerFrames = 0;
        private const int DefaultColliderRadiusRaw = 220000;
        private const int DefaultShapeRadiusRaw = 220000;
        private const int DefaultCapsuleHeightRaw = 660000;
        private static readonly Fix64 SectorMinDot = Fix64.FromRatio(707106, 1000000);

        public static CombatAuthoringPreviewReport Explain(
            CombatActionAuthoringAsset action,
            CombatSceneBindingAsset binding,
            int frame,
            ICombatAuthoringPreviewMarkerResolver markerResolver)
        {
            string actionName = action == null ? string.Empty : action.name;
            string bindingName = binding == null ? string.Empty : binding.name;
            int actionId = action == null ? 0 : Math.Max(0, action.ActionId);
            var queries = new List<CombatAuthoringPreviewQuery>();
            var candidates = new List<CombatAuthoringPreviewCandidate>();
            var resolves = new List<CombatAuthoringPreviewResolve>();
            var physicsDebugRows = new List<CombatAuthoringPreviewPhysicsDebug>();
            var reasons = new List<string>();

            reasons.Add("Authoring Preview v0: uses CombatPhysicsWorld and HitResolveSystem with authoring defaults; full runtime WeaponTrace, damage, target state, and action instance data are not available.");
            if (action == null)
            {
                reasons.Add("No CombatActionAuthoringAsset is selected.");
                return CreateReport(actionName, actionId, bindingName, frame, queries, candidates, resolves, reasons, physicsDebugRows);
            }

            int clampedFrame = ClampFrame(frame, action.TotalFrames);
            if (clampedFrame != frame)
            {
                reasons.Add("Frame " + frame + " was clamped to " + clampedFrame + ".");
            }

            frame = clampedFrame;
            if (binding == null)
            {
                reasons.Add("No CombatSceneBindingAsset is selected; generated queries cannot overlap scene bodies.");
            }

            CombatActorBindingData[] actors = CloneAndSort(binding == null ? null : binding.Actors, CompareActors);
            CombatActorBindingData sourceActor = actors.Length == 0 ? default : actors[0];
            CombatEntityId sourceEntityId = sourceActor.EntityId > 0 ? new CombatEntityId(sourceActor.EntityId) : new CombatEntityId(1);

            if (actors.Length == 0)
            {
                reasons.Add("Scene binding has no actors; using source entity id " + sourceEntityId.Value + " and an empty physics world.");
            }
            else
            {
                reasons.Add("Source actor is entity " + sourceEntityId.Value + " from stable actor sort.");
            }

            var world = new CombatPhysicsWorld();
            BuildPhysicsWorld(actors, markerResolver, world, reasons);

            var queryHits = new List<CombatQueryResult>();
            var hitInputs = new List<CandidateResolveInput>();
            int nextQueryId = 1;

            CombatShapeAuthoringData[] hitboxes = CloneAndSort(action.Hitboxes, CompareShapes);
            int activeHitboxCount = 0;
            for (int i = 0; i < hitboxes.Length; i++)
            {
                CombatShapeAuthoringData shape = hitboxes[i];
                if (!ContainsFrame(shape.FrameRange, frame))
                {
                    continue;
                }

                activeHitboxCount++;
                if (!TryResolvePose(markerResolver, shape.MarkerId, out CombatAuthoringPreviewMarkerPose pose))
                {
                    reasons.Add("Hitbox " + shape.TrackId + " skipped because marker is missing: " + shape.MarkerId + ".");
                    continue;
                }

                CombatQueryHeader header = new CombatQueryHeader(
                    nextQueryId++,
                    ToQueryKind(shape.ShapeKind),
                    sourceEntityId,
                    Math.Max(0, shape.TrackId),
                    actionId,
                    Math.Max(0, shape.SourceOrder),
                    CombatPhysicsLayerMask.FromLayer(TargetLayer));

                queryHits.Clear();
                CombatPhysicsQuery physicsQuery = ExecuteShapeQuery(world, shape, pose, header, queryHits);
                AddQueryRow(queries, frame, header, "Hitbox", "Hitbox " + shape.TrackId + " generated " + header.Kind + " query.");
                AddPhysicsDebugRows(frame, world.ExplainQuery(physicsQuery), physicsDebugRows);
                AddCandidateRows(frame, sourceEntityId, actionId, queryHits, candidates, hitInputs);
                AddQueryReason(reasons, header, queryHits.Count);
            }

            if (activeHitboxCount == 0)
            {
                reasons.Add("No hitbox is active on frame " + frame + ".");
            }

            CombatWeaponTraceAuthoringData[] traces = CloneAndSort(action.WeaponTraces, CompareTraces);
            int activeTraceCount = 0;
            for (int i = 0; i < traces.Length; i++)
            {
                CombatWeaponTraceAuthoringData trace = traces[i];
                if (!ContainsFrame(trace.FrameRange, frame))
                {
                    continue;
                }

                activeTraceCount++;
                if (!TryResolvePose(markerResolver, trace.RootMarkerId, out CombatAuthoringPreviewMarkerPose root)
                    || !TryResolvePose(markerResolver, trace.TipMarkerId, out CombatAuthoringPreviewMarkerPose tip))
                {
                    reasons.Add("WeaponTrace " + trace.TraceId + " skipped because root or tip marker is missing: " + trace.RootMarkerId + " -> " + trace.TipMarkerId + ".");
                    continue;
                }

                WeaponTraceFrame traceFrame = new WeaponTraceFrame(
                    Math.Max(0, trace.TraceId),
                    ToFixVector(root.Position),
                    ToFixVector(tip.Position),
                    ToFixVector(root.Position),
                    ToFixVector(tip.Position),
                    ToPositiveFix(trace.RadiusRaw, DefaultShapeRadiusRaw),
                    CombatPhysicsLayerMask.FromLayer(TargetLayer));
                CombatCapsuleQuery query = WeaponTraceQueryBuilder.BuildCurrentBladeCapsule(
                    traceFrame,
                    sourceEntityId,
                    actionId,
                    nextQueryId++,
                    Math.Max(0, trace.SourceOrder));

                queryHits.Clear();
                CombatPhysicsQuery physicsQuery = CombatPhysicsQuery.From(query);
                world.Query(physicsQuery, queryHits);
                AddQueryRow(queries, frame, query.Header, "WeaponTrace", "WeaponTrace " + trace.TraceId + " generated current blade capsule query.");
                AddPhysicsDebugRows(frame, world.ExplainQuery(physicsQuery), physicsDebugRows);
                AddCandidateRows(frame, sourceEntityId, actionId, queryHits, candidates, hitInputs);
                AddQueryReason(reasons, query.Header, queryHits.Count);
            }

            if (activeTraceCount == 0)
            {
                reasons.Add("No weapon trace is active on frame " + frame + ".");
            }

            var hitResolveSystem = new HitResolveSystem();
            var hitResolveResults = new List<HitResolveResult>();
            hitInputs.Sort();
            var sortedHitCandidates = new List<HitCandidate>(hitInputs.Count);
            var sortedContexts = new List<CandidateContext>(hitInputs.Count);
            for (int i = 0; i < hitInputs.Count; i++)
            {
                sortedHitCandidates.Add(hitInputs[i].Candidate);
                sortedContexts.Add(hitInputs[i].Context);
            }

            hitResolveSystem.Resolve(sortedHitCandidates, new HashSet<WeaponHitOnceKey>(), hitResolveResults);
            AddResolveRows(resolves, hitResolveResults, sortedContexts);
            if (hitResolveResults.Count == 0)
            {
                reasons.Add("HitResolveSystem received no candidates.");
            }
            else
            {
                reasons.Add("HitResolveSystem resolved " + hitResolveResults.Count + " candidate(s).");
            }

            return CreateReport(actionName, actionId, bindingName, frame, queries, candidates, resolves, reasons, physicsDebugRows);
        }

        private static CombatAuthoringPreviewReport CreateReport(
            string actionName,
            int actionId,
            string bindingName,
            int frame,
            List<CombatAuthoringPreviewQuery> queries,
            List<CombatAuthoringPreviewCandidate> candidates,
            List<CombatAuthoringPreviewResolve> resolves,
            List<string> reasons,
            List<CombatAuthoringPreviewPhysicsDebug> physicsDebugRows)
        {
            return new CombatAuthoringPreviewReport(
                actionName,
                actionId,
                bindingName,
                frame,
                false,
                "Authoring Preview v0",
                queries,
                candidates,
                resolves,
                reasons,
                physicsDebugRows);
        }

        private static void BuildPhysicsWorld(
            CombatActorBindingData[] actors,
            ICombatAuthoringPreviewMarkerResolver markerResolver,
            CombatPhysicsWorld world,
            List<string> reasons)
        {
            for (int i = 0; i < actors.Length; i++)
            {
                CombatActorBindingData actor = actors[i];
                if (actor.EntityId <= 0 || actor.BodyId <= 0)
                {
                    reasons.Add("Actor skipped because entity/body id is not positive: entity " + actor.EntityId + ", body " + actor.BodyId + ".");
                    continue;
                }

                FixVector3 bodyPosition = FixVector3.Zero;
                if (TryResolvePose(markerResolver, actor.MarkerId, out CombatAuthoringPreviewMarkerPose actorPose))
                {
                    bodyPosition = ToFixVector(actorPose.Position);
                }
                else
                {
                    reasons.Add("Actor " + actor.EntityId + " marker missing; body uses origin for preview: " + actor.MarkerId + ".");
                }

                var bodyId = new CombatBodyId(actor.BodyId);
                world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(actor.EntityId), bodyId, bodyPosition));

                CombatColliderBindingData[] colliders = CloneAndSort(actor.Colliders, CompareColliders);
                for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
                {
                    CombatColliderBindingData collider = colliders[colliderIndex];
                    if (collider.ColliderId <= 0)
                    {
                        reasons.Add("Collider skipped because collider id is not positive: " + collider.ColliderId + ".");
                        continue;
                    }

                    FixVector3 center = bodyPosition;
                    if (TryResolvePose(markerResolver, collider.MarkerId, out CombatAuthoringPreviewMarkerPose colliderPose))
                    {
                        center = ToFixVector(colliderPose.Position);
                    }
                    else
                    {
                        reasons.Add("Collider " + collider.ColliderId + " marker missing; collider uses actor body origin for preview: " + collider.MarkerId + ".");
                    }

                    FixVector3 localCenter = center - bodyPosition;
                    Fix64 radius = ToPositiveFix(DefaultColliderRadiusRaw, DefaultColliderRadiusRaw);
                    world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                        bodyId,
                        new CombatColliderId(collider.ColliderId),
                        TargetLayer,
                        localCenter - new FixVector3(radius, radius, radius),
                        localCenter + new FixVector3(radius, radius, radius)));
                }
            }

            reasons.Add("Physics world built with " + world.BodyCount + " body(s) and " + world.ColliderCount + " AABB collider(s).");
        }

        private static CombatPhysicsQuery ExecuteShapeQuery(
            CombatPhysicsWorld world,
            CombatShapeAuthoringData shape,
            CombatAuthoringPreviewMarkerPose pose,
            CombatQueryHeader header,
            List<CombatQueryResult> results)
        {
            FixVector3 center = ToFixVector(pose.Position + (pose.Rotation * shape.LocalCenter));
            Fix64 radius = ToPositiveFix(shape.RadiusRaw, DefaultShapeRadiusRaw);
            switch (shape.ShapeKind)
            {
                case CombatAuthoringShapeKind.Capsule:
                    Fix64 height = ToPositiveFix(shape.HeightRaw, DefaultCapsuleHeightRaw);
                    Fix64 halfLine = Fix64.Max(Fix64.Zero, (height / Fix64.FromInt(2)) - radius);
                    FixVector3 up = ToFixVector(pose.Rotation * Vector3.up);
                    CombatPhysicsQuery capsuleQuery = CombatPhysicsQuery.From(new CombatCapsuleQuery(header, center - (up * halfLine), center + (up * halfLine), radius));
                    world.Query(capsuleQuery, results);
                    return capsuleQuery;
                case CombatAuthoringShapeKind.Aabb:
                    CombatPhysicsQuery aabbQuery = CombatPhysicsQuery.From(new CombatAabbQuery(header, center - new FixVector3(radius, radius, radius), center + new FixVector3(radius, radius, radius)));
                    world.Query(aabbQuery, results);
                    return aabbQuery;
                case CombatAuthoringShapeKind.Sector:
                    FixVector3 forward = ToFixVector(pose.Rotation * Vector3.forward);
                    CombatPhysicsQuery sectorQuery = CombatPhysicsQuery.From(new CombatSectorQuery(header, center, forward, radius, SectorMinDot));
                    world.Query(sectorQuery, results);
                    return sectorQuery;
                case CombatAuthoringShapeKind.Sphere:
                default:
                    CombatPhysicsQuery sphereQuery = CombatPhysicsQuery.From(new CombatSphereQuery(header, center, radius));
                    world.Query(sphereQuery, results);
                    return sphereQuery;
            }
        }

        private static void AddPhysicsDebugRows(
            int frame,
            CombatPhysicsQueryDebugReport report,
            List<CombatAuthoringPreviewPhysicsDebug> physicsDebugRows)
        {
            physicsDebugRows.Add(new CombatAuthoringPreviewPhysicsDebug(
                frame,
                report.Query.Header.QueryId,
                0,
                0,
                0,
                -1,
                0,
                report.BroadphaseRawCandidateCount,
                report.BroadphaseCandidateCount,
                report.PostFilterCandidateCount,
                report.FilteredSourceCount,
                report.FilteredLayerCount,
                report.BroadphaseCellCount,
                report.HitCount,
                report.IsUnsupported ? "Unsupported" : "Summary",
                report.IsUnsupported
                    ? report.UnsupportedReason
                    : "Broadphase raw candidates are counted before dedupe; broadphase candidates are after dedupe; post-filter candidates remain after source and layer filters."));

            IReadOnlyList<CombatPhysicsQueryDebugRow> rows = report.Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                CombatPhysicsQueryDebugRow row = rows[i];
                physicsDebugRows.Add(new CombatAuthoringPreviewPhysicsDebug(
                    frame,
                    report.Query.Header.QueryId,
                    row.EntityId.Value,
                    row.BodyId.Value,
                    row.ColliderId.Value,
                    row.CandidateIndex,
                    row.Layer,
                    report.BroadphaseRawCandidateCount,
                    report.BroadphaseCandidateCount,
                    report.PostFilterCandidateCount,
                    report.FilteredSourceCount,
                    report.FilteredLayerCount,
                    report.BroadphaseCellCount,
                    report.HitCount,
                    row.Status.ToString(),
                    ToPhysicsDebugReason(row.Status)));
            }
        }

        private static string ToPhysicsDebugReason(CombatPhysicsQueryDebugRowStatus status)
        {
            switch (status)
            {
                case CombatPhysicsQueryDebugRowStatus.Hit:
                    return "candidate passed filters and overlapped the query shape.";
                case CombatPhysicsQueryDebugRowStatus.Miss:
                    return "candidate passed broadphase but missed the query shape.";
                case CombatPhysicsQueryDebugRowStatus.FilteredLayer:
                    return "candidate layer is not included by the query mask.";
                case CombatPhysicsQueryDebugRowStatus.FilteredSource:
                    return "candidate belongs to the source entity and source hits are disabled.";
                default:
                    return "candidate has an unknown debug status.";
            }
        }

        private static void AddQueryRow(
            List<CombatAuthoringPreviewQuery> queries,
            int frame,
            CombatQueryHeader header,
            string kind,
            string message)
        {
            queries.Add(new CombatAuthoringPreviewQuery(
                frame,
                header.QueryId,
                header.SourceEntityId.Value,
                0,
                0,
                header.TraceId,
                header.ActionId,
                header.SourceOrder,
                kind,
                message));
        }

        private static void AddCandidateRows(
            int frame,
            CombatEntityId sourceEntityId,
            int actionId,
            List<CombatQueryResult> runtimeHits,
            List<CombatAuthoringPreviewCandidate> candidates,
            List<CandidateResolveInput> hitInputs)
        {
            for (int i = 0; i < runtimeHits.Count; i++)
            {
                CombatQueryResult hit = runtimeHits[i];
                candidates.Add(new CombatAuthoringPreviewCandidate(
                    frame,
                    hit.Query.QueryId,
                    hit.TargetEntityId.Value,
                    hit.TargetBodyId.Value,
                    hit.TargetColliderId.Value,
                    hit.Query.TraceId,
                    hit.Query.ActionId,
                    hit.Query.SourceOrder,
                    "PhysicsHit",
                    "Query " + hit.Query.QueryId + " overlapped entity " + hit.TargetEntityId.Value + ", body " + hit.TargetBodyId.Value + ", collider " + hit.TargetColliderId.Value + "."));

                var candidate = new HitCandidate(
                    sourceEntityId,
                    hit.TargetEntityId,
                    actionId,
                    DefaultActionInstanceId,
                    hit.Query.TraceId,
                    new CombatFrame(frame),
                    hit,
                    DefaultDamage,
                    DefaultStaggerFrames,
                    FixVector3.Zero,
                    HitTargetStateFlags.Alive);

                var context = new CandidateContext(
                    hit.Query.QueryId,
                    hit.TargetBodyId.Value,
                    hit.TargetColliderId.Value,
                    hit.Query.SourceOrder);
                hitInputs.Add(new CandidateResolveInput(candidate, context));
            }
        }

        private static void AddResolveRows(
            List<CombatAuthoringPreviewResolve> resolves,
            List<HitResolveResult> results,
            List<CandidateContext> candidateContexts)
        {
            for (int i = 0; i < results.Count; i++)
            {
                HitResolveResult result = results[i];
                CandidateContext context = i < candidateContexts.Count ? candidateContexts[i] : default;
                resolves.Add(new CombatAuthoringPreviewResolve(
                    result.Frame.Value,
                    context.QueryId,
                    result.TargetId.Value,
                    context.BodyId,
                    context.ColliderId,
                    result.TraceId,
                    result.ActionId,
                    context.SourceOrder,
                    result.Kind.ToString(),
                    "HitResolveSystem returned " + result.Kind + " for target " + result.TargetId.Value + " with damage " + result.Damage + "."));
            }
        }

        private static void AddQueryReason(List<string> reasons, CombatQueryHeader header, int hitCount)
        {
            if (hitCount == 0)
            {
                reasons.Add("Query " + header.QueryId + " generated no candidate overlaps.");
                return;
            }

            reasons.Add("Query " + header.QueryId + " generated " + hitCount + " candidate overlap(s).");
        }

        private static bool TryResolvePose(
            ICombatAuthoringPreviewMarkerResolver resolver,
            string markerId,
            out CombatAuthoringPreviewMarkerPose pose)
        {
            if (resolver == null || string.IsNullOrWhiteSpace(markerId))
            {
                pose = default;
                return false;
            }

            return resolver.TryResolveMarker(markerId, out pose);
        }

        private static CombatQueryKind ToQueryKind(CombatAuthoringShapeKind kind)
        {
            switch (kind)
            {
                case CombatAuthoringShapeKind.Capsule:
                    return CombatQueryKind.Capsule;
                case CombatAuthoringShapeKind.Aabb:
                    return CombatQueryKind.Aabb;
                case CombatAuthoringShapeKind.Sector:
                    return CombatQueryKind.Sector;
                case CombatAuthoringShapeKind.Sphere:
                default:
                    return CombatQueryKind.Sphere;
            }
        }

        private static Fix64 ToPositiveFix(int rawValue, int defaultRaw)
        {
            int value = rawValue > 0 ? rawValue : defaultRaw;
            return Fix64.FromRaw(value);
        }

        private static FixVector3 ToFixVector(Vector3 value)
        {
            return new FixVector3(ToFix(value.x), ToFix(value.y), ToFix(value.z));
        }

        private static Fix64 ToFix(float value)
        {
            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }

        private static bool ContainsFrame(CombatAuthoringFrameRange range, int frame)
        {
            return !range.IsEmpty && frame >= range.StartFrame && frame <= range.EndFrame;
        }

        private static int ClampFrame(int frame, int totalFrames)
        {
            return Math.Max(0, Math.Min(frame, Math.Max(0, totalFrames - 1)));
        }

        private static T[] CloneAndSort<T>(T[] values, Comparison<T> comparison)
        {
            if (values == null || values.Length == 0)
            {
                return Array.Empty<T>();
            }

            T[] sorted = (T[])values.Clone();
            Array.Sort(sorted, comparison);
            return sorted;
        }

        private static int CompareActors(CombatActorBindingData left, CombatActorBindingData right)
        {
            int compare = left.EntityId.CompareTo(right.EntityId);
            return compare != 0 ? compare : left.BodyId.CompareTo(right.BodyId);
        }

        private static int CompareColliders(CombatColliderBindingData left, CombatColliderBindingData right)
        {
            int compare = left.ColliderId.CompareTo(right.ColliderId);
            return compare != 0 ? compare : left.SourceOrder.CompareTo(right.SourceOrder);
        }

        private static int CompareShapes(CombatShapeAuthoringData left, CombatShapeAuthoringData right)
        {
            int compare = left.TrackId.CompareTo(right.TrackId);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.FrameRange.StartFrame.CompareTo(right.FrameRange.StartFrame);
            return compare != 0 ? compare : left.SourceOrder.CompareTo(right.SourceOrder);
        }

        private static int CompareTraces(CombatWeaponTraceAuthoringData left, CombatWeaponTraceAuthoringData right)
        {
            int compare = left.TraceId.CompareTo(right.TraceId);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.FrameRange.StartFrame.CompareTo(right.FrameRange.StartFrame);
            return compare != 0 ? compare : left.SourceOrder.CompareTo(right.SourceOrder);
        }

        private readonly struct CandidateContext
        {
            public CandidateContext(int queryId, int bodyId, int colliderId, int sourceOrder)
            {
                QueryId = queryId;
                BodyId = bodyId;
                ColliderId = colliderId;
                SourceOrder = sourceOrder;
            }

            public int QueryId { get; }

            public int BodyId { get; }

            public int ColliderId { get; }

            public int SourceOrder { get; }
        }

        private readonly struct CandidateResolveInput : IComparable<CandidateResolveInput>
        {
            public CandidateResolveInput(HitCandidate candidate, CandidateContext context)
            {
                Candidate = candidate;
                Context = context;
            }

            public HitCandidate Candidate { get; }

            public CandidateContext Context { get; }

            public int CompareTo(CandidateResolveInput other)
            {
                return Candidate.CompareTo(other.Candidate);
            }
        }
    }
}
