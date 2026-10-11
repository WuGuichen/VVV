using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using MxFramework.CharacterAction;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Projectiles.Unity;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.Vfx;
using MxFramework.Vfx.CombatBridge;
using NUnit.Framework;
using Unity.Profiling;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestFireballPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/CharacterTestScene.unity";
        private const string FireballPrefabPath =
            "Assets/VFX/MxFramework/Samples/Fireball/Prefabs/Fireball.prefab";
        private const string FireballBurstFlightPrefabPath =
            "Assets/VFX/MxFramework/Samples/Fireball/Prefabs/Fireball_Burst.prefab";
        private const string FireballBurstImpactPrefabPath =
            "Assets/VFX/MxFramework/Samples/Fireball/Prefabs/Fireball_BurstImpact.prefab";
        private const string FireballExplosionImpactPrefabPath =
            "Assets/VFX/MxFramework/Samples/Fireball/Prefabs/Fireball_ExplosionImpact.prefab";
        private const string FlameLanceFlightPrefabPath =
            "Assets/VFX/MxFramework/Samples/Fireball/Prefabs/Flame_Lance.prefab";
        private const string FlameLanceImpactPrefabPath =
            "Assets/VFX/MxFramework/Samples/Fireball/Prefabs/Flame_LanceImpact.prefab";
        private const string BeamLanceBeamPrefabPath =
            "Assets/VFX/MxFramework/Samples/BeamLance/Prefabs/BeamLance_Beam.prefab";
        private const string BeamLanceImpactPrefabPath =
            "Assets/VFX/MxFramework/Samples/BeamLance/Prefabs/BeamLance_Impact.prefab";
        private const string BeamLanceChargePrefabPath =
            "Assets/VFX/MxFramework/Samples/BeamLance/Prefabs/BeamLance_Charge.prefab";
        private const string LaserLancePathPrefabPath =
            "Assets/VFX/MxFramework/Samples/LaserLance/Prefabs/LaserLance_Laser.prefab";
        private const string LaserLanceImpactPrefabPath =
            "Assets/VFX/MxFramework/Samples/LaserLance/Prefabs/LaserLance_Impact.prefab";
        private const string LaserLanceChargePrefabPath =
            "Assets/VFX/MxFramework/Samples/LaserLance/Prefabs/LaserLance_Charge.prefab";
        private const string ChannelBeamChargePrefabPath =
            "Assets/VFX/MxFramework/Samples/ChannelBeam/Prefabs/ChannelBeam_Charge.prefab";
        private const string ChannelBeamBeamPrefabPath =
            "Assets/VFX/MxFramework/Samples/ChannelBeam/Prefabs/ChannelBeam_Beam.prefab";
        private const string ChannelBeamImpactPrefabPath =
            "Assets/VFX/MxFramework/Samples/ChannelBeam/Prefabs/ChannelBeam_Impact.prefab";
        private const string ChainLightningChargePrefabPath =
            "Assets/VFX/MxFramework/Samples/ChainLightning/Prefabs/ChainLightning_Charge.prefab";
        private const string ChainLightningArcPrefabPath =
            "Assets/VFX/MxFramework/Samples/ChainLightning/Prefabs/ChainLightning_Arc.prefab";
        private const string ChainLightningImpactPrefabPath =
            "Assets/VFX/MxFramework/Samples/ChainLightning/Prefabs/ChainLightning_Impact.prefab";
        private const string PoisonBottleFlightPrefabPath =
            "Assets/VFX/MxFramework/Samples/PoisonBottle/Prefabs/PoisonBottle_Flight.prefab";
        private const string PoisonBottleShatterPrefabPath =
            "Assets/VFX/MxFramework/Samples/PoisonBottle/Prefabs/PoisonBottle_Shatter.prefab";
        private const string PoisonBottlePoolPrefabPath =
            "Assets/VFX/MxFramework/Samples/PoisonBottle/Prefabs/PoisonBottle_Pool.prefab";
        private const string PoisonedTickPrefabPath =
            "Assets/VFX/MxFramework/Samples/PoisonBottle/Prefabs/Poisoned_Tick.prefab";
        private const string ArcMineFlightPrefabPath =
            "Assets/VFX/MxFramework/CharacterTest/ArcMine/Prefabs/ArcMine_Flight.prefab";
        private const string ArcMineDeployPrefabPath =
            "Assets/VFX/MxFramework/CharacterTest/ArcMine/Prefabs/ArcMine_Deploy.prefab";
        private const string ArcMineFusePrefabPath =
            "Assets/VFX/MxFramework/CharacterTest/ArcMine/Prefabs/ArcMine_Fuse.prefab";
        private const string ArcMineTriggerPrefabPath =
            "Assets/VFX/MxFramework/CharacterTest/ArcMine/Prefabs/ArcMine_Trigger.prefab";
        private const string TornadoFlightPrefabPath =
            "Assets/VFX/MxFramework/Samples/Tornado/Prefabs/Tornado_Flight.prefab";
        private const string TornadoChargePrefabPath =
            "Assets/VFX/MxFramework/Samples/Tornado/Prefabs/Tornado_Charge.prefab";
        private const string TornadoImpactPrefabPath =
            "Assets/VFX/MxFramework/Samples/Tornado/Prefabs/Tornado_Impact.prefab";
        private const string TornadoTickPrefabPath =
            "Assets/VFX/MxFramework/Samples/Tornado/Prefabs/Tornado_Tick.prefab";
        private const string FireballImpactVfxObjectNamePrefix = "VFX_12001_";
        private const string FireballBurstImpactVfxObjectNamePrefix = "VFX_12002_";
        private const string FireballExplosionImpactVfxObjectNamePrefix = "VFX_12007_";
        private const string FlameLanceImpactVfxObjectNamePrefix = "VFX_12003_";
        private const string FireballChargeVfxObjectNamePrefix = "VFX_12004_";
        private const string TornadoChargeVfxObjectNamePrefix = "VFX_12008_";
        private const string TornadoImpactVfxObjectNamePrefix = "VFX_12009_";
        private const string TornadoTickVfxObjectNamePrefix = "VFX_12010_";
        private const string BeamLanceBeamVfxObjectNamePrefix = "VFX_12016_";
        private const string ChainLightningChargeVfxObjectNamePrefix = "VFX_12025_";
        private const string ChainLightningArcVfxObjectNamePrefix = "VFX_12026_";
        private const string ChainLightningImpactVfxObjectNamePrefix = "VFX_12027_";
        private const string PoisonBottleShatterVfxObjectNamePrefix = "VFX_12028_";
        private const string PoisonedTickVfxObjectNamePrefix = "VFX_12029_";
        private const string PoisonBottlePoolVfxObjectNamePrefix = "VFX_12030_";
        private const float DefaultProjectileSocketLineXOffset = -0.25f;
        private const float ProjectileSmokeTargetDistanceZ = 2f;
        private const float TornadoSmokeTargetSideOffsetX = 0.2f;
        private const float TornadoSmokePullTargetDistanceZ = 2.45f;
        private const float TornadoSmokeDamageTargetDistanceZ = 5.45f;
        private const int TornadoProfilerCaptureFrameCount = 720;
        private const int TornadoProfileAnalyzerCaptureFrameCount = 240;
        private const string TornadoProfilerRecorderReportPath =
            "Temp/CharacterTestTornadoPlayModeProfilerRecorderReport.txt";
        private const string TornadoProfileAnalyzerPDataPath =
            "Temp/ProfileAnalyzer/current_after_tornado_playmode.pdata";
        private const string TornadoProfileAnalyzerCaptureSummaryPath =
            "Temp/ProfileAnalyzer/current_after_tornado_playmode_profile_analyzer_capture_summary.txt";
        private const string TornadoProfileAnalyzerPairPDataPath =
            "Temp/ProfileAnalyzer/pair_tornado_playmode.pdata";
        private const string TornadoProfileAnalyzerPairCaptureSummaryPath =
            "Temp/ProfileAnalyzer/pair_tornado_playmode_profile_analyzer_capture_summary.txt";
        private const string TornadoProfileAnalyzerStressPairPDataPath =
            "Temp/ProfileAnalyzer/pair_tornado_stress_playmode.pdata";
        private const string TornadoProfileAnalyzerStressPairCaptureSummaryPath =
            "Temp/ProfileAnalyzer/pair_tornado_stress_playmode_profile_analyzer_capture_summary.txt";
        private const string TornadoProfilerRecorderScenarioName =
            "character-test.tornado.playmode-scene.profiler-recorder";
        private const string TornadoProfileAnalyzerCaptureScenarioName =
            "character-test.tornado.playmode-scene.profile-analyzer-after";
        private const string TornadoProfileAnalyzerPairScenarioName =
            "character-test.tornado.playmode-scene.profile-analyzer-pair";
        private const string TornadoProfileAnalyzerStressPairScenarioName =
            "character-test.tornado.playmode-scene.profile-analyzer-stress-pair";
        private const string TornadoFieldQueryMarkerName = "CharacterTest.InteractionPhysics.TornadoFieldQuery";
        private const int TornadoProfileAnalyzerStressTargetCount = 96;
        private const int TornadoProfileAnalyzerStressTargetColumns = 12;
        private const float TornadoProfileAnalyzerStressTargetBaseDistanceZ = 2.65f;
        private const float TornadoProfileAnalyzerStressTargetSpacingX = 0.5f;
        private const float TornadoProfileAnalyzerStressTargetSpacingZ = 0.2f;

        private static readonly string[] TornadoProfilerMarkerNames =
        {
            "CharacterTest.CharacterRuntime.InteractionConsumers",
            "CharacterTest.InteractionPhysics.TornadoIndexRebuild",
            TornadoFieldQueryMarkerName,
            "CharacterTest.InteractionPhysics.FallbackFieldQuery",
            "CharacterTest.CharacterRuntime.Motion",
            "CharacterTest.CharacterRuntime.CombatTrace"
        };

        [Test]
        public void CharacterTestScene_BindsFireballVfxPrefab()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameManager[] managers = Object.FindObjectsByType<GameManager>(UnityEngine.FindObjectsInactive.Exclude);
            Assert.AreEqual(1, managers.Length);
            CharacterTestSceneProfile profile = managers[0].ConfiguredProfile;
            Assert.IsNotNull(profile, "CharacterTestScene should bind a CharacterTestSceneProfile.");

            GameObject fireballPrefab = profile.FireballVfxPrefab;
            Assert.IsNotNull(fireballPrefab);
            Assert.AreEqual(FireballPrefabPath, AssetDatabase.GetAssetPath(fireballPrefab));
            AssertPrefabPath(profile.FireballBurstFlightVfxPrefab, FireballBurstFlightPrefabPath);
            AssertPrefabPath(profile.FireballBurstImpactVfxPrefab, FireballBurstImpactPrefabPath);
            AssertPrefabPath(profile.FireballExplosionImpactVfxPrefab, FireballExplosionImpactPrefabPath);
            AssertPrefabPath(profile.FlameLanceFlightVfxPrefab, FlameLanceFlightPrefabPath);
            AssertPrefabPath(profile.FlameLanceImpactVfxPrefab, FlameLanceImpactPrefabPath);
            AssertPrefabPath(profile.BeamLanceBeamVfxPrefab, BeamLanceBeamPrefabPath);
            AssertPrefabPath(profile.BeamLanceImpactVfxPrefab, BeamLanceImpactPrefabPath);
            AssertPrefabPath(profile.BeamLanceChargeVfxPrefab, BeamLanceChargePrefabPath);
            AssertPrefabPath(profile.LaserLancePathVfxPrefab, LaserLancePathPrefabPath);
            AssertPrefabPath(profile.LaserLanceImpactVfxPrefab, LaserLanceImpactPrefabPath);
            AssertPrefabPath(profile.LaserLanceChargeVfxPrefab, LaserLanceChargePrefabPath);
            AssertPrefabPath(profile.ChannelBeamChargeVfxPrefab, ChannelBeamChargePrefabPath);
            AssertPrefabPath(profile.ChannelBeamBeamVfxPrefab, ChannelBeamBeamPrefabPath);
            AssertPrefabPath(profile.ChannelBeamImpactVfxPrefab, ChannelBeamImpactPrefabPath);
            AssertPrefabPath(profile.ChainLightningChargeVfxPrefab, ChainLightningChargePrefabPath);
            AssertPrefabPath(profile.ChainLightningArcVfxPrefab, ChainLightningArcPrefabPath);
            AssertPrefabPath(profile.ChainLightningImpactVfxPrefab, ChainLightningImpactPrefabPath);
            AssertPrefabPath(profile.PoisonBottleFlightVfxPrefab, PoisonBottleFlightPrefabPath);
            AssertPrefabPath(profile.PoisonBottleShatterVfxPrefab, PoisonBottleShatterPrefabPath);
            AssertPrefabPath(profile.PoisonBottlePoolVfxPrefab, PoisonBottlePoolPrefabPath);
            AssertPrefabPath(profile.PoisonedTickVfxPrefab, PoisonedTickPrefabPath);
            AssertPrefabPath(profile.ArcMineFlightVfxPrefab, ArcMineFlightPrefabPath);
            AssertPrefabPath(profile.ArcMineDeployVfxPrefab, ArcMineDeployPrefabPath);
            AssertPrefabPath(profile.ArcMineFuseVfxPrefab, ArcMineFusePrefabPath);
            AssertPrefabPath(profile.ArcMineTriggerVfxPrefab, ArcMineTriggerPrefabPath);
            AssertPrefabPath(profile.TornadoFlightVfxPrefab, TornadoFlightPrefabPath);
            AssertPrefabPath(profile.TornadoChargeVfxPrefab, TornadoChargePrefabPath);
            AssertPrefabPath(profile.TornadoImpactVfxPrefab, TornadoImpactPrefabPath);
            AssertPrefabPath(profile.TornadoTickVfxPrefab, TornadoTickPrefabPath);
        }

        [Test]
        public void CharacterTestScene_TornadoVfxPrefabsUseLayeredVortexParticles()
        {
            AssertVfxPrefabStructure(
                TornadoFlightPrefabPath,
                minParticleSystems: 6,
                "CoreColumn",
                "InnerVortex",
                "GroundDustRing",
                "DebrisSpiral",
                "WindRibbons",
                "AttractionFieldRing");
            AssertVfxPrefabStructure(
                TornadoChargePrefabPath,
                minParticleSystems: 3,
                "ChargeGroundSpiral",
                "ChargeRisingWisps",
                "ChargeDustLift");
            AssertVfxPrefabStructure(
                TornadoImpactPrefabPath,
                minParticleSystems: 4,
                "ImpactWindBurst",
                "ImpactDustShockRing",
                "ImpactDebrisLift",
                "ImpactAirColumnCollapse");
            AssertVfxPrefabStructure(
                TornadoTickPrefabPath,
                minParticleSystems: 2,
                "TickPulseRing",
                "TickWindWisps");
        }

        [Test]
        public void CharacterTestScene_BeamLanceVfxPrefabsUseLayeredBeamParticles()
        {
            AssertVfxPrefabStructure(
                BeamLanceBeamPrefabPath,
                minParticleSystems: 5,
                "BeamCore",
                "BeamHalo",
                "BeamNeedleSparks",
                "BeamMotes",
                "BeamEndFlash");
            AssertVfxPrefabStructure(
                BeamLanceImpactPrefabPath,
                minParticleSystems: 4,
                "ImpactFlash",
                "ImpactRing",
                "ImpactSparks",
                "ImpactAfterglow");
            AssertVfxPrefabStructure(
                BeamLanceChargePrefabPath,
                minParticleSystems: 3,
                "ChargeCore",
                "ChargeRays",
                "ChargeMotes");
            AssertVfxPrefabChildrenLocalY(
                BeamLanceBeamPrefabPath,
                maxAbsLocalY: 0.25f,
                "BeamCore",
                "BeamHalo",
                "BeamNeedleSparks",
                "BeamMotes",
                "BeamEndFlash");
            AssertVfxPrefabChildrenLocalY(
                BeamLanceImpactPrefabPath,
                maxAbsLocalY: 0.25f,
                "ImpactFlash",
                "ImpactRing",
                "ImpactSparks",
                "ImpactAfterglow");
            AssertVfxPrefabChildrenLocalY(
                BeamLanceChargePrefabPath,
                maxAbsLocalY: 0.25f,
                "ChargeCore",
                "ChargeRays",
                "ChargeMotes");
            AssertVfxPrefabChildLocalZ(
                BeamLanceBeamPrefabPath,
                "BeamEndFlash",
                expectedZ: 43.75f,
                tolerance: 0.3f);
        }

        [Test]
        public void CharacterTestScene_LaserLanceVfxPrefabsUsePointToPointPathView()
        {
            AssertVfxPrefabStructure(
                LaserLanceImpactPrefabPath,
                minParticleSystems: 3,
                "ImpactFlash",
                "ImpactRing",
                "ImpactNeedles");
            AssertVfxPrefabStructure(
                LaserLanceChargePrefabPath,
                minParticleSystems: 3,
                "ChargeCore",
                "ChargeRays",
                "ChargeMotes");
            GameObject pathPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LaserLancePathPrefabPath);
            Assert.IsNotNull(pathPrefab, LaserLancePathPrefabPath + " should exist.");
            CharacterTestLaserPathVfxView pathView =
                pathPrefab.GetComponent<CharacterTestLaserPathVfxView>();
            Assert.IsNotNull(pathView, "Laser Lance path prefab should scale through CharacterTestLaserPathVfxView.");
            Assert.Less(pathView.LengthMeters, 2f, "Laser Lance path prefab should not bake the 44m endpoint.");
            Assert.IsNotNull(pathPrefab.transform.Find("LaserCore"));
            Assert.IsNotNull(pathPrefab.transform.Find("LaserHalo"));
            Assert.IsNotNull(pathPrefab.transform.Find("LaserEndpointFlash"));
            AssertVfxPrefabChildrenLocalY(
                LaserLanceImpactPrefabPath,
                maxAbsLocalY: 0.25f,
                "ImpactFlash",
                "ImpactRing",
                "ImpactNeedles");
            AssertVfxPrefabChildrenLocalY(
                LaserLanceChargePrefabPath,
                maxAbsLocalY: 0.25f,
                "ChargeCore",
                "ChargeRays",
                "ChargeMotes");
        }

        [Test]
        public void CharacterTestScene_ChannelBeamVfxPrefabsUseLayeredBeamParticles()
        {
            AssertVfxPrefabStructure(
                ChannelBeamChargePrefabPath,
                minParticleSystems: 3,
                "ChargeCore",
                "ChargeGatherRays",
                "ChargeMotes");
            AssertVfxPrefabStructure(
                ChannelBeamBeamPrefabPath,
                minParticleSystems: 5,
                "BeamCore",
                "BeamHalo",
                "BeamNeedleSparks",
                "BeamPulseMotes",
                "BeamEndFlash");
            AssertVfxPrefabStructure(
                ChannelBeamImpactPrefabPath,
                minParticleSystems: 4,
                "ImpactFlash",
                "ImpactRing",
                "ImpactNeedles",
                "ImpactAfterglow");
            AssertVfxPrefabChildrenLocalY(
                ChannelBeamChargePrefabPath,
                maxAbsLocalY: 0.25f,
                "ChargeCore",
                "ChargeGatherRays",
                "ChargeMotes");
            AssertVfxPrefabChildrenLocalY(
                ChannelBeamBeamPrefabPath,
                maxAbsLocalY: 0.25f,
                "BeamCore",
                "BeamHalo",
                "BeamNeedleSparks",
                "BeamPulseMotes",
                "BeamEndFlash");
            AssertVfxPrefabChildrenLocalY(
                ChannelBeamImpactPrefabPath,
                maxAbsLocalY: 0.25f,
                "ImpactFlash",
                "ImpactRing",
                "ImpactNeedles",
                "ImpactAfterglow");
            AssertVfxPrefabChildLocalZ(
                ChannelBeamBeamPrefabPath,
                "BeamEndFlash",
                expectedZ: 31.8f,
                tolerance: 0.3f);
        }

        [Test]
        public void CharacterTestScene_ChainLightningVfxPrefabsUseRuntimeLengthArc()
        {
            AssertVfxPrefabStructure(
                ChainLightningChargePrefabPath,
                minParticleSystems: 3,
                "ChargeCore",
                "ChargeForks",
                "ChargeSparks");
            AssertVfxPrefabStructure(
                ChainLightningImpactPrefabPath,
                minParticleSystems: 3,
                "ImpactFlash",
                "ImpactForkRing",
                "ImpactSparks");
            GameObject arcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChainLightningArcPrefabPath);
            Assert.IsNotNull(arcPrefab, ChainLightningArcPrefabPath + " should exist.");
            CharacterTestChainLightningArcVfxView arcView =
                arcPrefab.GetComponent<CharacterTestChainLightningArcVfxView>();
            Assert.IsNotNull(arcView, "Chain Lightning arc prefab should scale through CharacterTestChainLightningArcVfxView.");
            Assert.Less(arcView.LengthMeters, 2f, "Chain Lightning arc prefab should not bake a fixed jump length.");
            Assert.IsNotNull(arcPrefab.transform.Find("LightningCore"));
            Assert.IsNotNull(arcPrefab.transform.Find("LightningHalo"));
            AssertVfxPrefabChildrenLocalY(
                ChainLightningChargePrefabPath,
                maxAbsLocalY: 0.25f,
                "ChargeCore",
                "ChargeForks",
                "ChargeSparks");
            AssertVfxPrefabChildrenLocalY(
                ChainLightningImpactPrefabPath,
                maxAbsLocalY: 0.25f,
                "ImpactFlash",
                "ImpactForkRing",
                "ImpactSparks");
            AssertVfxPrefabChildrenLocalY(
                ChainLightningArcPrefabPath,
                maxAbsLocalY: 0.25f,
                "LightningCore",
                "LightningHalo");
        }

        [Test]
        public void CharacterTestScene_PoisonBottleVfxPrefabsUseLowAnchoredParticles()
        {
            AssertVfxPrefabStructure(
                PoisonBottleFlightPrefabPath,
                minParticleSystems: 3,
                "BottleBody",
                "BottleCore",
                "BottleTrail",
                "BottleMotes");
            AssertVfxPrefabStructure(
                PoisonBottleShatterPrefabPath,
                minParticleSystems: 3,
                "ShatterSplash",
                "ShatterMistRing",
                "BottleSpatter");
            AssertVfxPrefabStructure(
                PoisonBottlePoolPrefabPath,
                minParticleSystems: 3,
                "PoolSurface",
                "PoolBubbles",
                "PoolVapor");
            AssertVfxPrefabStructure(
                PoisonedTickPrefabPath,
                minParticleSystems: 3,
                "TickPulse",
                "TickVapor",
                "TickMotes");
            AssertVfxPrefabChildrenLocalY(
                PoisonBottleFlightPrefabPath,
                maxAbsLocalY: 0.25f,
                "BottleBody",
                "BottleCore",
                "BottleTrail",
                "BottleMotes");
            AssertVfxPrefabChildrenLocalY(
                PoisonBottleShatterPrefabPath,
                maxAbsLocalY: 0.25f,
                "ShatterSplash",
                "ShatterMistRing",
                "BottleSpatter");
            AssertVfxPrefabChildrenLocalY(
                PoisonBottlePoolPrefabPath,
                maxAbsLocalY: 0.25f,
                "PoolSurface",
                "PoolBubbles",
                "PoolVapor");
            AssertVfxPrefabChildrenLocalY(
                PoisonedTickPrefabPath,
                maxAbsLocalY: 0.25f,
                "TickPulse",
                "TickVapor",
                "TickMotes");
        }

        [Test]
        public void CharacterTestScene_ArcMineVfxPrefabsUseRuneElectricParticles()
        {
            AssertVfxPrefabStructure(
                ArcMineFlightPrefabPath,
                minParticleSystems: 3,
                "RuneCore",
                "ArcTrail",
                "RuneMotes");
            AssertVfxPrefabStructure(
                ArcMineDeployPrefabPath,
                minParticleSystems: 3,
                "GroundRune",
                "DeployShockRing",
                "ArmingSparks");
            AssertVfxPrefabStructure(
                ArcMineFusePrefabPath,
                minParticleSystems: 3,
                "FuseRunePulse",
                "CountdownArcs",
                "WarningMotes");
            AssertVfxPrefabStructure(
                ArcMineTriggerPrefabPath,
                minParticleSystems: 3,
                "RuneFlash",
                "ElectricBurstRing",
                "AfterglowMotes");
        }

        [Test]
        public void CharacterTestScene_PoisonBottleFlightVfxStaysVisibleForFullThrowLifetime()
        {
            const float expectedFlightSeconds = CharacterTestFireballProjectileAdapter.PoisonBottleLifetimeFrames / 60f;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PoisonBottleFlightPrefabPath);
            Assert.IsNotNull(prefab, PoisonBottleFlightPrefabPath + " should exist.");

            Transform body = prefab.transform.Find("BottleBody");
            Assert.IsNotNull(body, "Poison Bottle flight should keep a persistent visible bottle body.");
            Assert.IsNotNull(
                body.GetComponent<MeshRenderer>(),
                "Poison Bottle flight body should remain visible until the projectile presenter destroys the root.");

            AssertLoopingFlightParticle(
                PoisonBottleFlightPrefabPath,
                "BottleCore",
                expectedFlightSeconds);
            AssertLoopingFlightParticle(
                PoisonBottleFlightPrefabPath,
                "BottleTrail",
                expectedFlightSeconds);
            AssertLoopingFlightParticle(
                PoisonBottleFlightPrefabPath,
                "BottleMotes",
                expectedFlightSeconds);
        }

        [Test]
        public void CharacterTestGeneratedFireballCatalogs_LoadThroughRuntimeSources()
        {
            AbilityDefinitionRegistry abilities = CharacterTestAbilityCatalogSource.CreateRegistry();
            Assert.IsTrue(abilities.TryGetByInputIntent(
                CharacterTestAbilityIds.FireballInputIntentId,
                out AbilityDefinitionConfig ability));
            Assert.AreEqual(CharacterTestAbilityIds.FireballAbilityId, ability.AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.FireballCooldownFrames, ability.Cooldown.Frames);
            Assert.AreEqual(CharacterTestAbilityIds.FireballCooldownGroupId, ability.Cooldown.GroupId);
            Assert.AreEqual(0, ability.Costs.Count);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballActionId, ability.ActionBinding.ActionId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballIntentId, ability.ActionBinding.ActionIntentId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D411_Id2, ability.Damage.AnalysisCategory);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballPresentationProfileId,
                ability.PresentationProfileId);
            Assert.IsTrue(abilities.TryGetByInputIntent(
                CharacterTestAbilityIds.FireballBurstInputIntentId,
                out AbilityDefinitionConfig burstAbility));
            Assert.AreEqual(CharacterTestAbilityIds.FireballBurstAbilityId, burstAbility.AbilityId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballBurstActionId, burstAbility.ActionBinding.ActionId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D411_Id3, burstAbility.Damage.AnalysisCategory);
            Assert.IsTrue(abilities.TryGetByInputIntent(
                CharacterTestAbilityIds.FlameLanceInputIntentId,
                out AbilityDefinitionConfig flameLanceAbility));
            Assert.AreEqual(CharacterTestAbilityIds.FlameLanceAbilityId, flameLanceAbility.AbilityId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FlameLanceActionId, flameLanceAbility.ActionBinding.ActionId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D411_Id4, flameLanceAbility.Damage.AnalysisCategory);
            Assert.IsTrue(abilities.TryGetByInputIntent(
                CharacterTestAbilityIds.FireballLobInputIntentId,
                out AbilityDefinitionConfig fireballLobAbility));
            Assert.AreEqual(CharacterTestAbilityIds.FireballLobAbilityId, fireballLobAbility.AbilityId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballLobActionId, fireballLobAbility.ActionBinding.ActionId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D411_Id3, fireballLobAbility.Damage.AnalysisCategory);
            Assert.IsTrue(abilities.TryGetByInputIntent(
                CharacterTestAbilityIds.FireballSurgeInputIntentId,
                out AbilityDefinitionConfig fireballSurgeAbility));
            Assert.AreEqual(CharacterTestAbilityIds.FireballSurgeAbilityId, fireballSurgeAbility.AbilityId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballSurgeActionId, fireballSurgeAbility.ActionBinding.ActionId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D411_Id2, fireballSurgeAbility.Damage.AnalysisCategory);
            Assert.IsTrue(abilities.TryGetByInputIntent(
                CharacterTestAbilityIds.FireballReturnInputIntentId,
                out AbilityDefinitionConfig fireballReturnAbility));
            Assert.AreEqual(CharacterTestAbilityIds.FireballReturnAbilityId, fireballReturnAbility.AbilityId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballReturnActionId, fireballReturnAbility.ActionBinding.ActionId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D411_Id2, fireballReturnAbility.Damage.AnalysisCategory);
            Assert.IsTrue(abilities.TryGetByInputIntent(
                CharacterTestAbilityIds.FireballSwayInputIntentId,
                out AbilityDefinitionConfig fireballSwayAbility));
            Assert.AreEqual(CharacterTestAbilityIds.FireballSwayAbilityId, fireballSwayAbility.AbilityId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballSwayActionId, fireballSwayAbility.ActionBinding.ActionId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D411_Id2, fireballSwayAbility.Damage.AnalysisCategory);
            Assert.IsTrue(abilities.TryGetByInputIntent(
                CharacterTestAbilityIds.TornadoInputIntentId,
                out AbilityDefinitionConfig tornadoAbility));
            Assert.AreEqual(CharacterTestAbilityIds.TornadoAbilityId, tornadoAbility.AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.TornadoCooldownFrames, tornadoAbility.Cooldown.Frames);
            Assert.AreEqual(CharacterTestAbilityIds.TornadoCooldownGroupId, tornadoAbility.Cooldown.GroupId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.TornadoActionId, tornadoAbility.ActionBinding.ActionId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.TornadoIntentId, tornadoAbility.ActionBinding.ActionIntentId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D411_Id5, tornadoAbility.Damage.AnalysisCategory);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.TornadoPresentationProfileId,
                tornadoAbility.PresentationProfileId);
            Assert.IsTrue(abilities.TryGetByInputIntent(
                CharacterTestAbilityIds.BeamLanceInputIntentId,
                out AbilityDefinitionConfig beamLanceAbility));
            Assert.AreEqual(CharacterTestAbilityIds.BeamLanceAbilityId, beamLanceAbility.AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.BeamLanceCooldownFrames, beamLanceAbility.Cooldown.Frames);
            Assert.AreEqual(CharacterTestAbilityIds.BeamLanceCooldownGroupId, beamLanceAbility.Cooldown.GroupId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.BeamLanceActionId, beamLanceAbility.ActionBinding.ActionId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.BeamLanceIntentId, beamLanceAbility.ActionBinding.ActionIntentId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D411_Id6, beamLanceAbility.Damage.AnalysisCategory);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.BeamLancePresentationProfileId,
                beamLanceAbility.PresentationProfileId);
            Assert.IsTrue(abilities.TryGetByInputIntent(
                CharacterTestAbilityIds.LaserLanceInputIntentId,
                out AbilityDefinitionConfig laserLanceAbility));
            Assert.AreEqual(CharacterTestAbilityIds.LaserLanceAbilityId, laserLanceAbility.AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.LaserLanceCooldownFrames, laserLanceAbility.Cooldown.Frames);
            Assert.AreEqual(CharacterTestAbilityIds.LaserLanceCooldownGroupId, laserLanceAbility.Cooldown.GroupId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LaserLanceActionId, laserLanceAbility.ActionBinding.ActionId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LaserLanceIntentId, laserLanceAbility.ActionBinding.ActionIntentId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D411_Id8, laserLanceAbility.Damage.AnalysisCategory);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.LaserLancePresentationProfileId,
                laserLanceAbility.PresentationProfileId);
            Assert.IsTrue(abilities.TryGetByInputIntent(
                CharacterTestAbilityIds.ChannelBeamInputIntentId,
                out AbilityDefinitionConfig channelBeamAbility));
            Assert.AreEqual(CharacterTestAbilityIds.ChannelBeamAbilityId, channelBeamAbility.AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.ChannelBeamCooldownFrames, channelBeamAbility.Cooldown.Frames);
            Assert.AreEqual(CharacterTestAbilityIds.ChannelBeamCooldownGroupId, channelBeamAbility.Cooldown.GroupId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.ChannelBeamActionId, channelBeamAbility.ActionBinding.ActionId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.ChannelBeamIntentId, channelBeamAbility.ActionBinding.ActionIntentId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D411_Id7, channelBeamAbility.Damage.AnalysisCategory);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.ChannelBeamPresentationProfileId,
                channelBeamAbility.PresentationProfileId);
            Assert.IsTrue(abilities.TryGetByInputIntent(
                CharacterTestAbilityIds.ChainLightningInputIntentId,
                out AbilityDefinitionConfig chainLightningAbility));
            Assert.AreEqual(CharacterTestAbilityIds.ChainLightningAbilityId, chainLightningAbility.AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.ChainLightningCooldownFrames, chainLightningAbility.Cooldown.Frames);
            Assert.AreEqual(CharacterTestAbilityIds.ChainLightningCooldownGroupId, chainLightningAbility.Cooldown.GroupId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.ChainLightningActionId, chainLightningAbility.ActionBinding.ActionId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.ChainLightningIntentId, chainLightningAbility.ActionBinding.ActionIntentId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D411_Id9, chainLightningAbility.Damage.AnalysisCategory);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.ChainLightningPresentationProfileId,
                chainLightningAbility.PresentationProfileId);
            Assert.IsTrue(abilities.TryGetByInputIntent(
                CharacterTestAbilityIds.PoisonBottleInputIntentId,
                out AbilityDefinitionConfig poisonBottleAbility));
            Assert.AreEqual(CharacterTestAbilityIds.PoisonBottleAbilityId, poisonBottleAbility.AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.PoisonBottleCooldownFrames, poisonBottleAbility.Cooldown.Frames);
            Assert.AreEqual(CharacterTestAbilityIds.PoisonBottleCooldownGroupId, poisonBottleAbility.Cooldown.GroupId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.PoisonBottleActionId, poisonBottleAbility.ActionBinding.ActionId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.PoisonBottleIntentId, poisonBottleAbility.ActionBinding.ActionIntentId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D411_Id10, poisonBottleAbility.Damage.AnalysisCategory);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.PoisonBottlePresentationProfileId,
                poisonBottleAbility.PresentationProfileId);
            Assert.IsTrue(abilities.TryGetByInputIntent(
                CharacterTestAbilityIds.GuardAuraInputIntentId,
                out AbilityDefinitionConfig guardAuraAbility));
            Assert.AreEqual(CharacterTestAbilityIds.GuardAuraAbilityId, guardAuraAbility.AbilityId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.GuardAuraActionId, guardAuraAbility.ActionBinding.ActionId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.GuardAuraIntentId, guardAuraAbility.ActionBinding.ActionIntentId);
            Assert.IsFalse(guardAuraAbility.Damage.HasDamage);
            Assert.IsTrue(abilities.TryGetByInputIntent(
                CharacterTestAbilityIds.FireballHomingInputIntentId,
                out AbilityDefinitionConfig fireballHomingAbility));
            Assert.AreEqual(CharacterTestAbilityIds.FireballHomingAbilityId, fireballHomingAbility.AbilityId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballHomingActionId, fireballHomingAbility.ActionBinding.ActionId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D411_Id2, fireballHomingAbility.Damage.AnalysisCategory);
            Assert.IsTrue(abilities.TryGetByInputIntent(
                CharacterTestAbilityIds.FireballBezierInputIntentId,
                out AbilityDefinitionConfig fireballBezierAbility));
            Assert.AreEqual(CharacterTestAbilityIds.FireballBezierAbilityId, fireballBezierAbility.AbilityId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballBezierActionId, fireballBezierAbility.ActionBinding.ActionId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D411_Id2, fireballBezierAbility.Damage.AnalysisCategory);
            Assert.IsTrue(abilities.TryGetByInputIntent(
                CharacterTestAbilityIds.FireballExplosionInputIntentId,
                out AbilityDefinitionConfig fireballExplosionAbility));
            Assert.AreEqual(CharacterTestAbilityIds.FireballExplosionAbilityId, fireballExplosionAbility.AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.FireballExplosionCooldownFrames, fireballExplosionAbility.Cooldown.Frames);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballExplosionActionId, fireballExplosionAbility.ActionBinding.ActionId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballExplosionIntentId, fireballExplosionAbility.ActionBinding.ActionIntentId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D411_Id3, fireballExplosionAbility.Damage.AnalysisCategory);
            Assert.AreEqual(
                CharacterTestAbilityEffectTimelineSource.FireballExplosionTimelineId,
                fireballExplosionAbility.EffectTimelineId);

            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            Assert.IsTrue(actionRuntime.Registry.TryGetActionSet(
                CharacterTestCharacterActionConfigSource.ActionSetStableId,
                out CharacterActionSetConfig actionSet));
            AssertActionBindingExists(
                actionSet,
                CharacterTestCharacterActionConfigSource.FireballBezierIntentId,
                (CharacterTestCharacterActionConfigSource.FireballBezierActionId).ToString());
            AssertActionBindingExists(
                actionSet,
                CharacterTestCharacterActionConfigSource.FireballExplosionIntentId,
                (CharacterTestCharacterActionConfigSource.FireballExplosionActionId).ToString());
            AssertActionBindingExists(
                actionSet,
                CharacterTestCharacterActionConfigSource.TornadoIntentId,
                (CharacterTestCharacterActionConfigSource.TornadoActionId).ToString());
            AssertActionBindingExists(
                actionSet,
                CharacterTestCharacterActionConfigSource.LaserLanceIntentId,
                (CharacterTestCharacterActionConfigSource.LaserLanceActionId).ToString());
            AssertActionBindingExists(
                actionSet,
                CharacterTestCharacterActionConfigSource.ChannelBeamIntentId,
                (CharacterTestCharacterActionConfigSource.ChannelBeamActionId).ToString());
            AssertActionBindingExists(
                actionSet,
                CharacterTestCharacterActionConfigSource.ChainLightningIntentId,
                (CharacterTestCharacterActionConfigSource.ChainLightningActionId).ToString());
            AssertActionBindingExists(
                actionSet,
                CharacterTestCharacterActionConfigSource.PoisonBottleIntentId,
                (CharacterTestCharacterActionConfigSource.PoisonBottleActionId).ToString());
            Assert.IsTrue(actionRuntime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.FireballBezierActionId,
                out CharacterActionConfig fireballBezierAction));
            Assert.AreEqual(CharacterActionCategory.Skill, fireballBezierAction.Category);
            Assert.AreEqual(90, fireballBezierAction.DurationFrames);
            AssertProjectileGameplayEvent(
                fireballBezierAction,
                6,
                CharacterTestCharacterActionConfigSource.FireballBezierProjectileRequestId,
                CharacterTestCharacterActionConfigSource.FireballBezierProjectileImpactStableEventId);
            Assert.IsTrue(actionRuntime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.FireballExplosionActionId,
                out CharacterActionConfig fireballExplosionAction));
            Assert.AreEqual(CharacterActionCategory.Skill, fireballExplosionAction.Category);
            Assert.AreEqual(90, fireballExplosionAction.DurationFrames);
            AssertProjectileGameplayEvent(
                fireballExplosionAction,
                6,
                CharacterTestCharacterActionConfigSource.FireballExplosionProjectileRequestId,
                CharacterTestCharacterActionConfigSource.FireballExplosionProjectileImpactStableEventId);
            Assert.IsTrue(actionRuntime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.TornadoActionId,
                out CharacterActionConfig tornadoAction));
            Assert.AreEqual(CharacterActionCategory.Skill, tornadoAction.Category);
            Assert.AreEqual(90, tornadoAction.DurationFrames);
            AssertProjectileGameplayEvent(
                tornadoAction,
                6,
                CharacterTestCharacterActionConfigSource.TornadoProjectileRequestId,
                CharacterTestCharacterActionConfigSource.TornadoProjectileImpactStableEventId);
            Assert.IsTrue(actionRuntime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.LaserLanceActionId,
                out CharacterActionConfig laserLanceAction));
            Assert.AreEqual(CharacterActionCategory.Skill, laserLanceAction.Category);
            Assert.AreEqual(90, laserLanceAction.DurationFrames);
            AssertProjectileGameplayEvent(
                laserLanceAction,
                30,
                CharacterTestCharacterActionConfigSource.LaserLanceProjectileRequestId,
                CharacterTestCharacterActionConfigSource.LaserLanceProjectileImpactStableEventId);
            Assert.IsTrue(actionRuntime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.ChainLightningActionId,
                out CharacterActionConfig chainLightningAction));
            Assert.AreEqual(CharacterActionCategory.Skill, chainLightningAction.Category);
            Assert.AreEqual(90, chainLightningAction.DurationFrames);
            AssertProjectileGameplayEvent(
                chainLightningAction,
                30,
                CharacterTestCharacterActionConfigSource.ChainLightningGameplayRequestId,
                CharacterTestCharacterActionConfigSource.ChainLightningImpactStableEventId);
            Assert.IsTrue(actionRuntime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.PoisonBottleActionId,
                out CharacterActionConfig poisonBottleAction));
            Assert.AreEqual(CharacterActionCategory.Skill, poisonBottleAction.Category);
            Assert.AreEqual(90, poisonBottleAction.DurationFrames);
            AssertProjectileGameplayEvent(
                poisonBottleAction,
                30,
                CharacterTestCharacterActionConfigSource.PoisonBottleProjectileRequestId,
                CharacterTestCharacterActionConfigSource.PoisonBottleProjectileImpactStableEventId);

            VfxDefinitionRegistry vfxDefinitions = CharacterTestVfxDefinitionSource.CreateDefinitions();
            Assert.IsTrue(vfxDefinitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.FireballDefinitionId),
                out VfxDefinition fireballVfx));
            Assert.AreEqual(CharacterTestVfxIds.FireballDefinitionStableId, fireballVfx.StableName);
            Assert.AreEqual(CharacterTestVfxIds.FireballResourceKey, fireballVfx.PrefabResource);
            Assert.IsTrue(vfxDefinitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.FireballChargeDefinitionId),
                out VfxDefinition fireballChargeVfx));
            Assert.AreEqual(CharacterTestVfxIds.FireballChargeDefinitionStableId, fireballChargeVfx.StableName);
            Assert.AreEqual(CharacterTestVfxIds.FireballChargeResourceKey, fireballChargeVfx.PrefabResource);
            Assert.IsTrue(vfxDefinitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.FireballBurstImpactDefinitionId),
                out VfxDefinition fireballBurstVfx));
            Assert.AreEqual(CharacterTestVfxIds.FireballBurstImpactDefinitionStableId, fireballBurstVfx.StableName);
            Assert.AreEqual(CharacterTestVfxIds.FireballBurstImpactResourceKey, fireballBurstVfx.PrefabResource);
            Assert.IsTrue(vfxDefinitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.FireballExplosionImpactDefinitionId),
                out VfxDefinition fireballExplosionVfx));
            Assert.AreEqual(CharacterTestVfxIds.FireballExplosionImpactDefinitionStableId, fireballExplosionVfx.StableName);
            Assert.AreEqual(CharacterTestVfxIds.FireballExplosionImpactResourceKey, fireballExplosionVfx.PrefabResource);
            Assert.AreEqual(
                CharacterTestVfxIds.FireballExplosionImpactLifetimeFrames,
                fireballExplosionVfx.DefaultLifetimeFrames);
            Assert.IsTrue(vfxDefinitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.FireballBurstChargeDefinitionId),
                out VfxDefinition fireballBurstChargeVfx));
            Assert.AreEqual(CharacterTestVfxIds.FireballBurstChargeDefinitionStableId, fireballBurstChargeVfx.StableName);
            Assert.AreEqual(CharacterTestVfxIds.FireballBurstChargeResourceKey, fireballBurstChargeVfx.PrefabResource);
            Assert.IsTrue(vfxDefinitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.FlameLanceImpactDefinitionId),
                out VfxDefinition flameLanceVfx));
            Assert.AreEqual(CharacterTestVfxIds.FlameLanceImpactDefinitionStableId, flameLanceVfx.StableName);
            Assert.AreEqual(CharacterTestVfxIds.FlameLanceImpactResourceKey, flameLanceVfx.PrefabResource);
            Assert.IsTrue(vfxDefinitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.GuardAuraDefinitionId),
                out VfxDefinition guardAuraVfx));
            Assert.AreEqual(CharacterTestVfxIds.GuardAuraDefinitionStableId, guardAuraVfx.StableName);
            Assert.AreEqual(CharacterTestVfxIds.GuardAuraResourceKey, guardAuraVfx.PrefabResource);
            Assert.AreEqual(CharacterTestVfxIds.GuardAuraLifetimeFrames, guardAuraVfx.DefaultLifetimeFrames);
            Assert.IsTrue(vfxDefinitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.TornadoChargeDefinitionId),
                out VfxDefinition tornadoChargeVfx));
            Assert.AreEqual(CharacterTestVfxIds.TornadoChargeDefinitionStableId, tornadoChargeVfx.StableName);
            Assert.AreEqual(CharacterTestVfxIds.TornadoChargeResourceKey, tornadoChargeVfx.PrefabResource);
            Assert.AreEqual(CharacterTestVfxIds.TornadoChargeLifetimeFrames, tornadoChargeVfx.DefaultLifetimeFrames);
            Assert.IsTrue(vfxDefinitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.TornadoImpactDefinitionId),
                out VfxDefinition tornadoImpactVfx));
            Assert.AreEqual(CharacterTestVfxIds.TornadoImpactDefinitionStableId, tornadoImpactVfx.StableName);
            Assert.AreEqual(CharacterTestVfxIds.TornadoImpactResourceKey, tornadoImpactVfx.PrefabResource);
            Assert.AreEqual(CharacterTestVfxIds.TornadoImpactLifetimeFrames, tornadoImpactVfx.DefaultLifetimeFrames);
            Assert.IsTrue(vfxDefinitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.TornadoTickDefinitionId),
                out VfxDefinition tornadoTickVfx));
            Assert.AreEqual(CharacterTestVfxIds.TornadoTickDefinitionStableId, tornadoTickVfx.StableName);
            Assert.AreEqual(CharacterTestVfxIds.TornadoTickResourceKey, tornadoTickVfx.PrefabResource);
            Assert.AreEqual(CharacterTestVfxIds.TornadoTickLifetimeFrames, tornadoTickVfx.DefaultLifetimeFrames);
            Assert.IsTrue(vfxDefinitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.BeamLanceBeamDefinitionId),
                out VfxDefinition laserLanceBeamVfx));
            Assert.AreEqual(CharacterTestVfxIds.BeamLanceBeamDefinitionStableId, laserLanceBeamVfx.StableName);
            Assert.AreEqual(CharacterTestVfxIds.BeamLanceBeamResourceKey, laserLanceBeamVfx.PrefabResource);
            Assert.AreEqual(CharacterTestVfxIds.BeamLanceBeamLifetimeFrames, laserLanceBeamVfx.DefaultLifetimeFrames);
            Assert.IsTrue(vfxDefinitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.LaserLanceImpactDefinitionId),
                out VfxDefinition laserLanceImpactVfx));
            Assert.AreEqual(CharacterTestVfxIds.LaserLanceImpactDefinitionStableId, laserLanceImpactVfx.StableName);
            Assert.AreEqual(CharacterTestVfxIds.LaserLanceImpactResourceKey, laserLanceImpactVfx.PrefabResource);
            Assert.AreEqual(CharacterTestVfxIds.LaserLanceImpactLifetimeFrames, laserLanceImpactVfx.DefaultLifetimeFrames);
            Assert.IsTrue(vfxDefinitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.LaserLanceChargeDefinitionId),
                out VfxDefinition laserLanceChargeVfx));
            Assert.AreEqual(CharacterTestVfxIds.LaserLanceChargeDefinitionStableId, laserLanceChargeVfx.StableName);
            Assert.AreEqual(CharacterTestVfxIds.LaserLanceChargeResourceKey, laserLanceChargeVfx.PrefabResource);
            Assert.AreEqual(CharacterTestVfxIds.LaserLanceChargeLifetimeFrames, laserLanceChargeVfx.DefaultLifetimeFrames);
            Assert.IsTrue(vfxDefinitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.ChannelBeamChargeDefinitionId),
                out VfxDefinition channelBeamChargeVfx));
            Assert.AreEqual(CharacterTestVfxIds.ChannelBeamChargeDefinitionStableId, channelBeamChargeVfx.StableName);
            Assert.AreEqual(CharacterTestVfxIds.ChannelBeamChargeResourceKey, channelBeamChargeVfx.PrefabResource);
            Assert.AreEqual(CharacterTestVfxIds.ChannelBeamChargeLifetimeFrames, channelBeamChargeVfx.DefaultLifetimeFrames);
            Assert.IsTrue(vfxDefinitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.ChannelBeamBeamDefinitionId),
                out VfxDefinition channelBeamBeamVfx));
            Assert.AreEqual(CharacterTestVfxIds.ChannelBeamBeamDefinitionStableId, channelBeamBeamVfx.StableName);
            Assert.AreEqual(CharacterTestVfxIds.ChannelBeamBeamResourceKey, channelBeamBeamVfx.PrefabResource);
            Assert.AreEqual(CharacterTestVfxIds.ChannelBeamBeamLifetimeFrames, channelBeamBeamVfx.DefaultLifetimeFrames);
            Assert.IsTrue(vfxDefinitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.ChannelBeamImpactDefinitionId),
                out VfxDefinition channelBeamImpactVfx));
            Assert.AreEqual(CharacterTestVfxIds.ChannelBeamImpactDefinitionStableId, channelBeamImpactVfx.StableName);
            Assert.AreEqual(CharacterTestVfxIds.ChannelBeamImpactResourceKey, channelBeamImpactVfx.PrefabResource);
            Assert.AreEqual(CharacterTestVfxIds.ChannelBeamImpactLifetimeFrames, channelBeamImpactVfx.DefaultLifetimeFrames);
            Assert.IsTrue(vfxDefinitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.ChainLightningChargeDefinitionId),
                out VfxDefinition chainLightningChargeVfx));
            Assert.AreEqual(CharacterTestVfxIds.ChainLightningChargeDefinitionStableId, chainLightningChargeVfx.StableName);
            Assert.AreEqual(CharacterTestVfxIds.ChainLightningChargeResourceKey, chainLightningChargeVfx.PrefabResource);
            Assert.AreEqual(CharacterTestVfxIds.ChainLightningChargeLifetimeFrames, chainLightningChargeVfx.DefaultLifetimeFrames);
            Assert.IsTrue(vfxDefinitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.ChainLightningArcDefinitionId),
                out VfxDefinition chainLightningArcVfx));
            Assert.AreEqual(CharacterTestVfxIds.ChainLightningArcDefinitionStableId, chainLightningArcVfx.StableName);
            Assert.AreEqual(CharacterTestVfxIds.ChainLightningArcResourceKey, chainLightningArcVfx.PrefabResource);
            Assert.AreEqual(CharacterTestVfxIds.ChainLightningArcLifetimeFrames, chainLightningArcVfx.DefaultLifetimeFrames);
            Assert.IsTrue(vfxDefinitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.ChainLightningImpactDefinitionId),
                out VfxDefinition chainLightningImpactVfx));
            Assert.AreEqual(CharacterTestVfxIds.ChainLightningImpactDefinitionStableId, chainLightningImpactVfx.StableName);
            Assert.AreEqual(CharacterTestVfxIds.ChainLightningImpactResourceKey, chainLightningImpactVfx.PrefabResource);
            Assert.AreEqual(CharacterTestVfxIds.ChainLightningImpactLifetimeFrames, chainLightningImpactVfx.DefaultLifetimeFrames);
            Assert.IsTrue(vfxDefinitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.PoisonBottleShatterDefinitionId),
                out VfxDefinition poisonBottleShatterVfx));
            Assert.AreEqual(CharacterTestVfxIds.PoisonBottleShatterDefinitionStableId, poisonBottleShatterVfx.StableName);
            Assert.AreEqual(CharacterTestVfxIds.PoisonBottleShatterResourceKey, poisonBottleShatterVfx.PrefabResource);
            Assert.AreEqual(CharacterTestVfxIds.PoisonBottleShatterLifetimeFrames, poisonBottleShatterVfx.DefaultLifetimeFrames);
            Assert.IsTrue(vfxDefinitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.PoisonBottlePoolDefinitionId),
                out VfxDefinition poisonBottlePoolVfx));
            Assert.AreEqual(CharacterTestVfxIds.PoisonBottlePoolDefinitionStableId, poisonBottlePoolVfx.StableName);
            Assert.AreEqual(CharacterTestVfxIds.PoisonBottlePoolResourceKey, poisonBottlePoolVfx.PrefabResource);
            Assert.AreEqual(CharacterTestVfxIds.PoisonBottlePoolLifetimeFrames, poisonBottlePoolVfx.DefaultLifetimeFrames);
            Assert.IsTrue(vfxDefinitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.PoisonedTickDefinitionId),
                out VfxDefinition poisonedTickVfx));
            Assert.AreEqual(CharacterTestVfxIds.PoisonedTickDefinitionStableId, poisonedTickVfx.StableName);
            Assert.AreEqual(CharacterTestVfxIds.PoisonedTickResourceKey, poisonedTickVfx.PrefabResource);
            Assert.AreEqual(CharacterTestVfxIds.PoisonedTickLifetimeFrames, poisonedTickVfx.DefaultLifetimeFrames);
            Assert.IsTrue(vfxDefinitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.ArcMineFlightDefinitionId),
                out VfxDefinition arcMineFlightVfx));
            Assert.AreEqual(CharacterTestVfxIds.ArcMineFlightDefinitionStableId, arcMineFlightVfx.StableName);
            Assert.AreEqual(CharacterTestVfxIds.ArcMineFlightResourceKey, arcMineFlightVfx.PrefabResource);
            Assert.AreEqual(CharacterTestVfxIds.ArcMineFlightLifetimeFrames, arcMineFlightVfx.DefaultLifetimeFrames);
            Assert.IsTrue(vfxDefinitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.ArcMineDeployDefinitionId),
                out VfxDefinition arcMineDeployVfx));
            Assert.AreEqual(CharacterTestVfxIds.ArcMineDeployDefinitionStableId, arcMineDeployVfx.StableName);
            Assert.AreEqual(CharacterTestVfxIds.ArcMineDeployResourceKey, arcMineDeployVfx.PrefabResource);
            Assert.AreEqual(CharacterTestVfxIds.ArcMineDeployLifetimeFrames, arcMineDeployVfx.DefaultLifetimeFrames);
            Assert.IsTrue(vfxDefinitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.ArcMineFuseDefinitionId),
                out VfxDefinition arcMineFuseVfx));
            Assert.AreEqual(CharacterTestVfxIds.ArcMineFuseDefinitionStableId, arcMineFuseVfx.StableName);
            Assert.AreEqual(CharacterTestVfxIds.ArcMineFuseResourceKey, arcMineFuseVfx.PrefabResource);
            Assert.AreEqual(CharacterTestVfxIds.ArcMineFuseLifetimeFrames, arcMineFuseVfx.DefaultLifetimeFrames);
            Assert.IsTrue(vfxDefinitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.ArcMineTriggerDefinitionId),
                out VfxDefinition arcMineTriggerVfx));
            Assert.AreEqual(CharacterTestVfxIds.ArcMineTriggerDefinitionStableId, arcMineTriggerVfx.StableName);
            Assert.AreEqual(CharacterTestVfxIds.ArcMineTriggerResourceKey, arcMineTriggerVfx.PrefabResource);
            Assert.AreEqual(CharacterTestVfxIds.ArcMineTriggerLifetimeFrames, arcMineTriggerVfx.DefaultLifetimeFrames);

            AbilityEffectTimelineRegistry timelines = CharacterTestAbilityEffectTimelineSource.CreateRegistry();
            Assert.IsTrue(timelines.TryGet(
                CharacterTestAbilityEffectTimelineSource.FireballExplosionTimelineId,
                out AbilityEffectTimelineDefinition fireballExplosionTimeline));
            Assert.AreEqual(121, fireballExplosionTimeline.DurationFrames);
            Assert.AreEqual(3, fireballExplosionTimeline.Events.Count);
            AssertTimelineEvent(
                fireballExplosionTimeline,
                frame: 0,
                AbilityEffectTimelineTrackKind.Presentation,
                AbilityEffectTimelineEventKind.SpawnVfx,
                AbilityEffectTimelineShapeKind.None);
            AssertTimelineEvent(
                fireballExplosionTimeline,
                frame: 120,
                AbilityEffectTimelineTrackKind.Combat,
                AbilityEffectTimelineEventKind.AreaPulse,
                AbilityEffectTimelineShapeKind.Sphere);
            AssertTimelineEvent(
                fireballExplosionTimeline,
                frame: 120,
                AbilityEffectTimelineTrackKind.Presentation,
                AbilityEffectTimelineEventKind.SpawnVfx,
                AbilityEffectTimelineShapeKind.None);

            VfxTriggerMapResolver triggerResolver = CharacterTestVfxDefinitionSource.CreateTriggerResolver();
            int triggerId = CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestCharacterActionConfigSource.FireballPresentationProfileId);
            var requests = new List<VfxSpawnRequest>();
            int resolved = triggerResolver.Resolve(
                new VfxTriggerContext(
                    new RuntimeFrame(6),
                    VfxSourceDomains.CombatHit,
                    triggerId,
                    sourceId: 1,
                    targetId: 2,
                    sourceFrame: 6,
                    localFrame: 6,
                    sourceOrder: 0,
                    VfxAnchor.World(VfxPose.Identity),
                    VfxParameterBlock.Empty,
                    new RuntimeTraceIdentity(932043)),
                requests);

            Assert.AreEqual(1, resolved);
            Assert.AreEqual(1, requests.Count);
            Assert.AreEqual(CharacterTestVfxIds.FireballDefinitionId, requests[0].DefinitionId.Value);
            Assert.AreEqual(CharacterTestVfxIds.FireballLifetimeFrames, requests[0].LifetimeFramesOverride);
            int flameTriggerId = CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestCharacterActionConfigSource.FlameLancePresentationProfileId);
            requests.Clear();
            int flameResolved = triggerResolver.Resolve(
                new VfxTriggerContext(
                    new RuntimeFrame(6),
                    VfxSourceDomains.CombatHit,
                    flameTriggerId,
                    sourceId: 1,
                    targetId: 2,
                    sourceFrame: 6,
                    localFrame: 6,
                    sourceOrder: 0,
                    VfxAnchor.World(VfxPose.Identity),
                    VfxParameterBlock.Empty,
                    new RuntimeTraceIdentity(959458)),
                requests);
            Assert.AreEqual(1, flameResolved);
            Assert.AreEqual(CharacterTestVfxIds.FlameLanceImpactDefinitionId, requests[0].DefinitionId.Value);
            Assert.AreEqual(CharacterTestVfxIds.FlameLanceImpactLifetimeFrames, requests[0].LifetimeFramesOverride);
            int laserTriggerId = CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestCharacterActionConfigSource.LaserLancePresentationProfileId);
            requests.Clear();
            int laserResolved = triggerResolver.Resolve(
                new VfxTriggerContext(
                    new RuntimeFrame(6),
                    VfxSourceDomains.CombatHit,
                    laserTriggerId,
                    sourceId: 1,
                    targetId: 2,
                    sourceFrame: 6,
                    localFrame: 6,
                    sourceOrder: 0,
                    VfxAnchor.World(VfxPose.Identity),
                    VfxParameterBlock.Empty,
                    new RuntimeTraceIdentity(932769)),
                requests);
            Assert.AreEqual(1, laserResolved);
            Assert.AreEqual(CharacterTestVfxIds.LaserLanceImpactDefinitionId, requests[0].DefinitionId.Value);
            Assert.AreEqual(CharacterTestVfxIds.LaserLanceImpactLifetimeFrames, requests[0].LifetimeFramesOverride);
            int tornadoTriggerId = CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestCharacterActionConfigSource.TornadoPresentationProfileId);
            requests.Clear();
            int tornadoResolved = triggerResolver.Resolve(
                new VfxTriggerContext(
                    new RuntimeFrame(6),
                    VfxSourceDomains.CombatHit,
                    tornadoTriggerId,
                    sourceId: 1,
                    targetId: 2,
                    sourceFrame: 6,
                    localFrame: 6,
                    sourceOrder: 0,
                    VfxAnchor.World(VfxPose.Identity),
                    VfxParameterBlock.Empty,
                    new RuntimeTraceIdentity(941497)),
                requests);
            Assert.AreEqual(1, tornadoResolved);
            Assert.AreEqual(CharacterTestVfxIds.TornadoImpactDefinitionId, requests[0].DefinitionId.Value);
            Assert.AreEqual(CharacterTestVfxIds.TornadoImpactLifetimeFrames, requests[0].LifetimeFramesOverride);
            int tornadoTickTriggerId = CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestCharacterActionConfigSource.TornadoTickPresentationProfileId);
            requests.Clear();
            int tornadoTickResolved = triggerResolver.Resolve(
                new VfxTriggerContext(
                    new RuntimeFrame(6),
                    VfxSourceDomains.CombatHit,
                    tornadoTickTriggerId,
                    sourceId: 1,
                    targetId: 2,
                    sourceFrame: 6,
                    localFrame: 6,
                    sourceOrder: 0,
                    VfxAnchor.World(VfxPose.Identity),
                    VfxParameterBlock.Empty,
                    new RuntimeTraceIdentity(992360)),
                requests);
            Assert.AreEqual(1, tornadoTickResolved);
            Assert.AreEqual(CharacterTestVfxIds.TornadoTickDefinitionId, requests[0].DefinitionId.Value);
            Assert.AreEqual(CharacterTestVfxIds.TornadoTickLifetimeFrames, requests[0].LifetimeFramesOverride);
            int chainLightningTriggerId = CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestCharacterActionConfigSource.ChainLightningPresentationProfileId);
            requests.Clear();
            int chainLightningResolved = triggerResolver.Resolve(
                new VfxTriggerContext(
                    new RuntimeFrame(30),
                    VfxSourceDomains.CombatHit,
                    chainLightningTriggerId,
                    sourceId: 1,
                    targetId: 2,
                    sourceFrame: 30,
                    localFrame: 30,
                    sourceOrder: 0,
                    VfxAnchor.World(VfxPose.Identity),
                    VfxParameterBlock.Empty,
                    new RuntimeTraceIdentity(957228)),
                requests);
            Assert.AreEqual(1, chainLightningResolved);
            Assert.AreEqual(CharacterTestVfxIds.ChainLightningImpactDefinitionId, requests[0].DefinitionId.Value);
            Assert.AreEqual(CharacterTestVfxIds.ChainLightningImpactLifetimeFrames, requests[0].LifetimeFramesOverride);

            CombatProjectileDefinitionRegistry projectileDefinitions =
                CharacterTestFireballProjectileAdapter.CreateRegistry();
            CombatProjectileSpawnProfileRegistry spawnProfiles =
                CharacterTestFireballProjectileAdapter.CreateSpawnProfileRegistry();
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveSpawnProfile(
                spawnProfiles,
                CharacterTestCharacterActionConfigSource.FireballProjectileRequestId,
                out CombatProjectileSpawnProfile fireballSpawnProfile));
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballProjectileRequestId,
                fireballSpawnProfile.ProfileId);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.DefinitionId, fireballSpawnProfile.ProjectileDefinitionId);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.StableId, fireballSpawnProfile.ProjectileStableId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D400_Id12, fireballSpawnProfile.Origin.SocketId);
            Assert.AreEqual(CombatProjectileAimKind.LockedTargetOrCasterForwardRange, fireballSpawnProfile.Aim.Kind);
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveSpawnProfile(
                spawnProfiles,
                CharacterTestCharacterActionConfigSource.FireballBurstProjectileRequestId,
                out CombatProjectileSpawnProfile burstSpawnProfile));
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballBurstProjectileRequestId,
                burstSpawnProfile.ProfileId);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.BurstDefinitionId, burstSpawnProfile.ProjectileDefinitionId);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.BurstStableId, burstSpawnProfile.ProjectileStableId);
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveSpawnProfile(
                spawnProfiles,
                CharacterTestCharacterActionConfigSource.FlameLanceProjectileRequestId,
                out CombatProjectileSpawnProfile flameSpawnProfile));
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FlameLanceProjectileRequestId,
                flameSpawnProfile.ProfileId);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FlameLanceDefinitionId, flameSpawnProfile.ProjectileDefinitionId);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FlameLanceStableId, flameSpawnProfile.ProjectileStableId);
            Assert.AreEqual(CombatProjectileAimKind.LockedTargetOrCasterForwardRange, flameSpawnProfile.Aim.Kind);
            Assert.AreEqual(CombatProjectileAimKind.CasterForwardRange, flameSpawnProfile.Aim.FallbackKind);
            Assert.AreEqual(Fix64.FromInt(44), flameSpawnProfile.Aim.MaxRange);
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveSpawnProfile(
                spawnProfiles,
                CharacterTestCharacterActionConfigSource.FireballLobProjectileRequestId,
                out CombatProjectileSpawnProfile lobSpawnProfile));
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballLobProjectileRequestId,
                lobSpawnProfile.ProfileId);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FireballLobDefinitionId, lobSpawnProfile.ProjectileDefinitionId);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FireballLobStableId, lobSpawnProfile.ProjectileStableId);
            Assert.AreEqual(CombatProjectileAimKind.LockedTargetOrCasterForwardRange, lobSpawnProfile.Aim.Kind);
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveSpawnProfile(
                spawnProfiles,
                CharacterTestCharacterActionConfigSource.FireballSurgeProjectileRequestId,
                out CombatProjectileSpawnProfile surgeSpawnProfile));
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballSurgeProjectileRequestId,
                surgeSpawnProfile.ProfileId);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FireballSurgeDefinitionId, surgeSpawnProfile.ProjectileDefinitionId);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FireballSurgeStableId, surgeSpawnProfile.ProjectileStableId);
            Assert.AreEqual(CombatProjectileAimKind.CasterForwardRange, surgeSpawnProfile.Aim.Kind);
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveSpawnProfile(
                spawnProfiles,
                CharacterTestCharacterActionConfigSource.FireballReturnProjectileRequestId,
                out CombatProjectileSpawnProfile returnSpawnProfile));
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballReturnProjectileRequestId,
                returnSpawnProfile.ProfileId);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FireballReturnDefinitionId, returnSpawnProfile.ProjectileDefinitionId);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FireballReturnStableId, returnSpawnProfile.ProjectileStableId);
            Assert.AreEqual(CombatProjectileAimKind.CasterForwardRange, returnSpawnProfile.Aim.Kind);
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveSpawnProfile(
                spawnProfiles,
                CharacterTestCharacterActionConfigSource.FireballSwayProjectileRequestId,
                out CombatProjectileSpawnProfile swaySpawnProfile));
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballSwayProjectileRequestId,
                swaySpawnProfile.ProfileId);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FireballSwayDefinitionId, swaySpawnProfile.ProjectileDefinitionId);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FireballSwayStableId, swaySpawnProfile.ProjectileStableId);
            Assert.AreEqual(CombatProjectileAimKind.CasterForwardRange, swaySpawnProfile.Aim.Kind);
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveSpawnProfile(
                spawnProfiles,
                CharacterTestCharacterActionConfigSource.TornadoProjectileRequestId,
                out CombatProjectileSpawnProfile tornadoSpawnProfile));
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.TornadoProjectileRequestId,
                tornadoSpawnProfile.ProfileId);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.TornadoDefinitionId, tornadoSpawnProfile.ProjectileDefinitionId);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.TornadoStableId, tornadoSpawnProfile.ProjectileStableId);
            Assert.AreEqual(CombatProjectileAimKind.CasterForwardRange, tornadoSpawnProfile.Aim.Kind);
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveSpawnProfile(
                spawnProfiles,
                CharacterTestCharacterActionConfigSource.FireballHomingProjectileRequestId,
                out CombatProjectileSpawnProfile homingSpawnProfile));
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballHomingProjectileRequestId,
                homingSpawnProfile.ProfileId);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FireballHomingDefinitionId, homingSpawnProfile.ProjectileDefinitionId);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FireballHomingStableId, homingSpawnProfile.ProjectileStableId);
            Assert.AreEqual(CombatProjectileAimKind.LockedTargetOrCasterForwardRange, homingSpawnProfile.Aim.Kind);
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveSpawnProfile(
                spawnProfiles,
                CharacterTestCharacterActionConfigSource.FireballBezierProjectileRequestId,
                out CombatProjectileSpawnProfile bezierSpawnProfile));
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballBezierProjectileRequestId,
                bezierSpawnProfile.ProfileId);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FireballBezierDefinitionId, bezierSpawnProfile.ProjectileDefinitionId);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FireballBezierStableId, bezierSpawnProfile.ProjectileStableId);
            Assert.AreEqual(CombatProjectileAimKind.LockedTargetOrCasterForwardRange, bezierSpawnProfile.Aim.Kind);
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveSpawnProfile(
                spawnProfiles,
                CharacterTestCharacterActionConfigSource.FireballExplosionProjectileRequestId,
                out CombatProjectileSpawnProfile explosionSpawnProfile));
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballExplosionProjectileRequestId,
                explosionSpawnProfile.ProfileId);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FireballExplosionDefinitionId, explosionSpawnProfile.ProjectileDefinitionId);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FireballExplosionStableId, explosionSpawnProfile.ProjectileStableId);
            Assert.AreEqual(CombatProjectileAimKind.CasterForwardRange, explosionSpawnProfile.Aim.Kind);
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveDefinition(
                projectileDefinitions,
                CharacterTestCharacterActionConfigSource.FireballProjectileRequestId,
                out CombatProjectileDefinition projectileDefinition));
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.DefinitionId, projectileDefinition.DefinitionId);
            Assert.AreEqual(
                CharacterTestFireballProjectileAdapter.StableId,
                projectileDefinition.StableId);
            Assert.AreEqual(CombatProjectileSpeedProfileKind.Constant, projectileDefinition.Motion.SpeedProfileKind);
            Assert.AreEqual(Fix64.FromRatio(35, 100), projectileDefinition.Motion.SpeedPerFrame);
            Assert.AreEqual(90, projectileDefinition.Motion.LifetimeFrames);
            Assert.AreEqual(Fix64.FromRatio(25, 100), projectileDefinition.Collision.Radius);
            Assert.AreEqual(256, projectileDefinition.Collision.LayerMask.Value);
            Assert.IsTrue(projectileDefinition.Collision.StopOnFirstAcceptedImpact);
            Assert.IsTrue(projectileDefinition.Collision.StopOnBlockedImpact);
            Assert.AreEqual(0, projectileDefinition.Impact.PresentationProfileId);
            Assert.AreEqual(0, projectileDefinition.Impact.StableEventId);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballProjectileImpactStableEventId,
                projectileDefinition.Impact.HitEvent.StableEventId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D409_Id3, projectileDefinition.Impact.HitEvent.Damage.DamageTypeId);
            Assert.AreEqual(10, projectileDefinition.Impact.HitEvent.Damage.BaseDamage);
            Assert.AreEqual(8, projectileDefinition.Impact.HitEvent.Damage.ImpactPower);
            Assert.AreEqual(CombatProjectileImpactPolicyKind.DirectHit, projectileDefinition.Impact.PolicyKind);
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveDefinition(
                projectileDefinitions,
                CharacterTestCharacterActionConfigSource.FireballBurstProjectileRequestId,
                out CombatProjectileDefinition burstProjectileDefinition));
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.BurstDefinitionId, burstProjectileDefinition.DefinitionId);
            Assert.AreEqual(CombatProjectileImpactPolicyKind.AreaPulseOnImpact, burstProjectileDefinition.Impact.PolicyKind);
            Assert.AreEqual(Fix64.FromInt(2), burstProjectileDefinition.Impact.AreaRadius);
            Assert.AreEqual(256, burstProjectileDefinition.Impact.AreaLayerMask.Value);
            Assert.IsTrue(burstProjectileDefinition.Impact.TriggerOnExpire);
            Assert.AreEqual(0, burstProjectileDefinition.Impact.PresentationProfileId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D409_Id3, burstProjectileDefinition.Impact.HitEvent.Damage.DamageTypeId);
            Assert.AreEqual(8, burstProjectileDefinition.Impact.HitEvent.Damage.BaseDamage);
            Assert.AreEqual(6, burstProjectileDefinition.Impact.HitEvent.Damage.ImpactPower);
            Assert.AreEqual(
                burstProjectileDefinition.Impact.HitEvent.Damage,
                burstProjectileDefinition.Impact.AreaHitEvent.Damage);
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveDefinition(
                projectileDefinitions,
                CharacterTestCharacterActionConfigSource.FlameLanceProjectileRequestId,
                out CombatProjectileDefinition flameLanceProjectileDefinition));
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FlameLanceDefinitionId, flameLanceProjectileDefinition.DefinitionId);
            Assert.AreEqual(CombatProjectileImpactPolicyKind.DirectHit, flameLanceProjectileDefinition.Impact.PolicyKind);
            Assert.AreEqual(Fix64.FromRatio(55, 100), flameLanceProjectileDefinition.Motion.SpeedPerFrame);
            Assert.AreEqual(Fix64.FromRatio(15, 100), flameLanceProjectileDefinition.Collision.Radius);
            Assert.IsFalse(flameLanceProjectileDefinition.Collision.StopOnFirstAcceptedImpact);
            Assert.AreEqual(3, flameLanceProjectileDefinition.Collision.MaxAcceptedImpacts);
            Assert.AreEqual(
                CombatProjectileHitSameTargetPolicy.OncePerProjectile,
                flameLanceProjectileDefinition.Collision.HitSameTargetPolicy);
            Assert.AreEqual(0, flameLanceProjectileDefinition.Impact.PresentationProfileId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D409_Id3, flameLanceProjectileDefinition.Impact.HitEvent.Damage.DamageTypeId);
            Assert.AreEqual(7, flameLanceProjectileDefinition.Impact.HitEvent.Damage.BaseDamage);
            Assert.AreEqual(5, flameLanceProjectileDefinition.Impact.HitEvent.Damage.ImpactPower);
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveDefinition(
                projectileDefinitions,
                CharacterTestCharacterActionConfigSource.FireballLobProjectileRequestId,
                out CombatProjectileDefinition lobProjectileDefinition));
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FireballLobDefinitionId, lobProjectileDefinition.DefinitionId);
            Assert.AreEqual(CombatProjectileMotionKind.ArcToTarget, lobProjectileDefinition.Motion.Kind);
            Assert.AreEqual(CombatProjectileSpeedProfileKind.Constant, lobProjectileDefinition.Motion.SpeedProfileKind);
            Assert.AreEqual(Fix64.FromInt(4), lobProjectileDefinition.Motion.ArcHeight);
            Assert.AreEqual(CombatProjectileImpactPolicyKind.AreaPulseOnImpact, lobProjectileDefinition.Impact.PolicyKind);
            Assert.AreEqual(0, lobProjectileDefinition.Impact.PresentationProfileId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D409_Id3, lobProjectileDefinition.Impact.HitEvent.Damage.DamageTypeId);
            Assert.AreEqual(8, lobProjectileDefinition.Impact.HitEvent.Damage.BaseDamage);
            Assert.AreEqual(6, lobProjectileDefinition.Impact.HitEvent.Damage.ImpactPower);
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveDefinition(
                projectileDefinitions,
                CharacterTestCharacterActionConfigSource.FireballSurgeProjectileRequestId,
                out CombatProjectileDefinition surgeProjectileDefinition));
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FireballSurgeDefinitionId, surgeProjectileDefinition.DefinitionId);
            Assert.AreEqual(CombatProjectileMotionKind.Linear, surgeProjectileDefinition.Motion.Kind);
            Assert.AreEqual(CombatProjectileSpeedProfileKind.EaseInQuadratic, surgeProjectileDefinition.Motion.SpeedProfileKind);
            Assert.AreEqual(CombatProjectileImpactPolicyKind.DirectHit, surgeProjectileDefinition.Impact.PolicyKind);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D409_Id3, surgeProjectileDefinition.Impact.HitEvent.Damage.DamageTypeId);
            Assert.AreEqual(10, surgeProjectileDefinition.Impact.HitEvent.Damage.BaseDamage);
            Assert.AreEqual(0, surgeProjectileDefinition.Impact.PresentationProfileId);
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveDefinition(
                projectileDefinitions,
                CharacterTestCharacterActionConfigSource.FireballReturnProjectileRequestId,
                out CombatProjectileDefinition returnProjectileDefinition));
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FireballReturnDefinitionId, returnProjectileDefinition.DefinitionId);
            Assert.AreEqual(CombatProjectileMotionKind.BoomerangReturn, returnProjectileDefinition.Motion.Kind);
            Assert.AreEqual(CombatProjectileSpeedProfileKind.Constant, returnProjectileDefinition.Motion.SpeedProfileKind);
            Assert.IsFalse(returnProjectileDefinition.Collision.StopOnFirstAcceptedImpact);
            Assert.AreEqual(2, returnProjectileDefinition.Collision.MaxAcceptedImpacts);
            Assert.AreEqual(
                CombatProjectileHitSameTargetPolicy.OncePerProjectile,
                returnProjectileDefinition.Collision.HitSameTargetPolicy);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D409_Id3, returnProjectileDefinition.Impact.HitEvent.Damage.DamageTypeId);
            Assert.AreEqual(10, returnProjectileDefinition.Impact.HitEvent.Damage.BaseDamage);
            Assert.AreEqual(0, returnProjectileDefinition.Impact.PresentationProfileId);
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveDefinition(
                projectileDefinitions,
                CharacterTestCharacterActionConfigSource.FireballSwayProjectileRequestId,
                out CombatProjectileDefinition swayProjectileDefinition));
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FireballSwayDefinitionId, swayProjectileDefinition.DefinitionId);
            Assert.AreEqual(CombatProjectileMotionKind.SwayForward, swayProjectileDefinition.Motion.Kind);
            Assert.AreEqual(Fix64.FromRatio(75, 100), swayProjectileDefinition.Motion.LateralAmplitude);
            Assert.AreEqual(2, swayProjectileDefinition.Motion.LateralCycles);
            Assert.AreEqual(CombatProjectileSpeedProfileKind.Constant, swayProjectileDefinition.Motion.SpeedProfileKind);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D409_Id3, swayProjectileDefinition.Impact.HitEvent.Damage.DamageTypeId);
            Assert.AreEqual(10, swayProjectileDefinition.Impact.HitEvent.Damage.BaseDamage);
            Assert.AreEqual(0, swayProjectileDefinition.Impact.PresentationProfileId);
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveDefinition(
                projectileDefinitions,
                CharacterTestCharacterActionConfigSource.TornadoProjectileRequestId,
                out CombatProjectileDefinition tornadoProjectileDefinition));
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.TornadoDefinitionId, tornadoProjectileDefinition.DefinitionId);
            Assert.AreEqual(CombatProjectileMotionKind.SwayForward, tornadoProjectileDefinition.Motion.Kind);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.TornadoSpeedPerFrame, tornadoProjectileDefinition.Motion.SpeedPerFrame);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.TornadoLifetimeFrames, tornadoProjectileDefinition.Motion.LifetimeFrames);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.TornadoLateralAmplitude, tornadoProjectileDefinition.Motion.LateralAmplitude);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.TornadoLateralCycles, tornadoProjectileDefinition.Motion.LateralCycles);
            Assert.AreEqual(
                CharacterTestInteractionFieldProfiles.TornadoAttractionRadius,
                tornadoProjectileDefinition.Collision.Radius);
            Assert.AreEqual(0, tornadoProjectileDefinition.Collision.LayerMask.Value);
            Assert.AreEqual(CombatProjectileImpactPolicyKind.AreaPulseOnImpact, tornadoProjectileDefinition.Impact.PolicyKind);
            Assert.AreEqual(Fix64.FromRatio(5, 2), tornadoProjectileDefinition.Impact.AreaRadius);
            Assert.AreEqual(256, tornadoProjectileDefinition.Impact.AreaLayerMask.Value);
            Assert.IsTrue(tornadoProjectileDefinition.Impact.TriggerOnExpire);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D409_Id8, tornadoProjectileDefinition.Impact.HitEvent.Damage.DamageTypeId);
            Assert.AreEqual(7, tornadoProjectileDefinition.Impact.HitEvent.Damage.BaseDamage);
            Assert.AreEqual(10, tornadoProjectileDefinition.Impact.HitEvent.Damage.ImpactPower);
            Assert.AreEqual(
                tornadoProjectileDefinition.Impact.HitEvent.Damage,
                tornadoProjectileDefinition.Impact.AreaHitEvent.Damage);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.TornadoProjectileImpactStableEventId,
                tornadoProjectileDefinition.Impact.AreaHitEvent.StableEventId);
            Assert.AreEqual(0, tornadoProjectileDefinition.Impact.PresentationProfileId);
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveDefinition(
                projectileDefinitions,
                CharacterTestCharacterActionConfigSource.PoisonBottleProjectileRequestId,
                out CombatProjectileDefinition poisonBottleProjectileDefinition));
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.PoisonBottleDefinitionId, poisonBottleProjectileDefinition.DefinitionId);
            Assert.AreEqual(CombatProjectileMotionKind.BallisticArcToTarget, poisonBottleProjectileDefinition.Motion.Kind);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.PoisonBottleSpeedPerFrame, poisonBottleProjectileDefinition.Motion.SpeedPerFrame);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.PoisonBottleLifetimeFrames, poisonBottleProjectileDefinition.Motion.LifetimeFrames);
            Assert.AreEqual(Fix64.Zero, poisonBottleProjectileDefinition.Motion.ArcHeight);
            Assert.AreEqual(
                CharacterTestFireballProjectileAdapter.PoisonBottleGravityPerFrameSquared,
                poisonBottleProjectileDefinition.Motion.GravityPerFrameSquared);
            Assert.AreEqual(Fix64.Zero, poisonBottleProjectileDefinition.Motion.MaxLaunchAngleTangent);
            Assert.AreEqual(CombatProjectileImpactPolicyKind.DirectHit, poisonBottleProjectileDefinition.Impact.PolicyKind);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.PoisonBottleProjectileImpactStableEventId,
                poisonBottleProjectileDefinition.Impact.HitEvent.StableEventId);
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveDefinition(
                projectileDefinitions,
                CharacterTestCharacterActionConfigSource.BeamLanceProjectileRequestId,
                out CombatProjectileDefinition beamLanceProjectileDefinition));
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.BeamLanceDefinitionId, beamLanceProjectileDefinition.DefinitionId);
            Assert.AreEqual(CombatProjectileMotionKind.Linear, beamLanceProjectileDefinition.Motion.Kind);
            Assert.AreEqual(Fix64.FromInt(44), beamLanceProjectileDefinition.Motion.SpeedPerFrame);
            Assert.AreEqual(1, beamLanceProjectileDefinition.Motion.LifetimeFrames);
            Assert.AreEqual(Fix64.FromRatio(20, 100), beamLanceProjectileDefinition.Collision.Radius);
            Assert.AreEqual(256, beamLanceProjectileDefinition.Collision.LayerMask.Value);
            Assert.IsFalse(beamLanceProjectileDefinition.Collision.StopOnFirstAcceptedImpact);
            Assert.AreEqual(5, beamLanceProjectileDefinition.Collision.MaxAcceptedImpacts);
            Assert.AreEqual(
                CombatProjectileHitSameTargetPolicy.OncePerProjectile,
                beamLanceProjectileDefinition.Collision.HitSameTargetPolicy);
            Assert.AreEqual(CombatProjectileImpactPolicyKind.DirectHit, beamLanceProjectileDefinition.Impact.PolicyKind);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.BeamLanceProjectileImpactStableEventId,
                beamLanceProjectileDefinition.Impact.HitEvent.StableEventId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D409_Id9, beamLanceProjectileDefinition.Impact.HitEvent.Damage.DamageTypeId);
            Assert.AreEqual(8, beamLanceProjectileDefinition.Impact.HitEvent.Damage.BaseDamage);
            Assert.AreEqual(4, beamLanceProjectileDefinition.Impact.HitEvent.Damage.ImpactPower);
            Assert.AreEqual(1, beamLanceProjectileDefinition.Impact.HitEvent.OnHitBuffCount);
            Assert.AreEqual(0, beamLanceProjectileDefinition.Impact.PresentationProfileId);
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveDefinition(
                projectileDefinitions,
                CharacterTestCharacterActionConfigSource.LaserLanceProjectileRequestId,
                out CombatProjectileDefinition laserLanceProjectileDefinition));
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.LaserLanceDefinitionId, laserLanceProjectileDefinition.DefinitionId);
            Assert.AreEqual(CombatProjectileMotionKind.Linear, laserLanceProjectileDefinition.Motion.Kind);
            Assert.AreEqual(Fix64.FromInt(44), laserLanceProjectileDefinition.Motion.SpeedPerFrame);
            Assert.AreEqual(1, laserLanceProjectileDefinition.Motion.LifetimeFrames);
            Assert.AreEqual(Fix64.FromRatio(6, 100), laserLanceProjectileDefinition.Collision.Radius);
            Assert.IsTrue(laserLanceProjectileDefinition.Collision.StopOnFirstAcceptedImpact);
            Assert.AreEqual(1, laserLanceProjectileDefinition.Collision.MaxAcceptedImpacts);
            Assert.AreEqual(0, laserLanceProjectileDefinition.Impact.HitEvent.OnHitBuffCount);
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveSpawnProfile(
                spawnProfiles,
                CharacterTestCharacterActionConfigSource.HookChainProjectileRequestId,
                out CombatProjectileSpawnProfile hookChainSpawnProfile));
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.HookChainProjectileRequestId,
                hookChainSpawnProfile.ProfileId);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.HookChainDefinitionId, hookChainSpawnProfile.ProjectileDefinitionId);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.HookChainStableId, hookChainSpawnProfile.ProjectileStableId);
            Assert.AreEqual(CombatProjectileAimKind.LockedTargetOrCasterForwardRange, hookChainSpawnProfile.Aim.Kind);
            Assert.AreEqual(Fix64.FromInt(12), hookChainSpawnProfile.Aim.MaxRange);
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveDefinition(
                projectileDefinitions,
                CharacterTestCharacterActionConfigSource.HookChainProjectileRequestId,
                out CombatProjectileDefinition hookChainProjectileDefinition));
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.HookChainDefinitionId, hookChainProjectileDefinition.DefinitionId);
            Assert.AreEqual(CombatProjectileMotionKind.Linear, hookChainProjectileDefinition.Motion.Kind);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.HookChainSpeedPerFrame, hookChainProjectileDefinition.Motion.SpeedPerFrame);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.HookChainLifetimeFrames, hookChainProjectileDefinition.Motion.LifetimeFrames);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.HookChainRadius, hookChainProjectileDefinition.Collision.Radius);
            Assert.IsTrue(hookChainProjectileDefinition.Collision.LayerMask.ContainsLayer(CharacterTestCharacterPhysicsRuntime.DynamicCharacterLayer));
            Assert.IsTrue(hookChainProjectileDefinition.Collision.LayerMask.ContainsLayer(CharacterTestFireballProjectileAdapter.MeteorBarrageStaticEnvironmentLayer));
            Assert.IsTrue(hookChainProjectileDefinition.Collision.StopOnFirstAcceptedImpact);
            Assert.IsTrue(hookChainProjectileDefinition.Collision.StopOnBlockedImpact);
            Assert.AreEqual(1, hookChainProjectileDefinition.Collision.MaxAcceptedImpacts);
            Assert.AreEqual(CombatProjectileImpactPolicyKind.DirectHit, hookChainProjectileDefinition.Impact.PolicyKind);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.HookChainProjectileImpactStableEventId,
                hookChainProjectileDefinition.Impact.HitEvent.StableEventId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D409_Id11, hookChainProjectileDefinition.Impact.HitEvent.Damage.DamageTypeId);
            Assert.AreEqual(1, hookChainProjectileDefinition.Impact.HitEvent.Damage.BaseDamage);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.HookChainImpactPower, hookChainProjectileDefinition.Impact.HitEvent.Damage.ImpactPower);
            Assert.AreEqual(30, CharacterTestFireballProjectileAdapter.TornadoDamageTickIntervalFrames);
            Assert.AreEqual(
                CharacterTestFireballProjectileAdapter.TornadoDamageTickRadius,
                CharacterTestFireballProjectileAdapter.TornadoTickImpactConfig.AreaRadius);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.TornadoProjectileTickStableEventId,
                CharacterTestFireballProjectileAdapter.TornadoTickImpactConfig.AreaHitEvent.StableEventId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D409_Id8, CharacterTestFireballProjectileAdapter.TornadoTickImpactConfig.AreaHitEvent.Damage.DamageTypeId);
            Assert.AreEqual(2, CharacterTestFireballProjectileAdapter.TornadoTickImpactConfig.AreaHitEvent.Damage.BaseDamage);
            Assert.AreEqual(3, CharacterTestFireballProjectileAdapter.TornadoTickImpactConfig.AreaHitEvent.Damage.ImpactPower);
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveDefinition(
                projectileDefinitions,
                CharacterTestCharacterActionConfigSource.FireballHomingProjectileRequestId,
                out CombatProjectileDefinition homingProjectileDefinition));
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FireballHomingDefinitionId, homingProjectileDefinition.DefinitionId);
            Assert.AreEqual(CombatProjectileMotionKind.HomingToTarget, homingProjectileDefinition.Motion.Kind);
            Assert.AreEqual(CombatProjectileSpeedProfileKind.Constant, homingProjectileDefinition.Motion.SpeedProfileKind);
            Assert.AreEqual(Fix64.FromRatio(15, 100), homingProjectileDefinition.Motion.HomingTurnRatePerFrame);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D409_Id3, homingProjectileDefinition.Impact.HitEvent.Damage.DamageTypeId);
            Assert.AreEqual(10, homingProjectileDefinition.Impact.HitEvent.Damage.BaseDamage);
            Assert.AreEqual(0, homingProjectileDefinition.Impact.PresentationProfileId);
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveDefinition(
                projectileDefinitions,
                CharacterTestCharacterActionConfigSource.FireballBezierProjectileRequestId,
                out CombatProjectileDefinition bezierProjectileDefinition));
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FireballBezierDefinitionId, bezierProjectileDefinition.DefinitionId);
            Assert.AreEqual(CombatProjectileMotionKind.BezierToTarget, bezierProjectileDefinition.Motion.Kind);
            Assert.AreEqual(CombatProjectileSpeedProfileKind.Constant, bezierProjectileDefinition.Motion.SpeedProfileKind);
            Assert.AreEqual(new FixVector3(Fix64.Zero, Fix64.FromInt(3), Fix64.FromInt(6)), bezierProjectileDefinition.Motion.BezierControlOffsetA);
            Assert.AreEqual(new FixVector3(Fix64.Zero, Fix64.FromInt(2), Fix64.Zero - Fix64.FromInt(4)), bezierProjectileDefinition.Motion.BezierControlOffsetB);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D409_Id3, bezierProjectileDefinition.Impact.HitEvent.Damage.DamageTypeId);
            Assert.AreEqual(10, bezierProjectileDefinition.Impact.HitEvent.Damage.BaseDamage);
            Assert.AreEqual(0, bezierProjectileDefinition.Impact.PresentationProfileId);
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveDefinition(
                projectileDefinitions,
                CharacterTestCharacterActionConfigSource.FireballExplosionProjectileRequestId,
                out CombatProjectileDefinition explosionProjectileDefinition));
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FireballExplosionDefinitionId, explosionProjectileDefinition.DefinitionId);
            Assert.AreEqual(CombatProjectileImpactPolicyKind.AreaPulseOnImpact, explosionProjectileDefinition.Impact.PolicyKind);
            Assert.AreEqual(Fix64.FromRatio(5, 2), explosionProjectileDefinition.Impact.AreaRadius);
            Assert.AreEqual(256, explosionProjectileDefinition.Impact.AreaLayerMask.Value);
            Assert.AreEqual(
                CharacterTestAbilityEffectTimelineSource.FireballExplosionTimelineId,
                explosionProjectileDefinition.Impact.EffectTimelineId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D409_Id3, explosionProjectileDefinition.Impact.HitEvent.Damage.DamageTypeId);
            Assert.AreEqual(8, explosionProjectileDefinition.Impact.HitEvent.Damage.BaseDamage);
            Assert.AreEqual(6, explosionProjectileDefinition.Impact.HitEvent.Damage.ImpactPower);
            Assert.AreEqual(0, explosionProjectileDefinition.Impact.PresentationProfileId);

            CombatProjectileDefinitionRegistry embeddedFallbackDefinitions =
                CharacterTestFireballProjectileAdapter.CreateEmbeddedFallbackRegistry();
            AssertEmbeddedProjectileImpactUsesHitEvent(
                embeddedFallbackDefinitions,
                CharacterTestCharacterActionConfigSource.FireballProjectileRequestId,
                "fire",
                10);
            AssertEmbeddedProjectileImpactUsesHitEvent(
                embeddedFallbackDefinitions,
                CharacterTestCharacterActionConfigSource.FireballBurstProjectileRequestId,
                "fire",
                8);
            AssertEmbeddedProjectileImpactUsesHitEvent(
                embeddedFallbackDefinitions,
                CharacterTestCharacterActionConfigSource.FlameLanceProjectileRequestId,
                "fire",
                7);
            AssertEmbeddedProjectileImpactUsesHitEvent(
                embeddedFallbackDefinitions,
                CharacterTestCharacterActionConfigSource.FireballLobProjectileRequestId,
                "fire",
                8);
            AssertEmbeddedProjectileImpactUsesHitEvent(
                embeddedFallbackDefinitions,
                CharacterTestCharacterActionConfigSource.FireballSurgeProjectileRequestId,
                "fire",
                10);
            AssertEmbeddedProjectileImpactUsesHitEvent(
                embeddedFallbackDefinitions,
                CharacterTestCharacterActionConfigSource.FireballReturnProjectileRequestId,
                "fire",
                10);
            AssertEmbeddedProjectileImpactUsesHitEvent(
                embeddedFallbackDefinitions,
                CharacterTestCharacterActionConfigSource.FireballSwayProjectileRequestId,
                "fire",
                10);
            AssertEmbeddedProjectileImpactUsesHitEvent(
                embeddedFallbackDefinitions,
                CharacterTestCharacterActionConfigSource.TornadoProjectileRequestId,
                "wind",
                7);
            AssertEmbeddedProjectileImpactUsesHitEvent(
                embeddedFallbackDefinitions,
                CharacterTestCharacterActionConfigSource.LaserLanceProjectileRequestId,
                "light",
                8);
            AssertEmbeddedProjectileImpactUsesHitEvent(
                embeddedFallbackDefinitions,
                CharacterTestCharacterActionConfigSource.FireballHomingProjectileRequestId,
                "fire",
                10);
            AssertEmbeddedProjectileImpactUsesHitEvent(
                embeddedFallbackDefinitions,
                CharacterTestCharacterActionConfigSource.FireballBezierProjectileRequestId,
                "fire",
                10);
            AssertEmbeddedProjectileImpactUsesHitEvent(
                embeddedFallbackDefinitions,
                CharacterTestCharacterActionConfigSource.FireballExplosionProjectileRequestId,
                "fire",
                8);

            CombatProjectileUnityPresentationRegistry projectilePresentation =
                CharacterTestProjectilePresentationSource.CreateRegistry();
            Assert.IsTrue(projectilePresentation.TryGetV2(
                CharacterTestFireballProjectileAdapter.DefinitionId,
                out CombatProjectileUnityPresentationBindingV2 binding));
            Assert.AreEqual(CharacterTestVfxIds.FireballResourceKey, binding.FlightBodyResource);
            Assert.AreEqual(
                CharacterTestProjectilePresentationSource.FireballViewNamePrefix,
                binding.ViewNamePrefix);
            Assert.IsTrue(projectilePresentation.TryGetV2(
                CharacterTestFireballProjectileAdapter.BurstDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 burstBinding));
            Assert.AreEqual(CharacterTestVfxIds.FireballBurstFlightResourceKey, burstBinding.FlightBodyResource);
            Assert.IsTrue(projectilePresentation.TryGetV2(
                CharacterTestFireballProjectileAdapter.FlameLanceDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 flameBinding));
            Assert.AreEqual(CharacterTestVfxIds.FlameLanceFlightResourceKey, flameBinding.FlightBodyResource);
            Assert.IsTrue(projectilePresentation.TryGetV2(
                CharacterTestFireballProjectileAdapter.FireballLobDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 lobBinding));
            Assert.AreEqual(CharacterTestVfxIds.FireballBurstFlightResourceKey, lobBinding.FlightBodyResource);
            Assert.IsTrue(projectilePresentation.TryGetV2(
                CharacterTestFireballProjectileAdapter.FireballSurgeDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 surgeBinding));
            Assert.AreEqual(CharacterTestVfxIds.FireballResourceKey, surgeBinding.FlightBodyResource);
            Assert.IsTrue(projectilePresentation.TryGetV2(
                CharacterTestFireballProjectileAdapter.FireballReturnDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 returnBinding));
            Assert.AreEqual(CharacterTestVfxIds.FireballResourceKey, returnBinding.FlightBodyResource);
            Assert.IsTrue(projectilePresentation.TryGetV2(
                CharacterTestFireballProjectileAdapter.FireballSwayDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 swayBinding));
            Assert.AreEqual(CharacterTestVfxIds.FireballResourceKey, swayBinding.FlightBodyResource);
            Assert.IsTrue(projectilePresentation.TryGetV2(
                CharacterTestFireballProjectileAdapter.TornadoDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 tornadoBinding));
            Assert.AreEqual(CharacterTestVfxIds.TornadoFlightResourceKey, tornadoBinding.FlightBodyResource);
            Assert.IsTrue(projectilePresentation.TryGetV2(
                CharacterTestFireballProjectileAdapter.BeamLanceDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 beamLanceBinding));
            Assert.AreEqual(CharacterTestVfxIds.BeamLanceBeamResourceKey, beamLanceBinding.FlightBodyResource);
            Assert.IsTrue(projectilePresentation.TryGetV2(
                CharacterTestFireballProjectileAdapter.LaserLanceDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 laserBinding));
            Assert.AreEqual(CharacterTestVfxIds.LaserLancePathResourceKey, laserBinding.FlightBodyResource);
            Assert.IsTrue(projectilePresentation.TryGetV2(
                CharacterTestFireballProjectileAdapter.ChannelBeamDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 channelBeamBinding));
            Assert.AreEqual(CharacterTestVfxIds.ChannelBeamBeamResourceKey, channelBeamBinding.FlightBodyResource);
            Assert.IsTrue(projectilePresentation.TryGetV2(
                CharacterTestFireballProjectileAdapter.PoisonBottleDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 poisonBottleBinding));
            Assert.AreEqual(CharacterTestVfxIds.PoisonBottleFlightResourceKey, poisonBottleBinding.FlightBodyResource);
            Assert.IsTrue(projectilePresentation.TryGetV2(
                CharacterTestFireballProjectileAdapter.ArcMineDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 arcMineBinding));
            Assert.AreEqual(CharacterTestVfxIds.ArcMineFlightResourceKey, arcMineBinding.FlightBodyResource);
            Assert.IsTrue(projectilePresentation.TryGetV2(
                CharacterTestFireballProjectileAdapter.ArcMineDropDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 arcMineDropBinding));
            Assert.AreEqual(CharacterTestVfxIds.ArcMineFlightResourceKey, arcMineDropBinding.FlightBodyResource);
            Assert.IsTrue(projectilePresentation.TryGetV2(
                CharacterTestFireballProjectileAdapter.FireballHomingDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 homingBinding));
            Assert.AreEqual(CharacterTestVfxIds.FireballResourceKey, homingBinding.FlightBodyResource);
            Assert.IsTrue(projectilePresentation.TryGetV2(
                CharacterTestFireballProjectileAdapter.FireballBezierDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 bezierBinding));
            Assert.AreEqual(CharacterTestVfxIds.FireballResourceKey, bezierBinding.FlightBodyResource);
            Assert.IsTrue(projectilePresentation.TryGetV2(
                CharacterTestFireballProjectileAdapter.FireballExplosionDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 explosionBinding));
            Assert.AreEqual(CharacterTestVfxIds.FireballBurstFlightResourceKey, explosionBinding.FlightBodyResource);
        }

        private static void AssertEmbeddedProjectileImpactUsesHitEvent(
            CombatProjectileDefinitionRegistry registry,
            int spawnProfileId,
            string damageTypeId,
            int baseDamage)
        {
            Assert.IsTrue(CharacterTestFireballProjectileAdapter.TryResolveSpawnProfile(CharacterTestFireballProjectileAdapter.CreateSpawnProfileRegistry(), spawnProfileId, out CombatProjectileSpawnProfile profile));
            Assert.IsTrue(registry.TryGet(profile.ProjectileDefinitionId, out CombatProjectileDefinition definition));
            Assert.AreEqual(0, definition.Impact.PresentationProfileId);
            Assert.AreEqual(damageTypeId, definition.Impact.HitEvent.Damage.DamageTypeLabel);
            Assert.Greater(definition.Impact.HitEvent.Damage.DamageTypeId, 0);
            Assert.AreEqual(baseDamage, definition.Impact.HitEvent.Damage.BaseDamage);
        }

        private static void AssertActionBindingExists(
            CharacterActionSetConfig actionSet,
            int intentId,
            string actionId)
        {
            CharacterActionBinding[] bindings = actionSet?.CommandBindings ?? Array.Empty<CharacterActionBinding>();
            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].IntentId == intentId
                    && string.Equals((bindings[i].ActionId).ToString(), actionId, StringComparison.Ordinal))
                {
                    return;
                }
            }

            Assert.Fail("Missing action binding: " + intentId + " -> " + actionId);
        }

        private static void AssertProjectileGameplayEvent(
            CharacterActionConfig action,
            int frame,
            int requestId,
            int stableEventId)
        {
            GameplayTrackEvent[] events = action?.GameplayTrack?.Events ?? Array.Empty<GameplayTrackEvent>();
            for (int i = 0; i < events.Length; i++)
            {
                GameplayTrackEvent evt = events[i];
                if (evt.Frame == frame
                    && evt.Kind == CharacterActionTrackEventKind.SendGameplayRequest
                    && evt.RequestId == requestId
                    && evt.StableEventId == stableEventId)
                {
                    return;
                }
            }

            Assert.Fail("Missing projectile gameplay event: " + requestId);
        }

        private static void AssertTimelineEvent(
            AbilityEffectTimelineDefinition timeline,
            int frame,
            AbilityEffectTimelineTrackKind trackKind,
            AbilityEffectTimelineEventKind eventKind,
            AbilityEffectTimelineShapeKind shapeKind)
        {
            IReadOnlyList<AbilityEffectTimelineEvent> events = timeline?.Events
                ?? Array.Empty<AbilityEffectTimelineEvent>();
            for (int i = 0; i < events.Count; i++)
            {
                AbilityEffectTimelineEvent evt = events[i];
                if (evt.Frame != frame || evt.TrackKind != trackKind || evt.EventKind != eventKind)
                    continue;

                Assert.AreEqual(shapeKind, evt.ShapeKind);
                Assert.AreEqual(AbilityEffectTimelineAnchorMode.CapturedAnchor, evt.AnchorMode);
                if (eventKind == AbilityEffectTimelineEventKind.AreaPulse)
                {
                    Assert.AreEqual(Fix64.FromRatio(5, 2), evt.Radius);
                    Assert.AreEqual(256, evt.LayerMask);
                    Assert.AreEqual(CharacterTestAuthoredContentIds.D401_Id5, evt.RequestId);
                }
                else if (eventKind == AbilityEffectTimelineEventKind.SpawnVfx)
                {
                    Assert.AreEqual(CharacterTestVfxIds.FireballExplosionImpactDefinitionId, evt.EffectId);
                }

                return;
            }

            Assert.Fail(
                "Missing timeline event: frame="
                + frame.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " track="
                + trackKind
                + " kind="
                + eventKind);
        }

        [UnityTest]
        public IEnumerator CharacterTestScene_PlayModeFireballShowsMovingProjectileVfxAndImpactVfx()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            GameManager manager = Object.FindAnyObjectByType<GameManager>();
            bool managerFound = manager != null;
            bool menuClosed = false;
            bool localPlayerFound = false;
            bool targetPrepared = false;
            bool actionStarted = false;
            bool sawWindupVfx = false;
            bool sawProjectile = false;
            bool projectileMoved = false;
            bool projectileGone = false;
            bool sawImpactVfx = false;
            int projectilePresenterActiveCount = -1;
            int impactPresenterActiveCount = -1;
            Vector3 firstProjectilePosition = default;

            if (manager != null)
            {
                CharacterTestMainMenuCommandResult closeResult = manager.RuntimeContext.MainMenuCoordinator.CloseMainMenu();
                menuClosed = closeResult.Success && !manager.RuntimeContext.IsPaused;

                CharacterTestCharacterEntity localPlayer = null;
                for (int i = 0; i < 60; i++)
                {
                    yield return null;
                    localPlayer = FindLocalPlayer(manager.RuntimeContext.Slice?.CharacterEntities);
                    if (localPlayer != null)
                        break;
                }

                localPlayerFound = localPlayer != null;
                CharacterTestCharacterEntity target = FindFirstHostile(
                    manager.RuntimeContext.Slice?.CharacterEntities,
                    localPlayer);
                if (localPlayer != null && target != null)
                {
                    PrepareProjectileSmokeTarget(localPlayer, target);
                    targetPrepared = true;
                    yield return WaitForProjectileSmokeTargetSync();
                }

                if (localPlayer != null && manager.RuntimeContext.Slice?.ActionRuntime != null)
                {
                    actionStarted = manager.RuntimeContext.Slice.ActionRuntime.TryStartIntent(
                        localPlayer,
                        CharacterTestCharacterActionConfigSource.FireballIntentId,
                        manager.RuntimeContext.Slice.CurrentFrame);
                }

                string projectileNamePrefix =
                    CharacterTestProjectilePresentationSource.FireballViewNamePrefix
                    + "_"
                    + CharacterTestFireballProjectileAdapter.DefinitionId
                    + "_";
                for (int i = 0; i < 160; i++)
                {
                    yield return null;
                    if (FindActiveObjectByPrefix(FireballChargeVfxObjectNamePrefix) != null)
                        sawWindupVfx = true;

                    GameObject projectile = FindActiveObjectByPrefix(projectileNamePrefix);
                    if (projectile != null)
                    {
                        if (!sawProjectile)
                            firstProjectilePosition = projectile.transform.position;

                        sawProjectile = true;
                        if ((projectile.transform.position - firstProjectilePosition).sqrMagnitude > 0.0001f)
                            projectileMoved = true;
                    }
                    else if (sawProjectile)
                    {
                        projectileGone = true;
                    }

                    if (FindActiveObjectByPrefix(FireballImpactVfxObjectNamePrefix) != null)
                        sawImpactVfx = true;

                    if (sawWindupVfx && sawProjectile && projectileMoved && projectileGone && sawImpactVfx)
                        break;
                }

                projectilePresenterActiveCount = manager.RuntimeContext.Slice?.ProjectilePresenter != null
                    ? manager.RuntimeContext.Slice.ProjectilePresenter.ActiveCount
                    : -1;
                impactPresenterActiveCount = manager.RuntimeContext.Slice?.VfxPresenter != null
                    ? manager.RuntimeContext.Slice.VfxPresenter.ActiveCount
                    : -1;
            }

            yield return new ExitPlayMode();

            Assert.IsTrue(managerFound, "CharacterTestScene did not create a GameManager in Play Mode.");
            Assert.IsTrue(menuClosed, "CharacterTest main menu should close before the fireball smoke runs.");
            Assert.IsTrue(localPlayerFound, "CharacterTest scene did not spawn the local player.");
            Assert.IsTrue(targetPrepared, "CharacterTest scene did not expose a hostile target for the fireball smoke.");
            Assert.IsTrue(actionStarted, "Fireball action did not start from the local player.");
            Assert.IsTrue(sawWindupVfx, "Fireball windup VFX object was not created before projectile flight.");
            Assert.IsTrue(sawProjectile, "Fireball projectile flight VFX object was not created.");
            Assert.IsTrue(projectileMoved, "Fireball projectile flight VFX did not follow projectile movement.");
            Assert.IsTrue(projectileGone, "Fireball projectile flight VFX did not disappear after hit or expiry.");
            Assert.IsTrue(sawImpactVfx, "Fireball impact VFX was not produced after projectile hit.");
            Assert.AreEqual(0, projectilePresenterActiveCount);
            Assert.GreaterOrEqual(impactPresenterActiveCount, 0);
        }

        [UnityTest]
        public IEnumerator CharacterTestScene_PlayModeTornadoSlotSevenShowsProjectilePulseAndDamage()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            GameManager manager = Object.FindAnyObjectByType<GameManager>();
            bool managerFound = manager != null;
            bool menuClosed = false;
            bool localPlayerFound = false;
            bool targetPrepared = false;
            bool commandAccepted = false;
            bool sawWindupVfx = false;
            bool sawProjectile = false;
            bool projectileMoved = false;
            bool projectileGone = false;
            bool sawImpactVfx = false;
            bool sawTickVfx = false;
            bool targetPulled = false;
            bool targetDamaged = false;
            bool targetDamagedMultipleTimes = false;
            int projectilePresenterActiveCount = -1;
            int impactPresenterActiveCount = -1;
            Vector3 firstProjectilePosition = default;
            float targetStartX = 0f;
            int targetLastHealth = 0;
            int targetHealthDropCount = 0;
            bool profilerReportWritten = false;
            bool profilerReportHasTornadoFieldSamples = false;
            string profilerReportIssue = string.Empty;
            string profilerReportFullPath = Path.GetFullPath(TornadoProfilerRecorderReportPath);
            if (File.Exists(profilerReportFullPath))
                File.Delete(profilerReportFullPath);

            if (manager != null)
            {
                CharacterTestMainMenuCommandResult closeResult = manager.RuntimeContext.MainMenuCoordinator.CloseMainMenu();
                menuClosed = closeResult.Success && !manager.RuntimeContext.IsPaused;

                CharacterTestCharacterEntity localPlayer = null;
                for (int i = 0; i < 60; i++)
                {
                    yield return null;
                    localPlayer = FindLocalPlayer(manager.RuntimeContext.Slice?.CharacterEntities);
                    if (localPlayer != null)
                        break;
                }

                localPlayerFound = localPlayer != null;
                List<CharacterTestCharacterEntity> hostiles = FindHostiles(
                    manager.RuntimeContext.Slice?.CharacterEntities,
                    localPlayer);
                if (localPlayer != null
                    && hostiles.Count < 2
                    && hostiles.Count > 0
                    && manager.RuntimeContext.Slice?.CharacterRuntime != null)
                {
                    CharacterTestCharacterEntity seed = hostiles[0];
                    bool spawnAccepted = manager.RuntimeContext.Slice.CharacterRuntime.EnqueueSpawn(
                        new CharacterTestCharacterSpawnRequest(
                            seed.Identity.CharacterId,
                            seed.Team.TeamId,
                            localPlayer.Transform.X,
                            0f,
                            localPlayer.Transform.Z + TornadoSmokeDamageTargetDistanceZ,
                            CharacterTestCharacterControlOwner.None));
                    if (spawnAccepted)
                    {
                        for (int i = 0; i < 8 && hostiles.Count < 2; i++)
                        {
                            yield return null;
                            hostiles = FindHostiles(
                                manager.RuntimeContext.Slice?.CharacterEntities,
                                localPlayer);
                        }
                    }
                }

                CharacterTestCharacterEntity pullTarget = hostiles.Count > 0 ? hostiles[0] : null;
                CharacterTestCharacterEntity damageTarget = hostiles.Count > 1 ? hostiles[1] : null;
                int targetHealthBefore = SumUniqueHealth(pullTarget, damageTarget);
                targetLastHealth = targetHealthBefore;
                if (localPlayer != null
                    && pullTarget != null
                    && damageTarget != null
                    && !ReferenceEquals(pullTarget, damageTarget))
                {
                    PrepareTornadoSmokeTargets(localPlayer, pullTarget, damageTarget);
                    targetStartX = pullTarget.Transform.X;
                    targetPrepared = true;
                    yield return WaitForProjectileSmokeTargetSync();
                }

                if (localPlayer != null && manager.RuntimeContext.Slice != null)
                {
                    RuntimeCommandValidationResult commandResult =
                        manager.RuntimeContext.Slice.EnqueueCastAbilitySlot(
                            localPlayer.Id.Value,
                            slotNumber: 7,
                            abilityId: CharacterTestAbilityIds.TornadoAbilityId,
                            traceId: new RuntimeTraceIdentity(903323));
                    commandAccepted = commandResult.Success;
                }

                string projectileNamePrefix =
                    CharacterTestProjectilePresentationSource.FireballViewNamePrefix
                    + "_"
                    + CharacterTestFireballProjectileAdapter.TornadoDefinitionId
                    + "_";
                long captureStartFrame = manager.RuntimeContext.Slice?.CurrentFrame.Value ?? 0L;
                using (var profilerCapture = TornadoProfilerRecorderCapture.Start(
                    TornadoProfilerMarkerNames,
                    TornadoProfilerCaptureFrameCount))
                {
                    for (int i = 0; i < TornadoProfilerCaptureFrameCount; i++)
                    {
                        yield return null;
                        profilerCapture.Sample();

                        if (FindActiveObjectByPrefix(TornadoChargeVfxObjectNamePrefix) != null)
                            sawWindupVfx = true;

                        GameObject projectile = FindActiveObjectByPrefix(projectileNamePrefix);
                        if (projectile != null)
                        {
                            if (!sawProjectile)
                                firstProjectilePosition = projectile.transform.position;

                            sawProjectile = true;
                            if ((projectile.transform.position - firstProjectilePosition).sqrMagnitude > 0.0001f)
                                projectileMoved = true;
                        }
                        else if (sawProjectile)
                        {
                            projectileGone = true;
                        }

                        if (FindActiveObjectByPrefix(TornadoImpactVfxObjectNamePrefix) != null)
                            sawImpactVfx = true;
                        if (FindActiveObjectByPrefix(TornadoTickVfxObjectNamePrefix) != null)
                            sawTickVfx = true;

                        if (pullTarget != null && pullTarget.Transform.X < targetStartX - 0.01f)
                            targetPulled = true;

                        int currentTargetHealth = SumUniqueHealth(pullTarget, damageTarget);
                        if (currentTargetHealth < targetHealthBefore)
                            targetDamaged = true;
                        if (currentTargetHealth < targetLastHealth)
                        {
                            targetHealthDropCount++;
                            targetLastHealth = currentTargetHealth;
                        }

                        targetDamagedMultipleTimes = targetHealthDropCount >= 2;

                        if (sawWindupVfx
                            && sawProjectile
                            && projectileMoved
                            && sawTickVfx
                            && targetPulled
                            && targetDamagedMultipleTimes
                            && projectileGone
                            && sawImpactVfx
                            && targetDamaged)
                        {
                            break;
                        }
                    }

                    long endFrame = manager.RuntimeContext.Slice?.CurrentFrame.Value ?? captureStartFrame;
                    try
                    {
                        string directory = Path.GetDirectoryName(profilerReportFullPath);
                        if (!string.IsNullOrEmpty(directory))
                            Directory.CreateDirectory(directory);
                        File.WriteAllText(
                            profilerReportFullPath,
                            profilerCapture.CreateReportText(
                                TornadoProfilerRecorderScenarioName,
                                captureStartFrame,
                                endFrame,
                                TornadoProfilerCaptureFrameCount));
                        profilerReportWritten = File.Exists(profilerReportFullPath);
                        profilerReportHasTornadoFieldSamples =
                            profilerCapture.HasNonZeroSample(TornadoFieldQueryMarkerName);
                    }
                    catch (Exception ex)
                    {
                        profilerReportIssue = ex.ToString();
                    }
                }

                projectilePresenterActiveCount = manager.RuntimeContext.Slice?.ProjectilePresenter != null
                    ? manager.RuntimeContext.Slice.ProjectilePresenter.ActiveCount
                    : -1;
                impactPresenterActiveCount = manager.RuntimeContext.Slice?.VfxPresenter != null
                    ? manager.RuntimeContext.Slice.VfxPresenter.ActiveCount
                    : -1;
            }

            yield return new ExitPlayMode();

            Assert.IsTrue(managerFound, "CharacterTestScene did not create a GameManager in Play Mode.");
            Assert.IsTrue(menuClosed, "CharacterTest main menu should close before the tornado smoke runs.");
            Assert.IsTrue(localPlayerFound, "CharacterTest scene did not spawn the local player.");
            Assert.IsTrue(targetPrepared, "CharacterTest scene did not expose a hostile target for the tornado smoke.");
            Assert.IsTrue(commandAccepted, "Tornado slot 7 cast command was rejected.");
            Assert.IsTrue(sawWindupVfx, "Tornado windup VFX object was not created before projectile flight.");
            Assert.IsTrue(sawProjectile, "Tornado projectile flight VFX object was not created.");
            Assert.IsTrue(projectileMoved, "Tornado projectile flight VFX did not follow projectile movement.");
            Assert.IsTrue(sawTickVfx, "Tornado periodic tick VFX was not produced while the projectile was active.");
            Assert.IsTrue(targetPulled, "Tornado did not continuously pull the side target toward its path.");
            Assert.IsTrue(targetDamagedMultipleTimes, "Tornado did not reduce the target health more than once.");
            Assert.IsTrue(projectileGone, "Tornado projectile flight VFX did not disappear after hit or expiry.");
            Assert.IsTrue(sawImpactVfx, "Tornado impact VFX was not produced after projectile hit or expiry.");
            Assert.IsTrue(targetDamaged, "Tornado periodic damage or final pulse did not damage the prepared hostile target.");
            Assert.AreEqual(0, projectilePresenterActiveCount);
            Assert.GreaterOrEqual(impactPresenterActiveCount, 0);
            Assert.IsTrue(profilerReportWritten, profilerReportIssue);
            Assert.IsTrue(
                profilerReportHasTornadoFieldSamples,
                "Tornado profiler recorder should capture non-zero TornadoFieldQuery samples.");
        }

        [UnityTest]
        [Explicit("Generates a large Profile Analyzer .pdata artifact for CharacterTest Tornado performance review.")]
        public IEnumerator CharacterTestScene_PlayModeTornadoProfileAnalyzerCapture_WritesPDataIntoTemp()
        {
            return RunTornadoProfileAnalyzerCapture(
                TornadoProfileAnalyzerPDataPath,
                TornadoProfileAnalyzerCaptureSummaryPath,
                TornadoProfileAnalyzerCaptureScenarioName,
                requireTornadoFieldMarker: true);
        }

        [UnityTest]
        [Explicit("Generates a baseline-compatible Profile Analyzer .pdata artifact for before/after comparison.")]
        public IEnumerator CharacterTestScene_PlayModeTornadoProfileAnalyzerPairCapture_WritesPDataIntoTemp()
        {
            return CreateTornadoProfileAnalyzerPairCapture(
                TornadoProfileAnalyzerPairPDataPath,
                TornadoProfileAnalyzerPairCaptureSummaryPath);
        }

        public IEnumerator CreateTornadoProfileAnalyzerPairCapture(string pDataPath, string summaryPath)
        {
            return RunTornadoProfileAnalyzerCapture(
                string.IsNullOrEmpty(pDataPath) ? TornadoProfileAnalyzerPairPDataPath : pDataPath,
                string.IsNullOrEmpty(summaryPath) ? TornadoProfileAnalyzerPairCaptureSummaryPath : summaryPath,
                TornadoProfileAnalyzerPairScenarioName,
                requireTornadoFieldMarker: false);
        }

        [UnityTest]
        [Explicit("Generates a stress Profile Analyzer .pdata artifact for Tornado before/after comparison.")]
        public IEnumerator CharacterTestScene_PlayModeTornadoProfileAnalyzerStressPairCapture_WritesPDataIntoTemp()
        {
            return CreateTornadoProfileAnalyzerStressPairCapture(
                TornadoProfileAnalyzerStressPairPDataPath,
                TornadoProfileAnalyzerStressPairCaptureSummaryPath,
                stressTargetCount: 0);
        }

        public IEnumerator CreateTornadoProfileAnalyzerStressPairCapture(
            string pDataPath,
            string summaryPath,
            int stressTargetCount)
        {
            int normalizedStressTargetCount = stressTargetCount > 0
                ? stressTargetCount
                : TornadoProfileAnalyzerStressTargetCount;
            return RunTornadoProfileAnalyzerCapture(
                string.IsNullOrEmpty(pDataPath) ? TornadoProfileAnalyzerStressPairPDataPath : pDataPath,
                string.IsNullOrEmpty(summaryPath) ? TornadoProfileAnalyzerStressPairCaptureSummaryPath : summaryPath,
                TornadoProfileAnalyzerStressPairScenarioName,
                requireTornadoFieldMarker: false,
                normalizedStressTargetCount);
        }

        private IEnumerator RunTornadoProfileAnalyzerCapture(
            string profileAnalyzerPDataPath,
            string profileAnalyzerCaptureSummaryPath,
            string scenarioName,
            bool requireTornadoFieldMarker,
            int stressTargetCount = 0)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            GameManager manager = Object.FindAnyObjectByType<GameManager>();
            bool managerFound = manager != null;
            bool menuClosed = false;
            bool localPlayerFound = false;
            bool targetPrepared = false;
            bool commandAccepted = false;
            bool pDataWritten = false;
            bool profilerRecorderHasTornadoFieldSamples = false;
            bool sawProjectile = false;
            bool projectileMoved = false;
            bool sawTickVfx = false;
            bool targetPulled = false;
            bool targetDamaged = false;
            bool targetDamagedMultipleTimes = false;
            bool stressTargetsPrepared = stressTargetCount <= 0;
            string profileAnalyzerCaptureIssue = string.Empty;
            string profileAnalyzerPDataFullPath = GetProjectRelativeFullPath(profileAnalyzerPDataPath);
            string profileAnalyzerCaptureSummaryFullPath =
                GetProjectRelativeFullPath(profileAnalyzerCaptureSummaryPath);
            Vector3 firstProjectilePosition = default;
            float targetStartX = 0f;
            int targetHealthBefore = 0;
            int targetLastHealth = 0;
            int targetHealthDropCount = 0;
            int preparedHostileCount = 0;
            int requestedStressTargetCount = stressTargetCount > 0 ? Math.Max(2, stressTargetCount) : 0;
            long captureStartFrame = 0L;
            long captureEndFrame = 0L;
            if (File.Exists(profileAnalyzerPDataFullPath))
                File.Delete(profileAnalyzerPDataFullPath);
            if (File.Exists(profileAnalyzerCaptureSummaryFullPath))
                File.Delete(profileAnalyzerCaptureSummaryFullPath);

            if (manager != null)
            {
                CharacterTestMainMenuCommandResult closeResult = manager.RuntimeContext.MainMenuCoordinator.CloseMainMenu();
                menuClosed = closeResult.Success && !manager.RuntimeContext.IsPaused;

                CharacterTestCharacterEntity localPlayer = null;
                for (int i = 0; i < 60; i++)
                {
                    yield return null;
                    localPlayer = FindLocalPlayer(manager.RuntimeContext.Slice?.CharacterEntities);
                    if (localPlayer != null)
                        break;
                }

                localPlayerFound = localPlayer != null;
                List<CharacterTestCharacterEntity> hostiles = FindHostiles(
                    manager.RuntimeContext.Slice?.CharacterEntities,
                    localPlayer);
                int requestedHostileCount = requestedStressTargetCount > 0 ? requestedStressTargetCount : 2;
                if (localPlayer != null
                    && hostiles.Count < requestedHostileCount
                    && hostiles.Count > 0
                    && manager.RuntimeContext.Slice?.CharacterRuntime != null)
                {
                    CharacterTestCharacterEntity seed = hostiles[0];
                    int spawnRequestCount = requestedHostileCount - hostiles.Count;
                    int acceptedSpawnCount = 0;
                    for (int spawnIndex = 0; spawnIndex < spawnRequestCount; spawnIndex++)
                    {
                        int targetOrdinal = hostiles.Count + spawnIndex;
                        Vector3 spawnPosition = CalculateTornadoStressTargetPosition(localPlayer, targetOrdinal);
                        bool spawnAccepted = manager.RuntimeContext.Slice.CharacterRuntime.EnqueueSpawn(
                            new CharacterTestCharacterSpawnRequest(
                                seed.Identity.CharacterId,
                                seed.Team.TeamId,
                                spawnPosition.x,
                                0f,
                                spawnPosition.z,
                                CharacterTestCharacterControlOwner.None));
                        if (!spawnAccepted)
                            break;

                        acceptedSpawnCount++;
                    }

                    if (acceptedSpawnCount > 0)
                    {
                        for (int i = 0; i < 32 && hostiles.Count < requestedHostileCount; i++)
                        {
                            yield return null;
                            hostiles = FindHostiles(
                                manager.RuntimeContext.Slice?.CharacterEntities,
                                localPlayer);
                        }
                    }
                }

                CharacterTestCharacterEntity pullTarget = hostiles.Count > 0 ? hostiles[0] : null;
                CharacterTestCharacterEntity damageTarget = hostiles.Count > 1 ? hostiles[1] : null;
                targetHealthBefore = SumUniqueHealth(pullTarget, damageTarget);
                targetLastHealth = targetHealthBefore;
                preparedHostileCount = hostiles.Count;
                if (localPlayer != null
                    && pullTarget != null
                    && damageTarget != null
                    && !ReferenceEquals(pullTarget, damageTarget))
                {
                    if (requestedStressTargetCount > 0)
                    {
                        preparedHostileCount = PrepareTornadoStressTargets(
                            localPlayer,
                            hostiles,
                            requestedStressTargetCount);
                        stressTargetsPrepared = preparedHostileCount >= requestedStressTargetCount;
                    }
                    else
                    {
                        PrepareTornadoSmokeTargets(localPlayer, pullTarget, damageTarget);
                    }

                    targetStartX = pullTarget.Transform.X;
                    targetPrepared = true;
                    yield return WaitForProjectileSmokeTargetSync();
                }

                if (localPlayer != null && manager.RuntimeContext.Slice != null)
                {
                    RuntimeCommandValidationResult commandResult =
                        manager.RuntimeContext.Slice.EnqueueCastAbilitySlot(
                            localPlayer.Id.Value,
                            slotNumber: 7,
                            abilityId: CharacterTestAbilityIds.TornadoAbilityId,
                            traceId: new RuntimeTraceIdentity(921705));
                    commandAccepted = commandResult.Success;
                }

                Assert.IsTrue(managerFound, "CharacterTestScene did not create a GameManager in Play Mode.");
                Assert.IsTrue(menuClosed, "CharacterTest main menu should close before the tornado capture runs.");
                Assert.IsTrue(localPlayerFound, "CharacterTest scene did not spawn the local player.");
                Assert.IsTrue(targetPrepared, "CharacterTest scene did not expose hostile targets for the tornado capture.");
                Assert.IsTrue(commandAccepted, "Tornado slot 7 cast command was rejected.");

                string projectileNamePrefix =
                    CharacterTestProjectilePresentationSource.FireballViewNamePrefix
                    + "_"
                    + CharacterTestFireballProjectileAdapter.TornadoDefinitionId
                    + "_";
                captureStartFrame = manager.RuntimeContext.Slice?.CurrentFrame.Value ?? 0L;
                using (var profilerRecorder = TornadoProfilerRecorderCapture.Start(
                    TornadoProfilerMarkerNames,
                    TornadoProfileAnalyzerCaptureFrameCount))
                using (var profileAnalyzerCapture = TornadoProfileAnalyzerCapture.Start(
                    scenarioName,
                    profileAnalyzerPDataFullPath,
                    profileAnalyzerCaptureSummaryFullPath))
                {
                    for (int i = 0; i < TornadoProfileAnalyzerCaptureFrameCount; i++)
                    {
                        yield return null;
                        profilerRecorder.Sample();

                        GameObject projectile = FindActiveObjectByPrefix(projectileNamePrefix);
                        if (projectile != null)
                        {
                            if (!sawProjectile)
                                firstProjectilePosition = projectile.transform.position;

                            sawProjectile = true;
                            if ((projectile.transform.position - firstProjectilePosition).sqrMagnitude > 0.0001f)
                                projectileMoved = true;
                        }

                        if (FindActiveObjectByPrefix(TornadoTickVfxObjectNamePrefix) != null)
                            sawTickVfx = true;

                        if (pullTarget != null && pullTarget.Transform.X < targetStartX - 0.01f)
                            targetPulled = true;

                        int currentTargetHealth = SumUniqueHealth(pullTarget, damageTarget);
                        if (currentTargetHealth < targetHealthBefore)
                            targetDamaged = true;
                        if (currentTargetHealth < targetLastHealth)
                        {
                            targetHealthDropCount++;
                            targetLastHealth = currentTargetHealth;
                        }

                        targetDamagedMultipleTimes = targetHealthDropCount >= 2;
                    }

                    captureEndFrame = manager.RuntimeContext.Slice?.CurrentFrame.Value ?? captureStartFrame;
                    profileAnalyzerCapture.StopRecording();
                    yield return null;
                    profileAnalyzerCapture.SaveProfileData();
                    AppendTornadoProfileAnalyzerEvidence(
                        profileAnalyzerCaptureSummaryFullPath,
                        captureStartFrame,
                        captureEndFrame,
                        sawProjectile,
                        projectileMoved,
                        sawTickVfx,
                        targetPulled,
                        targetDamaged,
                        targetDamagedMultipleTimes,
                        requestedStressTargetCount,
                        preparedHostileCount);
                    pDataWritten = profileAnalyzerCapture.PDataWritten;
                    profileAnalyzerCaptureIssue = profileAnalyzerCapture.Issue;
                    profilerRecorderHasTornadoFieldSamples =
                        profilerRecorder.HasNonZeroSample(TornadoFieldQueryMarkerName);
                }
            }

            yield return new ExitPlayMode();

            Assert.IsTrue(managerFound, "CharacterTestScene did not create a GameManager in Play Mode.");
            Assert.IsTrue(menuClosed, "CharacterTest main menu should close before the tornado capture runs.");
            Assert.IsTrue(localPlayerFound, "CharacterTest scene did not spawn the local player.");
            Assert.IsTrue(targetPrepared, "CharacterTest scene did not expose hostile targets for the tornado capture.");
            Assert.IsTrue(commandAccepted, "Tornado slot 7 cast command was rejected.");
            Assert.IsTrue(sawProjectile, "Tornado Profile Analyzer capture did not include projectile flight.");
            Assert.IsTrue(projectileMoved, "Tornado Profile Analyzer capture did not include projectile movement.");
            Assert.IsTrue(sawTickVfx, "Tornado Profile Analyzer capture did not include periodic tick VFX.");
            if (requestedStressTargetCount <= 0)
                Assert.IsTrue(targetPulled, "Tornado Profile Analyzer capture did not include attraction movement.");
            Assert.IsTrue(targetDamaged, "Tornado Profile Analyzer capture did not include target damage.");
            Assert.IsTrue(
                stressTargetsPrepared,
                "Tornado stress capture did not prepare the requested hostile target count.");
            if (requestedStressTargetCount > 0)
            {
                Assert.IsTrue(
                    profilerRecorderHasTornadoFieldSamples,
                    "Tornado stress Profile Analyzer capture should include non-zero TornadoFieldQuery samples.");
            }

            if (requireTornadoFieldMarker)
            {
                Assert.IsTrue(
                    profilerRecorderHasTornadoFieldSamples,
                    "Tornado Profile Analyzer capture should include non-zero TornadoFieldQuery samples.");
            }

            Assert.IsTrue(pDataWritten, profileAnalyzerCaptureIssue);
        }

        private static string GetProjectRelativeFullPath(string path)
        {
            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? ".";
            return Path.GetFullPath(Path.Combine(projectRoot, path));
        }

        private static void AppendTornadoProfileAnalyzerEvidence(
            string summaryPath,
            long captureStartFrame,
            long captureEndFrame,
            bool sawProjectile,
            bool projectileMoved,
            bool sawTickVfx,
            bool targetPulled,
            bool targetDamaged,
            bool targetDamagedMultipleTimes,
            int requestedStressTargetCount,
            int preparedHostileCount)
        {
            var builder = new StringBuilder(512);
            builder.AppendLine("tornadoEvidence:");
            builder.Append("captureStartFrame: ")
                .AppendLine(captureStartFrame.ToString(CultureInfo.InvariantCulture));
            builder.Append("captureEndFrame: ")
                .AppendLine(captureEndFrame.ToString(CultureInfo.InvariantCulture));
            builder.Append("sawProjectile: ")
                .AppendLine(sawProjectile ? "true" : "false");
            builder.Append("projectileMoved: ")
                .AppendLine(projectileMoved ? "true" : "false");
            builder.Append("sawTickVfx: ")
                .AppendLine(sawTickVfx ? "true" : "false");
            builder.Append("targetPulled: ")
                .AppendLine(targetPulled ? "true" : "false");
            builder.Append("targetDamaged: ")
                .AppendLine(targetDamaged ? "true" : "false");
            builder.Append("targetDamagedMultipleTimes: ")
                .AppendLine(targetDamagedMultipleTimes ? "true" : "false");
            builder.Append("requestedStressTargetCount: ")
                .AppendLine(requestedStressTargetCount.ToString(CultureInfo.InvariantCulture));
            builder.Append("preparedHostileCount: ")
                .AppendLine(preparedHostileCount.ToString(CultureInfo.InvariantCulture));
            File.AppendAllText(summaryPath, builder.ToString());
        }

        [UnityTest]
        public IEnumerator CharacterTestScene_PlayModeFireballBurstShowsMovingProjectileVfxAndImpactVfx()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            GameManager manager = Object.FindAnyObjectByType<GameManager>();
            bool managerFound = manager != null;
            bool menuClosed = false;
            bool localPlayerFound = false;
            bool targetPrepared = false;
            bool actionStarted = false;
            bool sawProjectile = false;
            bool projectileMoved = false;
            bool projectileGone = false;
            bool sawImpactVfx = false;
            int projectilePresenterActiveCount = -1;
            int impactPresenterActiveCount = -1;
            Vector3 firstProjectilePosition = default;

            if (manager != null)
            {
                CharacterTestMainMenuCommandResult closeResult = manager.RuntimeContext.MainMenuCoordinator.CloseMainMenu();
                menuClosed = closeResult.Success && !manager.RuntimeContext.IsPaused;

                CharacterTestCharacterEntity localPlayer = null;
                for (int i = 0; i < 60; i++)
                {
                    yield return null;
                    localPlayer = FindLocalPlayer(manager.RuntimeContext.Slice?.CharacterEntities);
                    if (localPlayer != null)
                        break;
                }

                localPlayerFound = localPlayer != null;
                CharacterTestCharacterEntity target = FindFirstHostile(
                    manager.RuntimeContext.Slice?.CharacterEntities,
                    localPlayer);
                if (localPlayer != null && target != null)
                {
                    PrepareProjectileSmokeTarget(localPlayer, target);
                    targetPrepared = true;
                    yield return WaitForProjectileSmokeTargetSync();
                }

                if (localPlayer != null && manager.RuntimeContext.Slice?.ActionRuntime != null)
                {
                    actionStarted = manager.RuntimeContext.Slice.ActionRuntime.TryStartIntent(
                        localPlayer,
                        CharacterTestCharacterActionConfigSource.FireballBurstIntentId,
                        manager.RuntimeContext.Slice.CurrentFrame);
                }

                string projectileNamePrefix =
                    CharacterTestProjectilePresentationSource.FireballViewNamePrefix
                    + "_"
                    + CharacterTestFireballProjectileAdapter.BurstDefinitionId
                    + "_";
                for (int i = 0; i < 160; i++)
                {
                    yield return null;
                    GameObject projectile = FindActiveObjectByPrefix(projectileNamePrefix);
                    if (projectile != null)
                    {
                        if (!sawProjectile)
                            firstProjectilePosition = projectile.transform.position;

                        sawProjectile = true;
                        if ((projectile.transform.position - firstProjectilePosition).sqrMagnitude > 0.0001f)
                            projectileMoved = true;
                    }
                    else if (sawProjectile)
                    {
                        projectileGone = true;
                    }

                    if (FindActiveObjectByPrefix(FireballBurstImpactVfxObjectNamePrefix) != null)
                        sawImpactVfx = true;

                    if (sawProjectile && projectileMoved && projectileGone && sawImpactVfx)
                        break;
                }

                projectilePresenterActiveCount = manager.RuntimeContext.Slice?.ProjectilePresenter != null
                    ? manager.RuntimeContext.Slice.ProjectilePresenter.ActiveCount
                    : -1;
                impactPresenterActiveCount = manager.RuntimeContext.Slice?.VfxPresenter != null
                    ? manager.RuntimeContext.Slice.VfxPresenter.ActiveCount
                    : -1;
            }

            yield return new ExitPlayMode();

            Assert.IsTrue(managerFound, "CharacterTestScene did not create a GameManager in Play Mode.");
            Assert.IsTrue(menuClosed, "CharacterTest main menu should close before the fireball burst smoke runs.");
            Assert.IsTrue(localPlayerFound, "CharacterTest scene did not spawn the local player.");
            Assert.IsTrue(targetPrepared, "CharacterTest scene did not expose a hostile target for the fireball burst smoke.");
            Assert.IsTrue(actionStarted, "Fireball Burst action did not start from the local player.");
            Assert.IsTrue(sawProjectile, "Fireball Burst projectile flight VFX object was not created.");
            Assert.IsTrue(projectileMoved, "Fireball Burst projectile flight VFX did not follow projectile movement.");
            Assert.IsTrue(projectileGone, "Fireball Burst projectile flight VFX did not disappear after hit or expiry.");
            Assert.IsTrue(sawImpactVfx, "Fireball Burst impact VFX was not produced after projectile hit.");
            Assert.AreEqual(0, projectilePresenterActiveCount);
            Assert.GreaterOrEqual(impactPresenterActiveCount, 0);
        }

        [UnityTest]
        public IEnumerator CharacterTestScene_PlayModeFireballExplosionShowsImpactAndDelayedAfterblastVfx()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            GameManager manager = Object.FindAnyObjectByType<GameManager>();
            bool managerFound = manager != null;
            bool menuClosed = false;
            bool localPlayerFound = false;
            bool targetPrepared = false;
            bool actionStarted = false;
            bool sawProjectile = false;
            bool projectileMoved = false;
            bool projectileGone = false;
            bool sawFirstImpactVfx = false;
            bool sawImpactVfxGap = false;
            bool sawSecondImpactVfx = false;
            int projectilePresenterActiveCount = -1;
            int impactPresenterActiveCount = -1;
            Vector3 firstProjectilePosition = default;

            if (manager != null)
            {
                CharacterTestMainMenuCommandResult closeResult = manager.RuntimeContext.MainMenuCoordinator.CloseMainMenu();
                menuClosed = closeResult.Success && !manager.RuntimeContext.IsPaused;

                CharacterTestCharacterEntity localPlayer = null;
                for (int i = 0; i < 60; i++)
                {
                    yield return null;
                    localPlayer = FindLocalPlayer(manager.RuntimeContext.Slice?.CharacterEntities);
                    if (localPlayer != null)
                        break;
                }

                localPlayerFound = localPlayer != null;
                CharacterTestCharacterEntity target = FindFirstHostile(
                    manager.RuntimeContext.Slice?.CharacterEntities,
                    localPlayer);
                if (localPlayer != null && target != null)
                {
                    PrepareProjectileSmokeTarget(localPlayer, target);
                    targetPrepared = true;
                    yield return WaitForProjectileSmokeTargetSync();
                }

                if (localPlayer != null && manager.RuntimeContext.Slice?.ActionRuntime != null)
                {
                    actionStarted = manager.RuntimeContext.Slice.ActionRuntime.TryStartIntent(
                        localPlayer,
                        CharacterTestCharacterActionConfigSource.FireballExplosionIntentId,
                        manager.RuntimeContext.Slice.CurrentFrame);
                }

                string projectileNamePrefix =
                    CharacterTestProjectilePresentationSource.FireballViewNamePrefix
                    + "_"
                    + CharacterTestFireballProjectileAdapter.FireballExplosionDefinitionId
                    + "_";
                for (int i = 0; i < 260; i++)
                {
                    yield return null;
                    GameObject projectile = FindActiveObjectByPrefix(projectileNamePrefix);
                    if (projectile != null)
                    {
                        if (!sawProjectile)
                            firstProjectilePosition = projectile.transform.position;

                        sawProjectile = true;
                        if ((projectile.transform.position - firstProjectilePosition).sqrMagnitude > 0.0001f)
                            projectileMoved = true;
                    }
                    else if (sawProjectile)
                    {
                        projectileGone = true;
                    }

                    bool impactVfxActive = FindActiveObjectByPrefix(FireballExplosionImpactVfxObjectNamePrefix) != null;
                    if (impactVfxActive && !sawImpactVfxGap)
                        sawFirstImpactVfx = true;
                    else if (!impactVfxActive && sawFirstImpactVfx)
                        sawImpactVfxGap = true;
                    else if (impactVfxActive && sawImpactVfxGap)
                        sawSecondImpactVfx = true;

                    if (sawProjectile
                        && projectileMoved
                        && projectileGone
                        && sawFirstImpactVfx
                        && sawImpactVfxGap
                        && sawSecondImpactVfx)
                    {
                        break;
                    }
                }

                projectilePresenterActiveCount = manager.RuntimeContext.Slice?.ProjectilePresenter != null
                    ? manager.RuntimeContext.Slice.ProjectilePresenter.ActiveCount
                    : -1;
                impactPresenterActiveCount = manager.RuntimeContext.Slice?.VfxPresenter != null
                    ? manager.RuntimeContext.Slice.VfxPresenter.ActiveCount
                    : -1;
            }

            yield return new ExitPlayMode();

            Assert.IsTrue(managerFound, "CharacterTestScene did not create a GameManager in Play Mode.");
            Assert.IsTrue(menuClosed, "CharacterTest main menu should close before the fireball explosion smoke runs.");
            Assert.IsTrue(localPlayerFound, "CharacterTest scene did not spawn the local player.");
            Assert.IsTrue(targetPrepared, "CharacterTest scene did not expose a hostile target for the fireball explosion smoke.");
            Assert.IsTrue(actionStarted, "Fireball Explosion action did not start from the local player.");
            Assert.IsTrue(sawProjectile, "Fireball Explosion projectile flight VFX object was not created.");
            Assert.IsTrue(projectileMoved, "Fireball Explosion projectile flight VFX did not follow projectile movement.");
            Assert.IsTrue(projectileGone, "Fireball Explosion projectile flight VFX did not disappear after hit or expiry.");
            Assert.IsTrue(sawFirstImpactVfx, "Fireball Explosion initial timeline VFX was not produced after projectile hit.");
            Assert.IsTrue(sawImpactVfxGap, "Fireball Explosion initial VFX did not expire before the delayed afterblast.");
            Assert.IsTrue(sawSecondImpactVfx, "Fireball Explosion delayed afterblast VFX was not produced.");
            Assert.AreEqual(0, projectilePresenterActiveCount);
            Assert.GreaterOrEqual(impactPresenterActiveCount, 0);
        }

        [UnityTest]
        public IEnumerator CharacterTestScene_PlayModeFlameLanceShowsMovingProjectileVfxAndImpactVfx()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            GameManager manager = Object.FindAnyObjectByType<GameManager>();
            bool managerFound = manager != null;
            bool menuClosed = false;
            bool localPlayerFound = false;
            bool targetPrepared = false;
            bool actionStarted = false;
            bool sawProjectile = false;
            bool projectileMoved = false;
            bool projectileGone = false;
            bool sawImpactVfx = false;
            int projectilePresenterActiveCount = -1;
            int impactPresenterActiveCount = -1;
            Vector3 firstProjectilePosition = default;

            if (manager != null)
            {
                CharacterTestMainMenuCommandResult closeResult = manager.RuntimeContext.MainMenuCoordinator.CloseMainMenu();
                menuClosed = closeResult.Success && !manager.RuntimeContext.IsPaused;

                CharacterTestCharacterEntity localPlayer = null;
                for (int i = 0; i < 60; i++)
                {
                    yield return null;
                    localPlayer = FindLocalPlayer(manager.RuntimeContext.Slice?.CharacterEntities);
                    if (localPlayer != null)
                        break;
                }

                localPlayerFound = localPlayer != null;
                CharacterTestCharacterEntity target = FindFirstHostile(
                    manager.RuntimeContext.Slice?.CharacterEntities,
                    localPlayer);
                if (localPlayer != null && target != null)
                {
                    PrepareProjectileSmokeTarget(localPlayer, target);
                    targetPrepared = true;
                    yield return WaitForProjectileSmokeTargetSync();
                }

                if (localPlayer != null && manager.RuntimeContext.Slice?.ActionRuntime != null)
                {
                    actionStarted = manager.RuntimeContext.Slice.ActionRuntime.TryStartIntent(
                        localPlayer,
                        CharacterTestCharacterActionConfigSource.FlameLanceIntentId,
                        manager.RuntimeContext.Slice.CurrentFrame);
                }

                string projectileNamePrefix =
                    CharacterTestProjectilePresentationSource.FireballViewNamePrefix
                    + "_"
                    + CharacterTestFireballProjectileAdapter.FlameLanceDefinitionId
                    + "_";
                for (int i = 0; i < 220; i++)
                {
                    yield return null;
                    GameObject projectile = FindActiveObjectByPrefix(projectileNamePrefix);
                    if (projectile != null)
                    {
                        if (!sawProjectile)
                            firstProjectilePosition = projectile.transform.position;

                        sawProjectile = true;
                        if ((projectile.transform.position - firstProjectilePosition).sqrMagnitude > 0.0001f)
                            projectileMoved = true;
                    }
                    else if (sawProjectile)
                    {
                        projectileGone = true;
                    }

                    if (FindActiveObjectByPrefix(FlameLanceImpactVfxObjectNamePrefix) != null)
                        sawImpactVfx = true;

                    if (sawProjectile && projectileMoved && projectileGone && sawImpactVfx)
                        break;
                }

                projectilePresenterActiveCount = manager.RuntimeContext.Slice?.ProjectilePresenter != null
                    ? manager.RuntimeContext.Slice.ProjectilePresenter.ActiveCount
                    : -1;
                impactPresenterActiveCount = manager.RuntimeContext.Slice?.VfxPresenter != null
                    ? manager.RuntimeContext.Slice.VfxPresenter.ActiveCount
                    : -1;
            }

            yield return new ExitPlayMode();

            Assert.IsTrue(managerFound, "CharacterTestScene did not create a GameManager in Play Mode.");
            Assert.IsTrue(menuClosed, "CharacterTest main menu should close before the flame lance smoke runs.");
            Assert.IsTrue(localPlayerFound, "CharacterTest scene did not spawn the local player.");
            Assert.IsTrue(targetPrepared, "CharacterTest scene did not expose a hostile target for the flame lance smoke.");
            Assert.IsTrue(actionStarted, "Flame Lance action did not start from the local player.");
            Assert.IsTrue(sawProjectile, "Flame Lance projectile flight VFX object was not created.");
            Assert.IsTrue(projectileMoved, "Flame Lance projectile flight VFX did not follow projectile movement.");
            Assert.IsTrue(projectileGone, "Flame Lance projectile flight VFX did not disappear after hit or expiry.");
            Assert.IsTrue(sawImpactVfx, "Flame Lance impact VFX was not produced after projectile hit.");
            Assert.AreEqual(0, projectilePresenterActiveCount);
            Assert.GreaterOrEqual(impactPresenterActiveCount, 0);
        }

        [UnityTest]
        public IEnumerator CharacterTestScene_PlayModeBeamLanceAssignedSlotStartsCooldownAndBeamVfx()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            GameManager manager = Object.FindAnyObjectByType<GameManager>();
            bool managerFound = manager != null;
            bool menuClosed = false;
            bool localPlayerFound = false;
            bool slotAssigned = false;
            bool commandAccepted = false;
            bool actionStarted = false;
            bool cooldownStarted = false;
            bool sawBeamVfx = false;
            bool beamRootHeightClose = false;
            float beamRootHeightDelta = float.PositiveInfinity;

            if (manager != null)
            {
                CharacterTestMainMenuCommandResult closeResult = manager.RuntimeContext.MainMenuCoordinator.CloseMainMenu();
                menuClosed = closeResult.Success && !manager.RuntimeContext.IsPaused;

                CharacterTestCharacterEntity localPlayer = null;
                for (int i = 0; i < 60; i++)
                {
                    yield return null;
                    localPlayer = FindLocalPlayer(manager.RuntimeContext.Slice?.CharacterEntities);
                    if (localPlayer != null)
                        break;
                }

                localPlayerFound = localPlayer != null;
                GameSlice slice = manager.RuntimeContext.Slice;
                if (localPlayer != null && slice != null && slice.AbilityHotbar != null)
                {
                    slotAssigned = slice.AbilityHotbar.AssignSlot(
                        9,
                        CharacterTestAbilityIds.BeamLanceAbilityId,
                        out _);
                    RuntimeCommandValidationResult commandResult = slice.EnqueueCastAbilitySlot(
                        localPlayer.Id.Value,
                        slotNumber: 9,
                        abilityId: CharacterTestAbilityIds.BeamLanceAbilityId,
                        traceId: new RuntimeTraceIdentity(945421));
                    commandAccepted = commandResult.Success;
                }

                for (int i = 0; i < 90; i++)
                {
                    yield return null;
                    if (localPlayer != null
                        && string.Equals(
                            (localPlayer.Action.ActiveActionId).ToString(),
                            (CharacterTestCharacterActionConfigSource.BeamLanceActionId).ToString(),
                            StringComparison.Ordinal))
                    {
                        actionStarted = true;
                    }

                    if (localPlayer != null
                        && slice != null
                        && slice.AbilityRuntime != null
                        && slice.AbilityRuntime.Definitions.TryGet(
                            CharacterTestAbilityIds.BeamLanceAbilityId,
                            out AbilityDefinitionConfig ability)
                        && slice.AbilityRuntime.GetRemainingCooldownFrames(
                            localPlayer.Id.Value,
                            ability,
                            slice.CurrentFrame) > 0)
                    {
                        cooldownStarted = true;
                    }

                    GameObject beamVfx = FindActiveObjectByPrefix(BeamLanceBeamVfxObjectNamePrefix);
                    if (beamVfx != null)
                    {
                        sawBeamVfx = true;
                        if (localPlayer != null)
                        {
                            float expectedY = localPlayer.Transform.Y + CharacterTestActionVfxAnchor.HeightOffsetMeters;
                            beamRootHeightDelta = Mathf.Abs(beamVfx.transform.position.y - expectedY);
                            beamRootHeightClose |= beamRootHeightDelta <= 0.35f;
                        }
                    }

                    if (actionStarted && cooldownStarted && sawBeamVfx && beamRootHeightClose)
                        break;
                }
            }

            yield return new ExitPlayMode();

            Assert.IsTrue(managerFound, "CharacterTestScene did not create a GameManager in Play Mode.");
            Assert.IsTrue(menuClosed, "CharacterTest main menu should close before the beam lance smoke runs.");
            Assert.IsTrue(localPlayerFound, "CharacterTest scene did not spawn the local player.");
            Assert.IsTrue(slotAssigned, "Beam Lance should assign into hotbar slot 9 for the smoke.");
            Assert.IsTrue(commandAccepted, "Beam Lance slot 9 cast command was rejected.");
            Assert.IsTrue(actionStarted, "Beam Lance action did not start from the assigned hotbar slot.");
            Assert.IsTrue(cooldownStarted, "Beam Lance cooldown did not start after the hotbar cast.");
            Assert.IsTrue(sawBeamVfx, "Beam Lance beam VFX object was not created.");
            Assert.IsTrue(
                beamRootHeightClose,
                "Beam Lance beam root height should match the CharacterTest muzzle anchor. delta="
                + beamRootHeightDelta.ToString("0.###"));
        }

        [UnityTest]
        public IEnumerator CharacterTestScene_PlayModeChainLightningAssignedSlotStartsCooldownAndArcVfx()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            GameManager manager = Object.FindAnyObjectByType<GameManager>();
            bool managerFound = manager != null;
            bool menuClosed = false;
            bool localPlayerFound = false;
            bool targetPrepared = false;
            bool slotAssigned = false;
            bool commandAccepted = false;
            bool actionStarted = false;
            bool cooldownStarted = false;
            bool sawChargeVfx = false;
            bool sawArcVfx = false;
            bool sawImpactVfx = false;
            bool targetDamaged = false;
            int targetHealthBefore = 0;
            int targetHealthAfter = 0;

            if (manager != null)
            {
                CharacterTestMainMenuCommandResult closeResult = manager.RuntimeContext.MainMenuCoordinator.CloseMainMenu();
                menuClosed = closeResult.Success && !manager.RuntimeContext.IsPaused;

                CharacterTestCharacterEntity localPlayer = null;
                for (int i = 0; i < 60; i++)
                {
                    yield return null;
                    localPlayer = FindLocalPlayer(manager.RuntimeContext.Slice?.CharacterEntities);
                    if (localPlayer != null)
                        break;
                }

                localPlayerFound = localPlayer != null;
                GameSlice slice = manager.RuntimeContext.Slice;
                CharacterTestCharacterEntity target = FindFirstHostile(slice?.CharacterEntities, localPlayer);
                if (localPlayer != null && target != null)
                {
                    PrepareProjectileSmokeTarget(localPlayer, target);
                    yield return WaitForProjectileSmokeTargetSync();
                    targetPrepared = true;
                    targetHealthBefore = target.CombatState.Health;
                }

                if (localPlayer != null && slice != null && slice.AbilityHotbar != null)
                {
                    slotAssigned = slice.AbilityHotbar.AssignSlot(
                        9,
                        CharacterTestAbilityIds.ChainLightningAbilityId,
                        out _);
                    RuntimeCommandValidationResult commandResult = slice.EnqueueCastAbilitySlot(
                        localPlayer.Id.Value,
                        slotNumber: 9,
                        abilityId: CharacterTestAbilityIds.ChainLightningAbilityId,
                        traceId: new RuntimeTraceIdentity(972769));
                    commandAccepted = commandResult.Success;
                }

                for (int i = 0; i < 120; i++)
                {
                    yield return null;
                    if (localPlayer != null
                        && string.Equals(
                            (localPlayer.Action.ActiveActionId).ToString(),
                            (CharacterTestCharacterActionConfigSource.ChainLightningActionId).ToString(),
                            StringComparison.Ordinal))
                    {
                        actionStarted = true;
                    }

                    if (localPlayer != null
                        && slice != null
                        && slice.AbilityRuntime != null
                        && slice.AbilityRuntime.Definitions.TryGet(
                            CharacterTestAbilityIds.ChainLightningAbilityId,
                            out AbilityDefinitionConfig ability)
                        && slice.AbilityRuntime.GetRemainingCooldownFrames(
                            localPlayer.Id.Value,
                            ability,
                            slice.CurrentFrame) > 0)
                    {
                        cooldownStarted = true;
                    }

                    sawChargeVfx |= FindActiveObjectByPrefix(ChainLightningChargeVfxObjectNamePrefix) != null;
                    sawArcVfx |= FindActiveObjectByPrefix(ChainLightningArcVfxObjectNamePrefix) != null;
                    sawImpactVfx |= FindActiveObjectByPrefix(ChainLightningImpactVfxObjectNamePrefix) != null;
                    if (target != null && target.CombatState.Health < targetHealthBefore)
                    {
                        targetDamaged = true;
                        targetHealthAfter = target.CombatState.Health;
                    }

                    if (actionStarted
                        && cooldownStarted
                        && sawChargeVfx
                        && sawArcVfx
                        && sawImpactVfx
                        && targetDamaged)
                    {
                        break;
                    }
                }
            }

            yield return new ExitPlayMode();

            Assert.IsTrue(managerFound, "CharacterTestScene did not create a GameManager in Play Mode.");
            Assert.IsTrue(menuClosed, "CharacterTest main menu should close before the chain lightning smoke runs.");
            Assert.IsTrue(localPlayerFound, "CharacterTest scene did not spawn the local player.");
            Assert.IsTrue(targetPrepared, "CharacterTest scene did not expose a hostile target for the chain lightning smoke.");
            Assert.IsTrue(slotAssigned, "Chain Lightning should assign into hotbar slot 9 for the smoke.");
            Assert.IsTrue(commandAccepted, "Chain Lightning slot 9 cast command was rejected.");
            Assert.IsTrue(actionStarted, "Chain Lightning action did not start from the assigned hotbar slot.");
            Assert.IsTrue(cooldownStarted, "Chain Lightning cooldown did not start after the hotbar cast.");
            Assert.IsTrue(sawChargeVfx, "Chain Lightning charge VFX object was not created.");
            Assert.IsTrue(sawArcVfx, "Chain Lightning arc VFX object was not created.");
            Assert.IsTrue(sawImpactVfx, "Chain Lightning impact VFX object was not created.");
            Assert.IsTrue(
                targetDamaged,
                "Chain Lightning did not damage the prepared hostile target. before="
                + targetHealthBefore.ToString(CultureInfo.InvariantCulture)
                + " after="
                + targetHealthAfter.ToString(CultureInfo.InvariantCulture));
        }

        [UnityTest]
        public IEnumerator CharacterTestScene_PlayModePoisonBottleAssignedSlotStartsCooldownAndPoolPoisonTickVfx()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            GameManager manager = Object.FindAnyObjectByType<GameManager>();
            bool managerFound = manager != null;
            bool menuClosed = false;
            bool localPlayerFound = false;
            bool targetPrepared = false;
            bool targetLocked = false;
            bool slotAssigned = false;
            bool commandAccepted = false;
            bool actionStarted = false;
            bool cooldownStarted = false;
            bool sawProjectile = false;
            bool projectileMoved = false;
            bool projectileGone = false;
            bool sawShatterVfx = false;
            bool sawPoolVfx = false;
            bool sawPoisonTickVfx = false;
            bool sawPoisonRuntimeTick = false;
            bool sawPoisonTickPresentation = false;
            bool targetDamaged = false;
            int targetHealthBefore = 0;
            int targetHealthAfter = 0;
            Vector3 firstProjectilePosition = default;

            if (manager != null)
            {
                CharacterTestMainMenuCommandResult closeResult = manager.RuntimeContext.MainMenuCoordinator.CloseMainMenu();
                menuClosed = closeResult.Success && !manager.RuntimeContext.IsPaused;

                CharacterTestCharacterEntity localPlayer = null;
                for (int i = 0; i < 60; i++)
                {
                    yield return null;
                    localPlayer = FindLocalPlayer(manager.RuntimeContext.Slice?.CharacterEntities);
                    if (localPlayer != null)
                        break;
                }

                localPlayerFound = localPlayer != null;
                GameSlice slice = manager.RuntimeContext.Slice;
                CharacterTestCharacterEntity target = FindFirstHostile(slice?.CharacterEntities, localPlayer);
                if (localPlayer != null && target != null)
                {
                    PrepareProjectileSmokeTarget(localPlayer, target);
                    yield return WaitForProjectileSmokeTargetSync();
                    targetPrepared = true;
                    targetHealthBefore = target.CombatState.Health;
                    targetHealthAfter = targetHealthBefore;

                    RuntimeCommandValidationResult lockResult = slice.EnqueueRuntimeCommand(
                        CharacterTestRuntimeCommandFactory.SetTargetLock(
                            slice.CurrentFrame,
                            CharacterTestRuntimeCommandSources.SkillPanel,
                            true,
                            target.Id.Value,
                            new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0011)));
                    if (lockResult.Success)
                    {
                        for (int lockFrame = 0; lockFrame < 20; lockFrame++)
                        {
                            yield return null;
                            if (slice.TargetLockEnabled && slice.LockedTargetEntityId == target.Id.Value)
                            {
                                targetLocked = true;
                                break;
                            }
                        }
                    }
                }

                if (localPlayer != null && slice != null && slice.AbilityHotbar != null)
                {
                    slotAssigned = slice.AbilityHotbar.AssignSlot(
                        9,
                        CharacterTestAbilityIds.PoisonBottleAbilityId,
                        out _);
                    RuntimeCommandValidationResult commandResult = slice.EnqueueCastAbilitySlot(
                        localPlayer.Id.Value,
                        slotNumber: 9,
                        abilityId: CharacterTestAbilityIds.PoisonBottleAbilityId,
                        traceId: new RuntimeTraceIdentity(905420));
                    commandAccepted = commandResult.Success;
                }

                string projectileNamePrefix =
                    CharacterTestProjectilePresentationSource.FireballViewNamePrefix
                    + "_"
                    + CharacterTestFireballProjectileAdapter.PoisonBottleDefinitionId
                    + "_";
                for (int i = 0; i < 420; i++)
                {
                    yield return null;
                    if (localPlayer != null
                        && string.Equals(
                            (localPlayer.Action.ActiveActionId).ToString(),
                            (CharacterTestCharacterActionConfigSource.PoisonBottleActionId).ToString(),
                            StringComparison.Ordinal))
                    {
                        actionStarted = true;
                    }

                    if (localPlayer != null
                        && slice != null
                        && slice.AbilityRuntime != null
                        && slice.AbilityRuntime.Definitions.TryGet(
                            CharacterTestAbilityIds.PoisonBottleAbilityId,
                            out AbilityDefinitionConfig ability)
                        && slice.AbilityRuntime.GetRemainingCooldownFrames(
                            localPlayer.Id.Value,
                            ability,
                            slice.CurrentFrame) > 0)
                    {
                        cooldownStarted = true;
                    }

                    GameObject projectile = FindActiveObjectByPrefix(projectileNamePrefix);
                    if (projectile != null)
                    {
                        if (!sawProjectile)
                            firstProjectilePosition = projectile.transform.position;

                        sawProjectile = true;
                        if ((projectile.transform.position - firstProjectilePosition).sqrMagnitude > 0.0001f)
                            projectileMoved = true;
                    }
                    else if (sawProjectile)
                    {
                        projectileGone = true;
                    }

                    sawShatterVfx |= FindActiveObjectByPrefix(PoisonBottleShatterVfxObjectNamePrefix) != null;
                    sawPoolVfx |= FindActiveObjectByPrefix(PoisonBottlePoolVfxObjectNamePrefix) != null;
                    sawPoisonTickVfx |= FindActiveObjectByPrefix(PoisonedTickVfxObjectNamePrefix) != null;
                    if (slice?.CombatTraceSystem?.LastPoisonTickTargetCount > 0)
                        sawPoisonRuntimeTick = true;
                    IReadOnlyList<HitPresentationEvent> hitPresentationEvents =
                        slice?.CombatTraceSystem?.HitPresentationEvents;
                    if (hitPresentationEvents != null)
                    {
                        for (int eventIndex = 0; eventIndex < hitPresentationEvents.Count; eventIndex++)
                        {
                            if (hitPresentationEvents[eventIndex].PresentationProfileId == CharacterTestCharacterActionConfigSource.PoisonTickPresentationProfileId)
                            {
                                sawPoisonTickPresentation = true;
                                break;
                            }
                        }
                    }

                    if (target != null && target.CombatState.Health < targetHealthBefore)
                    {
                        targetDamaged = true;
                        targetHealthAfter = target.CombatState.Health;
                    }

                    if (actionStarted
                        && cooldownStarted
                        && sawProjectile
                        && projectileMoved
                        && projectileGone
                        && sawShatterVfx
                        && sawPoolVfx
                        && sawPoisonTickVfx
                        && sawPoisonRuntimeTick
                        && sawPoisonTickPresentation
                        && targetDamaged)
                    {
                        break;
                    }
                }
            }

            yield return new ExitPlayMode();

            Assert.IsTrue(managerFound, "CharacterTestScene did not create a GameManager in Play Mode.");
            Assert.IsTrue(menuClosed, "CharacterTest main menu should close before the poison bottle smoke runs.");
            Assert.IsTrue(localPlayerFound, "CharacterTest scene did not spawn the local player.");
            Assert.IsTrue(targetPrepared, "CharacterTest scene did not expose a hostile target for the poison bottle smoke.");
            Assert.IsTrue(targetLocked, "Poison Bottle smoke should lock the prepared hostile target before casting.");
            Assert.IsTrue(slotAssigned, "Poison Bottle should assign into hotbar slot 9 for the smoke.");
            Assert.IsTrue(commandAccepted, "Poison Bottle slot 9 cast command was rejected.");
            Assert.IsTrue(actionStarted, "Poison Bottle action did not start from the assigned hotbar slot.");
            Assert.IsTrue(cooldownStarted, "Poison Bottle cooldown did not start after the hotbar cast.");
            Assert.IsTrue(sawProjectile, "Poison Bottle projectile flight VFX object was not created.");
            Assert.IsTrue(projectileMoved, "Poison Bottle projectile flight VFX did not follow projectile movement.");
            Assert.IsTrue(projectileGone, "Poison Bottle projectile flight VFX did not disappear after hit or expiry.");
            Assert.IsTrue(sawShatterVfx, "Poison Bottle shatter VFX object was not created.");
            Assert.IsTrue(sawPoolVfx, "Poison Bottle pool VFX object was not created.");
            Assert.IsTrue(sawPoisonRuntimeTick, "Poisoned runtime tick did not execute after Poison Bottle pool refresh.");
            Assert.IsTrue(sawPoisonTickPresentation, "Poisoned tick hit presentation was not published.");
            Assert.IsTrue(sawPoisonTickVfx, "Poisoned tick VFX object was not created after Poison Bottle pool refresh.");
            Assert.IsTrue(
                targetDamaged,
                "Poison Bottle poison did not damage the prepared hostile target. before="
                + targetHealthBefore.ToString(CultureInfo.InvariantCulture)
                + " after="
                + targetHealthAfter.ToString(CultureInfo.InvariantCulture));
        }

        private static void AssertPrefabPath(GameObject prefab, string expectedPath)
        {
            Assert.IsNotNull(prefab, expectedPath + " should be bound in CharacterTestSceneProfile.");
            Assert.AreEqual(expectedPath, AssetDatabase.GetAssetPath(prefab));
        }

        private static void AssertVfxPrefabStructure(
            string path,
            int minParticleSystems,
            params string[] expectedChildren)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path + " should exist.");
            Assert.GreaterOrEqual(
                prefab.GetComponentsInChildren<ParticleSystem>(includeInactive: true).Length,
                minParticleSystems,
                path + " should use layered particle systems.");
            for (int i = 0; i < expectedChildren.Length; i++)
                Assert.IsNotNull(prefab.transform.Find(expectedChildren[i]), path + " missing child " + expectedChildren[i] + ".");
        }

        private static void AssertVfxPrefabChildrenLocalY(
            string path,
            float maxAbsLocalY,
            params string[] expectedChildren)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path + " should exist.");
            for (int i = 0; i < expectedChildren.Length; i++)
            {
                Transform child = prefab.transform.Find(expectedChildren[i]);
                Assert.IsNotNull(child, path + " missing child " + expectedChildren[i] + ".");
                Assert.LessOrEqual(
                    Mathf.Abs(child.localPosition.y),
                    maxAbsLocalY,
                    path + " child " + expectedChildren[i] + " should not add extra muzzle height.");
            }
        }

        private static void AssertVfxPrefabChildLocalZ(
            string path,
            string childName,
            float expectedZ,
            float tolerance)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path + " should exist.");
            Transform child = prefab.transform.Find(childName);
            Assert.IsNotNull(child, path + " missing child " + childName + ".");
            Assert.AreEqual(expectedZ, child.localPosition.z, tolerance);
        }

        private static void AssertLoopingFlightParticle(
            string path,
            string childName,
            float minDurationSeconds)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path + " should exist.");
            Transform child = prefab.transform.Find(childName);
            Assert.IsNotNull(child, path + " missing child " + childName + ".");

            ParticleSystem particles = child.GetComponent<ParticleSystem>();
            Assert.IsNotNull(particles, path + " child " + childName + " should have a ParticleSystem.");

            ParticleSystem.MainModule main = particles.main;
            Assert.IsTrue(main.loop, path + " child " + childName + " should loop for long poison bottle throws.");
            Assert.GreaterOrEqual(
                main.duration,
                minDurationSeconds,
                path + " child " + childName + " duration should cover the longest poison bottle flight.");

            ParticleSystem.EmissionModule emission = particles.emission;
            Assert.IsTrue(emission.enabled, path + " child " + childName + " should emit while flying.");
            Assert.Greater(
                emission.rateOverTime.constantMax,
                0f,
                path + " child " + childName + " should continuously emit while flying.");
        }

        private static CharacterTestCharacterEntity FindLocalPlayer(
            IReadOnlyList<CharacterTestCharacterEntity> entities)
        {
            if (entities == null)
                return null;

            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer)
                    return entity;
            }

            return null;
        }

        private static CharacterTestCharacterEntity FindFirstHostile(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestCharacterEntity source)
        {
            if (entities == null || source == null)
                return null;

            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (!ReferenceEquals(entity, source) && entity.Team.TeamId != source.Team.TeamId)
                    return entity;
            }

            return null;
        }

        private static void PrepareProjectileSmokeTarget(
            CharacterTestCharacterEntity localPlayer,
            CharacterTestCharacterEntity target)
        {
            localPlayer.SetTransform(new CharacterTestCharacterTransformComponent(
                localPlayer.Transform.X,
                localPlayer.Transform.Y,
                localPlayer.Transform.Z,
                yawDegrees: 0f));

            // CharacterTest projectiles spawn from the baked mainHand socket at the gameplay request frame.
            target.SetTransform(new CharacterTestCharacterTransformComponent(
                localPlayer.Transform.X + DefaultProjectileSocketLineXOffset,
                0f,
                localPlayer.Transform.Z + ProjectileSmokeTargetDistanceZ,
                yawDegrees: 180f));
        }

        private static void PrepareTornadoSmokeTargets(
            CharacterTestCharacterEntity localPlayer,
            CharacterTestCharacterEntity pullTarget,
            CharacterTestCharacterEntity damageTarget)
        {
            localPlayer.SetTransform(new CharacterTestCharacterTransformComponent(
                localPlayer.Transform.X,
                localPlayer.Transform.Y,
                localPlayer.Transform.Z,
                yawDegrees: 0f));

            pullTarget.SetTransform(new CharacterTestCharacterTransformComponent(
                localPlayer.Transform.X + TornadoSmokeTargetSideOffsetX,
                0f,
                localPlayer.Transform.Z + TornadoSmokePullTargetDistanceZ,
                yawDegrees: 180f));

            damageTarget.SetTransform(new CharacterTestCharacterTransformComponent(
                localPlayer.Transform.X,
                0f,
                localPlayer.Transform.Z + TornadoSmokeDamageTargetDistanceZ,
                yawDegrees: 180f));
        }

        private static int PrepareTornadoStressTargets(
            CharacterTestCharacterEntity localPlayer,
            List<CharacterTestCharacterEntity> hostiles,
            int requestedTargetCount)
        {
            if (localPlayer == null || hostiles == null || hostiles.Count < 2 || requestedTargetCount < 2)
                return 0;

            int preparedCount = Math.Min(hostiles.Count, requestedTargetCount);
            PrepareTornadoSmokeTargets(localPlayer, hostiles[0], hostiles[1]);
            for (int i = 2; i < preparedCount; i++)
            {
                CharacterTestCharacterEntity target = hostiles[i];
                Vector3 targetPosition = CalculateTornadoStressTargetPosition(localPlayer, i);
                target.SetInput(CharacterTestCharacterInputComponent.None);
                target.ClearMotionRequests();
                target.SetTransform(new CharacterTestCharacterTransformComponent(
                    targetPosition.x,
                    0f,
                    targetPosition.z,
                    yawDegrees: 180f));
            }

            return preparedCount;
        }

        private static Vector3 CalculateTornadoStressTargetPosition(
            CharacterTestCharacterEntity localPlayer,
            int targetOrdinal)
        {
            int column = targetOrdinal % TornadoProfileAnalyzerStressTargetColumns;
            int row = targetOrdinal / TornadoProfileAnalyzerStressTargetColumns;
            float centeredColumn = column - ((TornadoProfileAnalyzerStressTargetColumns - 1) * 0.5f);
            return new Vector3(
                localPlayer.Transform.X + (centeredColumn * TornadoProfileAnalyzerStressTargetSpacingX),
                0f,
                localPlayer.Transform.Z
                + TornadoProfileAnalyzerStressTargetBaseDistanceZ
                + (row * TornadoProfileAnalyzerStressTargetSpacingZ));
        }

        private static List<CharacterTestCharacterEntity> FindHostiles(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestCharacterEntity source)
        {
            var hostiles = new List<CharacterTestCharacterEntity>();
            if (entities == null || source == null)
                return hostiles;

            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (!ReferenceEquals(entity, source) && entity.Team.TeamId != source.Team.TeamId)
                    hostiles.Add(entity);
            }

            return hostiles;
        }

        private static int SumUniqueHealth(
            CharacterTestCharacterEntity first,
            CharacterTestCharacterEntity second)
        {
            int total = first != null ? first.CombatState.Health : 0;
            if (second != null && !ReferenceEquals(first, second))
                total += second.CombatState.Health;
            return total;
        }

        private static IEnumerator WaitForProjectileSmokeTargetSync()
        {
            for (int i = 0; i < 8; i++)
                yield return null;
        }

        private static GameObject FindActiveObjectByPrefix(string prefix)
        {
            GameObject[] objects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude);
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i].name.StartsWith(prefix, System.StringComparison.Ordinal))
                    return objects[i];
            }

            return null;
        }

        private sealed class TornadoProfilerRecorderCapture : IDisposable
        {
            private readonly string[] _markerNames;
            private readonly ProfilerRecorder[] _recorders;
            private readonly long[][] _samples;
            private readonly bool[] _valid;
            private int _sampleCount;

            private TornadoProfilerRecorderCapture(string[] markerNames, int sampleCapacity)
            {
                _markerNames = markerNames ?? Array.Empty<string>();
                _recorders = new ProfilerRecorder[_markerNames.Length];
                _samples = new long[_markerNames.Length][];
                _valid = new bool[_markerNames.Length];
                int capacity = Math.Max(1, sampleCapacity);
                for (int i = 0; i < _markerNames.Length; i++)
                {
                    _recorders[i] = ProfilerRecorder.StartNew(
                        ProfilerCategory.Scripts,
                        _markerNames[i],
                        capacity: 1);
                    _valid[i] = _recorders[i].Valid;
                    _samples[i] = new long[capacity];
                }
            }

            /// <remarks>AllocByDesign: test-only explicit profiler evidence capture.</remarks>
            public static TornadoProfilerRecorderCapture Start(string[] markerNames, int sampleCapacity)
            {
                return new TornadoProfilerRecorderCapture(markerNames, sampleCapacity);
            }

            public void Sample()
            {
                if (_samples.Length == 0 || _sampleCount >= _samples[0].Length)
                    return;

                for (int i = 0; i < _recorders.Length; i++)
                {
                    long value = _valid[i] ? _recorders[i].LastValue : 0L;
                    _samples[i][_sampleCount] = Math.Max(0L, value);
                }

                _sampleCount++;
            }

            public bool HasNonZeroSample(string markerName)
            {
                int markerIndex = IndexOfMarker(markerName);
                if (markerIndex < 0)
                    return false;

                long[] samples = _samples[markerIndex];
                int count = Math.Min(_sampleCount, samples.Length);
                for (int i = 0; i < count; i++)
                {
                    if (samples[i] > 0L)
                        return true;
                }

                return false;
            }

            /// <remarks>AllocByDesign: creates an explicit profiler evidence report for tests/manual review.</remarks>
            public string CreateReportText(
                string scenarioName,
                long captureStartFrame,
                long frame,
                int captureFrameCount)
            {
                var builder = new StringBuilder(4096);
                builder.Append("scenario: ").AppendLine(scenarioName);
                builder.AppendLine("profileKind: tornado-playmode-profiler-recorder");
                builder.Append("captureStartFrame: ")
                    .AppendLine(Math.Max(0L, captureStartFrame).ToString(CultureInfo.InvariantCulture));
                builder.Append("frame: ")
                    .AppendLine(Math.Max(0L, frame).ToString(CultureInfo.InvariantCulture));
                builder.Append("captureFrames: ")
                    .AppendLine(Math.Max(1, captureFrameCount).ToString(CultureInfo.InvariantCulture));
                builder.Append("sampledFrames: ")
                    .AppendLine(_sampleCount.ToString(CultureInfo.InvariantCulture));
                builder.Append("markerCount: ")
                    .AppendLine(_markerNames.Length.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine("markers:");

                for (int i = 0; i < _markerNames.Length; i++)
                    AppendMarkerStats(builder, _markerNames[i], _valid[i], _samples[i], _sampleCount);

                return builder.ToString();
            }

            public void Dispose()
            {
                for (int i = 0; i < _recorders.Length; i++)
                    _recorders[i].Dispose();
            }

            private int IndexOfMarker(string markerName)
            {
                for (int i = 0; i < _markerNames.Length; i++)
                {
                    if (string.Equals(_markerNames[i], markerName, StringComparison.Ordinal))
                        return i;
                }

                return -1;
            }

            private static void AppendMarkerStats(
                StringBuilder builder,
                string markerName,
                bool available,
                long[] samples,
                int sampleCount)
            {
                long[] sorted = CopyAndSort(samples, sampleCount, includeZero: true);
                long[] activeSorted = CopyAndSort(samples, sampleCount, includeZero: false);
                builder.Append("- marker: ").AppendLine(markerName);
                builder.Append("  available: ").AppendLine(available ? "true" : "false");
                builder.Append("  samples: ")
                    .AppendLine(Math.Max(0, sampleCount).ToString(CultureInfo.InvariantCulture));
                builder.Append("  activeSamples: ")
                    .AppendLine(activeSorted.Length.ToString(CultureInfo.InvariantCulture));
                builder.Append("  elapsedMs.min: ").AppendLine(FormatNanoseconds(sorted.Length > 0 ? sorted[0] : 0L));
                builder.Append("  elapsedMs.median: ").AppendLine(FormatNanoseconds(Percentile(sorted, 0.5d)));
                builder.Append("  elapsedMs.mean: ")
                    .AppendLine(CalculateMeanMilliseconds(sorted).ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append("  elapsedMs.p95: ").AppendLine(FormatNanoseconds(Percentile(sorted, 0.95d)));
                builder.Append("  elapsedMs.max: ")
                    .AppendLine(FormatNanoseconds(sorted.Length > 0 ? sorted[sorted.Length - 1] : 0L));
                builder.Append("  activeElapsedMs.median: ")
                    .AppendLine(FormatNanoseconds(Percentile(activeSorted, 0.5d)));
                builder.Append("  activeElapsedMs.p95: ")
                    .AppendLine(FormatNanoseconds(Percentile(activeSorted, 0.95d)));
                builder.Append("  activeElapsedMs.max: ")
                    .AppendLine(FormatNanoseconds(activeSorted.Length > 0 ? activeSorted[activeSorted.Length - 1] : 0L));
            }

            private static long[] CopyAndSort(long[] samples, int sampleCount, bool includeZero)
            {
                int safeCount = Math.Max(0, Math.Min(sampleCount, samples != null ? samples.Length : 0));
                int count = 0;
                for (int i = 0; i < safeCount; i++)
                {
                    if (includeZero || samples[i] > 0L)
                        count++;
                }

                var copy = new long[count];
                int index = 0;
                for (int i = 0; i < safeCount; i++)
                {
                    long value = Math.Max(0L, samples[i]);
                    if (includeZero || value > 0L)
                        copy[index++] = value;
                }

                Array.Sort(copy);
                return copy;
            }

            private static long Percentile(long[] sorted, double percentile)
            {
                if (sorted == null || sorted.Length == 0)
                    return 0L;

                double clamped = Math.Max(0d, Math.Min(1d, percentile));
                int index = (int)Math.Round((sorted.Length - 1) * clamped, MidpointRounding.AwayFromZero);
                return sorted[Math.Max(0, Math.Min(sorted.Length - 1, index))];
            }

            private static double CalculateMeanMilliseconds(long[] sorted)
            {
                if (sorted == null || sorted.Length == 0)
                    return 0d;

                double total = 0d;
                for (int i = 0; i < sorted.Length; i++)
                    total += sorted[i];

                return total / sorted.Length / 1000000d;
            }

            private static string FormatNanoseconds(long nanoseconds)
            {
                double milliseconds = Math.Max(0L, nanoseconds) / 1000000d;
                return milliseconds.ToString("0.###", CultureInfo.InvariantCulture);
            }
        }

        private sealed class TornadoProfileAnalyzerCapture : IDisposable
        {
            private const string ProfileAnalyzerAssemblyName = "Unity.Performance.Profile-Analyzer.Editor";
            private const string ProgressBarDisplayTypeName =
                "UnityEditor.Performance.ProfileAnalyzer.ProgressBarDisplay";
            private const string ProfilerWindowInterfaceTypeName =
                "UnityEditor.Performance.ProfileAnalyzer.ProfilerWindowInterface";
            private const string ProfileDataTypeName =
                "UnityEditor.Performance.ProfileAnalyzer.ProfileData";

            private readonly string _scenarioName;
            private readonly string _pDataPath;
            private readonly string _summaryPath;
            private readonly bool _previousEnabled;
            private readonly bool _previousProfileEditor;
            private bool _recordingStopped;
            private int _firstRawFrameIndex = -1;
            private int _lastRawFrameIndex = -1;

            private TornadoProfileAnalyzerCapture(string scenarioName, string pDataPath, string summaryPath)
            {
                _scenarioName = string.IsNullOrEmpty(scenarioName)
                    ? TornadoProfileAnalyzerCaptureScenarioName
                    : scenarioName;
                _pDataPath = pDataPath;
                _summaryPath = summaryPath;
                _previousEnabled = ProfilerDriver.enabled;
                _previousProfileEditor = ProfilerDriver.profileEditor;

                Directory.CreateDirectory(Path.GetDirectoryName(_pDataPath) ?? ".");
                Directory.CreateDirectory(Path.GetDirectoryName(_summaryPath) ?? ".");
                ProfilerDriver.ClearAllFrames();
                ProfilerDriver.profileEditor = true;
                ProfilerDriver.enabled = true;
            }

            public bool PDataWritten { get; private set; }

            public string Issue { get; private set; } = string.Empty;

            /// <remarks>AllocByDesign: test-only explicit Profile Analyzer artifact capture.</remarks>
            public static TornadoProfileAnalyzerCapture Start(
                string scenarioName,
                string pDataPath,
                string summaryPath)
            {
                return new TornadoProfileAnalyzerCapture(scenarioName, pDataPath, summaryPath);
            }

            public void StopRecording()
            {
                if (_recordingStopped)
                    return;

                _firstRawFrameIndex = ProfilerDriver.firstFrameIndex;
                _lastRawFrameIndex = ProfilerDriver.lastFrameIndex;
                ProfilerDriver.enabled = false;
                _recordingStopped = true;
            }

            public void SaveProfileData()
            {
                StopRecording();
                try
                {
                    Debug.Log("CharacterTest Profile Analyzer save started. pData=" + _pDataPath);
                    bool hasFrames = _firstRawFrameIndex >= 0 && _lastRawFrameIndex >= _firstRawFrameIndex;
                    if (!hasFrames)
                    {
                        Issue = "ProfilerDriver did not expose frames for the Tornado Profile Analyzer capture.";
                        WriteSummary("no-profiler-frames");
                        return;
                    }

                    Type progressBarType = ResolveProfileAnalyzerType(ProgressBarDisplayTypeName);
                    Type profilerWindowInterfaceType = ResolveProfileAnalyzerType(ProfilerWindowInterfaceTypeName);
                    Type profileDataType = ResolveProfileAnalyzerType(ProfileDataTypeName);
                    if (progressBarType == null || profilerWindowInterfaceType == null || profileDataType == null)
                    {
                        Issue = "Unity Profile Analyzer package reflection types were not found.";
                        WriteSummary("profile-analyzer-types-missing");
                        return;
                    }

                    object progressBar = Activator.CreateInstance(
                        progressBarType,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        binder: null,
                        args: Array.Empty<object>(),
                        culture: CultureInfo.InvariantCulture);
                    object profilerWindowInterface = Activator.CreateInstance(
                        profilerWindowInterfaceType,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        binder: null,
                        args: new[] { progressBar },
                        culture: CultureInfo.InvariantCulture);
                    MethodInfo pullFromProfiler = profilerWindowInterfaceType.GetMethod(
                        "PullFromProfiler",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    MethodInfo save = profileDataType.GetMethod(
                        "Save",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (pullFromProfiler == null || save == null)
                    {
                        Issue = "Unity Profile Analyzer reflection methods were not found.";
                        WriteSummary("profile-analyzer-methods-missing");
                        return;
                    }

                    object profileData = pullFromProfiler.Invoke(
                        profilerWindowInterface,
                        new object[] { _firstRawFrameIndex + 1, _lastRawFrameIndex + 1 });
                    object result = save.Invoke(null, new[] { _pDataPath, profileData });
                    PDataWritten = result is bool saved && saved && File.Exists(_pDataPath);
                    if (!PDataWritten)
                        Issue = "Unity Profile Analyzer Save returned false or did not create the .pdata file.";
                    WriteSummary(PDataWritten ? "finished" : "save-failed");
                    Debug.Log(
                        "CharacterTest Profile Analyzer save completed. pDataWritten="
                        + PDataWritten.ToString(CultureInfo.InvariantCulture)
                        + ", saveResult="
                        + (result == null ? "null" : result.ToString()));
                }
                catch (Exception ex)
                {
                    Issue = ex.ToString();
                    WriteSummary("exception");
                    Debug.LogException(ex);
                }
                finally
                {
                    UnityEditor.EditorUtility.ClearProgressBar();
                }
            }

            public void Dispose()
            {
                if (!_recordingStopped)
                    StopRecording();

                ProfilerDriver.profileEditor = _previousProfileEditor;
                ProfilerDriver.enabled = _previousEnabled;
            }

            private static Type ResolveProfileAnalyzerType(string typeName)
            {
                Type type = Type.GetType(typeName + ", " + ProfileAnalyzerAssemblyName, throwOnError: false);
                if (type != null)
                    return type;

                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    if (!string.Equals(
                            assemblies[i].GetName().Name,
                            ProfileAnalyzerAssemblyName,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    return assemblies[i].GetType(typeName, throwOnError: false);
                }

                return null;
            }

            private void WriteSummary(string status)
            {
                var builder = new StringBuilder(1024);
                builder.Append("scenario: ").AppendLine(_scenarioName);
                builder.Append("status: ").AppendLine(status);
                builder.Append("utc: ").AppendLine(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                builder.AppendLine("profileKind: profile-analyzer-pdata-capture");
                builder.Append("pData: ").AppendLine(_pDataPath);
                builder.Append("pDataWritten: ").AppendLine(PDataWritten ? "true" : "false");
                builder.Append("firstRawFrameIndex: ")
                    .AppendLine(_firstRawFrameIndex.ToString(CultureInfo.InvariantCulture));
                builder.Append("lastRawFrameIndex: ")
                    .AppendLine(_lastRawFrameIndex.ToString(CultureInfo.InvariantCulture));
                builder.Append("firstDisplayFrameIndex: ")
                    .AppendLine((_firstRawFrameIndex + 1).ToString(CultureInfo.InvariantCulture));
                builder.Append("lastDisplayFrameIndex: ")
                    .AppendLine((_lastRawFrameIndex + 1).ToString(CultureInfo.InvariantCulture));
                builder.Append("maxCaptureFrames: ")
                    .AppendLine(TornadoProfileAnalyzerCaptureFrameCount.ToString(CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(Issue))
                {
                    builder.AppendLine("issue:");
                    builder.AppendLine(Issue);
                }

                builder.AppendLine("notes:");
                builder.AppendLine("- .pdata is saved from a fresh ProfilerDriver buffer owned by this PlayMode test.");
                builder.AppendLine("- export CSV with Tools/Harness/profile_analyzer_gc_summary.py --pdata.");
                File.WriteAllText(_summaryPath, builder.ToString());
            }
        }
    }

    public static class CharacterTestProfileAnalyzerCommandLineCapture
    {
        private const string PDataArgumentName = "-mxProfileAnalyzerPData";
        private const string SummaryArgumentName = "-mxProfileAnalyzerSummary";
        private const string StressTargetsArgumentName = "-mxProfileAnalyzerStressTargets";
        private const int MaxUpdateTicks = 20000;
        private static readonly Stack<IEnumerator> Enumerators = new Stack<IEnumerator>(8);
        private static bool s_waitOneUpdate;
        private static bool s_waitForPlayMode;
        private static bool s_waitForEditMode;
        private static int s_waitForFrameCount = -1;
        private static int s_updateTicks;

        /// <remarks>AllocByDesign: command-line only Profile Analyzer artifact capture.</remarks>
        public static void RunPairCapture()
        {
            StartPairCapture(useStressScenario: false);
        }

        /// <remarks>AllocByDesign: command-line only Profile Analyzer artifact capture.</remarks>
        public static void RunStressPairCapture()
        {
            StartPairCapture(useStressScenario: true);
        }

        private static void StartPairCapture(bool useStressScenario)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                CompleteFailure(
                    new InvalidOperationException(
                        "CharacterTest Profile Analyzer capture must start from Edit Mode."));
                return;
            }

            Enumerators.Clear();
            CharacterTestFireballPlayModeTests test = new CharacterTestFireballPlayModeTests();
            string pDataPath = ReadCommandLineArgument(PDataArgumentName);
            string summaryPath = ReadCommandLineArgument(SummaryArgumentName);
            if (useStressScenario)
            {
                int stressTargets = ReadIntCommandLineArgument(StressTargetsArgumentName);
                Enumerators.Push(test.CreateTornadoProfileAnalyzerStressPairCapture(
                    pDataPath,
                    summaryPath,
                    stressTargets));
            }
            else
            {
                Enumerators.Push(test.CreateTornadoProfileAnalyzerPairCapture(pDataPath, summaryPath));
            }

            s_waitOneUpdate = false;
            s_waitForPlayMode = false;
            s_waitForEditMode = false;
            s_waitForFrameCount = -1;
            s_updateTicks = 0;

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.Log(
                "CharacterTest Profile Analyzer pair capture started. pData="
                + pDataPath
                + " stressScenario="
                + useStressScenario);
        }

        private static string ReadCommandLineArgument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (string.Equals(arguments[i], name, StringComparison.Ordinal))
                    return arguments[i + 1];
            }

            return string.Empty;
        }

        private static int ReadIntCommandLineArgument(string name)
        {
            string value = ReadCommandLineArgument(name);
            if (string.IsNullOrEmpty(value))
                return 0;

            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
                ? result
                : 0;
        }

        private static void Tick()
        {
            try
            {
                s_updateTicks++;
                if (s_updateTicks > MaxUpdateTicks)
                {
                    throw new TimeoutException(
                        "CharacterTest Profile Analyzer pair capture exceeded its update tick budget.");
                }

                if (s_waitOneUpdate)
                {
                    s_waitOneUpdate = false;
                    return;
                }

                if (s_waitForFrameCount >= 0)
                {
                    if (Time.frameCount < s_waitForFrameCount)
                        return;

                    s_waitForFrameCount = -1;
                    return;
                }

                if (s_waitForPlayMode)
                {
                    if (EditorApplication.isPlaying)
                    {
                        s_waitForPlayMode = false;
                        WaitForNextStep();
                    }

                    return;
                }

                if (s_waitForEditMode)
                {
                    if (!EditorApplication.isPlaying)
                    {
                        s_waitForEditMode = false;
                        s_waitOneUpdate = true;
                    }

                    return;
                }

                Advance();
            }
            catch (Exception ex)
            {
                CompleteFailure(ex);
            }
        }

        private static void Advance()
        {
            while (Enumerators.Count > 0)
            {
                IEnumerator currentEnumerator = Enumerators.Peek();
                if (!currentEnumerator.MoveNext())
                {
                    Enumerators.Pop();
                    continue;
                }

                object yielded = currentEnumerator.Current;
                if (yielded == null)
                {
                    WaitForNextStep();
                    return;
                }

                if (yielded is IEnumerator nestedEnumerator)
                {
                    Enumerators.Push(nestedEnumerator);
                    continue;
                }

                if (yielded is EnterPlayMode)
                {
                    EditorApplication.EnterPlaymode();
                    s_waitForPlayMode = true;
                    return;
                }

                if (yielded is ExitPlayMode)
                {
                    EditorApplication.ExitPlaymode();
                    s_waitForEditMode = true;
                    return;
                }

                throw new NotSupportedException(
                    "Unsupported CharacterTest Profile Analyzer capture yield instruction: "
                    + yielded.GetType().FullName);
            }

            CompleteSuccess();
        }

        private static void WaitForNextStep()
        {
            if (EditorApplication.isPlaying)
            {
                s_waitForFrameCount = Time.frameCount + 1;
                return;
            }

            s_waitOneUpdate = true;
        }

        private static void CompleteSuccess()
        {
            EditorApplication.update -= Tick;
            Debug.Log("CharacterTest Profile Analyzer pair capture finished.");
            EditorApplication.Exit(0);
        }

        private static void CompleteFailure(Exception ex)
        {
            EditorApplication.update -= Tick;
            Debug.LogException(ex);
            EditorApplication.Exit(1);
        }
    }
}
