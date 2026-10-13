using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Rendering
{
    public sealed class ShallowWaterSplashPolicyTests
    {
        private static CharacterTestShallowWaterSplashPolicy.Budget DefaultBudget =>
            new CharacterTestShallowWaterSplashPolicy.Budget(12, 16, .18f, .25f, 1.55f);
        private static CharacterTestShallowWaterSplashPolicy Ready(int id = 1, long frame = 2, double clock = .2)
        {
            var policy = new CharacterTestShallowWaterSplashPolicy();
            policy.BeginMotion(1, 0); policy.ObserveSubject(id, 1, 0, 0, 0, 0, true);
            policy.BeginMotion(frame, clock); policy.ObserveSubject(id, frame, clock, 0, 0, 0, true);
            return policy;
        }
        private static CharacterTestShallowWaterSplashPolicy.Contact Step(int id = 1, int point = 1, long frame = 2)
        {
            return new CharacterTestShallowWaterSplashPolicy.Contact { EntityId = id, ContactPointId = point,
                BodyPartId = 51 + point, Frame = frame, IsFoot = true, IsSubmerged = true,
                IsVisible = true, HorizontalSpeed = 1.6f, RootX = 0, RootY = 0, RootZ = 0 };
        }
        private static bool Admit(CharacterTestShallowWaterSplashPolicy policy,
            CharacterTestShallowWaterSplashPolicy.Contact contact, double clock = .2, int active = 0)
        { return policy.TryAdmit(contact, clock, active, DefaultBudget, out _, out _); }

        [Test] public void OnlyActualSubmergedRectangleContactsQualify()
        {
            Assert.IsTrue(CharacterTestShallowWaterSplashPolicy.IsSubmerged(0, -.04f, 0, 72, 50, .006f, .15f));
            Assert.IsFalse(CharacterTestShallowWaterSplashPolicy.IsSubmerged(0, 0, 0, 72, 50, .006f, .15f));
            Assert.IsFalse(CharacterTestShallowWaterSplashPolicy.IsSubmerged(0, .03f, 0, 72, 50, .006f, .15f), "Platform above water is dry.");
            Assert.IsFalse(CharacterTestShallowWaterSplashPolicy.IsSubmerged(0, -.005f, 0, 72, 50, .006f, .15f));
            Assert.IsFalse(CharacterTestShallowWaterSplashPolicy.IsSubmerged(0, -.151f, 0, 72, 50, .006f, .15f));
            Assert.IsFalse(CharacterTestShallowWaterSplashPolicy.IsSubmerged(36.01f, -.04f, 0, 72, 50, .006f, .15f));
            Assert.IsFalse(CharacterTestShallowWaterSplashPolicy.IsSubmerged(0, -.04f, 25.01f, 72, 50, .006f, .15f));
            Assert.IsFalse(CharacterTestShallowWaterSplashPolicy.IsSubmerged(float.NaN, -.04f, 0, 72, 50, .006f, .15f));
        }
        [Test] public void InitialSpawnContactDoesNotSplash()
        {
            var policy = new CharacterTestShallowWaterSplashPolicy();
            policy.BeginMotion(1, 0); policy.ObserveSubject(1, 1, 0, 0, 0, 0, true);
            Assert.IsFalse(Admit(policy, Step(frame: 1), 0));
            Assert.AreEqual(CharacterTestSplashRejection.NoMotionBaseline, policy.LastRejection);
        }
        [Test] public void UnknownBodyPartIsRejectedWithoutConsumingFootEmission()
        {
            var policy = Ready(); var contact = Step(); contact.IsFoot = false;
            Assert.IsFalse(Admit(policy, contact));
            Assert.AreEqual(CharacterTestSplashRejection.NotFoot, policy.LastRejection);
            Assert.IsTrue(Admit(policy, Step()));
        }
        [Test] public void StandingAndLowSpeedContactsDoNotEmit()
        {
            var policy = Ready(); var contact = Step(); contact.HorizontalSpeed = .1f; contact.NormalSpeed = .2f;
            Assert.IsFalse(Admit(policy, contact));
            Assert.AreEqual(CharacterTestSplashRejection.Stationary, policy.LastRejection);
            contact.HorizontalSpeed = .25f; Assert.IsTrue(Admit(policy, contact));
        }
        [Test] public void DryContactAndFarCameraDoNotConsumeEmission()
        {
            var policy = Ready(); var contact = Step(); contact.IsSubmerged = false;
            Assert.IsFalse(Admit(policy, contact)); contact.IsSubmerged = true; contact.IsVisible = false;
            Assert.IsFalse(Admit(policy, contact)); Assert.AreEqual(CharacterTestSplashRejection.OutOfView, policy.LastRejection);
            contact.IsVisible = true; Assert.IsTrue(Admit(policy, contact));
        }
        [Test] public void RepeatedEventAndRepeatedMotionInSameFrameDoNotEmitAgain()
        {
            var policy = Ready(); Assert.IsTrue(Admit(policy, Step()));
            Assert.IsFalse(policy.BeginMotion(2, .2)); policy.ObserveSubject(1, 2, .2, 0, 0, 0, true);
            var repeated = Step(); repeated.Sequence = 8;
            Assert.IsFalse(Admit(policy, repeated)); Assert.AreEqual(CharacterTestSplashRejection.Duplicate, policy.LastRejection);
            Assert.AreEqual(1, policy.Accepted);
        }
        [Test] public void FootCooldownAllowsLaterStep()
        {
            var policy = Ready(); Assert.IsTrue(Admit(policy, Step()));
            policy.BeginMotion(3, .29); policy.ObserveSubject(1, 3, .29, 0, 0, 0, true);
            Assert.IsFalse(Admit(policy, Step(frame: 3), .29)); Assert.AreEqual(CharacterTestSplashRejection.Cooldown, policy.LastRejection);
            policy.BeginMotion(4, .4); policy.ObserveSubject(1, 4, .4, 0, 0, 0, true);
            Assert.IsTrue(Admit(policy, Step(frame: 4), .4));
        }
        [Test] public void GroundedLandingUsesOneStrongerContactBurst()
        {
            var policy = Ready(); policy.ObserveLanding(1, 2, 3);
            var contact = Step(); contact.HorizontalSpeed = 0;
            Assert.IsTrue(policy.TryAdmit(contact, .2, 0, DefaultBudget, out bool landing, out float strength));
            Assert.IsTrue(landing); Assert.That(strength, Is.GreaterThan(1.35f));
            contact.ContactPointId = 2; contact.BodyPartId = 53;
            Assert.IsFalse(Admit(policy, contact)); Assert.AreEqual(CharacterTestSplashRejection.Duplicate, policy.LastRejection);
        }
        [Test] public void LandingFactWithoutPhysicalContactNeverAdmits()
        {
            var policy = Ready(); policy.ObserveLanding(1, 2, 3);
            Assert.AreEqual(0, policy.Accepted);
            var contact = Step(); contact.IsSubmerged = false; Assert.IsFalse(Admit(policy, contact));
            Assert.AreEqual(0, policy.Accepted);
        }
        [Test] public void AirborneFootAndStaleFrameAreRejected()
        {
            var policy = Ready(); policy.BeginMotion(3, .3); policy.ObserveSubject(1, 3, .3, 0, 0, 0, false);
            Assert.IsFalse(Admit(policy, Step(frame: 3), .3));
            Assert.IsFalse(Admit(policy, Step(), .3));
        }
        [Test] public void TeleportSuppressesImmediateContactAndResumesAfterStableMotion()
        {
            var policy = Ready(); policy.BeginMotion(3, .3); policy.ObserveSubject(1, 3, .3, 4, 0, 0, true);
            var contact = Step(frame: 3); contact.RootX = 4; Assert.IsFalse(Admit(policy, contact, .3));
            policy.BeginMotion(4, .4); policy.ObserveSubject(1, 4, .4, 4, 0, 0, true);
            contact.Frame = 4; Assert.IsFalse(Admit(policy, contact, .4));
            policy.BeginMotion(5, .6); policy.ObserveSubject(1, 5, .6, 4, 0, 0, true);
            contact.Frame = 5; Assert.IsTrue(Admit(policy, contact, .6));
        }
        [Test] public void RootChangedAfterMotionObservationIsRejected()
        {
            var policy = Ready(); var contact = Step(); contact.RootX = 1;
            Assert.IsFalse(Admit(policy, contact)); Assert.AreEqual(CharacterTestSplashRejection.Teleport, policy.LastRejection);
        }
        [Test] public void RuntimeRewindClearsAdmissionBaseline()
        {
            var policy = Ready(frame: 10); Assert.IsTrue(Admit(policy, Step(frame: 10)));
            Assert.IsTrue(policy.BeginMotion(2, .1)); policy.ObserveSubject(1, 2, .1, 0, 0, 0, true);
            Assert.IsFalse(Admit(policy, Step(), .1));
        }
        [Test] public void FullPoolDropsContactWithoutQueueingOrConsumingIt()
        {
            var policy = Ready(); Assert.IsFalse(Admit(policy, Step(), active: 12));
            Assert.AreEqual(CharacterTestSplashRejection.PoolFull, policy.LastRejection);
            Assert.IsTrue(Admit(policy, Step(), active: 0));
        }
        [Test] public void PerFrameBurstCapIsSharedAcrossSubjects()
        {
            var policy = new CharacterTestShallowWaterSplashPolicy(); policy.BeginMotion(1, 0);
            for (int id = 1; id <= 5; id++) policy.ObserveSubject(id, 1, 0, 0, 0, 0, true);
            policy.BeginMotion(2, .2);
            for (int id = 1; id <= 5; id++) policy.ObserveSubject(id, 2, .2, 0, 0, 0, true);
            for (int id = 1; id <= 4; id++) Assert.IsTrue(Admit(policy, Step(id)));
            Assert.IsFalse(Admit(policy, Step(5))); Assert.AreEqual(CharacterTestSplashRejection.Budget, policy.LastRejection);
        }
        [Test] public void PerSecondBudgetResetsAfterWindow()
        {
            var policy = new CharacterTestShallowWaterSplashPolicy(); policy.BeginMotion(1, 0);
            for (int id = 1; id <= 3; id++) policy.ObserveSubject(id, 1, 0, 0, 0, 0, true);
            policy.BeginMotion(2, .2);
            for (int id = 1; id <= 3; id++) policy.ObserveSubject(id, 2, .2, 0, 0, 0, true);
            var budget = new CharacterTestShallowWaterSplashPolicy.Budget(12, 2, .18f, .25f, 1.55f);
            Assert.IsTrue(policy.TryAdmit(Step(1), .2, 0, budget, out _, out _));
            Assert.IsTrue(policy.TryAdmit(Step(2), .2, 0, budget, out _, out _));
            Assert.IsFalse(policy.TryAdmit(Step(3), .2, 0, budget, out _, out _));
            policy.BeginMotion(3, 1.21); policy.ObserveSubject(3, 3, 1.21, 0, 0, 0, true);
            Assert.IsTrue(policy.TryAdmit(Step(3, frame: 3), 1.21, 0, budget, out _, out _));
        }
        [Test] public void SubjectCapacityCannotExpandFromContact()
        {
            var policy = new CharacterTestShallowWaterSplashPolicy(); policy.BeginMotion(1, 0);
            for (int id = 1; id <= 129; id++) policy.ObserveSubject(id, 1, 0, 0, 0, 0, true);
            policy.BeginMotion(2, .2);
            for (int id = 1; id <= 129; id++) policy.ObserveSubject(id, 2, .2, 0, 0, 0, true);
            Assert.IsFalse(Admit(policy, Step(129)));
            Assert.AreEqual(CharacterTestSplashRejection.NoMotionBaseline, policy.LastRejection);
        }
        [Test] public void RemovedSubjectCannotKeepContactAdmission()
        {
            var policy = Ready(); policy.BeginMotion(3, .3); policy.EndMotion(3);
            Assert.IsFalse(Admit(policy, Step(frame: 3), .3));
            Assert.AreEqual(CharacterTestSplashRejection.NoMotionBaseline, policy.LastRejection);
            policy.ObserveSubject(1, 3, .3, 0, 0, 0, true);
            Assert.IsFalse(Admit(policy, Step(frame: 3), .3), "Reusing an identity requires a fresh baseline.");
        }
        [Test] public void FastContinuousMotionIsAllowedButExplicitShortRecallIsSuppressed()
        {
            var policy = Ready(); policy.BeginMotion(3, .3);
            policy.ObserveSubject(1, 3, .3, .4f, 0, 0, true, horizontalSpeed: 20);
            var contact = Step(frame: 3); contact.RootX = .4f;
            Assert.IsTrue(Admit(policy, contact, .3));
            policy.BeginMotion(4, .5);
            policy.ObserveSubject(1, 4, .5, .5f, 0, 0, true, horizontalSpeed: 20, explicitTeleport: true);
            contact.Frame = 4; contact.RootX = .5f;
            Assert.IsFalse(Admit(policy, contact, .5));
        }
    }
}
