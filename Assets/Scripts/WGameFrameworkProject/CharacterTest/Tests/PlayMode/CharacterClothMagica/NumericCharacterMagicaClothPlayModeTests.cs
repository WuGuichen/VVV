using System.Collections;
using MagicaCloth2;
using MxFramework.CharacterCloth;
using MxFramework.CharacterCloth.Magica.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MxFramework.Tests.PlayMode.CharacterCloth.Magica
{
    public sealed class NumericCharacterMagicaClothPlayModeTests
    {
        private const string PrefabResourcePath =
            "MxFrameworkGenerated/CharacterPackages/000001/prefabs/000001_character_preview";

        [UnityTest]
        public IEnumerator MainGameMenuAndAuthorityPauseReachCachedCharacterCloth()
        {
            // Reflection accesses repository-only internals without making the optional adapter
            // assembly depend on the game. All objects use their normal constructors; no session
            // bootstrap, default save loading, trace file writing or resource mutation is invoked.
            const string gameAssembly = ", MxFramework.Game.CharacterTest";
            const string gameNamespace = "MxFramework.Game.CharacterTest.";
            var managerType = System.Type.GetType(gameNamespace + "GameManager" + gameAssembly);
            Assert.IsNotNull(managerType);
            GameObject host = new GameObject("[Cloth Acceptance] Inactive GameManager");
            host.SetActive(false); // Keep automatic Start/Update/bootstrap out of this fixture.
            var manager = host.AddComponent(managerType);
            var profileType = System.Type.GetType(gameNamespace + "CharacterTestSceneProfile" + gameAssembly);
            var profile = ScriptableObject.CreateInstance(profileType);
            GameObject actor = null;
            object slice = null;
            object view = null;
            var contextField = managerType.GetField("_context", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            try
            {
                var optionsType = System.Type.GetType(gameNamespace + "CharacterTestSliceOptions" + gameAssembly);
                var options = System.Activator.CreateInstance(optionsType);
                SetProperty(options, "EnableStoryBootstrap", false);
                SetProperty(options, "CreateCharacterViews", false);
                SetProperty(options, "EnableCharacterSimulation", false);
                SetProperty(options, "SuppressMissingStartupSpawnWarning", true);
                var sliceType = System.Type.GetType(gameNamespace + "GameSlice" + gameAssembly);
                slice = sliceType.GetConstructor(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                    null, new[] { optionsType }, null).Invoke(new[] { options });
                var runtime = GetProperty(slice, "CharacterRuntime");
                var bridge = GetProperty(runtime, "ViewBridge");
                var service = (CharacterClothPresentationService)GetProperty(bridge, "ClothPresentation");
                var contextType = System.Type.GetType(gameNamespace + "CharacterTestRuntimeContext" + gameAssembly);
                var context = System.Activator.CreateInstance(contextType, manager, profile);
                SetProperty(context, "Slice", slice);
                contextField.SetValue(manager, context);
                var builderType = System.Type.GetType(gameNamespace + "CharacterTestRuntimeSessionBuilder" + gameAssembly);
                var builder = System.Activator.CreateInstance(builderType, manager, context, profile);
                var menuType = System.Type.GetType(gameNamespace + "CharacterTestMainMenuCoordinator" + gameAssembly);
                var menu = System.Activator.CreateInstance(menuType, context, profile, builder);
                var sync = managerType.GetMethod("SyncClothPresentationState",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                actor = Object.Instantiate(UnityEngine.Resources.Load<GameObject>(PrefabResourcePath));
                var cloth = actor.GetComponentInChildren<MagicaCloth>(true);
                var viewType = System.Type.GetType(gameNamespace + "CharacterTestRuntimeCharacterView" + gameAssembly);
                var entityIdType = System.Type.GetType(gameNamespace + "CharacterTestCharacterEntityId" + gameAssembly);
                var entityId = System.Activator.CreateInstance(entityIdType, 901);
                view = System.Activator.CreateInstance(viewType, entityId, actor, null, null, null, null, null, null);
                viewType.GetMethod("AttachClothPresentation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(view, new object[] { service });
                Assert.AreEqual(1, service.GroupCount);
                menuType.GetMethod("SetPaused").Invoke(menu, new object[] { true });
                sync.Invoke(manager, null);
                for (int frame = 0; frame < 240 && !cloth.IsValid(); frame++) yield return null;
                Assert.IsTrue(cloth.IsValid());
                Assert.AreEqual(0f, cloth.GetTimeScale());
                yield return new WaitForEndOfFrame();
                float pausedTime = MagicaManager.Team.GetTeamDataRef(cloth.Process.TeamId).time;
                for (int frame = 0; frame < 4; frame++) yield return new WaitForEndOfFrame();
                Assert.AreEqual(pausedTime, MagicaManager.Team.GetTeamDataRef(cloth.Process.TeamId).time);
                menuType.GetMethod("SetPaused").Invoke(menu, new object[] { false });
                sync.Invoke(manager, null);
                Assert.AreEqual(1f, cloth.GetTimeScale());
                yield return new WaitForEndOfFrame();
                Assert.LessOrEqual(MagicaManager.Team.GetTeamDataRef(cloth.Process.TeamId).time - pausedTime,
                    Mathf.Max(0.001f, Time.deltaTime) + 0.001f);
                SetProperty(context, "MainMenuVisible", true);
                menuType.GetMethod("RefreshEffectivePauseState").Invoke(menu, null);
                sync.Invoke(manager, null);
                Assert.AreEqual(0f, cloth.GetTimeScale());
                SetProperty(context, "MainMenuVisible", false);
                menuType.GetMethod("RefreshEffectivePauseState").Invoke(menu, null);
                sync.Invoke(manager, null);

                var transformSync = viewType.GetMethod("SyncTransform");
                transformSync.Invoke(view, new object[] { new Vector3(10f, 0f, 0f), Quaternion.identity, false,
                    System.Activator.CreateInstance(transformSync.GetParameters()[3].ParameterType) });
                Assert.IsTrue(MagicaManager.Team.GetTeamDataRef(cloth.Process.TeamId).IsReset);
                sliceType.GetMethod("SuspendAuthority", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(slice, new object[] { GetProperty(slice, "CurrentFrame"), new System.InvalidOperationException("Isolated cloth acceptance fault") });
                sync.Invoke(manager, null);
                Assert.AreEqual(0f, cloth.GetTimeScale());
                viewType.GetMethod("Release").Invoke(view, null);
                view = null;
                Assert.IsFalse(cloth.enabled, "Release must stop cloth before deferred GameObject destruction.");
                Assert.AreEqual(0, service.GroupCount);
                yield return null;
            }
            finally
            {
                contextField.SetValue(manager, null);
                if (view != null) view.GetType().GetMethod("Release").Invoke(view, null);
                ((System.IDisposable)slice)?.Dispose();
                if (actor != null) Object.Destroy(actor);
                Object.Destroy(host);
                Object.Destroy(profile);
            }
        }

        private static object GetProperty(object target, string name) => target.GetType().GetProperty(name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).GetValue(target);
        private static void SetProperty(object target, string name, object value) => target.GetType().GetProperty(name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).SetValue(target, value);

        [UnityTest]
        public IEnumerator SessionPauseSurvivesAsyncBuildAndDoesNotAccumulateSimulationTime()
        {
            GameObject instance = Object.Instantiate(UnityEngine.Resources.Load<GameObject>(PrefabResourcePath));
            var presentation = new CharacterClothPresentationService();
            try
            {
                var system = instance.GetComponent<MagicaCharacterClothSystem>();
                var controller = instance.GetComponent<MagicaCharacterClothController>();
                var cloth = instance.GetComponentInChildren<MagicaCloth>(true);
                presentation.SetPaused(true);
                presentation.Register(system);
                system.ResetAll(); // Retained until a valid team exists.
                for (int frame = 0; frame < 240 && !cloth.IsValid(); frame++) yield return null;
                Assert.IsTrue(cloth.IsValid());
                Assert.AreEqual(CharacterClothState.Paused, controller.EffectiveState);
                Assert.AreEqual(0f, cloth.GetTimeScale());
                yield return new WaitForEndOfFrame();
                float pausedTime = MagicaManager.Team.GetTeamDataRef(cloth.Process.TeamId).time;
                for (int frame = 0; frame < 8; frame++) yield return new WaitForEndOfFrame();
                Assert.AreEqual(pausedTime, MagicaManager.Team.GetTeamDataRef(cloth.Process.TeamId).time);
                presentation.SetEnabled(false, true);
                presentation.SetPaused(false);
                Assert.IsFalse(cloth.enabled);
                presentation.SetPaused(true);
                presentation.SetEnabled(true);
                Assert.AreEqual(0f, cloth.GetTimeScale());
                presentation.SetPaused(false);
                yield return new WaitForEndOfFrame();
                var team = MagicaManager.Team.GetTeamDataRef(cloth.Process.TeamId);
                Assert.LessOrEqual(team.time - pausedTime, Mathf.Max(0.001f, Time.deltaTime) + 0.001f);
                Object.Destroy(instance);
                yield return null;
                Assert.AreEqual(0, presentation.GroupCount);
            }
            finally { presentation.Clear(false); if (instance != null) Object.Destroy(instance); }
        }

        [UnityTest]
        public IEnumerator DisabledStateIsNotLostWhenAsyncBuildFinishes()
        {
            GameObject instance = Object.Instantiate(UnityEngine.Resources.Load<GameObject>(PrefabResourcePath));
            try
            {
                var cloth = instance.GetComponentInChildren<MagicaCloth>(true);
                var controller = instance.GetComponent<MagicaCharacterClothController>();
                var system = instance.GetComponent<MagicaCharacterClothSystem>();
                system.SetPaused(true);
                yield return null; // Allow Start to initiate the asynchronous build.
                system.SetEnabled(false, true);
                system.ResetAll(true);
                for (int frame = 0; frame < 240 && !cloth.IsValid(); frame++) yield return null;
                Assert.IsTrue(cloth.IsValid());
                Assert.AreEqual(CharacterClothState.DisabledKeep, controller.EffectiveState);
                Assert.IsFalse(cloth.enabled);
                system.SetEnabled(false, false);
                Assert.AreEqual(CharacterClothState.DisabledReset, controller.EffectiveState);
                Assert.IsFalse(cloth.enabled);
                system.SetPresentationOverride(CharacterClothSystemOverride.Paused);
                system.SetEnabled(true);
                Assert.AreEqual(0f, cloth.GetTimeScale());
            }
            finally { if (instance != null) Object.Destroy(instance); }
        }

        [UnityTest]
        public IEnumerator GlobalBudgetChangesRealMagicaTimingAndPreviewRestoresOwner()
        {
            GameObject instance = Object.Instantiate(UnityEngine.Resources.Load<GameObject>(PrefabResourcePath));
            MagicaClothSimulationBudget budget = null;
            int previousFrequency = 0;
            int previousSteps = 0;
            try
            {
                var cloth = instance.GetComponentInChildren<MagicaCloth>(true);
                for (int frame = 0; frame < 240 && !cloth.IsValid(); frame++) yield return null;
                Assert.IsTrue(cloth.IsValid());
                previousFrequency = MagicaManager.GetSimulationFrequency();
                previousSteps = MagicaManager.GetMaxSimulationCountPerFrame();
                Assert.IsTrue(MagicaClothSimulationBudget.TryAcquire(30, 1, out budget));
                Assert.IsFalse(MagicaClothSimulationBudget.TryAcquire(60, 2, out _));
                yield return new WaitForEndOfFrame();
                Assert.AreEqual(30, MagicaManager.GetSimulationFrequency());
                Assert.AreEqual(1, MagicaManager.GetMaxSimulationCountPerFrame());
                var deltaProperty = typeof(TimeManager).GetProperty("SimulationDeltaTime",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.IsNotNull(deltaProperty);
                Assert.AreEqual(1f / 30f, (float)deltaProperty.GetValue(MagicaManager.Time), 0.00001f);
                using (MagicaClothSimulationBudget.SuspendForIndependentPreview())
                {
                    Assert.AreEqual(previousFrequency, MagicaManager.GetSimulationFrequency());
                    budget.SetBudget(60, 2);
                    Assert.AreEqual(previousFrequency, MagicaManager.GetSimulationFrequency());
                }
                Assert.AreEqual(60, MagicaManager.GetSimulationFrequency());
                Assert.AreEqual(2, MagicaManager.GetMaxSimulationCountPerFrame());
            }
            finally { budget?.Dispose(); budget?.Dispose(); if (instance != null) Object.Destroy(instance); }
            Assert.AreEqual(previousFrequency, MagicaManager.GetSimulationFrequency());
            Assert.AreEqual(previousSteps, MagicaManager.GetMaxSimulationCountPerFrame());
        }

        [UnityTest]
        public IEnumerator GeneratedNumericCharacter_BoneClothBuildsAndChangesRuntimeState()
        {
            GameObject prefab = UnityEngine.Resources.Load<GameObject>(PrefabResourcePath);
            Assert.IsNotNull(prefab, "Generated numeric-character runtime prefab was not found in Resources.");

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                MagicaCloth cloth = instance.GetComponentInChildren<MagicaCloth>(includeInactive: true);
                MagicaCharacterClothController controller =
                    instance.GetComponent<MagicaCharacterClothController>();
                MagicaCharacterClothSystem system =
                    instance.GetComponent<MagicaCharacterClothSystem>();
                Assert.IsNotNull(cloth);
                Assert.IsNotNull(controller);
                Assert.IsNotNull(system);
                Assert.AreEqual(ClothProcess.ClothType.BoneCloth, cloth.SerializeData.clothType);
                Assert.AreEqual(3, cloth.SerializeData.rootBones.Count);
                Assert.AreEqual(5, cloth.SerializeData.colliderCollisionConstraint.colliderList.Count);

                for (int frame = 0; frame < 240 && !cloth.IsValid(); frame++)
                    yield return null;
                Assert.IsTrue(cloth.IsValid(), "The numeric-character cape cloth did not complete its runtime build.");

                int teamId = cloth.Process.TeamId;
                controller.SetCollisionQuality(ColliderCollisionConstraint.Mode.Edge,
                    SelfCollisionConstraint.SelfCollisionMode.None);
                yield return new WaitForEndOfFrame();
                Assert.AreEqual(ColliderCollisionConstraint.Mode.Edge, cloth.SerializeData.colliderCollisionConstraint.mode);
                Assert.AreEqual(teamId, cloth.Process.TeamId, "Runtime collision quality must not rebuild the cloth team.");

                system.SetPaused(true);
                Assert.AreEqual(CharacterClothState.Paused, controller.EffectiveState);
                Assert.IsTrue(cloth.enabled);

                system.SetEnabled(false, keepPose: true);
                Assert.AreEqual(CharacterClothState.DisabledKeep, controller.EffectiveState);
                Assert.IsFalse(cloth.enabled);

                system.SetEnabled(true, keepPose: false);
                Assert.AreEqual(CharacterClothState.Running, controller.EffectiveState);
                Assert.IsTrue(cloth.enabled);
            }
            finally
            {
                Object.Destroy(instance);
            }
        }
    }
}
