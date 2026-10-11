using System.Collections;
using System.Reflection;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.Config;
using MxFramework.Gameplay.Triggers.Unity;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MxFramework.Tests.PlayMode.GameplayTriggers
{
    public sealed class GameplayTriggerUnityPhysicsPlayModeTests
    {
        private static readonly GameplayTriggerActionKindId ActionKindId =
            new GameplayTriggerActionKindId(73202);

        [UnityTest]
        public IEnumerator RealPhysicsCallbacks_ProduceEnteredStayedAndExitedLevels()
        {
            TestStack stack = CreateStack();
            GameObject zoneObject = CreateZone(stack, "zone", out _);
            GameObject subjectObject = CreateMultiColliderSubject(
                stack,
                "subject",
                out GameplayEntityId subject,
                out Collider firstCollider,
                out Collider secondCollider);

            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Tick(stack, 0);
            AssertOccurrence(stack.Runtime, GameplayTriggerOccurrenceKind.Entered, subject);

            yield return new WaitForFixedUpdate();
            Tick(stack, 1);
            AssertOccurrence(stack.Runtime, GameplayTriggerOccurrenceKind.Stayed, subject);

            firstCollider.transform.localPosition = new Vector3(10f, 0f, 0f);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Tick(stack, 2);
            AssertOccurrence(stack.Runtime, GameplayTriggerOccurrenceKind.Stayed, subject);

            secondCollider.transform.localPosition = new Vector3(10f, 0f, 0f);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Tick(stack, 3);
            AssertOccurrence(stack.Runtime, GameplayTriggerOccurrenceKind.Exited, subject);

            Object.Destroy(subjectObject);
            Object.Destroy(zoneObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisableAndDestroy_RetireOrUnbindBeforeNextFlush()
        {
            TestStack stack = CreateStack();
            GameObject zoneObject = CreateZone(stack, "zone", out _);
            GameObject subjectObject = CreateSubject(stack, "subject", out _);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Tick(stack, 0);
            Assert.That(stack.Adapter.GetLastZoneFlushResult(0).SubjectCount, Is.EqualTo(1));

            subjectObject.SetActive(false);
            Tick(stack, 1);
            Assert.That(stack.Adapter.GetLastZoneFlushResult(0).SubjectCount, Is.Zero);
            Object.Destroy(subjectObject);
            yield return null;

            GameObject replacement = CreateSubject(stack, "replacement", out _);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Tick(stack, 2);
            Assert.That(stack.Adapter.GetLastZoneFlushResult(0).SubjectCount, Is.EqualTo(1));

            Object.Destroy(replacement);
            yield return null;
            Tick(stack, 3);
            Assert.That(stack.Adapter.GetLastZoneFlushResult(0).SubjectCount, Is.Zero);

            zoneObject.SetActive(false);
            Tick(stack, 4);
            Assert.That(
                stack.Adapter.GetLastZoneFlushResult(0).Status,
                Is.EqualTo(GameplayTriggerUnityZoneFlushStatus.Submitted));
            Object.Destroy(zoneObject);
            yield return null;

            GameObject directDestroyZone = CreateZone(stack, "direct-destroy-zone", out _);
            Object.Destroy(directDestroyZone);
            yield return null;
            Tick(stack, 5);
            Assert.That(
                stack.Adapter.GetLastZoneFlushResult(0).Status,
                Is.EqualTo(GameplayTriggerUnityZoneFlushStatus.Submitted));
        }

        [UnityTest]
        public IEnumerator SceneUnload_PersistentModuleFlushesFinalEmptyAfterComponentsDie()
        {
            TestStack stack = CreateStack();
            Scene scene = SceneManager.CreateScene("issue-732-unload");
            GameObject zoneObject = CreateZone(stack, "zone", out _, scene);
            CreateSubject(stack, "subject", out _, scene);
            using (var host = new RuntimeHost())
            {
                host.RegisterModule(stack.FlushModule);
                host.RegisterModule(stack.GameplayModule);
                host.Initialize();
                host.Start();

                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                host.Tick(0, 0d);
                Assert.That(stack.Adapter.GetLastZoneFlushResult(0).SubjectCount, Is.EqualTo(1));

                AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone)
                    yield return null;
                Assert.That(zoneObject == null, Is.True);

                host.Tick(1, 0d);
                Assert.That(
                    stack.Adapter.GetLastZoneFlushResult(0).Status,
                    Is.EqualTo(GameplayTriggerUnityZoneFlushStatus.Submitted));
                Assert.That(stack.Adapter.GetLastZoneFlushResult(0).SubjectCount, Is.Zero);
            }
        }

        private static TestStack CreateStack()
        {
            GameplayTriggerCompiledCatalogPlan plan = CompilePlan();
            GameplayTriggerCatalogBootstrapResult bootstrap =
                GameplayTriggerCatalogBootstrap.TryCreateCatalog(
                    plan,
                    new[]
                    {
                        GameplayTriggerActionBootstrapBinding.FromSink(new Sink())
                    },
                    ExactProfile(plan.CapacityRequirements));
            Assert.That(bootstrap.Succeeded, Is.True, Describe(bootstrap));
            var runtime = new GameplayTriggerRuntime(
                bootstrap.Catalog,
                bootstrap.RuntimeOptions.Value);
            Assert.That(
                GameplayTriggerUnityOccupancyCatalog.TryCreate(
                    new[]
                    {
                        new GameplayTriggerUnityOccupancyDescriptor(
                            new GameplayTriggerSourceBindingId(1),
                            new GameplayTriggerSourceProviderId(7),
                            maxSubjects: 2)
                    },
                    plan,
                    out GameplayTriggerUnityOccupancyCatalog catalog,
                    out GameplayTriggerUnityCatalogResult catalogResult),
                Is.True,
                catalogResult.ErrorCode.ToString());

            RuntimeCommandBuffer commandBuffer = RuntimeCommandBuffer.CreateBounded(8);
            var componentWorld = new GameplayComponentWorld();
            var pipeline = new GameplaySystemPipeline();
            var commandSystem = new GameplayTriggerCommandSystem(
                runtime,
                GameplayTriggerCommandSystem.DefaultSystemId,
                GameplayTriggerCommandSystem.DefaultPriority,
                GameplayTriggerCommandSystem.DefaultMaxSignalCommandItems,
                maxOccupancyCommandItems: 2,
                GameplayTriggerCommandSystem.DefaultMaxScalarCommandItems);
            pipeline.Add(commandSystem);
            pipeline.Add(new GameplayTriggerEvaluationSystem(runtime));
            var gameplayModule = new GameplayRuntimeModule(
                new GameplayWorld(),
                new GameplayAbilityRegistry(),
                commandBuffer,
                tickWorldAutomatically: false,
                systemPipeline: pipeline,
                componentWorld: componentWorld);
            var options = new GameplayTriggerUnityOccupancyOptions(
                maxSubjectBindings: 2,
                maxSubjectColliders: 2,
                maxActiveContactPairs: 2);
            Assert.That(
                GameplayTriggerUnityOccupancyComposition.TryCreate(
                    732,
                    new RuntimeCommandSourceRegistry(1),
                    gameplayModule,
                    commandSystem,
                    runtime,
                    plan,
                    catalog,
                    GameplayTriggerUnitySubmissionMode.LiveExternal,
                    in options,
                    out GameplayTriggerUnityOccupancyAdapter adapter,
                    out GameplayTriggerUnityOccupancyFlushModule flushModule,
                    out GameplayTriggerUnityInitializationResult result),
                Is.True,
                result.ErrorCode.ToString());
            return new TestStack(
                runtime,
                componentWorld,
                gameplayModule,
                adapter,
                flushModule);
        }

        private static GameplayTriggerCompiledCatalogPlan CompilePlan()
        {
            var document = new GameplayTriggerAuthoringDocument(
                GameplayTriggerConfigSchemas.AuthoringId,
                GameplayTriggerConfigSchemas.AuthoringVersion,
                new[]
                {
                    new GameplayTriggerSourceAuthoring(
                        1,
                        GameplayTriggerSourceKind.Occupancy,
                        initialSamplePolicy:
                            GameplayTriggerInitialSamplePolicy.EmitEnteredIfActive,
                        departurePolicy:
                            GameplayTriggerSubjectDeparturePolicy.EmitExited)
                },
                new[]
                {
                    new GameplayTriggerConditionAuthoring(
                        1,
                        new GameplayTriggerConditionNodeAuthoring(
                            GameplayTriggerConditionOperation.PushTrue))
                },
                new[]
                {
                    new GameplayTriggerDefinitionAuthoring(
                        1,
                        1,
                        1,
                        new[]
                        {
                            new GameplayTriggerRouteAuthoring(
                                1,
                                GameplayTriggerOccurrenceKind.Entered |
                                    GameplayTriggerOccurrenceKind.Stayed |
                                    GameplayTriggerOccurrenceKind.Exited,
                                1,
                                new[]
                                {
                                    new GameplayTriggerActionAuthoring(
                                        ActionKindId.Value,
                                        GameplayTriggerTargetSelector.Subject)
                                },
                                GameplayTriggerStateScope.PerSubject,
                                maxStateKeys: 2)
                        })
                },
                new[] { new GameplayTriggerBindingAuthoring(1, 1, 1) });
            Assert.That(
                GameplayTriggerRetiredIdRegistry.TryCreate(
                    null,
                    out GameplayTriggerRetiredIdRegistry retired,
                    out _),
                Is.True);
            Assert.That(
                GameplayTriggerSourceBindingContractRegistry.TryCreate(
                    new[]
                    {
                        new GameplayTriggerSourceBindingContractDescriptor(
                            new GameplayTriggerSourceBindingId(1),
                            new GameplayTriggerSourceDefinitionId(1),
                            GameplayTriggerSourceKind.Occupancy,
                            new GameplayTriggerSourceProviderId(7),
                            GameplayTriggerSourceEntityCapability.SubjectPossible |
                                GameplayTriggerSourceEntityCapability.SubjectGuaranteed,
                            completeBatchPerEvaluation: true,
                            maxInputItemsPerEvaluation: 2,
                            maxActiveRelations: 2,
                            signalEnvelopeSchemaVersion: 0,
                            signalOrdering: GameplayTriggerSignalOrdering.UnorderedMultiset,
                            preservesOrderedSequence: false)
                    },
                    out GameplayTriggerSourceBindingContractRegistry contracts,
                    out _),
                Is.True);
            Assert.That(
                GameplayTriggerActionRequirementRegistry.TryCreate(
                    new[]
                    {
                        new GameplayTriggerActionRequirementDescriptor(
                            ActionKindId,
                            GameplayTriggerDeliveryContract.SameFrameReserved)
                    },
                    out GameplayTriggerActionRequirementRegistry actions,
                    out _),
                Is.True);
            Assert.That(
                GameplayTriggerCompilerEnvironment.TryCreate(
                    retired,
                    contracts,
                    actions,
                    new GameplayTriggerExpectedCatalogIdentity(
                        GameplayTriggerConfigSchemas.RuntimeCatalogId,
                        GameplayTriggerConfigSchemas.RuntimeCatalogVersion),
                    out GameplayTriggerCompilerEnvironment environment,
                    out _),
                Is.True);
            GameplayTriggerCompileResult compile = GameplayTriggerConfigCompiler.Compile(
                document,
                GameplayTriggerCompilerOptions.Default,
                environment);
            Assert.That(compile.Succeeded, Is.True, Describe(compile));
            return compile.CompiledCatalog;
        }

        private static GameObject CreateZone(
            TestStack stack,
            string name,
            out GameplayTriggerUnityOccupancyZone zone,
            Scene scene = default)
        {
            var gameObject = new GameObject(name);
            if (scene.IsValid())
                SceneManager.MoveGameObjectToScene(gameObject, scene);
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = Vector3.one * 4f;
            zone = gameObject.AddComponent<GameplayTriggerUnityOccupancyZone>();
            SetField(zone, "_sourceBindingId", 1);
            SetField(zone, "_triggerCollider", collider);
            Assert.That(zone.TryAttach(stack.Adapter, out var result), Is.True, result.Status.ToString());
            return gameObject;
        }

        private static GameObject CreateSubject(
            TestStack stack,
            string name,
            out GameplayEntityId subject,
            Scene scene = default)
        {
            var gameObject = new GameObject(name);
            if (scene.IsValid())
                SceneManager.MoveGameObjectToScene(gameObject, scene);
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            Rigidbody body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var binding = gameObject.AddComponent<GameplayTriggerUnitySubjectBinding>();
            SetField(binding, "_colliders", new Collider[] { collider });
            subject = stack.ComponentWorld.CreateEntity();
            Assert.That(
                binding.TryBind(stack.Adapter, subject, out var result),
                Is.True,
                result.Status.ToString());
            return gameObject;
        }

        private static GameObject CreateMultiColliderSubject(
            TestStack stack,
            string name,
            out GameplayEntityId subject,
            out Collider firstCollider,
            out Collider secondCollider)
        {
            var root = new GameObject(name);
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var first = new GameObject("first-collider");
            first.transform.SetParent(root.transform, worldPositionStays: false);
            var second = new GameObject("second-collider");
            second.transform.SetParent(root.transform, worldPositionStays: false);
            second.transform.localPosition = new Vector3(0.5f, 0f, 0f);
            firstCollider = first.AddComponent<BoxCollider>();
            secondCollider = second.AddComponent<BoxCollider>();
            var binding = root.AddComponent<GameplayTriggerUnitySubjectBinding>();
            SetField(binding, "_colliders", new[] { firstCollider, secondCollider });
            subject = stack.ComponentWorld.CreateEntity();
            Assert.That(
                binding.TryBind(stack.Adapter, subject, out var result),
                Is.True,
                result.Status.ToString());
            return root;
        }

        private static void SetField<T>(T target, string fieldName, object value)
        {
            FieldInfo field = typeof(T).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static void Tick(TestStack stack, long frame)
        {
            stack.FlushModule.Tick(new RuntimeTickContext(
                frame,
                0d,
                0d,
                RuntimeTickStage.PreSimulation));
            stack.GameplayModule.Tick(new RuntimeTickContext(
                frame,
                0d,
                0d,
                RuntimeTickStage.Simulation));
        }

        private static void AssertOccurrence(
            GameplayTriggerRuntime runtime,
            GameplayTriggerOccurrenceKind kind,
            GameplayEntityId subject)
        {
            Assert.That(runtime.OccurrenceCount, Is.EqualTo(1));
            Assert.That(runtime.GetOccurrence(0).Kind, Is.EqualTo(kind));
            Assert.That(runtime.GetOccurrence(0).Subject, Is.EqualTo(subject));
        }

        private static GameplayTriggerRuntimeCapacityProfile ExactProfile(
            GameplayTriggerRuntimeCapacityRequirements required) =>
            new GameplayTriggerRuntimeCapacityProfile(
                required.RequiredInstances,
                required.MaxInputBatches,
                required.MaxInputItems,
                required.MaxRelations,
                required.MaxOccurrences,
                required.MaxCandidates,
                required.MaxRouteStates,
                required.MaxActivations,
                required.MaxIntents,
                required.MaxPreparedActions,
                required.MaxConditionStackDepth,
                required.MaxDeferredRecords,
                required.MaxReliableOutboxRecords);

        private static string Describe(GameplayTriggerCompileResult result) =>
            result.DiagnosticCount == 0
                ? "success"
                : result.GetDiagnostic(0).Code + " @ " + result.GetDiagnostic(0).Path;

        private static string Describe(GameplayTriggerCatalogBootstrapResult result) =>
            result.DiagnosticCount == 0
                ? "success"
                : result.GetDiagnostic(0).Code + " @ " + result.GetDiagnostic(0).Path;

        private sealed class Sink : IGameplayTriggerActionSink
        {
            public GameplayTriggerActionKindId ActionKindId =>
                GameplayTriggerUnityPhysicsPlayModeTests.ActionKindId;
            public GameplayTriggerDeliveryContract DeliveryContract =>
                GameplayTriggerDeliveryContract.SameFrameReserved;

            public bool TryPrepare(
                in GameplayTriggerActionPreparation preparation,
                out int reservationToken)
            {
                reservationToken = 1;
                return true;
            }

            public void Abort(int reservationToken)
            {
            }

            public void Commit(
                int reservationToken,
                in GameplayTriggerActionIntent intent)
            {
            }
        }

        private readonly struct TestStack
        {
            public TestStack(
                GameplayTriggerRuntime runtime,
                GameplayComponentWorld componentWorld,
                GameplayRuntimeModule gameplayModule,
                GameplayTriggerUnityOccupancyAdapter adapter,
                GameplayTriggerUnityOccupancyFlushModule flushModule)
            {
                Runtime = runtime;
                ComponentWorld = componentWorld;
                GameplayModule = gameplayModule;
                Adapter = adapter;
                FlushModule = flushModule;
            }

            public GameplayTriggerRuntime Runtime { get; }
            public GameplayComponentWorld ComponentWorld { get; }
            public GameplayRuntimeModule GameplayModule { get; }
            public GameplayTriggerUnityOccupancyAdapter Adapter { get; }
            public GameplayTriggerUnityOccupancyFlushModule FlushModule { get; }
        }
    }
}
