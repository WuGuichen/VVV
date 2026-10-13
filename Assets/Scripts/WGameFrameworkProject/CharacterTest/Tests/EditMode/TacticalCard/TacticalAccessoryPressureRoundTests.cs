using System;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed class TacticalAccessoryPressureRoundTests
    {
        [Test] public void MultipleCausalRoundsConsumeRequestsButDoNotMultiplyFrameRecovery()
        {
            var world = new GameplayComponentWorld(); var recovering = world.CreateEntity(); var struck = world.CreateEntity();
            var store = world.GetOrCreateStore<GameplayScatterPressureComponent>();
            store.Set(recovering, new GameplayScatterPressureComponent {
                MaxScatterPressure = 100, RecoveryRate = 10, RecoveryDelayFrames = 1, CurrentScatterPressure = 80,
                CurrentCohesionLimit = 100, MaxScatterDebt = 100, CurrentBand = PressureBand.Critical, LastPressureFrame = 0 });
            store.Set(struck, new GameplayScatterPressureComponent(100, recoveryRate: 10, recoveryDelayFrames: 1));
            var system = new GameplayScatterPressureSystem();
            var context = new GameplaySystemContext(new RuntimeFrame(2), 0d, 0d, new GameplayWorld(),
                Array.Empty<RuntimeCommand>(), world.Events, componentWorld: world);
            for (int i = 0; i < 3; i++) {
                system.Enqueue(new GameplayScatterPressureRequest(struck, 10, i + 1));
                system.ApplyPendingRequests(context);
                Assert.AreEqual(0, system.PendingRequestCount);
                Assert.IsTrue(store.TryGet(recovering, out var still)); Assert.AreEqual(80, still.CurrentScatterPressure);
                Assert.IsTrue(store.TryGet(struck, out var hit)); Assert.AreEqual((i + 1) * 10, hit.CurrentScatterPressure);
            }
            system.Tick(context);
            Assert.IsTrue(store.TryGet(recovering, out var settled)); Assert.AreEqual(70, settled.CurrentScatterPressure);
            Assert.IsTrue(store.TryGet(struck, out var recent)); Assert.AreEqual(30, recent.CurrentScatterPressure);
        }
    }
}
