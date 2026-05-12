using System.Collections.Generic;
using MxFramework.Combat.Authoring;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Combat.Authoring
{
    public class CombatAuthoringValidatorTests
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
        public void Validate_ReportsDuplicateIdsInvalidFrameRangesNegativeRadiusAndMissingMarkers()
        {
            CombatActionAuthoringAsset action = CreateAction("ActionWithErrors");
            action.TotalFrames = 10;
            action.Startup = new CombatAuthoringFrameRange(-1, 2);
            action.Hitboxes = new[]
            {
                Shape(trackId: 1, start: 0, end: 9, marker: "root", radiusRaw: 100, sourceOrder: 1),
                Shape(trackId: 1, start: 11, end: 12, marker: "missing_shape", radiusRaw: -1, sourceOrder: 2),
            };
            action.WeaponTraces = new[]
            {
                Trace(traceId: 7, start: 0, end: 9, rootMarker: "root", tipMarker: "tip", radiusRaw: 50, sourceOrder: 1),
                Trace(traceId: 7, start: -1, end: 4, rootMarker: "missing_root", tipMarker: "missing_tip", radiusRaw: -2, sourceOrder: 2),
            };
            CombatSceneBindingAsset binding = CreateBinding("BindingWithMarkers");
            binding.Markers = new[]
            {
                Marker("root", sourceOrder: 2),
                Marker("tip", sourceOrder: 1),
            };

            CombatAuthoringReport report = CombatAuthoringValidator.Validate(action, binding);

            Assert.IsTrue(report.HasErrors);
            AssertIssue(report, "ActionWithErrors", "Startup", "startup", "Frame range is invalid.");
            AssertIssue(report, "ActionWithErrors", "Hitbox", "trackId", "Duplicate track id.");
            AssertIssue(report, "ActionWithErrors", "Hitbox", "frameRange", "Frame range must be inside totalFrames.");
            AssertIssue(report, "ActionWithErrors", "Hitbox", "radiusRaw", "Shape radius must be positive.");
            AssertIssue(report, "ActionWithErrors", "Hitbox", "markerId", "Shape marker is missing.");
            AssertIssue(report, "ActionWithErrors", "WeaponTrace", "traceId", "Duplicate trace id.");
            AssertIssue(report, "ActionWithErrors", "WeaponTrace", "frameRange", "Frame range is invalid.");
            AssertIssue(report, "ActionWithErrors", "WeaponTrace", "radiusRaw", "Weapon trace radius cannot be negative.");
            AssertIssue(report, "ActionWithErrors", "WeaponTrace", "rootMarkerId", "Weapon trace root marker is missing.");
            AssertIssue(report, "ActionWithErrors", "WeaponTrace", "tipMarkerId", "Weapon trace tip marker is missing.");
        }

        [Test]
        public void Validate_WarnsWhenCapsuleHeightIsSmallerThanDiameter()
        {
            CombatActionAuthoringAsset action = CreateAction("CapsuleAction");
            action.Hitboxes = new[]
            {
                Shape(
                    trackId: 1,
                    start: 0,
                    end: 1,
                    marker: "root",
                    radiusRaw: 100000,
                    sourceOrder: 1,
                    shapeKind: CombatAuthoringShapeKind.Capsule,
                    heightRaw: 150000),
            };

            CombatSceneBindingAsset binding = CreateBinding("BindingWithMarkers");
            binding.Markers = new[]
            {
                Marker("root", sourceOrder: 1),
            };

            CombatAuthoringReport report = CombatAuthoringValidator.Validate(action, binding);

            Assert.IsFalse(report.HasErrors);
            AssertIssue(
                report,
                "CapsuleAction",
                "Hitbox",
                "heightRaw",
                "Capsule height is smaller than diameter.",
                CombatAuthoringSeverity.Warning);
        }

        [Test]
        public void Validate_ReportsSceneBindingDuplicateIdsAndMissingMarkers()
        {
            CombatActionAuthoringAsset action = CreateAction("ValidAction");
            CombatSceneBindingAsset binding = CreateBinding("BindingWithErrors");
            binding.Actors = new[]
            {
                Actor(entityId: 2, bodyId: 10, marker: "actor_missing_b", Collider(colliderId: 5, marker: "collider_missing_b", sourceOrder: 20)),
                Actor(entityId: 2, bodyId: 10, marker: "actor_missing_a", Collider(colliderId: 5, marker: "collider_missing_a", sourceOrder: 10)),
            };

            CombatAuthoringReport report = CombatAuthoringValidator.Validate(action, binding);

            Assert.IsTrue(report.HasErrors);
            AssertIssue(report, "BindingWithErrors", "Actor", "entityId", "Duplicate entity id in scene binding.");
            AssertIssue(report, "BindingWithErrors", "Actor", "bodyId", "Duplicate body id in scene binding.");
            AssertIssue(report, "BindingWithErrors", "Actor", "markerId", "Actor marker is missing.");
            AssertIssue(report, "BindingWithErrors", "Collider", "colliderId", "Duplicate collider id in scene binding.");
            AssertIssue(report, "BindingWithErrors", "Collider", "markerId", "Collider marker is missing.");
        }

        [Test]
        public void Validate_SortsSceneBindingIssuesByExplicitKeys()
        {
            CombatActionAuthoringAsset action = CreateAction("ValidAction");
            CombatSceneBindingAsset first = CreateBinding("BindingA");
            first.Actors = new[]
            {
                Actor(entityId: 4, bodyId: 4, marker: "missing_4"),
                Actor(entityId: 2, bodyId: 2, marker: "missing_2"),
                Actor(entityId: 3, bodyId: 3, marker: "missing_3"),
            };
            CombatSceneBindingAsset second = CreateBinding("BindingB");
            second.Actors = new[]
            {
                Actor(entityId: 3, bodyId: 3, marker: "missing_3"),
                Actor(entityId: 4, bodyId: 4, marker: "missing_4"),
                Actor(entityId: 2, bodyId: 2, marker: "missing_2"),
            };

            string firstOrder = IssueOrder(CombatAuthoringValidator.Validate(action, first));
            string secondOrder = IssueOrder(CombatAuthoringValidator.Validate(action, second));

            Assert.AreEqual("Actor:2:markerId|Actor:3:markerId|Actor:4:markerId", firstOrder);
            Assert.AreEqual(firstOrder, secondOrder);
        }

        [Test]
        public void Report_SortsQueryRowsByStableKeys()
        {
            var first = new CombatAuthoringReport(
                issues: null,
                queryRows: new[]
                {
                    Query(frame: 2, query: 3, entity: 1, body: 1, collider: 1, trace: 1, action: 1, order: 1),
                    Query(frame: 1, query: 2, entity: 3, body: 1, collider: 1, trace: 1, action: 1, order: 1),
                    Query(frame: 1, query: 1, entity: 1, body: 2, collider: 2, trace: 1, action: 1, order: 1),
                    Query(frame: 1, query: 4, entity: 1, body: 2, collider: 1, trace: 2, action: 1, order: 1),
                    Query(frame: 1, query: 5, entity: 1, body: 2, collider: 1, trace: 1, action: 2, order: 1),
                    Query(frame: 1, query: 6, entity: 1, body: 2, collider: 1, trace: 1, action: 1, order: 2),
                    Query(frame: 1, query: 7, entity: 1, body: 2, collider: 1, trace: 1, action: 1, order: 1),
                });
            var second = new CombatAuthoringReport(
                issues: null,
                queryRows: new[]
                {
                    Query(frame: 1, query: 7, entity: 1, body: 2, collider: 1, trace: 1, action: 1, order: 1),
                    Query(frame: 1, query: 6, entity: 1, body: 2, collider: 1, trace: 1, action: 1, order: 2),
                    Query(frame: 1, query: 5, entity: 1, body: 2, collider: 1, trace: 1, action: 2, order: 1),
                    Query(frame: 1, query: 4, entity: 1, body: 2, collider: 1, trace: 2, action: 1, order: 1),
                    Query(frame: 1, query: 1, entity: 1, body: 2, collider: 2, trace: 1, action: 1, order: 1),
                    Query(frame: 1, query: 2, entity: 3, body: 1, collider: 1, trace: 1, action: 1, order: 1),
                    Query(frame: 2, query: 3, entity: 1, body: 1, collider: 1, trace: 1, action: 1, order: 1),
                });

            const string expected = "1:1:2:1:1:1:1:7|1:1:2:1:1:1:2:6|1:1:2:1:1:2:1:5|1:1:2:1:2:1:1:4|1:1:2:2:1:1:1:1|1:3:1:1:1:1:1:2|2:1:1:1:1:1:1:3";
            Assert.AreEqual(expected, QueryOrder(first));
            Assert.AreEqual(expected, QueryOrder(second));
        }

        private static void AssertIssue(
            CombatAuthoringReport report,
            string sourceAsset,
            string section,
            string field,
            string message,
            CombatAuthoringSeverity severity = CombatAuthoringSeverity.Error)
        {
            for (int i = 0; i < report.IssueCount; i++)
            {
                CombatAuthoringIssue issue = report.GetIssue(i);
                if (issue.SourceAsset == sourceAsset
                    && issue.Section == section
                    && issue.Field == field
                    && issue.Message == message)
                {
                    Assert.AreEqual(severity, issue.Severity);
                    Assert.IsFalse(string.IsNullOrEmpty(issue.SuggestedFix));
                    Assert.AreNotEqual(CombatAuthoringQuickActionKind.None, issue.QuickAction);
                    return;
                }
            }

            Assert.Fail("Expected issue was not found: {0}/{1}/{2}/{3}", sourceAsset, section, field, message);
        }

        private static string IssueOrder(CombatAuthoringReport report)
        {
            var keys = new List<string>();
            for (int i = 0; i < report.IssueCount; i++)
            {
                CombatAuthoringIssue issue = report.GetIssue(i);
                keys.Add(issue.Section + ":" + issue.TrackId + ":" + issue.Field);
            }

            return string.Join("|", keys);
        }

        private static string QueryOrder(CombatAuthoringReport report)
        {
            var keys = new List<string>();
            for (int i = 0; i < report.QueryRowCount; i++)
            {
                CombatAuthoringQueryRow row = report.GetQueryRow(i);
                keys.Add(row.Frame + ":" + row.EntityId + ":" + row.BodyId + ":" + row.ColliderId + ":" + row.TraceId + ":" + row.ActionId + ":" + row.SourceOrder + ":" + row.QueryId);
            }

            return string.Join("|", keys);
        }

        private CombatActionAuthoringAsset CreateAction(string name)
        {
            var action = ScriptableObject.CreateInstance<CombatActionAuthoringAsset>();
            action.name = name;
            action.ActionId = 1001;
            action.TotalFrames = 10;
            action.Startup = new CombatAuthoringFrameRange(0, 2);
            action.Active = new CombatAuthoringFrameRange(3, 5);
            action.Recovery = new CombatAuthoringFrameRange(6, 9);
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
            int sourceOrder,
            CombatAuthoringShapeKind shapeKind = CombatAuthoringShapeKind.Sphere,
            int heightRaw = 0)
        {
            return new CombatShapeAuthoringData
            {
                TrackId = trackId,
                ShapeKind = shapeKind,
                FrameRange = new CombatAuthoringFrameRange(start, end),
                MarkerId = marker,
                RadiusRaw = radiusRaw,
                HeightRaw = heightRaw,
                SourceOrder = sourceOrder,
            };
        }

        private static CombatWeaponTraceAuthoringData Trace(
            int traceId,
            int start,
            int end,
            string rootMarker,
            string tipMarker,
            int radiusRaw,
            int sourceOrder)
        {
            return new CombatWeaponTraceAuthoringData
            {
                TraceId = traceId,
                FrameRange = new CombatAuthoringFrameRange(start, end),
                RootMarkerId = rootMarker,
                TipMarkerId = tipMarker,
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

        private static CombatMarkerBindingData Marker(string markerId, int sourceOrder)
        {
            return new CombatMarkerBindingData
            {
                MarkerId = markerId,
                TargetPath = markerId,
                SourceOrder = sourceOrder,
            };
        }

        private static CombatAuthoringQueryRow Query(
            int frame,
            int query,
            int entity,
            int body,
            int collider,
            int trace,
            int action,
            int order)
        {
            return new CombatAuthoringQueryRow(frame, query, entity, body, collider, trace, action, order);
        }
    }
}
