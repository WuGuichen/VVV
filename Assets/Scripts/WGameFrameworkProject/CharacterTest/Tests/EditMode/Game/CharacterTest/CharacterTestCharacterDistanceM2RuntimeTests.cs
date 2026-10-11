using System;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestCharacterDistanceM2RuntimeTests
    {
        [Test]
        public void GeometryBindingV1_RemainsLegacyWithoutDamageZones()
        {
            CharacterImportedGeometryBinding binding = CharacterImportedPackageJson.LoadGeometryBinding(
                "{\"format\":\"mx.characterGeometryBinding.v1\",\"schemaVersion\":\"1.0\",\"packageId\":\"legacy\",\"bodyColliders\":[],\"hitZoneBindings\":[],\"sockets\":[],\"weaponAttachments\":[],\"weaponTraces\":[]}");
            Assert.AreEqual("mx.characterGeometryBinding.v1", binding.Format);
            Assert.AreEqual(0, binding.DamageZones.Length);
        }

        [Test]
        public void GeometryBindingV2_ParsesOrthogonalDamageZoneAttributes()
        {
            CharacterImportedGeometryBinding binding = CharacterImportedPackageJson.LoadGeometryBinding(
                "{\"format\":\"mx.characterGeometryBinding.v2\",\"schemaVersion\":\"2.0\",\"packageId\":\"dragon\",\"bodyColliders\":[],\"hitZoneBindings\":[{\"hitZoneId\":\"hit.head\",\"partId\":\"head\"}],\"damageZones\":[{\"zoneId\":\"zone.head\",\"displayName\":\"Head\",\"hitZoneIds\":[\"hit.head\"],\"hittable\":false,\"aimable\":true,\"approachable\":false,\"authoredPriority\":9}],\"sockets\":[],\"weaponAttachments\":[],\"weaponTraces\":[]}");
            Assert.AreEqual(1, binding.DamageZones.Length);
            Assert.IsFalse(binding.DamageZones[0].Hittable);
            Assert.IsTrue(binding.DamageZones[0].Aimable);
            Assert.IsFalse(binding.DamageZones[0].Approachable);
            Assert.AreEqual(9, binding.DamageZones[0].AuthoredPriority);
        }

        [Test]
        public void GeometryBindingV2_RejectsDuplicateUnknownAndUnorderedMembership()
        {
            const string prefix = "{\"format\":\"mx.characterGeometryBinding.v2\",\"schemaVersion\":\"2.0\",\"packageId\":\"bad\",\"bodyColliders\":[],\"hitZoneBindings\":[{\"hitZoneId\":\"hit.a\"},{\"hitZoneId\":\"hit.b\"}],\"damageZones\":";
            const string suffix = ",\"sockets\":[],\"weaponAttachments\":[],\"weaponTraces\":[]}";
            Assert.Throws<CharacterImportedPackageJsonException>(() => CharacterImportedPackageJson.LoadGeometryBinding(
                prefix + "[{\"zoneId\":\"zone.a\",\"hitZoneIds\":[\"hit.unknown\"]}]" + suffix));
            Assert.Throws<CharacterImportedPackageJsonException>(() => CharacterImportedPackageJson.LoadGeometryBinding(
                prefix + "[{\"zoneId\":\"zone.b\",\"hitZoneIds\":[\"hit.a\"]},{\"zoneId\":\"zone.a\",\"hitZoneIds\":[\"hit.b\"]}]" + suffix));
            Assert.Throws<CharacterImportedPackageJsonException>(() => CharacterImportedPackageJson.LoadGeometryBinding(
                prefix + "[{\"zoneId\":\"zone.a\",\"hitZoneIds\":[\"hit.a\"]},{\"zoneId\":\"zone.b\",\"hitZoneIds\":[\"hit.a\"]}]" + suffix));
        }

        [Test]
        public void DamageZoneSnapshot_AggregatesStableMembersBoundsAndAttributes()
        {
            CharacterTestDamageGeometrySnapshot geometry = CreateGeometry(reverse: true);
            CharacterDamageZoneRuntimeBinding[] bindings = CreateBindings();
            var snapshot = new CharacterTestDamageZoneSnapshot();
            snapshot.Build(geometry, bindings);

            Assert.IsTrue(snapshot.IsValid);
            Assert.AreEqual(2, snapshot.ZoneCount);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D400_Id151, snapshot.GetZone(0).ZoneId);
            Assert.AreEqual("zone.core", bindings[0].ZoneLabel);
            Assert.AreEqual(2, snapshot.GetZone(0).MemberCount);
            Assert.AreEqual("a_head", snapshot.GetMember(snapshot.GetZone(0).MemberStart).LogicalHurtboxId);
            Assert.AreEqual(new FixVector3(Fix64.FromInt(-1), Fix64.Zero, Fix64.FromInt(-1)), snapshot.GetZone(0).WorldMin);
            Assert.AreEqual(new FixVector3(Fix64.FromInt(3), Fix64.FromInt(4), Fix64.FromInt(2)), snapshot.GetZone(0).WorldMax);
            Assert.IsTrue(snapshot.GetZone(0).Hittable);
            Assert.IsFalse(snapshot.GetZone(0).Aimable);
            Assert.IsTrue(snapshot.GetZone(0).Approachable);
        }

        [Test]
        public void DamageZoneSnapshot_InputOrderDoesNotChangeIdentityOrBounds()
        {
            var first = new CharacterTestDamageZoneSnapshot();
            var second = new CharacterTestDamageZoneSnapshot();
            first.Build(CreateGeometry(reverse: false), CreateBindings());
            second.Build(CreateGeometry(reverse: true), CreateBindings());
            Assert.AreEqual(first.ZoneRevision, second.ZoneRevision);
            Assert.AreEqual(first.GetZone(0).WorldMin, second.GetZone(0).WorldMin);
            Assert.AreEqual(first.GetZone(0).WorldMax, second.GetZone(0).WorldMax);
            Assert.AreEqual(first.GetMember(0).LogicalHurtboxId, second.GetMember(0).LogicalHurtboxId);
        }

        [Test]
        public void DamageZoneSnapshot_WarmedBuildAndQueryAllocateZeroBytes()
        {
            CharacterTestDamageGeometrySnapshot geometry = CreateGeometry(reverse: false);
            CharacterDamageZoneRuntimeBinding[] bindings = CreateBindings();
            var snapshot = new CharacterTestDamageZoneSnapshot();
            snapshot.Build(geometry, bindings);
            _ = snapshot.TryGetZone(CharacterTestAuthoredContentIds.D400_Id151, out _);
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 32; i++)
            {
                snapshot.Build(geometry, bindings);
                Assert.IsTrue(snapshot.TryGetZone(CharacterTestAuthoredContentIds.D400_Id151, out _));
                _ = snapshot.GetMember(0);
            }
            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        private static CharacterTestDamageGeometrySnapshot CreateGeometry(bool reverse)
        {
            var geometry = new CharacterTestDamageGeometrySnapshot(3);
            geometry.BeginWrite(2, new GameplayEntityId(2, 1), new CombatEntityId(50002));
            if (reverse)
            {
                Add(geometry, "z_torso", 2, "hit.torso", 1, 1, 0, 3, 4, 2);
                Add(geometry, "a_head", 1, "hit.head", -1, 0, -1, 1, 2, 1);
            }
            else
            {
                Add(geometry, "a_head", 1, "hit.head", -1, 0, -1, 1, 2, 1);
                Add(geometry, "z_torso", 2, "hit.torso", 1, 1, 0, 3, 4, 2);
            }
            Add(geometry, "tail", 3, "hit.tail", -2, -1, -3, -1, 1, -1);
            geometry.SortCollidersByStableLogicalId();
            geometry.Commit(new RuntimeFrame(7), 11, 13);
            return geometry;
        }

        private static void Add(
            CharacterTestDamageGeometrySnapshot geometry,
            string id,
            int colliderId,
            string hitZoneLabel,
            int minX,
            int minY,
            int minZ,
            int maxX,
            int maxY,
            int maxZ)
        {
            geometry.AddCollider(
                ResolveLogicalColliderId(id),
                id,
                new CombatColliderId(colliderId),
                CharacterTestAuthoredContentIds.D400_Id34,
                "body",
                ResolveHitZoneId(hitZoneLabel),
                hitZoneLabel,
                0,
                CombatPhysicsShape.Aabb(FixVector3.Zero, FixVector3.Zero),
                new FixVector3(Fix64.FromInt(minX), Fix64.FromInt(minY), Fix64.FromInt(minZ)),
                new FixVector3(Fix64.FromInt(maxX), Fix64.FromInt(maxY), Fix64.FromInt(maxZ)));
        }

        private static int ResolveLogicalColliderId(string label)
        {
            switch (label)
            {
                case "a_head": return CharacterTestAuthoredContentIds.D400_Id71;
                case "z_torso": return CharacterTestAuthoredContentIds.D400_Id70;
                default: return CharacterTestAuthoredContentIds.D400_Id103;
            }
        }

        private static int ResolveHitZoneId(string label)
        {
            switch (label)
            {
                case "hit.head": return CharacterTestAuthoredContentIds.D400_Id56;
                case "hit.torso": return CharacterTestAuthoredContentIds.D400_Id55;
                default: return CharacterTestAuthoredContentIds.D400_Id100;
            }
        }

        private static CharacterDamageZoneRuntimeBinding[] CreateBindings()
        {
            return new[]
            {
                new CharacterDamageZoneRuntimeBinding(CharacterTestAuthoredContentIds.D400_Id151, "Core", new[] { CharacterTestAuthoredContentIds.D400_Id56, CharacterTestAuthoredContentIds.D400_Id55 }, true, false, true, 5, "zone.core"),
                new CharacterDamageZoneRuntimeBinding(CharacterTestAuthoredContentIds.D400_Id154, "Tail", new[] { CharacterTestAuthoredContentIds.D400_Id100 }, false, true, false, 1, "zone.tail")
            };
        }
    }
}
