using System.Collections.Generic;
using System.Text;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Authoring;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Combat.Authoring
{
    public sealed class CombatPhysicsDebugVisualizationTests
    {
        private const int ActionId = 1001;
        private const int TraceId = 77;
        private const int QueryId = 1;
        private const int SourceOrder = 9;

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
        public void WeaponAuthoringPreviewAndPhysicsDebugReport_ExposeStableHitQueryExplanation()
        {
            CombatAuthoringPreviewReport authoringReport = CreateWeaponAuthoringReport();
            CombatPhysicsQueryDebugReport physicsReport = CreateEquivalentPhysicsReport();

            string authoringDisplayText = authoringReport.ToDisplayText();
            string authoringDisplayTextAgain = CreateWeaponAuthoringReport().ToDisplayText();
            string physicsSignature = ToPhysicsDebugSignature(physicsReport);
            string physicsSignatureAgain = ToPhysicsDebugSignature(CreateEquivalentPhysicsReport());

            Assert.AreEqual(authoringDisplayText, authoringDisplayTextAgain);
            StringAssert.Contains("Generated Queries: 1", authoringDisplayText);
            StringAssert.Contains("Candidate Hits: 1", authoringDisplayText);
            StringAssert.Contains("Frame 0 / Query 1 / Entity 1 / Body 0 / Collider 0 / Trace 77 / Action 1001 / Order 9 / WeaponTrace", authoringDisplayText);
            StringAssert.Contains("Frame 0 / Query 1 / Entity 2 / Body 2 / Collider 1 / Trace 77 / Action 1001 / Order 9 / PhysicsHit", authoringDisplayText);
            StringAssert.Contains("Physics Query Debug", authoringDisplayText);
            StringAssert.Contains("Broadphase candidates", authoringDisplayText);
            StringAssert.Contains("Hits 1", authoringDisplayText);
            StringAssert.Contains("HitResolveSystem resolved 1 candidate(s).", authoringDisplayText);

            Assert.AreEqual(TraceId, physicsReport.Query.Header.TraceId);
            Assert.AreEqual(ActionId, physicsReport.Query.Header.ActionId);
            Assert.AreEqual(SourceOrder, physicsReport.Query.Header.SourceOrder);
            Assert.Greater(physicsReport.BroadphaseRawCandidateCount, physicsReport.BroadphaseCandidateCount);
            Assert.AreEqual(4, physicsReport.BroadphaseCandidateCount);
            Assert.AreEqual(1, physicsReport.FilteredSourceCount);
            Assert.AreEqual(1, physicsReport.FilteredLayerCount);
            Assert.AreEqual(2, physicsReport.PostFilterCandidateCount);
            Assert.AreEqual(1, physicsReport.HitCount);
            Assert.AreEqual(1, CountRows(physicsReport, CombatPhysicsQueryDebugRowStatus.Hit));
            Assert.AreEqual(1, CountRows(physicsReport, CombatPhysicsQueryDebugRowStatus.Miss));
            Assert.AreEqual(1, CountRows(physicsReport, CombatPhysicsQueryDebugRowStatus.FilteredSource));
            Assert.AreEqual(1, CountRows(physicsReport, CombatPhysicsQueryDebugRowStatus.FilteredLayer));
            Assert.AreEqual(physicsSignature, physicsSignatureAgain);
            StringAssert.Contains("raw=", physicsSignature);
            StringAssert.Contains("dedup=4", physicsSignature);
            StringAssert.Contains("postFilter=2", physicsSignature);
            StringAssert.Contains("hit=1", physicsSignature);
            StringAssert.Contains("entity=1/body=1/collider=1/layer=0/status=FilteredSource", physicsSignature);
            StringAssert.Contains("entity=2/body=2/collider=1/layer=0/status=Hit", physicsSignature);
            StringAssert.Contains("entity=3/body=3/collider=1/layer=0/status=Miss", physicsSignature);
            StringAssert.Contains("entity=4/body=4/collider=1/layer=1/status=FilteredLayer", physicsSignature);
        }

        private CombatAuthoringPreviewReport CreateWeaponAuthoringReport()
        {
            CombatActionAuthoringAsset action = ScriptableObject.CreateInstance<CombatActionAuthoringAsset>();
            action.name = "WeaponDebugPreview";
            action.ActionId = ActionId;
            action.TotalFrames = 2;
            action.Startup = new CombatAuthoringFrameRange(0, 0);
            action.Active = new CombatAuthoringFrameRange(0, 0);
            action.Recovery = new CombatAuthoringFrameRange(1, 1);
            action.WeaponTraces = new[]
            {
                new CombatWeaponTraceAuthoringData
                {
                    TraceId = TraceId,
                    FrameRange = new CombatAuthoringFrameRange(0, 0),
                    RootMarkerId = "blade_root",
                    TipMarkerId = "blade_tip",
                    RadiusRaw = 500000,
                    SourceOrder = SourceOrder,
                },
            };
            _createdAssets.Add(action);

            CombatSceneBindingAsset binding = ScriptableObject.CreateInstance<CombatSceneBindingAsset>();
            binding.name = "WeaponDebugBinding";
            binding.Actors = new[]
            {
                Actor(1, 1, "source", Collider(1, "source_collider", 1)),
                Actor(2, 2, "hit_target", Collider(1, "hit_target_collider", 2)),
                Actor(3, 3, "miss_target", Collider(1, "miss_target_collider", 3)),
            };
            _createdAssets.Add(binding);

            var resolver = new DictionaryMarkerResolver()
                .Add("blade_root", Vector3.zero)
                .Add("blade_tip", new Vector3(4f, 0f, 0f))
                .Add("source", Vector3.zero)
                .Add("source_collider", Vector3.zero)
                .Add("hit_target", new Vector3(3f, 0f, 0f))
                .Add("hit_target_collider", new Vector3(3f, 0f, 0f))
                .Add("miss_target", new Vector3(2f, 0.9f, 0f))
                .Add("miss_target_collider", new Vector3(2f, 0.9f, 0f));

            return CombatAuthoringPreviewExplainer.Explain(action, binding, 0, resolver);
        }

        private static CombatPhysicsQueryDebugReport CreateEquivalentPhysicsReport()
        {
            var world = new CombatPhysicsWorld(new CombatPhysicsBroadphaseConfig(Fix64.One));
            RegisterCollider(world, entity: 1, body: 1, collider: 1, layer: 0, position: FixVector3.Zero, halfSize: Fix64.FromRaw(200000));
            RegisterCollider(world, entity: 2, body: 2, collider: 1, layer: 0, position: new FixVector3(Fix64.FromInt(3), Fix64.Zero, Fix64.Zero), halfSize: Fix64.FromRaw(200000));
            RegisterCollider(world, entity: 3, body: 3, collider: 1, layer: 0, position: new FixVector3(Fix64.FromInt(2), Fix64.Half, Fix64.Half), halfSize: Fix64.FromRaw(50000));
            RegisterCollider(world, entity: 4, body: 4, collider: 1, layer: 1, position: new FixVector3(Fix64.FromRaw(1500000), Fix64.Zero, Fix64.Zero), halfSize: Fix64.FromRaw(200000));

            var frame = new WeaponTraceFrame(
                TraceId,
                rootPrev: FixVector3.Zero,
                tipPrev: FixVector3.Zero,
                rootNow: FixVector3.Zero,
                tipNow: new FixVector3(Fix64.FromInt(4), Fix64.Zero, Fix64.Zero),
                radius: Fix64.Half,
                targetMask: CombatPhysicsLayerMask.FromLayer(0));
            CombatCapsuleQuery capsule = WeaponTraceQueryBuilder.BuildCurrentBladeCapsule(
                frame,
                new CombatEntityId(1),
                ActionId,
                QueryId,
                SourceOrder);

            return world.ExplainQuery(CombatPhysicsQuery.From(capsule));
        }

        private static void RegisterCollider(
            CombatPhysicsWorld world,
            int entity,
            int body,
            int collider,
            int layer,
            FixVector3 position,
            Fix64 halfSize)
        {
            var bodyId = new CombatBodyId(body);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(entity), bodyId, position));
            var halfExtents = new FixVector3(halfSize, halfSize, halfSize);
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(collider),
                layer,
                -halfExtents,
                halfExtents));
        }

        private static string ToPhysicsDebugSignature(CombatPhysicsQueryDebugReport report)
        {
            var builder = new StringBuilder(512);
            builder.Append("shape=");
            builder.Append(report.ShapeKind);
            builder.Append(";raw=");
            builder.Append(report.BroadphaseRawCandidateCount);
            builder.Append(";dedup=");
            builder.Append(report.BroadphaseCandidateCount);
            builder.Append(";postFilter=");
            builder.Append(report.PostFilterCandidateCount);
            builder.Append(";sourceFilter=");
            builder.Append(report.FilteredSourceCount);
            builder.Append(";layerFilter=");
            builder.Append(report.FilteredLayerCount);
            builder.Append(";hit=");
            builder.Append(report.HitCount);
            builder.Append(";cells=");
            builder.Append(report.BroadphaseCellCount);
            for (int i = 0; i < report.Rows.Count; i++)
            {
                CombatPhysicsQueryDebugRow row = report.Rows[i];
                builder.Append(";row=");
                builder.Append(row.CandidateIndex);
                builder.Append("/entity=");
                builder.Append(row.EntityId.Value);
                builder.Append("/body=");
                builder.Append(row.BodyId.Value);
                builder.Append("/collider=");
                builder.Append(row.ColliderId.Value);
                builder.Append("/layer=");
                builder.Append(row.Layer);
                builder.Append("/status=");
                builder.Append(row.Status);
            }

            return builder.ToString();
        }

        private static int CountRows(CombatPhysicsQueryDebugReport report, CombatPhysicsQueryDebugRowStatus status)
        {
            int count = 0;
            for (int i = 0; i < report.Rows.Count; i++)
            {
                if (report.Rows[i].Status == status)
                {
                    count++;
                }
            }

            return count;
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
