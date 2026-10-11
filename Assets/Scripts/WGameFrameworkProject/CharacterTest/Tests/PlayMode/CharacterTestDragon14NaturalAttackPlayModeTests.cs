using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MxFramework.Animation;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MxFramework.Tests.PlayMode.CharacterTest
{
    public sealed class CharacterTestDragon14NaturalAttackPlayModeTests
    {
        private const double TickSeconds = 1d / 60d;

        [UnityTest]
        public IEnumerator GeneratedRuntimePrefabsDriveHeadPresentationThroughAbstractBones()
        {
            yield return null;

            string[] packageIds = { "000003", "000001" };
            for (int i = 0; i < packageIds.Length; i++)
            {
                string packageId = packageIds[i];
                string resourcePath = "MxFrameworkGenerated/CharacterPackages/"
                    + packageId + "/prefabs/" + packageId + "_character_preview";
                GameObject prefab = UnityEngine.Resources.Load<GameObject>(resourcePath);
                Assert.NotNull(prefab, "Missing runtime character prefab: " + resourcePath);

                GameObject instance = UnityEngine.Object.Instantiate(prefab);
                CharacterTestRuntimeCharacterView view = null;
                try
                {
                    CharacterAbstractBoneRuntimeMap map =
                        instance.GetComponent<CharacterAbstractBoneRuntimeMap>();
                    Assert.NotNull(map, packageId);
                    Assert.IsTrue(
                        map.TryResolve(CharacterAbstractBoneIds.Head, out Transform head),
                        packageId);
                    Assert.NotNull(head, packageId);

                    Animator animator = instance.GetComponentInChildren<Animator>(true);
                    Assert.NotNull(animator, packageId);
                    Assert.NotNull(animator.avatar, packageId);
                    Assert.IsTrue(animator.avatar.isValid, packageId);
                    Assert.AreEqual(
                        !string.Equals(packageId, "000003", StringComparison.Ordinal),
                        animator.avatar.isHuman,
                        packageId);

                    view = new CharacterTestRuntimeCharacterView(
                        new CharacterTestCharacterEntityId(i + 1),
                        instance,
                        null,
                        null,
                        null);
                    view.SyncCombatState(
                        CharacterTestCharacterCombatStateComponent.CreateDefault(),
                        packageId,
                        teamId: 1,
                        controlOwner: CharacterTestCharacterControlOwner.LocalPlayer,
                        hasLocalPlayerTeam: true,
                        localPlayerTeamId: 1);
                    Assert.AreEqual(1, view.HeadPadAnchorResolveCount, packageId);
                    Assert.AreEqual(0, view.RendererBoundsResolveCount, packageId);
                }
                finally
                {
                    if (view != null)
                        view.Release();
                    UnityEngine.Object.Destroy(instance);
                }
            }
        }

        [UnityTest]
        public IEnumerator NaturalActionInputsResolveDragon14BindingsInPlayMode()
        {
            yield return null;

            CharacterTestCharacterDefinition definition = FindDragon14Definition();
            CharacterTestTraceRecorder traceRecorder = CharacterTestTraceRecorder.CreateInMemory(1024);
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                physicsRuntime: CreatePhysicsRuntime(),
                traceRecorder: traceRecorder,
                createViews: false);
            using (var host = new RuntimeHost())
            {
                host.RegisterModule(module);
                host.Initialize();
                host.Start();
                Assert.That(module.EnqueueSpawn(new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer)), Is.True);

                int frame = 0;
                Tick(host, ref frame);
                CharacterTestCharacterEntity actor = module.Entities[0];
                Assert.That(actor.Identity.GeneratedPackageRoot.Replace('\\', '/'), Does.EndWith("/000003"));

                StartControlAction(module, host, actor, ref frame, attackPrimary: true);
                Assert.That(actor.Action.ActiveActionId, Is.EqualTo(new CharacterActionId(940000)));
                QueueNextPrimary(module, host, actor, ref frame, new CharacterActionId(940001));
                Assert.That(actor.Action.ActiveActionId, Is.EqualTo(new CharacterActionId(940001)));
                QueueNextPrimary(module, host, actor, ref frame, new CharacterActionId(940002));
                Assert.That(actor.Action.ActiveActionId, Is.EqualTo(new CharacterActionId(940002)));
                AdvanceUntilInactive(host, actor, ref frame, 220);

                StartControlAction(module, host, actor, ref frame, attackSecondary: true);
                Assert.That(actor.Action.ActiveActionId, Is.EqualTo(new CharacterActionId(940003)));
                Assert.That(actor.Animation.ActionKey, Is.EqualTo("char.000003.animation.attack13a"));
                AdvanceUntilInactive(host, actor, ref frame, 190);

                StartControlAction(
                    module,
                    host,
                    actor,
                    ref frame,
                    skillSlotPressedMask: CharacterTestSkillSlotRuntime.ToMask(0));
                Assert.That(actor.Action.ActiveActionId, Is.EqualTo(new CharacterActionId(940004)));
                Assert.That(actor.Animation.ActionKey, Is.EqualTo("char.000003.animation.attack30b"));
            }
        }

        [UnityTest]
        public IEnumerator BakedSocketCapsulesDamageTrainingTargetWithPerEventDeduplication()
        {
            yield return null;

            CharacterTestCharacterDefinition definition = FindDragon14Definition();
            CharacterSocketBakeCatalog socketCatalog = LoadSocketCatalog(definition);
            CharacterTestTraceRecorder traceRecorder = CharacterTestTraceRecorder.CreateInMemory(2048);
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreatePhysicsRuntime();
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                physicsRuntime: physicsRuntime,
                traceRecorder: traceRecorder,
                createViews: false);
            using (var host = new RuntimeHost())
            {
                host.RegisterModule(module);
                host.Initialize();
                host.Start();
                Assert.That(module.EnqueueSpawn(new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    1,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer)), Is.True);
                Assert.That(module.EnqueueSpawn(new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    2,
                    12f,
                    0f,
                    12f,
                    CharacterTestCharacterControlOwner.None)), Is.True);

                int frame = 0;
                Tick(host, ref frame);
                CharacterTestCharacterEntity actor = module.Entities[0];
                CharacterTestCharacterEntity target = module.Entities[1];
                int initialHp = module.World.GetCurrentAttribute(
                    target,
                    CharacterTestCharacterAttributeIds.Hp);

                StartControlAction(module, host, actor, ref frame, attackSecondary: true);
                AdvanceUntilInactive(host, actor, ref frame, 190);
                Assert.That(
                    module.World.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp),
                    Is.EqualTo(initialHp),
                    "Attack13a is presentation-only in this phase.");

                StartControlAction(module, host, actor, ref frame, attackPrimary: true);
                Assert.That(actor.Action.ActiveActionId, Is.EqualTo(new CharacterActionId(940000)));
                QueueNextPrimary(module, host, actor, ref frame, new CharacterActionId(940001));
                Assert.That(actor.Action.ActiveActionId, Is.EqualTo(new CharacterActionId(940001)));
                AdvanceToLocalFrame(host, actor, ref frame, 110);
                MoveTargetToSocketCapsule(
                    actor,
                    target,
                    physicsRuntime,
                    socketCatalog,
                    "char.000003.animation.attack6a",
                    CharacterTestAuthoredContentIds.D400_Id41,
                    CharacterTestAuthoredContentIds.D400_Id42,
                    39);
                StartControlAction(module, host, actor, ref frame, attackPrimary: true);
                AdvanceUntilAction(host, actor, ref frame, new CharacterActionId(940002), 20);
                Assert.That(actor.Action.ActiveActionId, Is.EqualTo(new CharacterActionId(940002)));
                AdvanceToLocalFrame(host, actor, ref frame, 50);
                int hpAfterBite = module.World.GetCurrentAttribute(
                    target,
                    CharacterTestCharacterAttributeIds.Hp);
                Assert.That(hpAfterBite, Is.LessThan(initialHp));
                Assert.That(
                    CountHitPointDebugFrames(module.CombatTraceSystem, actor, target),
                    Is.EqualTo(1),
                    "The bite window must resolve once for this target.");

                MoveTargetToSocketCapsule(
                    actor,
                    target,
                    physicsRuntime,
                    socketCatalog,
                    "char.000003.animation.attack6a",
                    CharacterTestAuthoredContentIds.D400_Id44,
                    CharacterTestAuthoredContentIds.D400_Id45,
                    119);
                AdvanceToLocalFrame(host, actor, ref frame, 135);
                int hpAfterTail = module.World.GetCurrentAttribute(
                    target,
                    CharacterTestCharacterAttributeIds.Hp);
                Assert.That(hpAfterTail, Is.LessThan(hpAfterBite));
                Assert.That(
                    CountHitPointDebugFrames(module.CombatTraceSystem, actor, target),
                    Is.EqualTo(1),
                    "The tail-sweep window must resolve once for the same target.");
                AdvanceUntilInactive(host, actor, ref frame, 80);

                MoveTargetToSocketCapsule(
                    actor,
                    target,
                    physicsRuntime,
                    socketCatalog,
                    "char.000003.animation.attack30b",
                    CharacterTestAuthoredContentIds.D400_Id44,
                    CharacterTestAuthoredContentIds.D400_Id45,
                    120);
                StartControlAction(
                    module,
                    host,
                    actor,
                    ref frame,
                    skillSlotPressedMask: CharacterTestSkillSlotRuntime.ToMask(0));
                Assert.That(actor.Action.ActiveActionId, Is.EqualTo(new CharacterActionId(940004)));
                int hpBeforeSpin = module.World.GetCurrentAttribute(
                    target,
                    CharacterTestCharacterAttributeIds.Hp);
                AdvanceToLocalFrame(host, actor, ref frame, 140);
                int hpAfterSpinWindow = module.World.GetCurrentAttribute(
                    target,
                    CharacterTestCharacterAttributeIds.Hp);
                Assert.That(
                    hpAfterSpinWindow,
                    Is.LessThan(hpBeforeSpin));
                Assert.That(
                    CountHitPointDebugFrames(module.CombatTraceSystem, actor, target),
                    Is.EqualTo(1),
                    "The full spin must resolve one debug hit for this target.");
                AdvanceUntilInactive(host, actor, ref frame, 90);
                Assert.That(
                    module.World.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp),
                    Is.EqualTo(hpAfterSpinWindow),
                    "The remaining spin frames must not damage the same target again.");
            }
        }

        private static CharacterTestCharacterDefinition FindDragon14Definition()
        {
            var definitions = new CharacterTestCharacterDefinitionRegistry();
            for (int i = 0; i < definitions.Definitions.Count; i++)
            {
                CharacterTestCharacterDefinition candidate = definitions.Definitions[i];
                if (candidate.GeneratedPackageRoot.Replace('\\', '/').EndsWith(
                        "/000003",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            Assert.Fail("Dragon14 was not discovered as a playable generated package.");
            return default;
        }

        private static CharacterSocketBakeCatalog LoadSocketCatalog(
            CharacterTestCharacterDefinition definition)
        {
            string packageRoot = definition.GeneratedPackageRoot;
            string packageId = Path.GetFileName(
                Path.GetFullPath(packageRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            Assert.That(
                CharacterSocketBakeCatalogLoader.TryLoadFromDirectory(
                    packageRoot,
                    packageId,
                    out CharacterSocketBakeCatalog catalog,
                    out _,
                    out string[] issues),
                Is.True,
                "Missing or invalid socket bake pair under " + packageRoot + ": " + string.Join(" | ", issues));
            return catalog;
        }

        private static void QueueNextPrimary(
            CharacterTestCharacterRuntimeModule module,
            RuntimeHost host,
            CharacterTestCharacterEntity actor,
            ref int frame,
            CharacterActionId expectedActionId)
        {
            AdvanceToLocalFrame(host, actor, ref frame, 110);
            StartControlAction(module, host, actor, ref frame, attackPrimary: true);
            AdvanceUntilAction(host, actor, ref frame, expectedActionId, 20);
        }

        private static void StartControlAction(
            CharacterTestCharacterRuntimeModule module,
            RuntimeHost host,
            CharacterTestCharacterEntity actor,
            ref int frame,
            bool attackPrimary = false,
            bool attackSecondary = false,
            int skillSlotPressedMask = 0)
        {
            var intent = new CharacterTestControlIntent(
                actor.Id.Value,
                Vector2.zero,
                Vector2.zero,
                jumpPressed: false,
                jumpHeld: false,
                jumpReleased: false,
                attackPrimaryPressed: attackPrimary,
                attackSecondaryPressed: attackSecondary,
                skillSlotPressedMask: skillSlotPressedMask);
            Assert.That(
                module.TryApplyControlIntent(new RuntimeFrame(frame), intent, out string message),
                Is.True,
                message);
            Tick(host, ref frame);
        }

        private static void AdvanceUntilInactive(
            RuntimeHost host,
            CharacterTestCharacterEntity actor,
            ref int frame,
            int maxFrames)
        {
            int endFrame = frame + maxFrames;
            while (actor.Action.ActiveActionId.IsValid && frame < endFrame)
                Tick(host, ref frame);
            Assert.That(actor.Action.ActiveActionId.IsValid, Is.False);
        }

        private static void AdvanceToLocalFrame(
            RuntimeHost host,
            CharacterTestCharacterEntity actor,
            ref int frame,
            int localFrame)
        {
            int maxFrame = frame + localFrame + 4;
            while (actor.Action.LocalFrame < localFrame && frame < maxFrame)
                Tick(host, ref frame);
            Assert.That(actor.Action.LocalFrame, Is.GreaterThanOrEqualTo(localFrame));
        }

        private static void AdvanceUntilAction(
            RuntimeHost host,
            CharacterTestCharacterEntity actor,
            ref int frame,
            CharacterActionId expectedActionId,
            int maxFrames)
        {
            int endFrame = frame + maxFrames;
            while (actor.Action.ActiveActionId != expectedActionId
                   && frame < endFrame)
            {
                Tick(host, ref frame);
            }
            Assert.That(actor.Action.ActiveActionId, Is.EqualTo(expectedActionId));
        }

        private static void Tick(RuntimeHost host, ref int frame)
        {
            host.Tick(frame, TickSeconds, frame * TickSeconds);
            frame++;
        }

        private static void MoveTargetToSocketCapsule(
            CharacterTestCharacterEntity actor,
            CharacterTestCharacterEntity target,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterSocketBakeCatalog catalog,
            string clipKey,
            int startSocketId,
            int endSocketId,
            int localFrame)
        {
            Assert.That(
                catalog.TryGetFrame(clipKey, startSocketId, localFrame, out MxAnimationBakedSocketFrame start),
                Is.True);
            Assert.That(
                catalog.TryGetFrame(clipKey, endSocketId, localFrame, out MxAnimationBakedSocketFrame end),
                Is.True);

            CharacterTestDirectHitTraceSourcePose sourcePose =
                CharacterTestDirectHitTraceSourcePose.FromTransform(localFrame, actor.Transform);
            FixVector3 socketMidpoint = sourcePose.TransformLocal(new FixVector3(
                Fix64.FromRaw((start.Position.X + end.Position.X) / 2L),
                Fix64.FromRaw((start.Position.Y + end.Position.Y) / 2L),
                Fix64.FromRaw((start.Position.Z + end.Position.Z) / 2L)));
            FixVector3 hurtboxLocalCenter = GetHurtboxLocalBoundsCenter(physicsRuntime, target);
            FixVector3 targetRoot = socketMidpoint - hurtboxLocalCenter;
            int targetX = CharacterTestDeterministicUnits.Fix64MetersToMillimeters(targetRoot.X);
            int targetY = System.Math.Max(
                0,
                CharacterTestDeterministicUnits.Fix64MetersToMillimeters(targetRoot.Y));
            int targetZ = CharacterTestDeterministicUnits.Fix64MetersToMillimeters(targetRoot.Z);

            CharacterTestCharacterPhysicsComponent stationaryPhysics = target.Physics.WithRuntimeModifiers(
                target.Physics.MassScale,
                target.Physics.RadiusScale,
                target.Physics.PushScale,
                blocksCharacters: false,
                canBePushed: false);
            target.SetPhysics(stationaryPhysics);
            target.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(
                targetX,
                targetY,
                targetZ));
            target.SetMotionState(0f, Vector2.zero, isGrounded: true);
            MoveFloatingTargetSupport(physicsRuntime.World, targetX, targetY, targetZ);
        }

        private static FixVector3 GetHurtboxLocalBoundsCenter(
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestCharacterEntity target)
        {
            CombatBodyId targetBodyId = physicsRuntime.GetHurtboxBodyId(target);
            bool hasBounds = false;
            FixVector3 boundsMin = FixVector3.Zero;
            FixVector3 boundsMax = FixVector3.Zero;

            var aabbs = new List<CombatPhysicsAabbCollider>();
            physicsRuntime.World.CopyAabbCollidersTo(aabbs);
            for (int i = 0; i < aabbs.Count; i++)
            {
                CombatPhysicsAabbCollider collider = aabbs[i];
                if (collider.BodyId.Equals(targetBodyId))
                    IncludeBounds(collider.LocalMin, collider.LocalMax, ref hasBounds, ref boundsMin, ref boundsMax);
            }

            var spheres = new List<CombatPhysicsSphereCollider>();
            physicsRuntime.World.CopySphereCollidersTo(spheres);
            for (int i = 0; i < spheres.Count; i++)
            {
                CombatPhysicsSphereCollider collider = spheres[i];
                if (!collider.BodyId.Equals(targetBodyId))
                    continue;
                var extent = new FixVector3(collider.Radius, collider.Radius, collider.Radius);
                IncludeBounds(
                    collider.LocalCenter - extent,
                    collider.LocalCenter + extent,
                    ref hasBounds,
                    ref boundsMin,
                    ref boundsMax);
            }

            var capsules = new List<CombatPhysicsCapsuleCollider>();
            physicsRuntime.World.CopyCapsuleCollidersTo(capsules);
            for (int i = 0; i < capsules.Count; i++)
            {
                CombatPhysicsCapsuleCollider collider = capsules[i];
                if (!collider.BodyId.Equals(targetBodyId))
                    continue;
                var extent = new FixVector3(collider.Radius, collider.Radius, collider.Radius);
                IncludeBounds(
                    Min(collider.LocalPointA, collider.LocalPointB) - extent,
                    Max(collider.LocalPointA, collider.LocalPointB) + extent,
                    ref hasBounds,
                    ref boundsMin,
                    ref boundsMax);
            }

            var obbs = new List<CombatPhysicsObbCollider>();
            physicsRuntime.World.CopyObbCollidersTo(obbs);
            for (int i = 0; i < obbs.Count; i++)
            {
                CombatPhysicsObbCollider collider = obbs[i];
                if (!collider.BodyId.Equals(targetBodyId))
                    continue;
                collider.GetWorldAabb(FixVector3.Zero, out FixVector3 min, out FixVector3 max);
                IncludeBounds(min, max, ref hasBounds, ref boundsMin, ref boundsMax);
            }

            Assert.That(hasBounds, Is.True, "The target must expose authoritative runtime Hurtbox bounds.");
            return (boundsMin + boundsMax) * Fix64.Half;
        }

        private static void MoveFloatingTargetSupport(
            CombatPhysicsWorld world,
            int targetX,
            int targetY,
            int targetZ)
        {
            var bodyId = new CombatBodyId(101);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(101),
                bodyId,
                FixVector3.Zero));
            Fix64 x = CharacterTestDeterministicUnits.MillimetersToFix64Meters(targetX);
            Fix64 y = CharacterTestDeterministicUnits.MillimetersToFix64Meters(targetY);
            Fix64 z = CharacterTestDeterministicUnits.MillimetersToFix64Meters(targetZ);
            Fix64 halfWidth = Fix64.FromRatio(1, 10);
            Fix64 thickness = Fix64.FromRatio(1, 4);
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(1),
                1,
                new FixVector3(x - halfWidth, y - thickness, z - halfWidth),
                new FixVector3(x + halfWidth, y, z + halfWidth)));
        }

        private static void IncludeBounds(
            FixVector3 min,
            FixVector3 max,
            ref bool hasBounds,
            ref FixVector3 boundsMin,
            ref FixVector3 boundsMax)
        {
            if (!hasBounds)
            {
                hasBounds = true;
                boundsMin = min;
                boundsMax = max;
                return;
            }

            boundsMin = Min(boundsMin, min);
            boundsMax = Max(boundsMax, max);
        }

        private static FixVector3 Min(FixVector3 left, FixVector3 right)
        {
            return new FixVector3(
                Fix64.Min(left.X, right.X),
                Fix64.Min(left.Y, right.Y),
                Fix64.Min(left.Z, right.Z));
        }

        private static FixVector3 Max(FixVector3 left, FixVector3 right)
        {
            return new FixVector3(
                Fix64.Max(left.X, right.X),
                Fix64.Max(left.Y, right.Y),
                Fix64.Max(left.Z, right.Z));
        }

        private static int CountHitPointDebugFrames(
            CharacterTestCharacterCombatTraceSystem combat,
            CharacterTestCharacterEntity actor,
            CharacterTestCharacterEntity target)
        {
            int count = 0;
            for (int i = 0; i < combat.HitPointDebugFrames.Count; i++)
            {
                CharacterTestHitPointDebugFrame item = combat.HitPointDebugFrames[i];
                if (item.SourceEntityId == actor.Id.Value
                    && item.TargetEntityId == target.Id.Value)
                {
                    count++;
                }
            }
            return count;
        }

        private static CharacterTestCharacterPhysicsRuntime CreatePhysicsRuntime()
        {
            var world = new CombatPhysicsWorld();
            var bodyId = new CombatBodyId(100);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(100),
                bodyId,
                FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(1),
                1,
                new FixVector3(-Fix64.FromInt(20), -Fix64.One, -Fix64.FromInt(20)),
                new FixVector3(Fix64.FromInt(20), Fix64.Zero, Fix64.FromInt(20))));
            return new CharacterTestCharacterPhysicsRuntime(
                world,
                CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                Fix64.Zero,
                1,
                "000003 natural attack playmode");
        }
    }
}
