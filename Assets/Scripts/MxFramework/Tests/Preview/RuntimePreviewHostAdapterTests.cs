using System.Collections.Generic;
using System.IO;
using MxFramework.Buffs;
using MxFramework.Preview;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Preview
{
    public sealed class RuntimePreviewHostAdapterTests
    {
        private const string BuffId = "910001";
        private const string CasterId = "CasterA";
        private const string TargetId = "TargetA";

        [Test]
        public void RuntimePreviewHostAdapter_InitializeStartStop_DrivesHostLifecycle()
        {
            var factory = new FixtureBuffFactory();
            var adapter = new RuntimePreviewHostAdapter(new DummyPreviewWorld(factory));
            try
            {
                Assert.AreEqual(RuntimeLifecycleState.Created, adapter.HostState);

                adapter.Initialize();
                Assert.AreEqual(RuntimeLifecycleState.Initialized, adapter.HostState);

                adapter.Start();
                Assert.AreEqual(RuntimeLifecycleState.Started, adapter.HostState);

                adapter.Stop();
                Assert.AreEqual(RuntimeLifecycleState.Stopped, adapter.HostState);
            }
            finally
            {
                adapter.Dispose();
            }
        }

        [Test]
        public void RuntimePreviewHostAdapter_ApplyBuff_UsesRuntimeHostCommandTickAndReplay()
        {
            var factory = new FixtureBuffFactory();
            factory.SetDefinition(910001, damagePerTick: 25);
            var adapter = CreateStartedAdapter(factory);
            try
            {
                RuntimePreviewAdapterResult result = adapter.ApplyBuff(new RuntimePreviewApplyBuffRequest
                {
                    BuffId = BuffId,
                    CasterId = CasterId,
                    TargetId = TargetId,
                    Stack = 2,
                    WaitTicks = 60,
                });

                Assert.IsTrue(result.Success, result.ErrorMessage);
                Assert.AreEqual(RuntimeLifecycleState.Started, adapter.HostState);
                Assert.AreEqual(61, adapter.HostTickCount);
                Assert.AreEqual(new RuntimeFrame(61), adapter.CurrentFrame);
                Assert.AreEqual(0, adapter.PendingCommandCount);
                Assert.AreEqual(61, adapter.ReplayFrameCount);
                Assert.AreEqual(61, adapter.CreateReplaySnapshot().Count);

                Assert.AreEqual(1, result.Snapshot.BuffSnapshots.Count);
                Assert.AreEqual(BuffId, result.Snapshot.BuffSnapshots[0].BuffId);
                Assert.AreEqual(TargetId, result.Snapshot.BuffSnapshots[0].OwnerId);
                Assert.AreEqual(CasterId, result.Snapshot.BuffSnapshots[0].CasterId);
                Assert.AreEqual(2, result.Snapshot.BuffSnapshots[0].Stack);
                Assert.AreEqual(1, result.Snapshot.AttributeChanges.Count);
                Assert.AreEqual(TargetId, result.Snapshot.AttributeChanges[0].OwnerId);
                Assert.AreEqual("Hp", result.Snapshot.AttributeChanges[0].Attribute);
                Assert.AreEqual(1000, result.Snapshot.AttributeChanges[0].Before);
                Assert.AreEqual(950, result.Snapshot.AttributeChanges[0].After);
            }
            finally
            {
                adapter.Dispose();
            }
        }

        [Test]
        public void RuntimePreviewHostAdapter_Tick_AdvancesThroughRuntimeHost()
        {
            var factory = new FixtureBuffFactory();
            factory.SetDefinition(910001, damagePerTick: 10);
            var adapter = CreateStartedAdapter(factory);
            try
            {
                RuntimePreviewAdapterResult apply = adapter.ApplyBuff(new RuntimePreviewApplyBuffRequest
                {
                    BuffId = BuffId,
                    CasterId = CasterId,
                    TargetId = TargetId,
                    Stack = 1,
                });
                Assert.IsTrue(apply.Success, apply.ErrorMessage);

                RuntimePreviewAdapterResult tick = adapter.Tick(60, TargetId);

                Assert.IsTrue(tick.Success, tick.ErrorMessage);
                Assert.AreEqual(61, adapter.HostTickCount);
                Assert.AreEqual(61, adapter.ReplayFrameCount);
                Assert.AreEqual(1, tick.Snapshot.AttributeChanges.Count);
                Assert.AreEqual(990, tick.Snapshot.AttributeChanges[0].After);
            }
            finally
            {
                adapter.Dispose();
            }
        }

        [Test]
        public void RuntimePreviewHostAdapter_Reset_ClearsClockCommandsRecorderAndWorldState()
        {
            var factory = new FixtureBuffFactory();
            factory.SetDefinition(910001, damagePerTick: 10);
            var adapter = CreateStartedAdapter(factory);
            try
            {
                RuntimePreviewAdapterResult apply = adapter.ApplyBuff(new RuntimePreviewApplyBuffRequest
                {
                    BuffId = BuffId,
                    CasterId = CasterId,
                    TargetId = TargetId,
                    Stack = 1,
                    WaitTicks = 1,
                });
                Assert.IsTrue(apply.Success, apply.ErrorMessage);
                Assert.Greater(adapter.HostTickCount, 0);
                Assert.Greater(adapter.ReplayFrameCount, 0);

                RuntimePreviewAdapterResult reset = adapter.Reset(reloadBase: false);

                Assert.IsTrue(reset.Success, reset.ErrorMessage);
                Assert.AreEqual(RuntimeLifecycleState.Started, adapter.HostState);
                Assert.AreEqual(0, adapter.HostTickCount);
                Assert.AreEqual(RuntimeFrame.Zero, adapter.CurrentFrame);
                Assert.AreEqual(0, adapter.PendingCommandCount);
                Assert.AreEqual(0, adapter.ReplayFrameCount);
                Assert.AreEqual(0, adapter.CreateReplaySnapshot().Count);
                Assert.AreEqual(0, adapter.Snapshot(TargetId).BuffSnapshots.Count);
                Assert.AreEqual(0, adapter.Snapshot(TargetId).AttributeChanges.Count);
            }
            finally
            {
                adapter.Dispose();
            }
        }

        [Test]
        public void RuntimePreviewHostAdapter_HostModuleException_ReturnsStructuredPreviewFailure()
        {
            var factory = new FixtureBuffFactory();
            factory.SetDefinition(910001, damagePerTick: 10);
            var modules = new RuntimePreviewHostModuleFactory[]
            {
                () => new ThrowingRuntimeModule()
            };
            var adapter = CreateStartedAdapter(factory, modules);
            try
            {
                RuntimePreviewAdapterResult result = adapter.ApplyBuff(new RuntimePreviewApplyBuffRequest
                {
                    BuffId = BuffId,
                    CasterId = CasterId,
                    TargetId = TargetId,
                    Stack = 1,
                });

                Assert.IsFalse(result.Success);
                Assert.AreEqual(PreviewError.ApplyBuffFailed, result.ErrorCode);
                Assert.AreEqual("host_exception", result.ErrorReason);
                StringAssert.Contains("fixture.host.throw", result.ErrorMessage);
                StringAssert.Contains("fixture module failed", result.ErrorMessage);
                Assert.AreEqual(1, adapter.HostErrors.Count);
                Assert.AreEqual(1, adapter.ReplayFrameCount);
            }
            finally
            {
                adapter.Dispose();
            }
        }

        [Test]
        public void RuntimePreviewHostAdapter_AsmdefReferencesRuntimeWithoutRuntimeReferencingPreview()
        {
            string previewAsmdef = ReadProjectFile("Assets/Scripts/MxFramework/Preview/Runtime/MxFramework.Preview.Runtime.asmdef");
            string runtimeAsmdef = ReadProjectFile("Assets/Scripts/MxFramework/Runtime/MxFramework.Runtime.asmdef");

            StringAssert.Contains("\"MxFramework.Runtime\"", previewAsmdef);
            Assert.IsFalse(runtimeAsmdef.Contains("MxFramework.Preview.Runtime"), "MxFramework.Runtime must not reference Preview.Runtime.");
        }

        private static RuntimePreviewHostAdapter CreateStartedAdapter(
            FixtureBuffFactory factory,
            IEnumerable<RuntimePreviewHostModuleFactory> modules = null)
        {
            var adapter = new RuntimePreviewHostAdapter(new DummyPreviewWorld(factory), moduleFactories: modules);
            adapter.Initialize();
            adapter.Start();
            return adapter;
        }

        private static string ReadProjectFile(string projectRelativePath)
        {
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), projectRelativePath);
            return File.ReadAllText(fullPath);
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

        private sealed class ThrowingRuntimeModule : RuntimeModule
        {
            public ThrowingRuntimeModule()
                : base("fixture.host.throw", RuntimeTickStage.Simulation, -1000)
            {
            }

            public override void Tick(RuntimeTickContext context)
            {
                throw new System.InvalidOperationException("fixture module failed");
            }
        }
    }
}
