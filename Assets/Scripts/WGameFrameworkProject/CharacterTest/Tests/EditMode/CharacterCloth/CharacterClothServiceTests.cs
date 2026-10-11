using MxFramework.CharacterCloth;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterCloth
{
    public sealed class CharacterClothServiceTests
    {
        [Test]
        public void PausedOverridePreservesLocallyDisabledControllers()
        {
            var controller = new FakeController(CharacterClothState.DisabledKeep);
            var service = new CharacterClothService();

            Assert.That(service.Register(controller), Is.True);
            service.SetSystemOverride(CharacterClothSystemOverride.Paused);

            Assert.That(controller.RequestedState, Is.EqualTo(CharacterClothState.DisabledKeep));
            Assert.That(controller.EffectiveState, Is.EqualTo(CharacterClothState.DisabledKeep));
        }

        [Test]
        public void ClearingOverrideRestoresEachLocalRequest()
        {
            var running = new FakeController(CharacterClothState.Running);
            var disabled = new FakeController(CharacterClothState.DisabledReset);
            var service = new CharacterClothService();
            service.Register(running);
            service.Register(disabled);

            service.SetSystemOverride(CharacterClothSystemOverride.DisabledKeep);
            Assert.That(running.EffectiveState, Is.EqualTo(CharacterClothState.DisabledKeep));
            Assert.That(disabled.EffectiveState, Is.EqualTo(CharacterClothState.DisabledKeep));

            service.SetSystemOverride(CharacterClothSystemOverride.None);
            Assert.That(running.EffectiveState, Is.EqualTo(CharacterClothState.Running));
            Assert.That(disabled.EffectiveState, Is.EqualTo(CharacterClothState.DisabledReset));
        }

        [Test]
        public void ResetAllForwardsKeepPoseWithoutChangingState()
        {
            var controller = new FakeController(CharacterClothState.Paused);
            var service = new CharacterClothService();
            service.Register(controller);

            service.ResetAll(keepPose: true);

            Assert.That(controller.ResetCount, Is.EqualTo(1));
            Assert.That(controller.LastResetKeptPose, Is.True);
            Assert.That(controller.RequestedState, Is.EqualTo(CharacterClothState.Paused));
        }

        [Test]
        public void PresentationPauseAppliesToLateSpawnsAndIgnoresIndependentPreview()
        {
            var service = new CharacterClothPresentationService();
            var actor = new FakeGroup();
            var preview = new FakeGroup();
            service.SetPaused(true);
            Assert.That(service.Register(actor), Is.True);
            Assert.That(service.Register(actor), Is.False);
            Assert.That(actor.Override, Is.EqualTo(CharacterClothSystemOverride.Paused));
            Assert.That(preview.Override, Is.EqualTo(CharacterClothSystemOverride.None));
            service.SetPaused(false);
            Assert.That(actor.Override, Is.EqualTo(CharacterClothSystemOverride.None));
        }

        [Test]
        public void PresentationEnableRestoresPauseAndRepeatedCommandsAreIdempotent()
        {
            var service = new CharacterClothPresentationService();
            var actor = new FakeGroup();
            service.Register(actor);
            service.SetPaused(true);
            service.SetEnabled(false, true);
            service.SetPaused(false);
            Assert.That(actor.Override, Is.EqualTo(CharacterClothSystemOverride.DisabledKeep));
            service.SetPaused(true);
            service.SetEnabled(true);
            Assert.That(actor.Override, Is.EqualTo(CharacterClothSystemOverride.Paused));
            int calls = actor.StateCalls;
            service.SetPaused(true);
            service.SetEnabled(true);
            Assert.That(actor.StateCalls, Is.EqualTo(calls));
        }

        [Test]
        public void PresentationDestructionRemovesGroupWithoutResumingIt()
        {
            var service = new CharacterClothPresentationService();
            var actor = new FakeGroup();
            service.Register(actor);
            service.SetPaused(true);
            actor.Destroy();
            actor.Destroy();
            Assert.That(service.GroupCount, Is.EqualTo(0));
            service.SetPaused(false);
            service.ResetAll();
            Assert.That(actor.Override, Is.EqualTo(CharacterClothSystemOverride.Paused));
            Assert.That(actor.ResetCount, Is.EqualTo(0));
        }

        [Test]
        public void PresentationResetAndUnregisterPreserveExplicitReleasePolicy()
        {
            var service = new CharacterClothPresentationService();
            var actor = new FakeGroup();
            service.Register(actor);
            service.SetEnabled(false);
            service.ResetAll(true);
            Assert.That(actor.ResetCount, Is.EqualTo(1));
            Assert.That(actor.LastKeepPose, Is.True);
            Assert.That(service.Unregister(actor, false), Is.True);
            Assert.That(service.Unregister(actor, false), Is.False);
            Assert.That(actor.Override, Is.EqualTo(CharacterClothSystemOverride.DisabledReset));
            service.Register(actor);
            service.Clear();
            Assert.That(actor.Override, Is.EqualTo(CharacterClothSystemOverride.None));
            Assert.That(service.GroupCount, Is.EqualTo(0));
        }

        private sealed class FakeGroup : ICharacterClothGroup
        {
            public event System.Action<ICharacterClothGroup> Destroyed;
            public CharacterClothSystemOverride Override;
            public int StateCalls;
            public int ResetCount;
            public bool LastKeepPose;
            public void SetPresentationOverride(CharacterClothSystemOverride value)
            {
                Override = value;
                StateCalls++;
            }
            public void ResetAll(bool keepPose = false) { ResetCount++; LastKeepPose = keepPose; }
            public void Destroy() => Destroyed?.Invoke(this);
        }

        private sealed class FakeController : ICharacterClothController
        {
            public FakeController(CharacterClothState requestedState)
            {
                RequestedState = requestedState;
            }

            public CharacterClothState RequestedState { get; private set; }
            public CharacterClothState EffectiveState =>
                CharacterClothStateResolver.Resolve(RequestedState, SystemOverride);
            public CharacterClothSystemOverride SystemOverride { get; private set; }
            public int ResetCount { get; private set; }
            public bool LastResetKeptPose { get; private set; }

            public void SetRequestedState(CharacterClothState state)
            {
                RequestedState = state;
            }

            public void SetSystemOverride(CharacterClothSystemOverride systemOverride)
            {
                SystemOverride = systemOverride;
            }

            public void Reset(bool keepPose = false)
            {
                ResetCount++;
                LastResetKeptPose = keepPose;
            }
        }
    }
}
