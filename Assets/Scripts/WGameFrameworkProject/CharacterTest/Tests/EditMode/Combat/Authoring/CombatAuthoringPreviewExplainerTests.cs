using System.Collections.Generic;
using MxFramework.Combat.Authoring;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Combat.Authoring
{
    public class CombatAuthoringPreviewExplainerTests
    {
        private readonly List<Object> _createdAssets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdAssets.Count; i++)
            {
                Object.DestroyImmediate(_createdAssets[i]);
            }

            _createdAssets.Clear();
        }

        [Test]
        public void Explain_GeneratesQueryCandidateAndResolveThroughRuntimeSystems()
        {
            CombatActionAuthoringAsset action = CreateAction("PreviewAction");
            action.Hitboxes = new[]
            {
                Shape(trackId: 10, start: 0, end: 0, marker: "hit", radiusRaw: 350000, sourceOrder: 5),
            };

            CombatSceneBindingAsset binding = CreateBinding("PreviewBinding");
            binding.Actors = new[]
            {
                Actor(1, 1, "source", Collider(1, "source_collider", 1)),
                Actor(2, 2, "target", Collider(2, "target_collider", 2)),
            };

            var resolver = new DictionaryMarkerResolver()
                .Add("source", Vector3.zero)
                .Add("source_collider", Vector3.zero)
                .Add("target", Vector3.right)
                .Add("target_collider", Vector3.right)
                .Add("hit", Vector3.right);

            CombatAuthoringPreviewReport report = CombatAuthoringPreviewExplainer.Explain(action, binding, 0, resolver);

            Assert.AreEqual(1, report.QueryCount);
            Assert.AreEqual(1, report.CandidateCount);
            Assert.AreEqual(1, report.ResolveCount);
            Assert.AreEqual("Damage", report.GetResolve(0).Kind);
            Assert.AreEqual(2, report.GetCandidate(0).EntityId);
            Assert.IsFalse(report.HasRuntimePreview);
            StringAssert.Contains("Authoring Preview v0", report.ToDisplayText());
            Assert.IsFalse(string.IsNullOrEmpty(report.StableHash));
        }

        [Test]
        public void Explain_SortsGeneratedRowsByStableKeys()
        {
            CombatActionAuthoringAsset first = CreateAction("StablePreview");
            first.Hitboxes = new[]
            {
                Shape(trackId: 2, start: 0, end: 0, marker: "hit_b", radiusRaw: 350000, sourceOrder: 20),
                Shape(trackId: 1, start: 0, end: 0, marker: "hit_a", radiusRaw: 350000, sourceOrder: 10),
            };

            CombatActionAuthoringAsset second = CreateAction("StablePreview");
            second.Hitboxes = new[]
            {
                Shape(trackId: 1, start: 0, end: 0, marker: "hit_a", radiusRaw: 350000, sourceOrder: 10),
                Shape(trackId: 2, start: 0, end: 0, marker: "hit_b", radiusRaw: 350000, sourceOrder: 20),
            };

            CombatSceneBindingAsset binding = CreateBinding("StableBinding");
            binding.Actors = new[]
            {
                Actor(2, 2, "target", Collider(2, "target_collider", 2)),
                Actor(1, 1, "source", Collider(1, "source_collider", 1)),
            };

            var resolver = new DictionaryMarkerResolver()
                .Add("source", Vector3.zero)
                .Add("source_collider", Vector3.zero)
                .Add("target", Vector3.right)
                .Add("target_collider", Vector3.right)
                .Add("hit_a", Vector3.right)
                .Add("hit_b", Vector3.right);

            CombatAuthoringPreviewReport firstReport = CombatAuthoringPreviewExplainer.Explain(first, binding, 0, resolver);
            CombatAuthoringPreviewReport secondReport = CombatAuthoringPreviewExplainer.Explain(second, binding, 0, resolver);

            Assert.AreEqual("1:1:10|2:2:20", QueryOrder(firstReport));
            Assert.AreEqual(QueryOrder(firstReport), QueryOrder(secondReport));
            Assert.AreEqual(firstReport.StableHash, secondReport.StableHash);
        }

        [Test]
        public void Explain_ReportsReadableReasonsWhenBindingOrMarkerIsMissing()
        {
            CombatActionAuthoringAsset action = CreateAction("MissingPreview");
            action.Hitboxes = new[]
            {
                Shape(trackId: 1, start: 0, end: 0, marker: "missing", radiusRaw: 350000, sourceOrder: 1),
            };

            CombatAuthoringPreviewReport report = CombatAuthoringPreviewExplainer.Explain(action, null, 0, new DictionaryMarkerResolver());

            Assert.AreEqual(0, report.QueryCount);
            Assert.AreEqual(0, report.CandidateCount);
            Assert.AreEqual(0, report.ResolveCount);
            StringAssert.Contains("No CombatSceneBindingAsset is selected", report.ToDisplayText());
            StringAssert.Contains("marker is missing", report.ToDisplayText());
        }

        [Test]
        public void Explain_DisplayTextIncludesPhysicsQueryDebugRows()
        {
            CombatActionAuthoringAsset action = CreateAction("PhysicsDebugPreview");
            action.Hitboxes = new[]
            {
                Shape(trackId: 1, start: 0, end: 0, marker: "hit", radiusRaw: 350000, sourceOrder: 1),
            };

            CombatSceneBindingAsset binding = CreateBinding("PhysicsDebugBinding");
            binding.Actors = new[]
            {
                Actor(1, 1, "source", Collider(1, "source_collider", 1)),
                Actor(2, 2, "miss_target", Collider(2, "miss_collider", 2)),
            };

            var resolver = new DictionaryMarkerResolver()
                .Add("source", Vector3.zero)
                .Add("source_collider", Vector3.zero)
                .Add("hit", Vector3.zero)
                .Add("miss_target", new Vector3(0.56f, 0.56f, 0.56f))
                .Add("miss_collider", new Vector3(0.56f, 0.56f, 0.56f));

            CombatAuthoringPreviewReport report = CombatAuthoringPreviewExplainer.Explain(action, binding, 0, resolver);

            Assert.AreEqual(0, report.CandidateCount);
            Assert.AreEqual(3, report.PhysicsDebugCount);
            Assert.AreEqual("Summary", report.GetPhysicsDebug(0).Status);
            Assert.AreEqual("FilteredSource", report.GetPhysicsDebug(1).Status);
            Assert.AreEqual("Miss", report.GetPhysicsDebug(2).Status);
            string displayText = report.ToDisplayText();
            StringAssert.Contains("Physics Query Debug", displayText);
            StringAssert.Contains("Broadphase candidates 2", displayText);
            StringAssert.Contains("Post-filter 1", displayText);
            StringAssert.Contains("Filtered source 1", displayText);
            StringAssert.Contains("candidate belongs to the source entity", displayText);
            StringAssert.Contains("candidate passed broadphase but missed", displayText);
        }

        private CombatActionAuthoringAsset CreateAction(string name)
        {
            var action = ScriptableObject.CreateInstance<CombatActionAuthoringAsset>();
            action.name = name;
            action.ActionId = 1001;
            action.TotalFrames = 4;
            action.Startup = new CombatAuthoringFrameRange(0, 0);
            action.Active = new CombatAuthoringFrameRange(0, 1);
            action.Recovery = new CombatAuthoringFrameRange(2, 3);
            _createdAssets.Add(action);
            return action;
        }

        private CombatSceneBindingAsset CreateBinding(string name)
        {
            var binding = ScriptableObject.CreateInstance<CombatSceneBindingAsset>();
            binding.name = name;
            _createdAssets.Add(binding);
            return binding;
        }

        private static CombatShapeAuthoringData Shape(
            int trackId,
            int start,
            int end,
            string marker,
            int radiusRaw,
            int sourceOrder)
        {
            return new CombatShapeAuthoringData
            {
                TrackId = trackId,
                ShapeKind = CombatAuthoringShapeKind.Sphere,
                FrameRange = new CombatAuthoringFrameRange(start, end),
                MarkerId = marker,
                RadiusRaw = radiusRaw,
                SourceOrder = sourceOrder,
            };
        }

        private static CombatActorBindingData Actor(
            int entityId,
            int bodyId,
            string marker,
            params CombatColliderBindingData[] colliders)
        {
            return new CombatActorBindingData
            {
                EntityId = entityId,
                BodyId = bodyId,
                MarkerId = marker,
                Colliders = colliders,
            };
        }

        private static CombatColliderBindingData Collider(int colliderId, string marker, int sourceOrder)
        {
            return new CombatColliderBindingData
            {
                ColliderId = colliderId,
                MarkerId = marker,
                SourceOrder = sourceOrder,
            };
        }

        private static string QueryOrder(CombatAuthoringPreviewReport report)
        {
            var keys = new List<string>();
            for (int i = 0; i < report.QueryCount; i++)
            {
                CombatAuthoringPreviewQuery query = report.GetQuery(i);
                keys.Add(query.QueryId + ":" + query.TraceId + ":" + query.SourceOrder);
            }

            return string.Join("|", keys);
        }

        private sealed class DictionaryMarkerResolver : ICombatAuthoringPreviewMarkerResolver
        {
            private readonly Dictionary<string, CombatAuthoringPreviewMarkerPose> _poses =
                new Dictionary<string, CombatAuthoringPreviewMarkerPose>();

            public DictionaryMarkerResolver Add(string markerId, Vector3 position)
            {
                _poses[markerId] = new CombatAuthoringPreviewMarkerPose(position, Quaternion.identity);
                return this;
            }

            public bool TryResolveMarker(string markerId, out CombatAuthoringPreviewMarkerPose pose)
            {
                return _poses.TryGetValue(markerId, out pose);
            }
        }
    }
}
