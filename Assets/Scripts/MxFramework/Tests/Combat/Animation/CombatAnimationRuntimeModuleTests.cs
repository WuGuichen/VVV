using System;
using System.Collections.Generic;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Animation
{
    public class CombatAnimationRuntimeModuleTests
    {
        [Test]
        public void RuntimeHost_TicksCombatAnimationModulesThroughStages()
        {
            CombatAnimationContext animationContext = CreateRuntime(
                out RuntimeHost host,
                out CombatActionRegistry registry,
                out CombatActionTimelineTraceProvider traceProvider,
                out CombatPhysicsWorld physicsWorld);
            RegisterTimeline(registry);
            RegisterBodyWithAabb(physicsWorld, entity: 1, body: 1, collider: 1, layer: 1, x: 0);
            RegisterBodyWithAabb(physicsWorld, entity: 2, body: 2, collider: 1, layer: 1, x: 3);

            host.RegisterModule(new CombatActionRuntimeModule());
            host.RegisterModule(new CombatWeaponTraceRuntimeModule());
            host.RegisterModule(new CombatAnimationDiagnosticsModule());
            host.Initialize();
            host.Start();
            animationContext.ActionRunner.StartAction(new CombatEntityId(1), 1001, CombatFrame.Zero);
            traceProvider.RegisterTrace(1001, localFrame: 1, Trace(traceId: 7, radius: Fix64.Half));

            host.Tick(1, 1d / 60d, 1d / 60d);

            Assert.AreEqual(1, animationContext.LastFrameHitCandidates.Count);
            Assert.AreEqual(2, animationContext.LastFrameHitCandidates[0].TargetId.Value);
            Assert.IsTrue(animationContext.LastSnapshot.HasValue);
            CombatAnimationSnapshot snapshot = animationContext.LastSnapshot.Value;
            Assert.AreEqual(1, snapshot.RunningActionCount);
            Assert.AreEqual(1, snapshot.ActivePhaseCount);
            Assert.AreEqual(1, snapshot.HitCandidateCount);
            Assert.AreEqual(1L, snapshot.FrameIndex);

            host.Dispose();
        }

        [Test]
        public void PreSimulationContext_DoesNotTickActionModule()
        {
            var services = new RuntimeServiceRegistry();
            var animationContext = new CombatAnimationContext();
            var registry = new CombatActionRegistry();
            RegisterTimeline(registry);
            services.Register<ICombatAnimationContext>(animationContext);
            services.Register(registry);
            var host = new RuntimeHost(new RuntimeHostOptions { Services = services });
            var module = new CombatActionRuntimeModule();
            host.RegisterModule(module);
            host.Initialize();
            animationContext.ActionRunner.StartAction(new CombatEntityId(1), 1001, CombatFrame.Zero);

            module.Tick(new RuntimeTickContext(1, 0d, 0d, RuntimeTickStage.PreSimulation));

            CombatActionState state = animationContext.ActionRunner.GetActionState(new CombatEntityId(1)).Value;
            Assert.AreEqual(0, state.LocalFrame);
            Assert.AreEqual(CombatActionPhase.Startup, state.Phase);

            host.Dispose();
        }

        [Test]
        public void RuntimeHost_StopCancelsRunningActions()
        {
            CombatAnimationContext animationContext = CreateRuntime(
                out RuntimeHost host,
                out CombatActionRegistry registry,
                out _,
                out _);
            RegisterTimeline(registry);
            host.RegisterModule(new CombatActionRuntimeModule());
            host.Initialize();
            host.Start();
            animationContext.ActionRunner.StartAction(new CombatEntityId(1), 1001, CombatFrame.Zero);

            host.Stop();

            Assert.AreEqual(0, animationContext.ActionRunner.GetRunningActions().Length);
            host.Dispose();
        }

        [Test]
        public void Initialize_WithoutCombatAnimationContext_ThrowsRuntimeHostException()
        {
            var host = new RuntimeHost();
            host.RegisterModule(new CombatActionRuntimeModule());

            RuntimeHostException exception = Assert.Throws<RuntimeHostException>(() => host.Initialize());

            Assert.AreEqual(CombatActionRuntimeModule.DefaultModuleId, exception.Error.ModuleId);
            StringAssert.Contains("ICombatAnimationContext", exception.Error.Message);
        }

        [Test]
        public void WeaponTraceModule_RequiresActionRunnerFromSharedContext()
        {
            var services = new RuntimeServiceRegistry();
            services.Register<ICombatAnimationContext>(new CombatAnimationContext());
            services.Register(new CombatPhysicsWorld());
            services.Register<ICombatActionTraceProvider>(new CombatActionTimelineTraceProvider());
            var host = new RuntimeHost(new RuntimeHostOptions { Services = services });
            host.RegisterModule(new CombatWeaponTraceRuntimeModule());

            RuntimeHostException exception = Assert.Throws<RuntimeHostException>(() => host.Initialize());

            Assert.AreEqual(CombatWeaponTraceRuntimeModule.DefaultModuleId, exception.Error.ModuleId);
            StringAssert.Contains("Combat action runner is not initialized", exception.Error.Message);
        }

        [Test]
        public void Context_SetActionRunnerOnlyAllowsOneAssignment()
        {
            var context = new CombatAnimationContext();
            var registry = new CombatActionRegistry();
            CombatActionRunner first = new CombatActionRunner(registry);
            CombatActionRunner second = new CombatActionRunner(registry);

            context.SetActionRunner(first);

            Assert.Throws<InvalidOperationException>(() => context.SetActionRunner(second));
        }

        private static CombatAnimationContext CreateRuntime(
            out RuntimeHost host,
            out CombatActionRegistry registry,
            out CombatActionTimelineTraceProvider traceProvider,
            out CombatPhysicsWorld physicsWorld)
        {
            var animationContext = new CombatAnimationContext();
            registry = new CombatActionRegistry();
            traceProvider = new CombatActionTimelineTraceProvider();
            physicsWorld = new CombatPhysicsWorld();
            var services = new RuntimeServiceRegistry();
            services.Register<ICombatAnimationContext>(animationContext);
            services.Register(registry);
            services.Register<CombatPhysicsWorld>(physicsWorld);
            services.Register<ICombatActionTraceProvider>(traceProvider);
            host = new RuntimeHost(new RuntimeHostOptions { Services = services });
            return animationContext;
        }

        private static void RegisterTimeline(CombatActionRegistry registry)
        {
            registry.RegisterTimeline(1001, new CombatActionTimeline(
                1001,
                4,
                new CombatFrameRange(0, 0),
                new CombatFrameRange(1, 2),
                new CombatFrameRange(3, 3),
                windows: null,
                events: null));
        }

        private static WeaponTraceFrame Trace(int traceId, Fix64 radius)
        {
            return new WeaponTraceFrame(
                traceId,
                rootPrev: FixVector3.Zero,
                tipPrev: FixVector3.Zero,
                rootNow: FixVector3.Zero,
                tipNow: new FixVector3(Fix64.FromInt(4), Fix64.Zero, Fix64.Zero),
                radius,
                CombatPhysicsLayerMask.FromLayer(1));
        }

        private static void RegisterBodyWithAabb(
            CombatPhysicsWorld world,
            int entity,
            int body,
            int collider,
            int layer,
            int x)
        {
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(entity),
                new CombatBodyId(body),
                new FixVector3(Fix64.FromInt(x), Fix64.Zero, Fix64.Zero)));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                new CombatBodyId(body),
                new CombatColliderId(collider),
                layer,
                new FixVector3(-Fix64.Half, -Fix64.Half, -Fix64.Half),
                new FixVector3(Fix64.Half, Fix64.Half, Fix64.Half)));
        }
    }
}
