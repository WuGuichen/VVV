using System.Collections.Generic;
using MxFramework.Buffs;
using MxFramework.Preview;
using NUnit.Framework;
using PreviewBuffSnapshot = MxFramework.Preview.BuffSnapshot;

namespace MxFramework.Tests.Preview
{
    public sealed class RuntimePreviewAdapterFixtureTests
    {
        private const string BuffId = "910001";
        private const string CasterId = "CasterA";
        private const string TargetId = "TargetA";

        [Test]
        public void RuntimePreviewAdapterFixture_ApplyTickSnapshotReset_CoversSuccessfulLoop()
        {
            var factory = new FixtureBuffFactory();
            factory.SetDefinition(910001, damagePerTick: 25);
            var world = new DummyPreviewWorld(factory);

            bool applied = world.ApplyBuff(BuffId, CasterId, TargetId, stack: 2, durationOverrideMs: null);
            world.Tick(60);

            IReadOnlyList<PreviewBuffSnapshot> buffs = world.SnapshotBuffs(TargetId);
            IReadOnlyList<AttributeChange> changes = world.SnapshotAttributeChanges(TargetId);

            Assert.IsTrue(applied);
            Assert.AreEqual(1, buffs.Count);
            Assert.AreEqual(BuffId, buffs[0].BuffId);
            Assert.AreEqual(TargetId, buffs[0].OwnerId);
            Assert.AreEqual(CasterId, buffs[0].CasterId);
            Assert.AreEqual(2, buffs[0].Stack);
            Assert.Greater(buffs[0].RemainingMs, 0);
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(TargetId, changes[0].OwnerId);
            Assert.AreEqual("Hp", changes[0].Attribute);
            Assert.AreEqual(1000, changes[0].Before);
            Assert.AreEqual(950, changes[0].After);

            world.Reset(reloadBase: false);

            Assert.AreEqual(0, world.SnapshotBuffs(TargetId).Count);
            Assert.AreEqual(0, world.SnapshotAttributeChanges(TargetId).Count);
        }

        [Test]
        public void RuntimePreviewAdapterFixture_ConsecutivePreviews_ResetIsolation_DoesNotLeakState()
        {
            var factory = new FixtureBuffFactory();
            factory.SetDefinition(910001, damagePerTick: 10);
            var world = new DummyPreviewWorld(factory);

            Assert.IsTrue(world.ApplyBuff(BuffId, CasterId, TargetId, stack: 1, durationOverrideMs: null));
            world.Tick(60);
            Assert.AreEqual(1, world.SnapshotAttributeChanges(TargetId).Count);

            world.Reset(reloadBase: false);

            Assert.AreEqual(0, world.SnapshotBuffs(TargetId).Count);
            Assert.AreEqual(0, world.SnapshotAttributeChanges(TargetId).Count);

            Assert.IsTrue(world.ApplyBuff(BuffId, CasterId, TargetId, stack: 1, durationOverrideMs: null));

            IReadOnlyList<PreviewBuffSnapshot> secondBuffs = world.SnapshotBuffs(TargetId);
            IReadOnlyList<AttributeChange> secondChangesBeforeTick = world.SnapshotAttributeChanges(TargetId);

            Assert.AreEqual(1, secondBuffs.Count);
            Assert.AreEqual(BuffId, secondBuffs[0].BuffId);
            Assert.AreEqual(0, secondChangesBeforeTick.Count);
        }

        [Test]
        public void RuntimePreviewAdapterFixture_InvalidBuffInput_FailsWithoutMutatingSnapshot()
        {
            var factory = new FixtureBuffFactory();
            factory.SetDefinition(910001, damagePerTick: 10);
            var world = new DummyPreviewWorld(factory);

            bool invalidText = world.ApplyBuff("not-an-int", CasterId, TargetId, stack: 1, durationOverrideMs: null);
            bool unknownId = world.ApplyBuff("999999", CasterId, TargetId, stack: 1, durationOverrideMs: null);

            Assert.IsFalse(invalidText);
            Assert.IsFalse(unknownId);
            Assert.AreEqual(0, world.SnapshotBuffs(TargetId).Count);
            Assert.AreEqual(0, world.SnapshotAttributeChanges(TargetId).Count);
        }

        [Test]
        public void RuntimePreviewAdapterFixture_ConfigChange_AffectsNewInstancesOnly()
        {
            var factory = new FixtureBuffFactory();
            factory.SetDefinition(910001, damagePerTick: 10);
            var world = new DummyPreviewWorld(factory);

            Assert.IsTrue(world.ApplyBuff(BuffId, CasterId, "OldTarget", stack: 1, durationOverrideMs: null));

            factory.SetDefinition(910001, damagePerTick: 40);
            world.Tick(60);

            IReadOnlyList<AttributeChange> oldChanges = world.SnapshotAttributeChanges("OldTarget");
            Assert.AreEqual(1, oldChanges.Count);
            Assert.AreEqual(990, oldChanges[0].After);

            Assert.IsTrue(world.ApplyBuff(BuffId, CasterId, "NewTarget", stack: 1, durationOverrideMs: null));
            world.Tick(60);

            IReadOnlyList<AttributeChange> newChanges = world.SnapshotAttributeChanges("NewTarget");
            Assert.AreEqual(1, newChanges.Count);
            Assert.AreEqual(960, newChanges[0].After);
        }

        [Test]
        public void RuntimePreviewAdapterFixture_ErrorResultContract_UsesStableApplyBuffCode()
        {
            var result = new RuntimePreviewResult
            {
                RequestId = "contract-invalid-buff",
                Success = false,
                AppliedBuffId = "999999",
            };
            result.Errors.Add(new RuntimePreviewError
            {
                Code = PreviewError.ApplyBuffFailed,
                Message = "Buff factory rejected id 999999",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual(2003, result.Errors[0].Code);
            StringAssert.Contains("999999", result.Errors[0].Message);
        }

        private sealed class FixtureBuffFactory : IBuffFactory
        {
            private readonly Dictionary<int, int> _damageByBuffId = new Dictionary<int, int>();

            public void SetDefinition(int buffId, int damagePerTick)
            {
                _damageByBuffId[buffId] = damagePerTick;
            }

            public bool TryCreate(int buffId, out IBuff buff)
            {
                if (!_damageByBuffId.TryGetValue(buffId, out int damagePerTick))
                {
                    buff = null;
                    return false;
                }

                buff = new FixtureDamageBuff(buffId, damagePerTick);
                return true;
            }
        }

        private sealed class FixtureDamageBuff : BuffBase
        {
            private const float TickIntervalSeconds = 1f;
            private readonly int _damagePerTick;
            private float _elapsedSeconds;

            public FixtureDamageBuff(int id, int damagePerTick)
                : base(id, duration: 5f, maxLayers: 8)
            {
                _damagePerTick = damagePerTick;
            }

            public override void OnTick(float deltaTime, IBuffTarget target)
            {
                if (target == null || deltaTime <= 0f)
                    return;

                _elapsedSeconds += deltaTime;
                while (_elapsedSeconds + 0.0001f >= TickIntervalSeconds && !IsExpired)
                {
                    _elapsedSeconds -= TickIntervalSeconds;
                    target.Attributes.AddAttribute(
                        DummyPreviewWorld.AttrHp,
                        -_damagePerTick * CurrentLayers,
                        this);
                }

                base.OnTick(deltaTime, target);
            }
        }
    }
}
