using System;
using System.Collections.Generic;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Projectiles.Unity;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using MxFramework.Vfx.CombatBridge;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestSwordWaveVfxMode
    {
        Flight = 0,
        Charge = 1,
        Impact = 2,
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Game/Character Test/Sword Wave VFX View")]
    public sealed class CharacterTestSwordWaveVfxView :
        MonoBehaviour,
        IUnityVfxInstanceReceiver,
        ICombatProjectileUnityPresentationReceiver
    {
        private const int ArcPointCount = 17;
        private const float FlightWidth = 1.42f;
        private const float FlightForwardDepth = 0.58f;
        private const float FlightLift = 0.34f;
        private const float FlightSlashSlope = 0.36f;
        private const float ChargeRadius = 0.34f;
        private const float ImpactRadius = 0.72f;

        [SerializeField] private CharacterTestSwordWaveVfxMode _mode;
        [SerializeField] private Material _lineMaterialOverride;

        private LineRenderer _coreArc;
        private LineRenderer _innerArc;
        private LineRenderer _haloArc;
        private LineRenderer _trailLeft;
        private LineRenderer _trailRight;
        private LineRenderer _ring;
        private LineRenderer _slashA;
        private LineRenderer _slashB;
        private Material _runtimeLineMaterial;
        private Material _lineMaterial;
        private float _ageSeconds;
        private float _flightRollDegrees;

        public CharacterTestSwordWaveVfxMode Mode => _mode;
        public float FlightRollDegrees => _flightRollDegrees;

        public static GameObject CreateRuntimePrefab(CharacterTestSwordWaveVfxMode mode)
        {
            var prefab = new GameObject("CharacterTest_SwordWave_" + mode + "_Vfx");
            prefab.hideFlags = HideFlags.HideAndDontSave;
            CharacterTestSwordWaveVfxView view = prefab.AddComponent<CharacterTestSwordWaveVfxView>();
            view._mode = mode;
            view.RebuildVisuals();
            prefab.SetActive(false);
            return prefab;
        }

        public void OnUnityVfxInstanceSpawned(VfxInstanceSnapshot instance)
        {
            _ageSeconds = 0f;
            RebuildVisuals();
        }

        public void OnCombatProjectilePresentationUpdated(in CombatProjectileUnityPresentationUpdate update)
        {
            if (_mode != CharacterTestSwordWaveVfxMode.Flight)
                return;

            _flightRollDegrees = ResolveFlightRollDegrees(update.LocalFrame);
            EnsureVisuals();
        }

        public static float ResolveFlightRollDegrees(int localFrame)
        {
            return CharacterTestFireballProjectileAdapter.ResolveSwordWaveRollDegrees(localFrame);
        }

        public void OnUnityVfxInstanceReleased(VfxInstanceId instanceId)
        {
        }

        public void RebuildVisuals()
        {
            EnsureVisuals();
        }

        private void OnEnable()
        {
            _ageSeconds = 0f;
            EnsureVisuals();
        }

        private void OnDisable()
        {
            if (_runtimeLineMaterial != null)
                DestroyUnityObject(_runtimeLineMaterial);

            _runtimeLineMaterial = null;
            _lineMaterial = null;
        }

        private void Update()
        {
            _ageSeconds += Time.deltaTime;
            EnsureVisuals();
        }

        private void EnsureVisuals()
        {
            Material material = ResolveLineMaterial();
            bool isFlight = _mode == CharacterTestSwordWaveVfxMode.Flight;
            bool isCharge = _mode == CharacterTestSwordWaveVfxMode.Charge;
            bool isImpact = _mode == CharacterTestSwordWaveVfxMode.Impact;

            SetLineEnabled(ref _coreArc, "CoreArc", isFlight || isCharge, material, 0.055f);
            SetLineEnabled(ref _innerArc, "InnerArc", isFlight, material, 0.032f);
            SetLineEnabled(ref _haloArc, "HaloArc", isFlight, material, 0.18f);
            SetLineEnabled(ref _trailLeft, "TrailLeft", isFlight, material, 0.035f);
            SetLineEnabled(ref _trailRight, "TrailRight", isFlight, material, 0.035f);
            SetLineEnabled(ref _ring, "Ring", isCharge || isImpact, material, isCharge ? 0.028f : 0.04f);
            SetLineEnabled(ref _slashA, "SlashA", isImpact, material, 0.055f);
            SetLineEnabled(ref _slashB, "SlashB", isImpact, material, 0.035f);

            if (isFlight)
                ApplyFlight();
            else if (isCharge)
                ApplyCharge();
            else
                ApplyImpact();
        }

        private void ApplyFlight()
        {
            float pulse = 0.5f + Mathf.Sin(_ageSeconds * 18f) * 0.5f;
            Color core = new Color(0.9f, 1f, 1f, 0.94f);
            Color inner = new Color(0.42f, 0.82f, 1f, 0.62f);
            Color halo = new Color(0.22f, 0.58f, 1f, 0.18f + pulse * 0.06f);
            Quaternion roll = Quaternion.AngleAxis(_flightRollDegrees, Vector3.forward);
            ApplyForwardBladeArc(
                _coreArc,
                FlightWidth,
                FlightForwardDepth,
                FlightLift,
                0f,
                0.12f,
                FlightSlashSlope,
                roll,
                core);
            ApplyForwardBladeArc(
                _innerArc,
                FlightWidth * 0.68f,
                FlightForwardDepth * 0.66f,
                FlightLift * 0.72f,
                -0.12f,
                0.08f,
                FlightSlashSlope * 0.7f,
                roll,
                inner);
            ApplyForwardBladeArc(
                _haloArc,
                FlightWidth * 1.12f,
                FlightForwardDepth * 0.92f,
                FlightLift * 0.82f,
                -0.05f,
                0.1f,
                FlightSlashSlope * 0.82f,
                roll,
                halo);
            ApplyTrail(
                _trailLeft,
                -FlightWidth * 0.34f,
                FlightLift * 0.55f,
                roll,
                new Color(0.55f, 0.9f, 1f, 0.32f));
            ApplyTrail(
                _trailRight,
                FlightWidth * 0.34f,
                FlightLift * 0.55f,
                roll,
                new Color(0.55f, 0.9f, 1f, 0.32f));
        }

        private void ApplyCharge()
        {
            float angle = _ageSeconds * 210f;
            Quaternion rotation = Quaternion.Euler(0f, angle, 0f);
            ApplyRing(_ring, ChargeRadius, 0f, new Color(0.62f, 0.9f, 1f, 0.58f), rotation);
            ApplyArc(_coreArc, ChargeRadius * 1.65f, ChargeRadius * 0.55f, 0f, 18f, 162f, new Color(0.92f, 1f, 1f, 0.86f));
        }

        private void ApplyImpact()
        {
            float fade = Mathf.Clamp01(1f - _ageSeconds / 0.32f);
            ApplyRing(_ring, ImpactRadius * (1.05f + (1f - fade) * 0.55f), 0f, new Color(0.58f, 0.88f, 1f, 0.42f * fade), Quaternion.identity);
            ApplySlash(_slashA, -0.65f, -0.08f, 0.65f, 0.26f, new Color(0.95f, 1f, 1f, 0.95f * fade));
            ApplySlash(_slashB, -0.38f, 0.28f, 0.48f, -0.18f, new Color(0.33f, 0.76f, 1f, 0.72f * fade));
        }

        private void SetLineEnabled(
            ref LineRenderer line,
            string name,
            bool enabled,
            Material material,
            float width)
        {
            if (!enabled)
            {
                if (line != null)
                    line.enabled = false;
                return;
            }

            line = EnsureLine(name, line, material, width);
            line.enabled = true;
        }

        private LineRenderer EnsureLine(string name, LineRenderer existing, Material material, float width)
        {
            if (existing == null)
            {
                Transform child = transform.Find(name);
                GameObject go = child != null ? child.gameObject : new GameObject(name);
                go.transform.SetParent(transform, false);
                existing = go.GetComponent<LineRenderer>();
                if (existing == null)
                    existing = go.AddComponent<LineRenderer>();
            }

            existing.useWorldSpace = false;
            existing.loop = false;
            existing.sharedMaterial = material;
            existing.startWidth = width;
            existing.endWidth = width;
            existing.numCapVertices = 6;
            existing.numCornerVertices = 2;
            existing.textureMode = LineTextureMode.Stretch;
            existing.alignment = _mode == CharacterTestSwordWaveVfxMode.Flight
                ? LineAlignment.TransformZ
                : LineAlignment.View;
            existing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            existing.receiveShadows = false;
            return existing;
        }

        private static void ApplyForwardBladeArc(
            LineRenderer line,
            float width,
            float forwardDepth,
            float lift,
            float zOffset,
            float rise,
            float slope,
            Quaternion roll,
            Color color)
        {
            line.positionCount = ArcPointCount;
            for (int i = 0; i < ArcPointCount; i++)
            {
                float t = i / (float)(ArcPointCount - 1);
                float side = t * 2f - 1f;
                float sideAbs = Mathf.Abs(side);
                float curve = 1f - sideAbs * sideAbs;
                float x = side * width * 0.5f;
                float y = lift + curve * rise + side * slope;
                float z = zOffset + curve * forwardDepth - sideAbs * 0.18f;
                line.SetPosition(i, roll * new Vector3(x, y, z));
            }

            ApplyColor(line, color, color);
        }

        private static void ApplyArc(
            LineRenderer line,
            float width,
            float height,
            float z,
            float startDegrees,
            float endDegrees,
            Color color)
        {
            line.positionCount = ArcPointCount;
            float range = endDegrees - startDegrees;
            for (int i = 0; i < ArcPointCount; i++)
            {
                float t = i / (float)(ArcPointCount - 1);
                float radians = (startDegrees + range * t) * Mathf.Deg2Rad;
                float x = Mathf.Cos(radians) * width * 0.5f;
                float y = Mathf.Sin(radians) * height;
                line.SetPosition(i, new Vector3(x, y, z));
            }

            ApplyColor(line, color, color);
        }

        private static void ApplyTrail(LineRenderer line, float x, float y, Quaternion roll, Color color)
        {
            line.positionCount = 3;
            line.SetPosition(0, roll * new Vector3(x, y, 0.1f));
            line.SetPosition(1, roll * new Vector3(x * 0.58f, y * 0.75f, -0.42f));
            line.SetPosition(2, roll * new Vector3(x * 0.22f, y * 0.35f, -0.92f));
            ApplyColor(line, color, new Color(color.r, color.g, color.b, 0f));
        }

        private static void ApplyRing(LineRenderer line, float radius, float y, Color color, Quaternion rotation)
        {
            line.positionCount = ArcPointCount + 1;
            for (int i = 0; i <= ArcPointCount; i++)
            {
                float t = i / (float)ArcPointCount;
                float radians = t * Mathf.PI * 2f;
                Vector3 point = new Vector3(
                    Mathf.Cos(radians) * radius,
                    y,
                    Mathf.Sin(radians) * radius);
                line.SetPosition(i, rotation * point);
            }

            line.loop = true;
            ApplyColor(line, color, color);
        }

        private static void ApplySlash(
            LineRenderer line,
            float x0,
            float y0,
            float x1,
            float y1,
            Color color)
        {
            line.positionCount = 2;
            line.SetPosition(0, new Vector3(x0, y0, 0f));
            line.SetPosition(1, new Vector3(x1, y1, 0f));
            ApplyColor(line, color, new Color(color.r, color.g, color.b, 0f));
        }

        private static void ApplyColor(LineRenderer line, Color start, Color end)
        {
            line.startColor = start;
            line.endColor = end;
        }

        private Material ResolveLineMaterial()
        {
            if (_lineMaterial != null)
                return _lineMaterial;

            if (_lineMaterialOverride != null)
            {
                _lineMaterial = _lineMaterialOverride;
                return _lineMaterial;
            }

            _runtimeLineMaterial = CharacterTestUnlitMaterials.CreateVfx("CharacterTest_SwordWave_RuntimeLine", Color.white);
            _runtimeLineMaterial.hideFlags = HideFlags.HideAndDontSave;
            _lineMaterial = _runtimeLineMaterial;
            return _lineMaterial;
        }

        private static void DestroyUnityObject(UnityEngine.Object value)
        {
            if (value == null)
                return;

            if (Application.isPlaying)
                Destroy(value);
            else
                DestroyImmediate(value);
        }
    }

    public static class CharacterTestVfxIds
    {
        public const int FireballDefinitionId = 12001;
        public const int FireballLifetimeFrames = 30;
        public const int FireballBurstImpactDefinitionId = 12002;
        public const int FireballBurstImpactLifetimeFrames = 30;
        public const int FlameLanceImpactDefinitionId = 12003;
        public const int FlameLanceImpactLifetimeFrames = 30;
        public const int FireballChargeDefinitionId = 12004;
        public const int FireballChargeLifetimeFrames = 30;
        public const int FireballBurstChargeDefinitionId = 12005;
        public const int FireballBurstChargeLifetimeFrames = 30;
        public const int GuardAuraDefinitionId = 12006;
        public const int GuardAuraLifetimeFrames = 300;
        public const int PhysicalShieldDefinitionId = 12031;
        public const int PhysicalShieldLifetimeFrames = CharacterTestPhysicalShieldRuntime.LifetimeFrames;
        public const int StunnedStatusDefinitionId = 12032;
        public const int StunnedStatusLifetimeFrames = 0;
        public const int ParalyzedStatusDefinitionId = 12033;
        public const int ParalyzedStatusLifetimeFrames = 0;
        public const int StunBoltChargeDefinitionId = 12034;
        public const int StunBoltChargeLifetimeFrames = 30;
        public const int StunBoltImpactDefinitionId = 12035;
        public const int StunBoltImpactLifetimeFrames = 30;
        public const int MeteorBarrageChargeDefinitionId = 12036;
        public const int MeteorBarrageChargeLifetimeFrames = 30;
        public const int MeteorBarrageTelegraphDefinitionId = 12037;
        public const int MeteorBarrageTelegraphLifetimeFrames = 60;
        public const int MeteorBarrageImpactDefinitionId = 12038;
        public const int MeteorBarrageImpactLifetimeFrames = 30;
        public const int SwordWaveChargeDefinitionId = 12039;
        public const int SwordWaveChargeLifetimeFrames = 30;
        public const int SwordWaveImpactDefinitionId = 12040;
        public const int SwordWaveImpactLifetimeFrames = 30;
        public const int ArcMineDeployDefinitionId = 12041;
        public const int ArcMineDeployLifetimeFrames = CharacterTestArcMineIds.DeployVfxLifetimeFrames;
        public const int ArcMineTriggerDefinitionId = 12042;
        public const int ArcMineTriggerLifetimeFrames = 30;
        public const int ArcMineFuseDefinitionId = 12043;
        public const int ArcMineFuseLifetimeFrames = CharacterTestArcMineIds.FuseVfxLifetimeFrames;
        public const int ArcMineFlightDefinitionId = 12044;
        public const int ArcMineFlightLifetimeFrames = 90;
        public const int RuneTurretDeployDefinitionId = 12045;
        public const int RuneTurretDeployLifetimeFrames = CharacterTestRuneTurretIds.DeployVfxLifetimeFrames;
        public const int RuneTurretIdleDefinitionId = 12046;
        public const int RuneTurretIdleLifetimeFrames = CharacterTestRuneTurretIds.IdleVfxLifetimeFrames;
        public const int RuneTurretFireDefinitionId = 12047;
        public const int RuneTurretFireLifetimeFrames = 30;
        public const int RuneTurretFlightDefinitionId = 12048;
        public const int RuneTurretFlightLifetimeFrames = 30;
        public const int RuneTurretImpactDefinitionId = 12049;
        public const int RuneTurretImpactLifetimeFrames = 30;
        public const int RuneTurretExpireDefinitionId = 12050;
        public const int RuneTurretExpireLifetimeFrames = 30;
        public const int FireballExplosionImpactDefinitionId = 12007;
        public const int FireballExplosionImpactLifetimeFrames = 45;
        public const int TornadoChargeDefinitionId = 12008;
        public const int TornadoChargeLifetimeFrames = 30;
        public const int TornadoImpactDefinitionId = 12009;
        public const int TornadoImpactLifetimeFrames = 45;
        public const int TornadoTickDefinitionId = 12010;
        public const int TornadoTickLifetimeFrames = 30;
        public const int AreaConfinementZoneDefinitionId = 12011;
        public const int AreaConfinementZoneLifetimeFrames = (int)CharacterTestAreaConfinementIds.ZoneDurationFrames;
        public const int AreaConfinementCharacterDefinitionId = 12012;
        public const int AreaConfinementCharacterLifetimeFrames = 0;
        public const int AreaConfinementTelegraphDefinitionId = 12013;
        public const int AreaConfinementTelegraphLifetimeFrames = CharacterTestAreaConfinementIds.TelegraphFrames;
        public const int AreaConfinementTelegraphCancelDefinitionId = 12014;
        public const int AreaConfinementTelegraphCancelLifetimeFrames = 30;
        public const int AreaConfinementCharacterReleaseDefinitionId = 12015;
        public const int AreaConfinementCharacterReleaseLifetimeFrames = 30;
        public const int BeamLanceBeamDefinitionId = 12016;
        public const int BeamLanceBeamLifetimeFrames = 31;
        public const int BeamLanceImpactDefinitionId = 12017;
        public const int BeamLanceImpactLifetimeFrames = 30;
        public const int BeamLanceChargeDefinitionId = 12018;
        public const int BeamLanceChargeLifetimeFrames = 30;
        public const int ChannelBeamChargeDefinitionId = 12019;
        public const int ChannelBeamChargeLifetimeFrames = 30;
        public const int ChannelBeamBeamDefinitionId = 12020;
        public const int ChannelBeamBeamLifetimeFrames = 90;
        public const int ChannelBeamImpactDefinitionId = 12021;
        public const int ChannelBeamImpactLifetimeFrames = 30;
        public const int LaserLanceChargeDefinitionId = 12022;
        public const int LaserLanceChargeLifetimeFrames = 30;
        public const int LaserLancePathDefinitionId = 12023;
        public const int LaserLancePathLifetimeFrames = 31;
        public const int LaserLanceImpactDefinitionId = 12024;
        public const int LaserLanceImpactLifetimeFrames = 30;
        public const int ChainLightningChargeDefinitionId = 12025;
        public const int ChainLightningChargeLifetimeFrames = 30;
        public const int ChainLightningArcDefinitionId = 12026;
        public const int ChainLightningArcLifetimeFrames = 31;
        public const int ChainLightningImpactDefinitionId = 12027;
        public const int ChainLightningImpactLifetimeFrames = 30;
        public const int PoisonBottleShatterDefinitionId = 12028;
        public const int PoisonBottleShatterLifetimeFrames = 30;
        public const int PoisonedTickDefinitionId = 12029;
        public const int PoisonedTickLifetimeFrames = 30;
        public const int PoisonBottlePoolDefinitionId = 12030;
        public const int PoisonBottlePoolLifetimeFrames = (int)CharacterTestPoisonIds.PoisonBottlePoolDurationFrames;
        public const int PoisonDartImpactDefinitionId = PoisonBottleShatterDefinitionId;
        public const int PoisonDartImpactLifetimeFrames = PoisonBottleShatterLifetimeFrames;
        public const string FireballDefinitionStableId = "vfx.magic.fireball";
        public const string FireballBurstImpactDefinitionStableId = "vfx.magic.fireball_burst.impact";
        public const string FlameLanceImpactDefinitionStableId = "vfx.magic.flame_lance.impact";
        public const string FireballChargeDefinitionStableId = "vfx.magic.fireball.charge";
        public const string FireballBurstChargeDefinitionStableId = "vfx.magic.fireball_burst.charge";
        public const string GuardAuraDefinitionStableId = "vfx.magic.guard_aura";
        public const string PhysicalShieldDefinitionStableId = "vfx.magic.physical_shield";
        public const string StunnedStatusDefinitionStableId = "vfx.status.stunned.loop";
        public const string ParalyzedStatusDefinitionStableId = "vfx.status.paralyzed.loop";
        public const string StunBoltChargeDefinitionStableId = "vfx.magic.stun_bolt.charge";
        public const string StunBoltImpactDefinitionStableId = "vfx.magic.stun_bolt.impact";
        public const string MeteorBarrageChargeDefinitionStableId = "vfx.magic.meteor_barrage.charge";
        public const string MeteorBarrageTelegraphDefinitionStableId = "vfx.magic.meteor_barrage.telegraph";
        public const string MeteorBarrageImpactDefinitionStableId = "vfx.magic.meteor_barrage.impact";
        public const string SwordWaveChargeDefinitionStableId = "vfx.magic.sword_wave.charge";
        public const string SwordWaveImpactDefinitionStableId = "vfx.magic.sword_wave.impact";
        public const string ArcMineFlightDefinitionStableId = "vfx.magic.arc_mine.flight";
        public const string ArcMineDeployDefinitionStableId = "vfx.magic.arc_mine.deploy";
        public const string ArcMineFuseDefinitionStableId = "vfx.magic.arc_mine.fuse";
        public const string ArcMineTriggerDefinitionStableId = "vfx.magic.arc_mine.trigger";
        public const string RuneTurretDeployDefinitionStableId = "vfx.magic.rune_turret.deploy";
        public const string RuneTurretIdleDefinitionStableId = "vfx.magic.rune_turret.idle";
        public const string RuneTurretFireDefinitionStableId = "vfx.magic.rune_turret.fire";
        public const string RuneTurretFlightDefinitionStableId = "vfx.magic.rune_turret.flight";
        public const string RuneTurretImpactDefinitionStableId = "vfx.magic.rune_turret.impact";
        public const string RuneTurretExpireDefinitionStableId = "vfx.magic.rune_turret.expire";
        public const string FireballExplosionImpactDefinitionStableId = "vfx.magic.fireball_explosion.impact";
        public const string TornadoChargeDefinitionStableId = "vfx.magic.tornado.charge";
        public const string TornadoImpactDefinitionStableId = "vfx.magic.tornado.impact";
        public const string TornadoTickDefinitionStableId = "vfx.magic.tornado.tick";
        public const string AreaConfinementZoneDefinitionStableId = "vfx.magic.area_confinement.zone";
        public const string AreaConfinementCharacterDefinitionStableId = "vfx.magic.area_confinement.character";
        public const string AreaConfinementTelegraphDefinitionStableId = "vfx.magic.area_confinement.telegraph";
        public const string AreaConfinementTelegraphCancelDefinitionStableId = "vfx.magic.area_confinement.telegraph_cancel";
        public const string AreaConfinementCharacterReleaseDefinitionStableId = "vfx.magic.area_confinement.character_release";
        public const string BeamLanceBeamDefinitionStableId = "vfx.magic.beam_lance.beam";
        public const string BeamLanceImpactDefinitionStableId = "vfx.magic.beam_lance.impact";
        public const string BeamLanceChargeDefinitionStableId = "vfx.magic.beam_lance.charge";
        public const string ChannelBeamChargeDefinitionStableId = "vfx.magic.channel_beam.charge";
        public const string ChannelBeamBeamDefinitionStableId = "vfx.magic.channel_beam.beam";
        public const string ChannelBeamImpactDefinitionStableId = "vfx.magic.channel_beam.impact";
        public const string LaserLanceChargeDefinitionStableId = "vfx.magic.laser_lance.charge";
        public const string LaserLancePathDefinitionStableId = "vfx.magic.laser_lance.laser";
        public const string LaserLanceImpactDefinitionStableId = "vfx.magic.laser_lance.impact";
        public const string ChainLightningChargeDefinitionStableId = "vfx.magic.chain_lightning.charge";
        public const string ChainLightningArcDefinitionStableId = "vfx.magic.chain_lightning.arc";
        public const string ChainLightningImpactDefinitionStableId = "vfx.magic.chain_lightning.impact";
        public const string PoisonBottleShatterDefinitionStableId = "vfx.magic.poison_bottle.shatter";
        public const string PoisonBottlePoolDefinitionStableId = "vfx.magic.poison_bottle.pool";
        public const string PoisonDartImpactDefinitionStableId = PoisonBottleShatterDefinitionStableId;
        public const string PoisonedTickDefinitionStableId = "vfx.status.poisoned.tick";
        public const string FireballResourceId = "vfx.magic.fireball";
        public const string FireballBurstFlightResourceId = "vfx.magic.fireball_burst.flight";
        public const string FireballBurstImpactResourceId = "vfx.magic.fireball_burst.impact";
        public const string FlameLanceFlightResourceId = "vfx.magic.flame_lance.flight";
        public const string FlameLanceImpactResourceId = "vfx.magic.flame_lance.impact";
        public const string FireballChargeResourceId = "vfx.magic.fireball.charge";
        public const string FireballBurstChargeResourceId = "vfx.magic.fireball_burst.charge";
        public const string GuardAuraResourceId = "vfx.magic.guard_aura";
        public const string PhysicalShieldResourceId = "vfx.magic.physical_shield";
        public const string StunnedStatusResourceId = "vfx.character_test.status.stunned";
        public const string ParalyzedStatusResourceId = "vfx.character_test.status.paralyzed";
        public const string StunBoltFlightResourceId = "vfx.magic.stun_bolt.flight";
        public const string StunBoltChargeResourceId = "vfx.magic.stun_bolt.charge";
        public const string StunBoltImpactResourceId = "vfx.magic.stun_bolt.impact";
        public const string MeteorBarrageFlightResourceId = "vfx.magic.meteor_barrage.flight";
        public const string MeteorBarrageChargeResourceId = "vfx.magic.meteor_barrage.charge";
        public const string MeteorBarrageTelegraphResourceId = "vfx.magic.meteor_barrage.telegraph";
        public const string MeteorBarrageImpactResourceId = "vfx.magic.meteor_barrage.impact";
        public const string SwordWaveFlightResourceId = "vfx.magic.sword_wave.flight";
        public const string SwordWaveChargeResourceId = "vfx.magic.sword_wave.charge";
        public const string SwordWaveImpactResourceId = "vfx.magic.sword_wave.impact";
        public const string ArcMineFlightResourceId = "vfx.magic.arc_mine.flight";
        public const string ArcMineDeployResourceId = "vfx.magic.arc_mine.deploy";
        public const string ArcMineFuseResourceId = "vfx.magic.arc_mine.fuse";
        public const string ArcMineTriggerResourceId = "vfx.magic.arc_mine.trigger";
        public const string RuneTurretDeployResourceId = "vfx.magic.rune_turret.deploy";
        public const string RuneTurretIdleResourceId = "vfx.magic.rune_turret.idle";
        public const string RuneTurretFireResourceId = "vfx.magic.rune_turret.fire";
        public const string RuneTurretFlightResourceId = "vfx.magic.rune_turret.flight";
        public const string RuneTurretImpactResourceId = "vfx.magic.rune_turret.impact";
        public const string RuneTurretExpireResourceId = "vfx.magic.rune_turret.expire";
        public const string FireballExplosionImpactResourceId = "vfx.magic.fireball_explosion.impact";
        public const string TornadoFlightResourceId = "vfx.magic.tornado.flight";
        public const string TornadoChargeResourceId = "vfx.magic.tornado.charge";
        public const string TornadoImpactResourceId = "vfx.magic.tornado.impact";
        public const string TornadoTickResourceId = "vfx.magic.tornado.tick";
        public const string AreaConfinementZoneResourceId = "vfx.magic.area_confinement.zone";
        public const string AreaConfinementCharacterResourceId = "vfx.magic.area_confinement.character";
        public const string AreaConfinementTelegraphResourceId = "vfx.magic.area_confinement.telegraph";
        public const string AreaConfinementTelegraphCancelResourceId = "vfx.magic.area_confinement.telegraph_cancel";
        public const string AreaConfinementCharacterReleaseResourceId = "vfx.magic.area_confinement.character_release";
        public const string BeamLanceBeamResourceId = "vfx.magic.beam_lance.beam";
        public const string BeamLanceImpactResourceId = "vfx.magic.beam_lance.impact";
        public const string BeamLanceChargeResourceId = "vfx.magic.beam_lance.charge";
        public const string ChannelBeamChargeResourceId = "vfx.magic.channel_beam.charge";
        public const string ChannelBeamBeamResourceId = "vfx.magic.channel_beam.beam";
        public const string ChannelBeamImpactResourceId = "vfx.magic.channel_beam.impact";
        public const string LaserLanceChargeResourceId = "vfx.magic.laser_lance.charge";
        public const string LaserLancePathResourceId = "vfx.magic.laser_lance.laser";
        public const string LaserLanceImpactResourceId = "vfx.magic.laser_lance.impact";
        public const string ChainLightningChargeResourceId = "vfx.magic.chain_lightning.charge";
        public const string ChainLightningArcResourceId = "vfx.magic.chain_lightning.arc";
        public const string ChainLightningImpactResourceId = "vfx.magic.chain_lightning.impact";
        public const string PoisonBottleFlightResourceId = "vfx.magic.poison_bottle.flight";
        public const string PoisonBottleShatterResourceId = "vfx.magic.poison_bottle.shatter";
        public const string PoisonBottlePoolResourceId = "vfx.magic.poison_bottle.pool";
        public const string PoisonDartFlightResourceId = PoisonBottleFlightResourceId;
        public const string PoisonDartImpactResourceId = PoisonBottleShatterResourceId;
        public const string PoisonedTickResourceId = "vfx.status.poisoned.tick";
        public const string FireballResourcePackageId = "mxframework.samples";
        public const string FireballResourceAddress = "mxframework.samples/vfx/magic/fireball";
        public const string FireballBurstFlightResourceAddress = "mxframework.samples/vfx/magic/fireball_burst/flight";
        public const string FireballBurstImpactResourceAddress = "mxframework.samples/vfx/magic/fireball_burst/impact";
        public const string FlameLanceFlightResourceAddress = "mxframework.samples/vfx/magic/flame_lance/flight";
        public const string FlameLanceImpactResourceAddress = "mxframework.samples/vfx/magic/flame_lance/impact";
        public const string FireballChargeResourceAddress = "mxframework.samples/vfx/magic/fireball/charge";
        public const string FireballBurstChargeResourceAddress = "mxframework.samples/vfx/magic/fireball_burst/charge";
        public const string GuardAuraResourceAddress = "mxframework.samples/vfx/magic/guard_aura";
        public const string PhysicalShieldResourceAddress = "mxframework.samples/vfx/magic/physical_shield";
        public const string StunnedStatusResourceAddress = "mxframework.samples/vfx/character_test/status/stunned";
        public const string ParalyzedStatusResourceAddress = "mxframework.samples/vfx/character_test/status/paralyzed";
        public const string StunBoltFlightResourceAddress = "mxframework.samples/vfx/magic/stun_bolt/flight";
        public const string StunBoltChargeResourceAddress = "mxframework.samples/vfx/magic/stun_bolt/charge";
        public const string StunBoltImpactResourceAddress = "mxframework.samples/vfx/magic/stun_bolt/impact";
        public const string MeteorBarrageFlightResourceAddress =
            "mxframework.samples/vfx/magic/meteor_barrage/flight";
        public const string MeteorBarrageChargeResourceAddress =
            "mxframework.samples/vfx/magic/meteor_barrage/charge";
        public const string MeteorBarrageTelegraphResourceAddress =
            "mxframework.samples/vfx/magic/meteor_barrage/telegraph";
        public const string MeteorBarrageImpactResourceAddress =
            "mxframework.samples/vfx/magic/meteor_barrage/impact";
        public const string SwordWaveFlightResourceAddress =
            "mxframework.samples/vfx/magic/sword_wave/flight";
        public const string SwordWaveChargeResourceAddress =
            "mxframework.samples/vfx/magic/sword_wave/charge";
        public const string SwordWaveImpactResourceAddress =
            "mxframework.samples/vfx/magic/sword_wave/impact";
        public const string ArcMineFlightResourceAddress =
            "mxframework.character_test/vfx/magic/arc_mine/flight";
        public const string ArcMineDeployResourceAddress =
            "mxframework.character_test/vfx/magic/arc_mine/deploy";
        public const string ArcMineFuseResourceAddress =
            "mxframework.character_test/vfx/magic/arc_mine/fuse";
        public const string ArcMineTriggerResourceAddress =
            "mxframework.character_test/vfx/magic/arc_mine/trigger";
        public const string RuneTurretDeployResourceAddress =
            "mxframework.character_test/vfx/magic/rune_turret/deploy";
        public const string RuneTurretIdleResourceAddress =
            "mxframework.character_test/vfx/magic/rune_turret/idle";
        public const string RuneTurretFireResourceAddress =
            "mxframework.character_test/vfx/magic/rune_turret/fire";
        public const string RuneTurretFlightResourceAddress =
            "mxframework.character_test/vfx/magic/rune_turret/flight";
        public const string RuneTurretImpactResourceAddress =
            "mxframework.character_test/vfx/magic/rune_turret/impact";
        public const string RuneTurretExpireResourceAddress =
            "mxframework.character_test/vfx/magic/rune_turret/expire";
        public const string FireballExplosionImpactResourceAddress = "mxframework.samples/vfx/magic/fireball_explosion/impact";
        public const string TornadoFlightResourceAddress = "mxframework.samples/vfx/magic/tornado/flight";
        public const string TornadoChargeResourceAddress = "mxframework.samples/vfx/magic/tornado/charge";
        public const string TornadoImpactResourceAddress = "mxframework.samples/vfx/magic/tornado/impact";
        public const string TornadoTickResourceAddress = "mxframework.samples/vfx/magic/tornado/tick";
        public const string AreaConfinementZoneResourceAddress =
            "mxframework.samples/vfx/magic/area_confinement/zone";
        public const string AreaConfinementCharacterResourceAddress =
            "mxframework.samples/vfx/magic/area_confinement/character";
        public const string AreaConfinementTelegraphResourceAddress =
            "mxframework.samples/vfx/magic/area_confinement/telegraph";
        public const string AreaConfinementTelegraphCancelResourceAddress =
            "mxframework.samples/vfx/magic/area_confinement/telegraph_cancel";
        public const string AreaConfinementCharacterReleaseResourceAddress =
            "mxframework.samples/vfx/magic/area_confinement/character_release";
        public const string BeamLanceBeamResourceAddress = "mxframework.samples/vfx/magic/beam_lance/beam";
        public const string BeamLanceImpactResourceAddress = "mxframework.samples/vfx/magic/beam_lance/impact";
        public const string BeamLanceChargeResourceAddress = "mxframework.samples/vfx/magic/beam_lance/charge";
        public const string ChannelBeamChargeResourceAddress = "mxframework.samples/vfx/magic/channel_beam/charge";
        public const string ChannelBeamBeamResourceAddress = "mxframework.samples/vfx/magic/channel_beam/beam";
        public const string ChannelBeamImpactResourceAddress = "mxframework.samples/vfx/magic/channel_beam/impact";
        public const string LaserLanceChargeResourceAddress = "mxframework.samples/vfx/magic/laser_lance/charge";
        public const string LaserLancePathResourceAddress = "mxframework.samples/vfx/magic/laser_lance/laser";
        public const string LaserLanceImpactResourceAddress = "mxframework.samples/vfx/magic/laser_lance/impact";
        public const string ChainLightningChargeResourceAddress =
            "mxframework.samples/vfx/magic/chain_lightning/charge";
        public const string ChainLightningArcResourceAddress =
            "mxframework.samples/vfx/magic/chain_lightning/arc";
        public const string ChainLightningImpactResourceAddress =
            "mxframework.samples/vfx/magic/chain_lightning/impact";
        public const string PoisonBottleFlightResourceAddress =
            "mxframework.samples/vfx/magic/poison_bottle/flight";
        public const string PoisonBottleShatterResourceAddress =
            "mxframework.samples/vfx/magic/poison_bottle/shatter";
        public const string PoisonBottlePoolResourceAddress =
            "mxframework.samples/vfx/magic/poison_bottle/pool";
        public const string PoisonDartFlightResourceAddress = PoisonBottleFlightResourceAddress;
        public const string PoisonDartImpactResourceAddress = PoisonBottleShatterResourceAddress;
        public const string PoisonedTickResourceAddress =
            "mxframework.samples/vfx/status/poisoned/tick";
        public const string FireballCatalogId = "character_test.vfx.fireball";

        public static ResourceKey FireballResourceKey =>
            new ResourceKey(
                FireballResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey FireballBurstFlightResourceKey =>
            new ResourceKey(
                FireballBurstFlightResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey FireballBurstImpactResourceKey =>
            new ResourceKey(
                FireballBurstImpactResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey FlameLanceFlightResourceKey =>
            new ResourceKey(
                FlameLanceFlightResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey FlameLanceImpactResourceKey =>
            new ResourceKey(
                FlameLanceImpactResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey FireballChargeResourceKey =>
            new ResourceKey(
                FireballChargeResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey FireballBurstChargeResourceKey =>
            new ResourceKey(
                FireballBurstChargeResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey GuardAuraResourceKey =>
            new ResourceKey(
                GuardAuraResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey PhysicalShieldResourceKey =>
            new ResourceKey(
                PhysicalShieldResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey StunnedStatusResourceKey =>
            new ResourceKey(
                StunnedStatusResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey ParalyzedStatusResourceKey =>
            new ResourceKey(
                ParalyzedStatusResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey StunBoltFlightResourceKey =>
            new ResourceKey(
                StunBoltFlightResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey StunBoltChargeResourceKey =>
            new ResourceKey(
                StunBoltChargeResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey StunBoltImpactResourceKey =>
            new ResourceKey(
                StunBoltImpactResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey MeteorBarrageFlightResourceKey =>
            new ResourceKey(
                MeteorBarrageFlightResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey MeteorBarrageChargeResourceKey =>
            new ResourceKey(
                MeteorBarrageChargeResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey MeteorBarrageTelegraphResourceKey =>
            new ResourceKey(
                MeteorBarrageTelegraphResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey MeteorBarrageImpactResourceKey =>
            new ResourceKey(
                MeteorBarrageImpactResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey SwordWaveFlightResourceKey =>
            new ResourceKey(
                SwordWaveFlightResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey SwordWaveChargeResourceKey =>
            new ResourceKey(
                SwordWaveChargeResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey SwordWaveImpactResourceKey =>
            new ResourceKey(
                SwordWaveImpactResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey ArcMineFlightResourceKey =>
            new ResourceKey(
                ArcMineFlightResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey ArcMineDeployResourceKey =>
            new ResourceKey(
                ArcMineDeployResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey ArcMineFuseResourceKey =>
            new ResourceKey(
                ArcMineFuseResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey ArcMineTriggerResourceKey =>
            new ResourceKey(
                ArcMineTriggerResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey RuneTurretDeployResourceKey =>
            new ResourceKey(
                RuneTurretDeployResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey RuneTurretIdleResourceKey =>
            new ResourceKey(
                RuneTurretIdleResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey RuneTurretFireResourceKey =>
            new ResourceKey(
                RuneTurretFireResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey RuneTurretFlightResourceKey =>
            new ResourceKey(
                RuneTurretFlightResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey RuneTurretImpactResourceKey =>
            new ResourceKey(
                RuneTurretImpactResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey RuneTurretExpireResourceKey =>
            new ResourceKey(
                RuneTurretExpireResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey FireballExplosionImpactResourceKey =>
            new ResourceKey(
                FireballExplosionImpactResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey TornadoFlightResourceKey =>
            new ResourceKey(
                TornadoFlightResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey TornadoChargeResourceKey =>
            new ResourceKey(
                TornadoChargeResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey TornadoImpactResourceKey =>
            new ResourceKey(
                TornadoImpactResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey TornadoTickResourceKey =>
            new ResourceKey(
                TornadoTickResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey AreaConfinementZoneResourceKey =>
            new ResourceKey(
                AreaConfinementZoneResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey AreaConfinementCharacterResourceKey =>
            new ResourceKey(
                AreaConfinementCharacterResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey AreaConfinementTelegraphResourceKey =>
            new ResourceKey(
                AreaConfinementTelegraphResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey AreaConfinementTelegraphCancelResourceKey =>
            new ResourceKey(
                AreaConfinementTelegraphCancelResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey AreaConfinementCharacterReleaseResourceKey =>
            new ResourceKey(
                AreaConfinementCharacterReleaseResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey BeamLanceBeamResourceKey =>
            new ResourceKey(
                BeamLanceBeamResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey BeamLanceImpactResourceKey =>
            new ResourceKey(
                BeamLanceImpactResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey BeamLanceChargeResourceKey =>
            new ResourceKey(
                BeamLanceChargeResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey ChannelBeamChargeResourceKey =>
            new ResourceKey(
                ChannelBeamChargeResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey ChannelBeamBeamResourceKey =>
            new ResourceKey(
                ChannelBeamBeamResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey ChannelBeamImpactResourceKey =>
            new ResourceKey(
                ChannelBeamImpactResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey LaserLanceChargeResourceKey =>
            new ResourceKey(
                LaserLanceChargeResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey LaserLancePathResourceKey =>
            new ResourceKey(
                LaserLancePathResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey LaserLanceImpactResourceKey =>
            new ResourceKey(
                LaserLanceImpactResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey ChainLightningChargeResourceKey =>
            new ResourceKey(
                ChainLightningChargeResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey ChainLightningArcResourceKey =>
            new ResourceKey(
                ChainLightningArcResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey ChainLightningImpactResourceKey =>
            new ResourceKey(
                ChainLightningImpactResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey PoisonBottleFlightResourceKey =>
            new ResourceKey(
                PoisonBottleFlightResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey PoisonBottleShatterResourceKey =>
            new ResourceKey(
                PoisonBottleShatterResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey PoisonBottlePoolResourceKey =>
            new ResourceKey(
                PoisonBottlePoolResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);

        public static ResourceKey PoisonDartFlightResourceKey =>
            PoisonBottleFlightResourceKey;

        public static ResourceKey PoisonDartImpactResourceKey =>
            PoisonBottleShatterResourceKey;

        public static ResourceKey PoisonedTickResourceKey =>
            new ResourceKey(
                PoisonedTickResourceId,
                ResourceTypeIds.GameObject,
                string.Empty,
                FireballResourcePackageId);
    }

    public static class CharacterTestVfxDefinitionSource
    {
        public const string DefaultGeneratedDefinitionsPath =
            "Assets/MxFrameworkGenerated/Vfx/vfx_definitions.json";
        public const string DefaultGeneratedTriggerMapsPath =
            "Assets/MxFrameworkGenerated/Vfx/vfx_trigger_maps.json";

        public static VfxDefinitionRegistry CreateDefinitions()
        {
            VfxDefinitionRegistry registry = LoadGeneratedDefinitionsOrEmpty(DefaultGeneratedDefinitionsPath);
            return registry.Count > 0 ? registry : CreateEmbeddedFallbackDefinitions();
        }

        public static VfxDefinitionRegistry LoadGeneratedDefinitionsOrEmpty(string path)
        {
            return CharacterTestRuntimeCatalogBinaryFallback.LoadBinaryThenJsonOrEmpty(
                path,
                VfxConfigBinaryImporter.DefinitionCatalogKind,
                VfxConfigBinaryImporter.ImportDefinitionFile,
                VfxConfigJsonImporter.ImportDefinitionFile,
                registry => registry?.Count ?? 0,
                () => new VfxDefinitionRegistry());
        }

        public static VfxDefinitionRegistry CreateEmbeddedFallbackDefinitions()
        {
            var registry = new VfxDefinitionRegistry();
            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.FireballDefinitionId),
                CharacterTestVfxIds.FireballDefinitionStableId,
                CharacterTestVfxIds.FireballResourceKey,
                CharacterTestVfxIds.FireballLifetimeFrames,
                "vfx.magic.fireball.v1"), out string failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.FireballBurstImpactDefinitionId),
                CharacterTestVfxIds.FireballBurstImpactDefinitionStableId,
                CharacterTestVfxIds.FireballBurstImpactResourceKey,
                CharacterTestVfxIds.FireballBurstImpactLifetimeFrames,
                "vfx.magic.fireball_burst.impact.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.FlameLanceImpactDefinitionId),
                CharacterTestVfxIds.FlameLanceImpactDefinitionStableId,
                CharacterTestVfxIds.FlameLanceImpactResourceKey,
                CharacterTestVfxIds.FlameLanceImpactLifetimeFrames,
                "vfx.magic.flame_lance.impact.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.FireballChargeDefinitionId),
                CharacterTestVfxIds.FireballChargeDefinitionStableId,
                CharacterTestVfxIds.FireballChargeResourceKey,
                CharacterTestVfxIds.FireballChargeLifetimeFrames,
                "vfx.magic.fireball.charge.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.FireballBurstChargeDefinitionId),
                CharacterTestVfxIds.FireballBurstChargeDefinitionStableId,
                CharacterTestVfxIds.FireballBurstChargeResourceKey,
                CharacterTestVfxIds.FireballBurstChargeLifetimeFrames,
                "vfx.magic.fireball_burst.charge.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.GuardAuraDefinitionId),
                CharacterTestVfxIds.GuardAuraDefinitionStableId,
                CharacterTestVfxIds.GuardAuraResourceKey,
                CharacterTestVfxIds.GuardAuraLifetimeFrames,
                "vfx.magic.guard_aura.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.PhysicalShieldDefinitionId),
                CharacterTestVfxIds.PhysicalShieldDefinitionStableId,
                CharacterTestVfxIds.PhysicalShieldResourceKey,
                CharacterTestVfxIds.PhysicalShieldLifetimeFrames,
                "vfx.magic.physical_shield.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.StunnedStatusDefinitionId),
                CharacterTestVfxIds.StunnedStatusDefinitionStableId,
                CharacterTestVfxIds.StunnedStatusResourceKey,
                CharacterTestVfxIds.StunnedStatusLifetimeFrames,
                "vfx.status.stunned.loop.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.ParalyzedStatusDefinitionId),
                CharacterTestVfxIds.ParalyzedStatusDefinitionStableId,
                CharacterTestVfxIds.ParalyzedStatusResourceKey,
                CharacterTestVfxIds.ParalyzedStatusLifetimeFrames,
                "vfx.status.paralyzed.loop.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.StunBoltChargeDefinitionId),
                CharacterTestVfxIds.StunBoltChargeDefinitionStableId,
                CharacterTestVfxIds.StunBoltChargeResourceKey,
                CharacterTestVfxIds.StunBoltChargeLifetimeFrames,
                "vfx.magic.stun_bolt.charge.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.StunBoltImpactDefinitionId),
                CharacterTestVfxIds.StunBoltImpactDefinitionStableId,
                CharacterTestVfxIds.StunBoltImpactResourceKey,
                CharacterTestVfxIds.StunBoltImpactLifetimeFrames,
                "vfx.magic.stun_bolt.impact.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.MeteorBarrageChargeDefinitionId),
                CharacterTestVfxIds.MeteorBarrageChargeDefinitionStableId,
                CharacterTestVfxIds.MeteorBarrageChargeResourceKey,
                CharacterTestVfxIds.MeteorBarrageChargeLifetimeFrames,
                "vfx.magic.meteor_barrage.charge.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.MeteorBarrageTelegraphDefinitionId),
                CharacterTestVfxIds.MeteorBarrageTelegraphDefinitionStableId,
                CharacterTestVfxIds.MeteorBarrageTelegraphResourceKey,
                CharacterTestVfxIds.MeteorBarrageTelegraphLifetimeFrames,
                "vfx.magic.meteor_barrage.telegraph.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.MeteorBarrageImpactDefinitionId),
                CharacterTestVfxIds.MeteorBarrageImpactDefinitionStableId,
                CharacterTestVfxIds.MeteorBarrageImpactResourceKey,
                CharacterTestVfxIds.MeteorBarrageImpactLifetimeFrames,
                "vfx.magic.meteor_barrage.impact.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.SwordWaveChargeDefinitionId),
                CharacterTestVfxIds.SwordWaveChargeDefinitionStableId,
                CharacterTestVfxIds.SwordWaveChargeResourceKey,
                CharacterTestVfxIds.SwordWaveChargeLifetimeFrames,
                "vfx.magic.sword_wave.charge.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.SwordWaveImpactDefinitionId),
                CharacterTestVfxIds.SwordWaveImpactDefinitionStableId,
                CharacterTestVfxIds.SwordWaveImpactResourceKey,
                CharacterTestVfxIds.SwordWaveImpactLifetimeFrames,
                "vfx.magic.sword_wave.impact.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.ArcMineFlightDefinitionId),
                CharacterTestVfxIds.ArcMineFlightDefinitionStableId,
                CharacterTestVfxIds.ArcMineFlightResourceKey,
                CharacterTestVfxIds.ArcMineFlightLifetimeFrames,
                "vfx.magic.arc_mine.flight.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.ArcMineDeployDefinitionId),
                CharacterTestVfxIds.ArcMineDeployDefinitionStableId,
                CharacterTestVfxIds.ArcMineDeployResourceKey,
                CharacterTestVfxIds.ArcMineDeployLifetimeFrames,
                "vfx.magic.arc_mine.deploy.v2"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.ArcMineFuseDefinitionId),
                CharacterTestVfxIds.ArcMineFuseDefinitionStableId,
                CharacterTestVfxIds.ArcMineFuseResourceKey,
                CharacterTestVfxIds.ArcMineFuseLifetimeFrames,
                "vfx.magic.arc_mine.fuse.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.ArcMineTriggerDefinitionId),
                CharacterTestVfxIds.ArcMineTriggerDefinitionStableId,
                CharacterTestVfxIds.ArcMineTriggerResourceKey,
                CharacterTestVfxIds.ArcMineTriggerLifetimeFrames,
                "vfx.magic.arc_mine.trigger.v2"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.RuneTurretDeployDefinitionId),
                CharacterTestVfxIds.RuneTurretDeployDefinitionStableId,
                CharacterTestVfxIds.RuneTurretDeployResourceKey,
                CharacterTestVfxIds.RuneTurretDeployLifetimeFrames,
                "vfx.magic.rune_turret.deploy.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.RuneTurretIdleDefinitionId),
                CharacterTestVfxIds.RuneTurretIdleDefinitionStableId,
                CharacterTestVfxIds.RuneTurretIdleResourceKey,
                CharacterTestVfxIds.RuneTurretIdleLifetimeFrames,
                "vfx.magic.rune_turret.idle.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.RuneTurretFireDefinitionId),
                CharacterTestVfxIds.RuneTurretFireDefinitionStableId,
                CharacterTestVfxIds.RuneTurretFireResourceKey,
                CharacterTestVfxIds.RuneTurretFireLifetimeFrames,
                "vfx.magic.rune_turret.fire.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.RuneTurretFlightDefinitionId),
                CharacterTestVfxIds.RuneTurretFlightDefinitionStableId,
                CharacterTestVfxIds.RuneTurretFlightResourceKey,
                CharacterTestVfxIds.RuneTurretFlightLifetimeFrames,
                "vfx.magic.rune_turret.flight.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.RuneTurretImpactDefinitionId),
                CharacterTestVfxIds.RuneTurretImpactDefinitionStableId,
                CharacterTestVfxIds.RuneTurretImpactResourceKey,
                CharacterTestVfxIds.RuneTurretImpactLifetimeFrames,
                "vfx.magic.rune_turret.impact.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.RuneTurretExpireDefinitionId),
                CharacterTestVfxIds.RuneTurretExpireDefinitionStableId,
                CharacterTestVfxIds.RuneTurretExpireResourceKey,
                CharacterTestVfxIds.RuneTurretExpireLifetimeFrames,
                "vfx.magic.rune_turret.expire.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.FireballExplosionImpactDefinitionId),
                CharacterTestVfxIds.FireballExplosionImpactDefinitionStableId,
                CharacterTestVfxIds.FireballExplosionImpactResourceKey,
                CharacterTestVfxIds.FireballExplosionImpactLifetimeFrames,
                "vfx.magic.fireball_explosion.impact.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.TornadoChargeDefinitionId),
                CharacterTestVfxIds.TornadoChargeDefinitionStableId,
                CharacterTestVfxIds.TornadoChargeResourceKey,
                CharacterTestVfxIds.TornadoChargeLifetimeFrames,
                "vfx.magic.tornado.charge.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.TornadoImpactDefinitionId),
                CharacterTestVfxIds.TornadoImpactDefinitionStableId,
                CharacterTestVfxIds.TornadoImpactResourceKey,
                CharacterTestVfxIds.TornadoImpactLifetimeFrames,
                "vfx.magic.tornado.impact.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.TornadoTickDefinitionId),
                CharacterTestVfxIds.TornadoTickDefinitionStableId,
                CharacterTestVfxIds.TornadoTickResourceKey,
                CharacterTestVfxIds.TornadoTickLifetimeFrames,
                "vfx.magic.tornado.tick.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.AreaConfinementZoneDefinitionId),
                CharacterTestVfxIds.AreaConfinementZoneDefinitionStableId,
                CharacterTestVfxIds.AreaConfinementZoneResourceKey,
                CharacterTestVfxIds.AreaConfinementZoneLifetimeFrames,
                "vfx.magic.area_confinement.zone.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.AreaConfinementCharacterDefinitionId),
                CharacterTestVfxIds.AreaConfinementCharacterDefinitionStableId,
                CharacterTestVfxIds.AreaConfinementCharacterResourceKey,
                CharacterTestVfxIds.AreaConfinementCharacterLifetimeFrames,
                "vfx.magic.area_confinement.character.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.AreaConfinementTelegraphDefinitionId),
                CharacterTestVfxIds.AreaConfinementTelegraphDefinitionStableId,
                CharacterTestVfxIds.AreaConfinementTelegraphResourceKey,
                CharacterTestVfxIds.AreaConfinementTelegraphLifetimeFrames,
                "vfx.magic.area_confinement.telegraph.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.AreaConfinementTelegraphCancelDefinitionId),
                CharacterTestVfxIds.AreaConfinementTelegraphCancelDefinitionStableId,
                CharacterTestVfxIds.AreaConfinementTelegraphCancelResourceKey,
                CharacterTestVfxIds.AreaConfinementTelegraphCancelLifetimeFrames,
                "vfx.magic.area_confinement.telegraph_cancel.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.AreaConfinementCharacterReleaseDefinitionId),
                CharacterTestVfxIds.AreaConfinementCharacterReleaseDefinitionStableId,
                CharacterTestVfxIds.AreaConfinementCharacterReleaseResourceKey,
                CharacterTestVfxIds.AreaConfinementCharacterReleaseLifetimeFrames,
                "vfx.magic.area_confinement.character_release.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.BeamLanceBeamDefinitionId),
                CharacterTestVfxIds.BeamLanceBeamDefinitionStableId,
                CharacterTestVfxIds.BeamLanceBeamResourceKey,
                CharacterTestVfxIds.BeamLanceBeamLifetimeFrames,
                "vfx.magic.beam_lance.beam.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.BeamLanceImpactDefinitionId),
                CharacterTestVfxIds.BeamLanceImpactDefinitionStableId,
                CharacterTestVfxIds.BeamLanceImpactResourceKey,
                CharacterTestVfxIds.BeamLanceImpactLifetimeFrames,
                "vfx.magic.beam_lance.impact.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.BeamLanceChargeDefinitionId),
                CharacterTestVfxIds.BeamLanceChargeDefinitionStableId,
                CharacterTestVfxIds.BeamLanceChargeResourceKey,
                CharacterTestVfxIds.BeamLanceChargeLifetimeFrames,
                "vfx.magic.beam_lance.charge.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.ChannelBeamChargeDefinitionId),
                CharacterTestVfxIds.ChannelBeamChargeDefinitionStableId,
                CharacterTestVfxIds.ChannelBeamChargeResourceKey,
                CharacterTestVfxIds.ChannelBeamChargeLifetimeFrames,
                "vfx.magic.channel_beam.charge.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.ChannelBeamBeamDefinitionId),
                CharacterTestVfxIds.ChannelBeamBeamDefinitionStableId,
                CharacterTestVfxIds.ChannelBeamBeamResourceKey,
                CharacterTestVfxIds.ChannelBeamBeamLifetimeFrames,
                "vfx.magic.channel_beam.beam.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.ChannelBeamImpactDefinitionId),
                CharacterTestVfxIds.ChannelBeamImpactDefinitionStableId,
                CharacterTestVfxIds.ChannelBeamImpactResourceKey,
                CharacterTestVfxIds.ChannelBeamImpactLifetimeFrames,
                "vfx.magic.channel_beam.impact.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.LaserLanceChargeDefinitionId),
                CharacterTestVfxIds.LaserLanceChargeDefinitionStableId,
                CharacterTestVfxIds.LaserLanceChargeResourceKey,
                CharacterTestVfxIds.LaserLanceChargeLifetimeFrames,
                "vfx.magic.laser_lance.charge.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.LaserLancePathDefinitionId),
                CharacterTestVfxIds.LaserLancePathDefinitionStableId,
                CharacterTestVfxIds.LaserLancePathResourceKey,
                CharacterTestVfxIds.LaserLancePathLifetimeFrames,
                "vfx.magic.laser_lance.laser.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.LaserLanceImpactDefinitionId),
                CharacterTestVfxIds.LaserLanceImpactDefinitionStableId,
                CharacterTestVfxIds.LaserLanceImpactResourceKey,
                CharacterTestVfxIds.LaserLanceImpactLifetimeFrames,
                "vfx.magic.laser_lance.impact.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.ChainLightningChargeDefinitionId),
                CharacterTestVfxIds.ChainLightningChargeDefinitionStableId,
                CharacterTestVfxIds.ChainLightningChargeResourceKey,
                CharacterTestVfxIds.ChainLightningChargeLifetimeFrames,
                "vfx.magic.chain_lightning.charge.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.ChainLightningArcDefinitionId),
                CharacterTestVfxIds.ChainLightningArcDefinitionStableId,
                CharacterTestVfxIds.ChainLightningArcResourceKey,
                CharacterTestVfxIds.ChainLightningArcLifetimeFrames,
                "vfx.magic.chain_lightning.arc.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.ChainLightningImpactDefinitionId),
                CharacterTestVfxIds.ChainLightningImpactDefinitionStableId,
                CharacterTestVfxIds.ChainLightningImpactResourceKey,
                CharacterTestVfxIds.ChainLightningImpactLifetimeFrames,
                "vfx.magic.chain_lightning.impact.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.PoisonBottleShatterDefinitionId),
                CharacterTestVfxIds.PoisonBottleShatterDefinitionStableId,
                CharacterTestVfxIds.PoisonBottleShatterResourceKey,
                CharacterTestVfxIds.PoisonBottleShatterLifetimeFrames,
                "vfx.magic.poison_bottle.shatter.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.PoisonBottlePoolDefinitionId),
                CharacterTestVfxIds.PoisonBottlePoolDefinitionStableId,
                CharacterTestVfxIds.PoisonBottlePoolResourceKey,
                CharacterTestVfxIds.PoisonBottlePoolLifetimeFrames,
                "vfx.magic.poison_bottle.pool.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            if (!registry.TryRegister(new VfxDefinition(
                new VfxDefinitionId(CharacterTestVfxIds.PoisonedTickDefinitionId),
                CharacterTestVfxIds.PoisonedTickDefinitionStableId,
                CharacterTestVfxIds.PoisonedTickResourceKey,
                CharacterTestVfxIds.PoisonedTickLifetimeFrames,
                "vfx.status.poisoned.tick.v1"), out failure))
            {
                throw new InvalidOperationException(failure);
            }

            CharacterTestRuntimeCatalogLoadDiagnostics.RecordEmbedded(
                VfxConfigBinaryImporter.DefinitionCatalogKind,
                registry.Count,
                "Generated VFX definition catalog was empty or missing.");
            return registry;
        }

        public static VfxTriggerMapResolver CreateTriggerResolver()
        {
            VfxTriggerMapResolver resolver =
                CharacterTestRuntimeCatalogBinaryFallback.LoadBinaryThenJsonOrEmpty(
                    DefaultGeneratedTriggerMapsPath,
                    VfxConfigBinaryImporter.TriggerMapCatalogKind,
                    VfxConfigBinaryImporter.ImportTriggerMapFile,
                    VfxConfigJsonImporter.ImportTriggerMapFile,
                    _ => -1,
                    () => null,
                    acceptEmptyBinary: true);
            return resolver ?? CreateEmbeddedFallbackTriggerResolver();
        }

        public static VfxTriggerMapResolver CreateEmbeddedFallbackTriggerResolver()
        {
            int trigger = CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestAuthoredContentIds.D407_Id1);
            int burstTrigger = CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestAuthoredContentIds.D407_Id2);
            int flameLanceTrigger = CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestAuthoredContentIds.D407_Id3);
            int tornadoTrigger = CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestAuthoredContentIds.D407_Id4);
            int tornadoTickTrigger = CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestAuthoredContentIds.D407_Id17);
            int beamLanceTrigger = CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestAuthoredContentIds.D407_Id5);
            int channelBeamTrigger = CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestAuthoredContentIds.D407_Id6);
            int laserLanceTrigger = CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestAuthoredContentIds.D407_Id7);
            int chainLightningTrigger = CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestAuthoredContentIds.D407_Id8);
            int poisonBottleTrigger = CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestAuthoredContentIds.D407_Id9);
            int stunBoltTrigger = CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestAuthoredContentIds.D407_Id10);
            int meteorBarrageTrigger = CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestAuthoredContentIds.D407_Id11);
            int swordWaveTrigger = CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestAuthoredContentIds.D407_Id12);
            int dashStrikeTrigger = CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestAuthoredContentIds.D407_Id13);
            int hookChainTrigger = CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestAuthoredContentIds.D407_Id14);
            int runeTurretTrigger = CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestAuthoredContentIds.D407_Id16);
            int poisonTickTrigger = CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestAuthoredContentIds.D407_Id18);
            VfxTriggerMapResolver resolver = new VfxTriggerMapResolver()
                .Add(new VfxTriggerMapping(
                    VfxSourceDomains.CombatHit,
                    trigger,
                    new VfxDefinitionId(CharacterTestVfxIds.FireballDefinitionId),
                    CharacterTestVfxIds.FireballLifetimeFrames))
                .Add(new VfxTriggerMapping(
                    VfxSourceDomains.CombatHit,
                    burstTrigger,
                    new VfxDefinitionId(CharacterTestVfxIds.FireballBurstImpactDefinitionId),
                    CharacterTestVfxIds.FireballBurstImpactLifetimeFrames))
                .Add(new VfxTriggerMapping(
                    VfxSourceDomains.CombatHit,
                    flameLanceTrigger,
                    new VfxDefinitionId(CharacterTestVfxIds.FlameLanceImpactDefinitionId),
                    CharacterTestVfxIds.FlameLanceImpactLifetimeFrames))
                .Add(new VfxTriggerMapping(
                    VfxSourceDomains.CombatHit,
                    tornadoTrigger,
                    new VfxDefinitionId(CharacterTestVfxIds.TornadoImpactDefinitionId),
                    CharacterTestVfxIds.TornadoImpactLifetimeFrames))
                .Add(new VfxTriggerMapping(
                    VfxSourceDomains.CombatHit,
                    tornadoTickTrigger,
                    new VfxDefinitionId(CharacterTestVfxIds.TornadoTickDefinitionId),
                    CharacterTestVfxIds.TornadoTickLifetimeFrames))
                .Add(new VfxTriggerMapping(
                    VfxSourceDomains.CombatHit,
                    beamLanceTrigger,
                    new VfxDefinitionId(CharacterTestVfxIds.BeamLanceImpactDefinitionId),
                    CharacterTestVfxIds.BeamLanceImpactLifetimeFrames))
                .Add(new VfxTriggerMapping(
                    VfxSourceDomains.CombatHit,
                    channelBeamTrigger,
                    new VfxDefinitionId(CharacterTestVfxIds.ChannelBeamImpactDefinitionId),
                    CharacterTestVfxIds.ChannelBeamImpactLifetimeFrames))
                .Add(new VfxTriggerMapping(
                    VfxSourceDomains.CombatHit,
                    laserLanceTrigger,
                    new VfxDefinitionId(CharacterTestVfxIds.LaserLanceImpactDefinitionId),
                    CharacterTestVfxIds.LaserLanceImpactLifetimeFrames))
                .Add(new VfxTriggerMapping(
                    VfxSourceDomains.CombatHit,
                    chainLightningTrigger,
                    new VfxDefinitionId(CharacterTestVfxIds.ChainLightningImpactDefinitionId),
                    CharacterTestVfxIds.ChainLightningImpactLifetimeFrames))
                .Add(new VfxTriggerMapping(
                    VfxSourceDomains.CombatHit,
                    poisonBottleTrigger,
                    new VfxDefinitionId(CharacterTestVfxIds.PoisonBottleShatterDefinitionId),
                    CharacterTestVfxIds.PoisonBottleShatterLifetimeFrames))
                .Add(new VfxTriggerMapping(
                    VfxSourceDomains.CombatHit,
                    stunBoltTrigger,
                    new VfxDefinitionId(CharacterTestVfxIds.StunBoltImpactDefinitionId),
                    CharacterTestVfxIds.StunBoltImpactLifetimeFrames))
                .Add(new VfxTriggerMapping(
                    VfxSourceDomains.CombatHit,
                    meteorBarrageTrigger,
                    new VfxDefinitionId(CharacterTestVfxIds.MeteorBarrageImpactDefinitionId),
                    CharacterTestVfxIds.MeteorBarrageImpactLifetimeFrames))
                .Add(new VfxTriggerMapping(
                    VfxSourceDomains.CombatHit,
                    swordWaveTrigger,
                    new VfxDefinitionId(CharacterTestVfxIds.SwordWaveImpactDefinitionId),
                    CharacterTestVfxIds.SwordWaveImpactLifetimeFrames))
                .Add(new VfxTriggerMapping(
                    VfxSourceDomains.CombatHit,
                    dashStrikeTrigger,
                    new VfxDefinitionId(CharacterTestVfxIds.SwordWaveImpactDefinitionId),
                    CharacterTestVfxIds.SwordWaveImpactLifetimeFrames))
                .Add(new VfxTriggerMapping(
                    VfxSourceDomains.CombatHit,
                    hookChainTrigger,
                    new VfxDefinitionId(CharacterTestVfxIds.StunBoltImpactDefinitionId),
                    CharacterTestVfxIds.StunBoltImpactLifetimeFrames))
                .Add(new VfxTriggerMapping(
                    VfxSourceDomains.CombatHit,
                    runeTurretTrigger,
                    new VfxDefinitionId(CharacterTestVfxIds.RuneTurretImpactDefinitionId),
                    CharacterTestVfxIds.RuneTurretImpactLifetimeFrames))
                .Add(new VfxTriggerMapping(
                    VfxSourceDomains.CombatHit,
                    poisonTickTrigger,
                    new VfxDefinitionId(CharacterTestVfxIds.PoisonedTickDefinitionId),
                    CharacterTestVfxIds.PoisonedTickLifetimeFrames));
            CharacterTestRuntimeCatalogLoadDiagnostics.RecordEmbedded(
                VfxConfigBinaryImporter.TriggerMapCatalogKind,
                16,
                "Generated VFX trigger map catalog was empty or missing.");
            return resolver;
        }

        public static MemoryResourceProvider CreateMemoryProvider(
            GameObject fireballPrefab,
            GameObject fireballBurstFlightPrefab = null,
            GameObject fireballBurstImpactPrefab = null,
            GameObject flameLanceFlightPrefab = null,
            GameObject flameLanceImpactPrefab = null,
            GameObject beamLanceBeamPrefab = null,
            GameObject beamLanceImpactPrefab = null,
            GameObject beamLanceChargePrefab = null,
            GameObject channelBeamChargePrefab = null,
            GameObject channelBeamBeamPrefab = null,
            GameObject channelBeamImpactPrefab = null,
            GameObject laserLanceChargePrefab = null,
            GameObject laserLancePathPrefab = null,
            GameObject laserLanceImpactPrefab = null,
            GameObject chainLightningChargePrefab = null,
            GameObject chainLightningArcPrefab = null,
            GameObject chainLightningImpactPrefab = null,
            GameObject poisonDartFlightPrefab = null,
            GameObject poisonDartImpactPrefab = null,
            GameObject poisonBottlePoolPrefab = null,
            GameObject poisonedTickPrefab = null,
            GameObject guardAuraPrefab = null,
            GameObject physicalShieldPrefab = null,
            GameObject fireballExplosionImpactPrefab = null,
            GameObject tornadoFlightPrefab = null,
            GameObject tornadoImpactPrefab = null,
            GameObject tornadoChargePrefab = null,
            GameObject tornadoTickPrefab = null,
            GameObject areaConfinementZonePrefab = null,
            GameObject areaConfinementCharacterPrefab = null,
            GameObject stunnedStatusPrefab = null,
            GameObject paralyzedStatusPrefab = null,
            GameObject swordWaveFlightPrefab = null,
            GameObject swordWaveChargePrefab = null,
            GameObject swordWaveImpactPrefab = null,
            GameObject arcMineFlightPrefab = null,
            GameObject arcMineDeployPrefab = null,
            GameObject arcMineFusePrefab = null,
            GameObject arcMineTriggerPrefab = null,
            GameObject runeTurretDeployPrefab = null,
            GameObject runeTurretIdlePrefab = null,
            GameObject runeTurretFirePrefab = null,
            GameObject runeTurretFlightPrefab = null,
            GameObject runeTurretImpactPrefab = null,
            GameObject runeTurretExpirePrefab = null,
            IVfxUnityPrefabBindingSource publishedBindings = null,
            GameObject bladeTrailPrefab = null)
        {

            var provider = new MemoryResourceProvider(ReleaseRuntimeTemplate);
            var published = new List<VfxUnityPrefabBinding>();
            publishedBindings?.CopyBindingsTo(published);
            GameObject CreateFallback(string address, Func<GameObject> create)
            {
                for (int i = 0; i < published.Count; i++)
                {
                    VfxUnityPrefabBinding binding = published[i];
                    if (binding != null && binding.ResourceKey.IsValid
                        && string.Equals(binding.ResourceKey.TypeId, ResourceTypeIds.GameObject, StringComparison.Ordinal)
                        && string.Equals(binding.Address, address, StringComparison.Ordinal)
                        && binding.Prefab != null)
                        return binding.Prefab;
                }
                GameObject template = create();
                provider.RegisterOwned(address, template);
                return template;
            }
            try
            {
                GameObject burstFlight = fireballBurstFlightPrefab != null ? fireballBurstFlightPrefab : fireballPrefab;
                GameObject burstImpact = fireballBurstImpactPrefab != null ? fireballBurstImpactPrefab : burstFlight;
                GameObject flameFlight = flameLanceFlightPrefab != null ? flameLanceFlightPrefab : fireballPrefab;
                GameObject flameImpact = flameLanceImpactPrefab != null ? flameLanceImpactPrefab : flameFlight;
                GameObject beamLanceBeam = beamLanceBeamPrefab != null ? beamLanceBeamPrefab : flameFlight;
                GameObject beamLanceImpact = beamLanceImpactPrefab != null ? beamLanceImpactPrefab : flameImpact;
                GameObject beamLanceCharge = beamLanceChargePrefab != null ? beamLanceChargePrefab : beamLanceBeam;
                GameObject channelBeamCharge = channelBeamChargePrefab != null ? channelBeamChargePrefab : beamLanceCharge;
                GameObject channelBeamBeam = channelBeamBeamPrefab != null ? channelBeamBeamPrefab : beamLanceBeam;
                GameObject channelBeamImpact = channelBeamImpactPrefab != null ? channelBeamImpactPrefab : beamLanceImpact;
                GameObject laserLanceCharge = laserLanceChargePrefab != null ? laserLanceChargePrefab : beamLanceCharge;
                GameObject laserLancePath = laserLancePathPrefab != null ? laserLancePathPrefab : beamLanceBeam;
                GameObject laserLanceImpact = laserLanceImpactPrefab != null ? laserLanceImpactPrefab : beamLanceImpact;
                GameObject chainLightningCharge =
                    chainLightningChargePrefab != null ? chainLightningChargePrefab : laserLanceCharge;
                GameObject chainLightningArc = chainLightningArcPrefab != null
                    ? chainLightningArcPrefab
                    : CreateFallback(CharacterTestVfxIds.ChainLightningArcResourceAddress, () => CharacterTestChainLightningArcVfxView.CreateRuntimePrefab());
                GameObject chainLightningImpact =
                    chainLightningImpactPrefab != null ? chainLightningImpactPrefab : laserLanceImpact;
                GameObject stunBoltFlight = chainLightningArc;
                GameObject stunBoltCharge = chainLightningCharge;
                GameObject stunBoltImpact = chainLightningImpact;
                GameObject meteorBarrageFlight =
                    CreateFallback(CharacterTestVfxIds.MeteorBarrageFlightResourceAddress, () => CharacterTestMeteorBarrageVfxView.CreateRuntimePrefab(
                        CharacterTestMeteorBarrageVfxMode.Flight));
                GameObject meteorBarrageCharge =
                    CreateFallback(CharacterTestVfxIds.MeteorBarrageChargeResourceAddress, () => CharacterTestMeteorBarrageVfxView.CreateRuntimePrefab(
                        CharacterTestMeteorBarrageVfxMode.Charge));
                GameObject meteorBarrageTelegraph =
                    CreateFallback(CharacterTestVfxIds.MeteorBarrageTelegraphResourceAddress, () => CharacterTestMeteorBarrageVfxView.CreateRuntimePrefab(
                        CharacterTestMeteorBarrageVfxMode.Telegraph));
                GameObject meteorBarrageImpact =
                    CreateFallback(CharacterTestVfxIds.MeteorBarrageImpactResourceAddress, () => CharacterTestMeteorBarrageVfxView.CreateRuntimePrefab(
                        CharacterTestMeteorBarrageVfxMode.Impact));
                GameObject swordWaveFlight = swordWaveFlightPrefab != null
                    ? swordWaveFlightPrefab
                    : CreateFallback(CharacterTestVfxIds.SwordWaveFlightResourceAddress, () => CharacterTestSwordWaveVfxView.CreateRuntimePrefab(
                        CharacterTestSwordWaveVfxMode.Flight));
                GameObject swordWaveCharge = swordWaveChargePrefab != null
                    ? swordWaveChargePrefab
                    : CreateFallback(CharacterTestVfxIds.SwordWaveChargeResourceAddress, () => CharacterTestSwordWaveVfxView.CreateRuntimePrefab(
                        CharacterTestSwordWaveVfxMode.Charge));
                GameObject swordWaveImpact = swordWaveImpactPrefab != null
                    ? swordWaveImpactPrefab
                    : CreateFallback(CharacterTestVfxIds.SwordWaveImpactResourceAddress, () => CharacterTestSwordWaveVfxView.CreateRuntimePrefab(
                        CharacterTestSwordWaveVfxMode.Impact));
                GameObject poisonDartFlight = poisonDartFlightPrefab != null ? poisonDartFlightPrefab : flameFlight;
                GameObject poisonDartImpact = poisonDartImpactPrefab != null ? poisonDartImpactPrefab : flameImpact;
                GameObject poisonBottlePool = poisonBottlePoolPrefab != null ? poisonBottlePoolPrefab : poisonDartImpact;
                GameObject poisonedTick = poisonedTickPrefab != null ? poisonedTickPrefab : poisonDartImpact;
                GameObject guardAura = guardAuraPrefab != null
                    ? guardAuraPrefab
                    : CreateFallback(CharacterTestVfxIds.GuardAuraResourceAddress, () => CharacterTestAuraVfxView.CreateRuntimePrefab());
                GameObject physicalShield = physicalShieldPrefab != null
                    ? physicalShieldPrefab
                    : CreateFallback(CharacterTestVfxIds.PhysicalShieldResourceAddress, () => CharacterTestPhysicalShieldVfxView.CreateRuntimePrefab());
                GameObject explosionImpact =
                    fireballExplosionImpactPrefab != null ? fireballExplosionImpactPrefab : burstImpact;
                GameObject tornadoFlight = tornadoFlightPrefab != null ? tornadoFlightPrefab : burstFlight;
                GameObject tornadoImpact = tornadoImpactPrefab != null ? tornadoImpactPrefab : burstImpact;
                GameObject tornadoCharge = tornadoChargePrefab != null ? tornadoChargePrefab : tornadoFlight;
                GameObject tornadoTick = tornadoTickPrefab != null ? tornadoTickPrefab : tornadoImpact;
                GameObject areaConfinementZone = areaConfinementZonePrefab != null
                    ? areaConfinementZonePrefab
                    : CreateFallback(CharacterTestVfxIds.AreaConfinementZoneResourceAddress, () => CharacterTestAreaConfinementVfxView.CreateRuntimePrefab(
                        CharacterTestAreaConfinementVfxMode.Zone));
                GameObject areaConfinementCharacter = areaConfinementCharacterPrefab != null
                    ? areaConfinementCharacterPrefab
                    : CreateFallback(CharacterTestVfxIds.AreaConfinementCharacterResourceAddress, () => CharacterTestAreaConfinementCharacterVfxView.CreateRuntimePrefab(
                        CharacterTestAreaConfinementCharacterVfxMode.Active));
                GameObject areaConfinementTelegraph =
                    CreateFallback(CharacterTestVfxIds.AreaConfinementTelegraphResourceAddress, () => CharacterTestAreaConfinementVfxView.CreateRuntimePrefab(
                        CharacterTestAreaConfinementVfxMode.Telegraph));
                GameObject areaConfinementTelegraphCancel =
                    CreateFallback(CharacterTestVfxIds.AreaConfinementTelegraphCancelResourceAddress, () => CharacterTestAreaConfinementVfxView.CreateRuntimePrefab(
                        CharacterTestAreaConfinementVfxMode.TelegraphCancel));
                GameObject areaConfinementCharacterRelease =
                    CreateFallback(CharacterTestVfxIds.AreaConfinementCharacterReleaseResourceAddress, () => CharacterTestAreaConfinementCharacterVfxView.CreateRuntimePrefab(
                        CharacterTestAreaConfinementCharacterVfxMode.Release));
                GameObject arcMineFlight = arcMineFlightPrefab != null
                    ? arcMineFlightPrefab
                    : CreateFallback(CharacterTestVfxIds.ArcMineFlightResourceAddress, () => CharacterTestArcMineVfxView.CreateRuntimePrefab(CharacterTestArcMineVfxMode.Flight));
                GameObject arcMineDeploy = arcMineDeployPrefab != null
                    ? arcMineDeployPrefab
                    : CreateFallback(CharacterTestVfxIds.ArcMineDeployResourceAddress, () => CharacterTestArcMineVfxView.CreateRuntimePrefab(CharacterTestArcMineVfxMode.Deploy));
                GameObject arcMineFuse = arcMineFusePrefab != null
                    ? arcMineFusePrefab
                    : CreateFallback(CharacterTestVfxIds.ArcMineFuseResourceAddress, () => CharacterTestArcMineVfxView.CreateRuntimePrefab(CharacterTestArcMineVfxMode.Fuse));
                GameObject arcMineTrigger = arcMineTriggerPrefab != null
                    ? arcMineTriggerPrefab
                    : CreateFallback(CharacterTestVfxIds.ArcMineTriggerResourceAddress, () => CharacterTestArcMineVfxView.CreateRuntimePrefab(CharacterTestArcMineVfxMode.Trigger));
                GameObject runeTurretDeploy = runeTurretDeployPrefab != null
                    ? runeTurretDeployPrefab
                    : CreateFallback(CharacterTestVfxIds.RuneTurretDeployResourceAddress, () => CharacterTestRuneTurretVfxView.CreateRuntimePrefab(CharacterTestRuneTurretVfxMode.Deploy));
                GameObject runeTurretIdle = runeTurretIdlePrefab != null
                    ? runeTurretIdlePrefab
                    : CreateFallback(CharacterTestVfxIds.RuneTurretIdleResourceAddress, () => CharacterTestRuneTurretVfxView.CreateRuntimePrefab(CharacterTestRuneTurretVfxMode.Idle));
                GameObject runeTurretFire = runeTurretFirePrefab != null
                    ? runeTurretFirePrefab
                    : CreateFallback(CharacterTestVfxIds.RuneTurretFireResourceAddress, () => CharacterTestRuneTurretVfxView.CreateRuntimePrefab(CharacterTestRuneTurretVfxMode.Fire));
                GameObject runeTurretFlight = runeTurretFlightPrefab != null
                    ? runeTurretFlightPrefab
                    : CreateFallback(CharacterTestVfxIds.RuneTurretFlightResourceAddress, () => CharacterTestRuneTurretVfxView.CreateRuntimePrefab(CharacterTestRuneTurretVfxMode.Flight));
                GameObject runeTurretImpact = runeTurretImpactPrefab != null
                    ? runeTurretImpactPrefab
                    : CreateFallback(CharacterTestVfxIds.RuneTurretImpactResourceAddress, () => CharacterTestRuneTurretVfxView.CreateRuntimePrefab(CharacterTestRuneTurretVfxMode.Impact));
                GameObject runeTurretExpire = runeTurretExpirePrefab != null
                    ? runeTurretExpirePrefab
                    : CreateFallback(CharacterTestVfxIds.RuneTurretExpireResourceAddress, () => CharacterTestRuneTurretVfxView.CreateRuntimePrefab(CharacterTestRuneTurretVfxMode.Expire));
                GameObject stunnedStatus = stunnedStatusPrefab != null
                    ? stunnedStatusPrefab
                    : CreateFallback(CharacterTestVfxIds.StunnedStatusResourceAddress, () => CharacterTestControlStatusVfxView.CreateRuntimePrefab(
                        CharacterTestControlStatusVfxMode.Stunned));
                GameObject paralyzedStatus = paralyzedStatusPrefab != null
                    ? paralyzedStatusPrefab
                    : CreateFallback(CharacterTestVfxIds.ParalyzedStatusResourceAddress, () => CharacterTestControlStatusVfxView.CreateRuntimePrefab(
                        CharacterTestControlStatusVfxMode.Paralyzed));
                provider
                    .Register(CharacterTestVfxIds.FireballResourceAddress, fireballPrefab)
                    .Register(CharacterTestVfxIds.FireballBurstFlightResourceAddress, burstFlight)
                    .Register(CharacterTestVfxIds.FireballBurstImpactResourceAddress, burstImpact)
                    .Register(CharacterTestVfxIds.FlameLanceFlightResourceAddress, flameFlight)
                    .Register(CharacterTestVfxIds.FlameLanceImpactResourceAddress, flameImpact)
                    .Register(CharacterTestVfxIds.BeamLanceBeamResourceAddress, beamLanceBeam)
                    .Register(CharacterTestVfxIds.BeamLanceImpactResourceAddress, beamLanceImpact)
                    .Register(CharacterTestVfxIds.BeamLanceChargeResourceAddress, beamLanceCharge)
                    .Register(CharacterTestVfxIds.ChannelBeamChargeResourceAddress, channelBeamCharge)
                    .Register(CharacterTestVfxIds.ChannelBeamBeamResourceAddress, channelBeamBeam)
                    .Register(CharacterTestVfxIds.ChannelBeamImpactResourceAddress, channelBeamImpact)
                    .Register(CharacterTestVfxIds.LaserLanceChargeResourceAddress, laserLanceCharge)
                    .Register(CharacterTestVfxIds.LaserLancePathResourceAddress, laserLancePath)
                    .Register(CharacterTestVfxIds.LaserLanceImpactResourceAddress, laserLanceImpact)
                    .Register(CharacterTestVfxIds.ChainLightningChargeResourceAddress, chainLightningCharge)
                    .Register(CharacterTestVfxIds.ChainLightningArcResourceAddress, chainLightningArc)
                    .Register(CharacterTestVfxIds.ChainLightningImpactResourceAddress, chainLightningImpact)
                    .Register(CharacterTestVfxIds.StunBoltFlightResourceAddress, stunBoltFlight)
                    .Register(CharacterTestVfxIds.StunBoltChargeResourceAddress, stunBoltCharge)
                    .Register(CharacterTestVfxIds.StunBoltImpactResourceAddress, stunBoltImpact)
                    .Register(CharacterTestVfxIds.MeteorBarrageFlightResourceAddress, meteorBarrageFlight)
                    .Register(CharacterTestVfxIds.MeteorBarrageChargeResourceAddress, meteorBarrageCharge)
                    .Register(CharacterTestVfxIds.MeteorBarrageTelegraphResourceAddress, meteorBarrageTelegraph)
                    .Register(CharacterTestVfxIds.MeteorBarrageImpactResourceAddress, meteorBarrageImpact)
                    .Register(CharacterTestVfxIds.SwordWaveFlightResourceAddress, swordWaveFlight)
                    .Register(CharacterTestVfxIds.SwordWaveChargeResourceAddress, swordWaveCharge)
                    .Register(CharacterTestVfxIds.SwordWaveImpactResourceAddress, swordWaveImpact)
                    .Register(CharacterTestVfxIds.ArcMineFlightResourceAddress, arcMineFlight)
                    .Register(CharacterTestVfxIds.ArcMineDeployResourceAddress, arcMineDeploy)
                    .Register(CharacterTestVfxIds.ArcMineFuseResourceAddress, arcMineFuse)
                    .Register(CharacterTestVfxIds.ArcMineTriggerResourceAddress, arcMineTrigger)
                    .Register(CharacterTestVfxIds.RuneTurretDeployResourceAddress, runeTurretDeploy)
                    .Register(CharacterTestVfxIds.RuneTurretIdleResourceAddress, runeTurretIdle)
                    .Register(CharacterTestVfxIds.RuneTurretFireResourceAddress, runeTurretFire)
                    .Register(CharacterTestVfxIds.RuneTurretFlightResourceAddress, runeTurretFlight)
                    .Register(CharacterTestVfxIds.RuneTurretImpactResourceAddress, runeTurretImpact)
                    .Register(CharacterTestVfxIds.RuneTurretExpireResourceAddress, runeTurretExpire)
                    .Register(CharacterTestVfxIds.PoisonDartFlightResourceAddress, poisonDartFlight)
                    .Register(CharacterTestVfxIds.PoisonDartImpactResourceAddress, poisonDartImpact)
                    .Register(CharacterTestVfxIds.PoisonBottlePoolResourceAddress, poisonBottlePool)
                    .Register(CharacterTestVfxIds.PoisonedTickResourceAddress, poisonedTick)
                    .Register(CharacterTestVfxIds.FireballChargeResourceAddress, fireballPrefab)
                    .Register(CharacterTestVfxIds.FireballBurstChargeResourceAddress, burstFlight)
                    .Register(CharacterTestVfxIds.GuardAuraResourceAddress, guardAura)
                    .Register(CharacterTestVfxIds.PhysicalShieldResourceAddress, physicalShield)
                    .Register(CharacterTestVfxIds.FireballExplosionImpactResourceAddress, explosionImpact)
                    .Register(CharacterTestVfxIds.TornadoFlightResourceAddress, tornadoFlight)
                    .Register(CharacterTestVfxIds.TornadoChargeResourceAddress, tornadoCharge)
                    .Register(CharacterTestVfxIds.TornadoImpactResourceAddress, tornadoImpact)
                    .Register(CharacterTestVfxIds.TornadoTickResourceAddress, tornadoTick)
                    .Register(CharacterTestVfxIds.AreaConfinementZoneResourceAddress, areaConfinementZone)
                    .Register(CharacterTestVfxIds.AreaConfinementCharacterResourceAddress, areaConfinementCharacter)
                    .Register(CharacterTestVfxIds.AreaConfinementTelegraphResourceAddress, areaConfinementTelegraph)
                    .Register(CharacterTestVfxIds.AreaConfinementTelegraphCancelResourceAddress, areaConfinementTelegraphCancel)
                    .Register(CharacterTestVfxIds.AreaConfinementCharacterReleaseResourceAddress, areaConfinementCharacterRelease)
                    .Register(CharacterTestVfxIds.StunnedStatusResourceAddress, stunnedStatus)
                    .Register(CharacterTestVfxIds.ParalyzedStatusResourceAddress, paralyzedStatus);
                if (bladeTrailPrefab != null)
                    provider.Register(CharacterTestBladeTrailVfxIds.ResourceAddress, bladeTrailPrefab);
                RegisterPublishedBindings(provider, publishedBindings);
                return provider;
            }
            catch
            {
                provider.Dispose();
                throw;
            }
        }

        private static void ReleaseRuntimeTemplate(object value)
        {
            if (value is not GameObject template || template == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(template);
            else UnityEngine.Object.DestroyImmediate(template);
        }
        private static void RegisterPublishedBindings(
            MemoryResourceProvider provider,
            IVfxUnityPrefabBindingSource publishedBindings)
        {
            if (provider == null || publishedBindings == null)
                return;

            var bindings = new List<VfxUnityPrefabBinding>();
            publishedBindings.CopyBindingsTo(bindings);
            for (int i = 0; i < bindings.Count; i++)
            {
                VfxUnityPrefabBinding binding = bindings[i];
                if (binding == null
                    || !binding.ResourceKey.IsValid
                    || !string.Equals(binding.ResourceKey.TypeId, ResourceTypeIds.GameObject, StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(binding.Address)
                    || binding.Prefab == null)
                {
                    continue;
                }

                provider.Register(binding.Address, binding.Prefab);
            }
        }

        public static ResourceCatalog CreateResourceCatalog()
        {
            return new ResourceCatalog(
                CharacterTestVfxIds.FireballCatalogId,
                CharacterTestVfxIds.FireballResourcePackageId,
                new[]
                {
                    new ResourceCatalogEntry(CharacterTestBladeTrailVfxIds.ResourceId, ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id, CharacterTestBladeTrailVfxIds.ResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "Weapon", "BladeTrail" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.FireballResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.FireballResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "Fireball" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.FireballBurstFlightResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.FireballBurstFlightResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "FireballBurst", "Flight" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.FireballBurstImpactResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.FireballBurstImpactResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "FireballBurst", "Impact" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.FlameLanceFlightResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.FlameLanceFlightResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "FlameLance", "Flight" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.FlameLanceImpactResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.FlameLanceImpactResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "FlameLance", "Impact" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.BeamLanceBeamResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.BeamLanceBeamResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "BeamLance", "Beam" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.BeamLanceImpactResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.BeamLanceImpactResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "BeamLance", "Impact" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.BeamLanceChargeResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.BeamLanceChargeResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "BeamLance", "Charge" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.ChannelBeamChargeResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.ChannelBeamChargeResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "ChannelBeam", "Charge" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.ChannelBeamBeamResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.ChannelBeamBeamResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "ChannelBeam", "Beam" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.ChannelBeamImpactResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.ChannelBeamImpactResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "ChannelBeam", "Impact" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.LaserLanceChargeResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.LaserLanceChargeResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "LaserLance", "Charge" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.LaserLancePathResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.LaserLancePathResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "LaserLance", "Laser" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.LaserLanceImpactResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.LaserLanceImpactResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "LaserLance", "Impact" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.ChainLightningChargeResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.ChainLightningChargeResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "ChainLightning", "Charge" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.ChainLightningArcResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.ChainLightningArcResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "ChainLightning", "Arc" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.ChainLightningImpactResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.ChainLightningImpactResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "ChainLightning", "Impact" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.StunBoltFlightResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.StunBoltFlightResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "StunBolt", "Flight" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.StunBoltChargeResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.StunBoltChargeResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "StunBolt", "Charge" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.StunBoltImpactResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.StunBoltImpactResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "StunBolt", "Impact" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.MeteorBarrageFlightResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.MeteorBarrageFlightResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "MeteorBarrage", "Flight" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.MeteorBarrageChargeResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.MeteorBarrageChargeResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "MeteorBarrage", "Charge" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.MeteorBarrageTelegraphResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.MeteorBarrageTelegraphResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "MeteorBarrage", "Telegraph" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.MeteorBarrageImpactResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.MeteorBarrageImpactResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "MeteorBarrage", "Impact" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.SwordWaveFlightResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.SwordWaveFlightResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "SwordWave", "Flight" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.SwordWaveChargeResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.SwordWaveChargeResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "SwordWave", "Charge" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.SwordWaveImpactResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.SwordWaveImpactResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "SwordWave", "Impact" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.ArcMineFlightResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.ArcMineFlightResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "ArcMine", "Flight" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.ArcMineDeployResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.ArcMineDeployResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "ArcMine", "Deploy" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.ArcMineFuseResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.ArcMineFuseResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "ArcMine", "Fuse" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.ArcMineTriggerResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.ArcMineTriggerResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "ArcMine", "Trigger" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.RuneTurretDeployResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.RuneTurretDeployResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "RuneTurret", "Deploy" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.RuneTurretIdleResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.RuneTurretIdleResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "RuneTurret", "Idle" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.RuneTurretFireResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.RuneTurretFireResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "RuneTurret", "Fire" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.RuneTurretFlightResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.RuneTurretFlightResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "RuneTurret", "Flight" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.RuneTurretImpactResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.RuneTurretImpactResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "RuneTurret", "Impact" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.RuneTurretExpireResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.RuneTurretExpireResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "RuneTurret", "Expire" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.PoisonBottleFlightResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.PoisonBottleFlightResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "PoisonBottle", "Flight" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.PoisonBottleShatterResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.PoisonBottleShatterResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "PoisonBottle", "Shatter" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.PoisonBottlePoolResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.PoisonBottlePoolResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "PoisonBottle", "Pool" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.PoisonedTickResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.PoisonedTickResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "Poisoned", "Tick" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.FireballChargeResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.FireballChargeResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "Fireball", "Charge" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.FireballBurstChargeResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.FireballBurstChargeResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "FireballBurst", "Charge" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.GuardAuraResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.GuardAuraResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "GuardAura", "Aura" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.PhysicalShieldResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.PhysicalShieldResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "PhysicalShield", "Shield" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.StunnedStatusResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.StunnedStatusResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "Status", "Stunned" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.ParalyzedStatusResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.ParalyzedStatusResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "Status", "Paralyzed" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.FireballExplosionImpactResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.FireballExplosionImpactResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "FireballExplosion", "Impact" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.TornadoFlightResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.TornadoFlightResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "Tornado", "Flight" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.TornadoChargeResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.TornadoChargeResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "Tornado", "Charge" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.TornadoImpactResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.TornadoImpactResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "Tornado", "Impact" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.TornadoTickResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.TornadoTickResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "Tornado", "Tick" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.AreaConfinementZoneResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.AreaConfinementZoneResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "AreaConfinement", "Zone" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.AreaConfinementCharacterResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.AreaConfinementCharacterResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "AreaConfinement", "Character" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.AreaConfinementTelegraphResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.AreaConfinementTelegraphResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "AreaConfinement", "Telegraph" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.AreaConfinementTelegraphCancelResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.AreaConfinementTelegraphCancelResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "AreaConfinement", "TelegraphCancel" }),
                    new ResourceCatalogEntry(
                        CharacterTestVfxIds.AreaConfinementCharacterReleaseResourceId,
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        CharacterTestVfxIds.AreaConfinementCharacterReleaseResourceAddress,
                        packageId: CharacterTestVfxIds.FireballResourcePackageId,
                        labels: new[] { "CharacterTest", "VFX", "AreaConfinement", "CharacterRelease" })
                });
        }
    }

    public static class CharacterTestProjectilePresentationSource
    {
        public const string FireballViewNamePrefix = CombatProjectileUnityPresentationBindingV2.DefaultViewNamePrefix;
        public const string DefaultGeneratedBindingsPath =
            "Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_presentation_bindings.json";

        public static CombatProjectileUnityPresentationRegistry CreateRegistry()
        {
            CombatProjectileUnityPresentationRegistry registry =
                LoadGeneratedBindingsOrEmpty(DefaultGeneratedBindingsPath);
            return registry.Count > 0 ? registry : CreateEmbeddedFallbackRegistry();
        }

        public static CombatProjectileUnityPresentationRegistry LoadGeneratedBindingsOrEmpty(string path)
        {
            return CharacterTestRuntimeCatalogBinaryFallback.LoadBinaryThenJsonOrEmpty(
                path,
                CombatProjectileUnityPresentationConfigBinaryImporter.CatalogKind,
                CombatProjectileUnityPresentationConfigBinaryImporter.ImportFile,
                CombatProjectileUnityPresentationConfigJsonImporter.ImportFile,
                registry => registry?.Count ?? 0,
                () => new CombatProjectileUnityPresentationRegistry());
        }

        public static CombatProjectileUnityPresentationRegistry CreateEmbeddedFallbackRegistry()
        {
            var registry = new CombatProjectileUnityPresentationRegistry(new[]
            {
                CreateFlightBinding(CharacterTestFireballProjectileAdapter.DefinitionId, CharacterTestVfxIds.FireballResourceKey),
                CreateFlightBinding(CharacterTestFireballProjectileAdapter.BurstDefinitionId, CharacterTestVfxIds.FireballBurstFlightResourceKey),
                CreateFlightBinding(CharacterTestFireballProjectileAdapter.FlameLanceDefinitionId, CharacterTestVfxIds.FlameLanceFlightResourceKey),
                CreateFlightBinding(CharacterTestFireballProjectileAdapter.FireballLobDefinitionId, CharacterTestVfxIds.FireballBurstFlightResourceKey),
                CreateFlightBinding(CharacterTestFireballProjectileAdapter.FireballSurgeDefinitionId, CharacterTestVfxIds.FireballResourceKey),
                CreateFlightBinding(CharacterTestFireballProjectileAdapter.FireballReturnDefinitionId, CharacterTestVfxIds.FireballResourceKey),
                CreateFlightBinding(CharacterTestFireballProjectileAdapter.FireballSwayDefinitionId, CharacterTestVfxIds.FireballResourceKey),
                CreateFlightBinding(CharacterTestFireballProjectileAdapter.FireballHomingDefinitionId, CharacterTestVfxIds.FireballResourceKey),
                CreateFlightBinding(CharacterTestFireballProjectileAdapter.FireballBezierDefinitionId, CharacterTestVfxIds.FireballResourceKey),
                CreateFlightBinding(CharacterTestFireballProjectileAdapter.FireballExplosionDefinitionId, CharacterTestVfxIds.FireballBurstFlightResourceKey),
                CreateFlightBinding(CharacterTestFireballProjectileAdapter.TornadoDefinitionId, CharacterTestVfxIds.TornadoFlightResourceKey),
                CreateFlightBinding(CharacterTestFireballProjectileAdapter.BeamLanceDefinitionId, CharacterTestVfxIds.BeamLanceBeamResourceKey),
                CreateFlightBinding(CharacterTestFireballProjectileAdapter.ChannelBeamDefinitionId, CharacterTestVfxIds.ChannelBeamBeamResourceKey),
                CreateFlightBinding(CharacterTestFireballProjectileAdapter.LaserLanceDefinitionId, CharacterTestVfxIds.LaserLancePathResourceKey),
                CreateFlightBinding(CharacterTestFireballProjectileAdapter.PoisonBottleDefinitionId, CharacterTestVfxIds.PoisonBottleFlightResourceKey),
                CreateFlightBinding(CharacterTestFireballProjectileAdapter.StunBoltDefinitionId, CharacterTestVfxIds.StunBoltFlightResourceKey),
                CreateFlightBinding(CharacterTestFireballProjectileAdapter.MeteorBarrageDefinitionId, CharacterTestVfxIds.MeteorBarrageFlightResourceKey),
                CreateFlightBinding(CharacterTestFireballProjectileAdapter.SwordWaveDefinitionId, CharacterTestVfxIds.SwordWaveFlightResourceKey)
            });
            CharacterTestRuntimeCatalogLoadDiagnostics.RecordEmbedded(
                CombatProjectileUnityPresentationConfigBinaryImporter.CatalogKind,
                registry.Count,
                "Generated projectile presentation catalog was empty or missing.");
            return registry;
        }

        private static CombatProjectileUnityPresentationBindingV2 CreateFlightBinding(
            int projectileDefinitionId,
            ResourceKey resource)
        {
            return new CombatProjectileUnityPresentationBindingV2(
                projectileDefinitionId,
                FireballViewNamePrefix,
                CombatProjectileUnityPresentationExitPolicy.Default,
                new CombatProjectileUnityPresentationSlot(
                    CombatProjectileUnityPresentationSlotKind.FlightBody,
                    resource,
                    CombatProjectileUnityPresentationOrientation.ProjectileForward,
                    FixVector3.Zero,
                    0));
        }
    }

    internal sealed class CharacterTestLaserPathVfxBridgeModule : RuntimeModule
    {
        private readonly Func<IReadOnlyList<CharacterTestLaserPathVfxEvent>> _eventsProvider;
        private readonly VfxRuntimeModule _vfx;
        private readonly VfxDefinitionId _definitionId =
            new VfxDefinitionId(CharacterTestVfxIds.LaserLancePathDefinitionId);

        public CharacterTestLaserPathVfxBridgeModule(
            Func<IReadOnlyList<CharacterTestLaserPathVfxEvent>> eventsProvider,
            VfxRuntimeModule vfx)
            : base("CharacterTest.LaserPathVfxBridge", RuntimeTickStage.PostSimulation, priority: 145)
        {
            _eventsProvider = eventsProvider ?? throw new ArgumentNullException(nameof(eventsProvider));
            _vfx = vfx ?? throw new ArgumentNullException(nameof(vfx));
        }

        public int LastFrameSpawnedCount { get; private set; }

        /// <summary>
        /// NoAlloc: bridges already-buffered laser path events into VFX spawn requests.
        /// </summary>
        public override void Tick(RuntimeTickContext context)
        {
            LastFrameSpawnedCount = 0;
            IReadOnlyList<CharacterTestLaserPathVfxEvent> events = _eventsProvider();
            if (events == null || events.Count == 0)
                return;

            int triggerId = CharacterTestCharacterActionConfigSource.LaserLancePathVfxStableEventId;
            for (int i = 0; i < events.Count; i++)
            {
                CharacterTestLaserPathVfxEvent evt = events[i];
                _vfx.EnqueueSpawnRequest(new VfxSpawnRequest(
                    evt.Frame,
                    VfxSourceDomains.Gameplay,
                    evt.Frame.Value,
                    evt.LocalFrame,
                    evt.ProjectileId,
                    triggerId,
                    evt.SourceEntityId,
                    evt.TargetEntityId,
                    _definitionId,
                    VfxAnchor.World(new VfxPose(ToVfxVector(evt.Origin), ToVfxVector(evt.Direction))),
                    new VfxParameterBlock(
                        evt.LengthMillimeters,
                        evt.Hit ? 1 : 0,
                        evt.ProjectileId,
                        evt.ActionInstanceId),
                    CharacterTestVfxIds.LaserLancePathLifetimeFrames,
                    new RuntimeTraceIdentity(CharacterTestFrameworkTraceKinds.ProjectilePathVfx, evt.SourceEntityId, evt.ActionInstanceId, evt.ProjectileId)));
                LastFrameSpawnedCount++;
            }
        }

        private static VfxVector3 ToVfxVector(FixVector3 value)
        {
            return VfxVector3.FromRaw(ToVfxRaw(value.X), ToVfxRaw(value.Y), ToVfxRaw(value.Z));
        }

        private static long ToVfxRaw(Fix64 value)
        {
            return checked(value.RawValue * VfxVector3.Scale / Fix64.Scale);
        }
    }

    internal sealed class CharacterTestChainLightningVfxBridgeModule : RuntimeModule
    {
        private readonly Func<IReadOnlyList<CharacterTestChainLightningVfxEvent>> _eventsProvider;
        private readonly VfxRuntimeModule _vfx;
        private readonly VfxDefinitionId _definitionId =
            new VfxDefinitionId(CharacterTestVfxIds.ChainLightningArcDefinitionId);

        public CharacterTestChainLightningVfxBridgeModule(
            Func<IReadOnlyList<CharacterTestChainLightningVfxEvent>> eventsProvider,
            VfxRuntimeModule vfx)
            : base("CharacterTest.ChainLightningVfxBridge", RuntimeTickStage.PostSimulation, priority: 146)
        {
            _eventsProvider = eventsProvider ?? throw new ArgumentNullException(nameof(eventsProvider));
            _vfx = vfx ?? throw new ArgumentNullException(nameof(vfx));
        }

        public int LastFrameSpawnedCount { get; private set; }

        /// <remarks>LowFreqAlloc: view-only VFX bridge enqueues presentation requests after simulation; authoritative combat state and runtime hash do not depend on these allocations.</remarks>
        public override void Tick(RuntimeTickContext context)
        {
            LastFrameSpawnedCount = 0;
            IReadOnlyList<CharacterTestChainLightningVfxEvent> events = _eventsProvider();
            if (events == null || events.Count == 0)
                return;

            int triggerId = CharacterTestCharacterActionConfigSource.ChainLightningArcVfxStableEventId;
            for (int i = 0; i < events.Count; i++)
            {
                CharacterTestChainLightningVfxEvent evt = events[i];
                _vfx.EnqueueSpawnRequest(new VfxSpawnRequest(
                    evt.Frame,
                    VfxSourceDomains.Gameplay,
                    evt.Frame.Value,
                    evt.LocalFrame,
                    evt.SegmentIndex,
                    triggerId,
                    evt.SourceEntityId,
                    evt.TargetEntityId,
                    _definitionId,
                    VfxAnchor.World(new VfxPose(ToVfxVector(evt.Origin), ToVfxVector(evt.Direction))),
                    new VfxParameterBlock(
                        evt.LengthMillimeters,
                        evt.Hit ? 1 : 0,
                        evt.SegmentIndex,
                        evt.ActionInstanceId),
                    CharacterTestVfxIds.ChainLightningArcLifetimeFrames,
                    new RuntimeTraceIdentity(CharacterTestFrameworkTraceKinds.ProjectilePathVfx, evt.SourceEntityId, evt.ActionInstanceId, evt.SegmentIndex)));
                LastFrameSpawnedCount++;
            }
        }

        private static VfxVector3 ToVfxVector(FixVector3 value)
        {
            return VfxVector3.FromRaw(ToVfxRaw(value.X), ToVfxRaw(value.Y), ToVfxRaw(value.Z));
        }

        private static long ToVfxRaw(Fix64 value)
        {
            return checked(value.RawValue * VfxVector3.Scale / Fix64.Scale);
        }
    }

    internal sealed class CharacterTestCombatVfxBridgeModule : RuntimeModule
    {
        private readonly Func<IReadOnlyList<HitPresentationEvent>> _eventsProvider;
        private readonly CombatVfxBridge _bridge;

        public CharacterTestCombatVfxBridgeModule(
            Func<IReadOnlyList<HitPresentationEvent>> eventsProvider,
            CombatVfxBridge bridge)
            : base("CharacterTest.CombatVfxBridge", RuntimeTickStage.PostSimulation, priority: 150)
        {
            _eventsProvider = eventsProvider ?? throw new ArgumentNullException(nameof(eventsProvider));
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public override void Start(RuntimeHostContext context)
        {
            _bridge.Install();
        }

        /// <remarks>LowFreqAlloc: presentation ticks may update labels, diagnostics, or backend views; simulation hot paths should use lower-level NoAlloc APIs.</remarks>
        public override void Tick(RuntimeTickContext context)
        {
            IReadOnlyList<HitPresentationEvent> events = _eventsProvider();
            if (events == null || events.Count == 0)
                return;

            var frame = new RuntimeFrame(context.FrameIndex);
            for (int i = 0; i < events.Count; i++)
            {
                HitPresentationEvent evt = events[i];
                if (!ShouldPublish(evt))
                    continue;

                _bridge.PublishHitPresentationEvent(frame, evt);
            }
        }

        public override void Dispose()
        {
            _bridge.Dispose();
        }

        internal static bool ShouldPublish(in HitPresentationEvent evt)
        {
            if ((evt.PresentationProfileId == CharacterTestCharacterActionConfigSource.FireballBurstPresentationProfileId)
                || (evt.PresentationProfileId == CharacterTestCharacterActionConfigSource.MeteorBarragePresentationProfileId))
            {
                // These source overlays are owned by AreaPulseTrigger -> ProjectilePresentation.
                // Target/material/shield reaction systems remain independent of this generic profile map.
                return false;
            }
            bool isStunBoltProfile = (evt.PresentationProfileId == CharacterTestCharacterActionConfigSource.StunBoltPresentationProfileId);
            bool isKnownProjectileProfile = (evt.PresentationProfileId == CharacterTestCharacterActionConfigSource.FireballPresentationProfileId)
                || (evt.PresentationProfileId == CharacterTestCharacterActionConfigSource.FireballBurstPresentationProfileId)
                || (evt.PresentationProfileId == CharacterTestCharacterActionConfigSource.FlameLancePresentationProfileId)
                || (evt.PresentationProfileId == CharacterTestCharacterActionConfigSource.TornadoPresentationProfileId)
                || (evt.PresentationProfileId == CharacterTestCharacterActionConfigSource.TornadoTickPresentationProfileId)
                || (evt.PresentationProfileId == CharacterTestCharacterActionConfigSource.BeamLancePresentationProfileId)
                || (evt.PresentationProfileId == CharacterTestCharacterActionConfigSource.LaserLancePresentationProfileId)
                || (evt.PresentationProfileId == CharacterTestCharacterActionConfigSource.ChannelBeamPresentationProfileId)
                || (evt.PresentationProfileId == CharacterTestCharacterActionConfigSource.ChainLightningPresentationProfileId)
                || (evt.PresentationProfileId == CharacterTestCharacterActionConfigSource.PoisonBottlePresentationProfileId)
                || isStunBoltProfile
                || (evt.PresentationProfileId == CharacterTestCharacterActionConfigSource.MeteorBarragePresentationProfileId)
                || (evt.PresentationProfileId == CharacterTestCharacterActionConfigSource.DashStrikePresentationProfileId)
                || (evt.PresentationProfileId == CharacterTestCharacterActionConfigSource.HookChainPresentationProfileId)
                || (evt.PresentationProfileId == CharacterTestCharacterActionConfigSource.PoisonTickPresentationProfileId);
            return isKnownProjectileProfile
                && (evt.DamageEvent.FinalDamage > 0 || isStunBoltProfile)
                && evt.DamageEvent.HitResult.Kind == HitResolveKind.Damage;
        }
    }

    internal sealed class CharacterTestStatusVfxBridgeModule : RuntimeModule
    {
        public const int AreaConfinementCharacterTraceId = CharacterTestAuthoredContentIds.D401_Id70;
        public const int AreaConfinementCharacterReleaseTraceId = CharacterTestAuthoredContentIds.D401_Id71;
        public const int StunnedStatusTraceId = CharacterTestAuthoredContentIds.D401_Id72;
        public const int ParalyzedStatusTraceId = CharacterTestAuthoredContentIds.D401_Id73;

        private readonly Func<CharacterTestCharacterRuntimeWorld> _worldProvider;
        private readonly VfxRuntimeModule _vfx;
        private readonly Dictionary<int, ActiveStatusVfx> _activeByEntityId =
            new Dictionary<int, ActiveStatusVfx>();
        private readonly List<VfxInstanceSnapshot> _activeInstances =
            new List<VfxInstanceSnapshot>();
        private readonly HashSet<int> _presentEntityIds = new HashSet<int>();
        private readonly List<int> _removeEntityIds = new List<int>();
        private readonly VfxDefinitionId _areaConfinementCharacterReleaseDefinitionId =
            new VfxDefinitionId(CharacterTestVfxIds.AreaConfinementCharacterReleaseDefinitionId);

        public CharacterTestStatusVfxBridgeModule(
            Func<CharacterTestCharacterRuntimeWorld> worldProvider,
            VfxRuntimeModule vfx)
            : base("CharacterTest.StatusVfxBridge", RuntimeTickStage.PostSimulation, priority: 160)
        {
            _worldProvider = worldProvider ?? throw new ArgumentNullException(nameof(worldProvider));
            _vfx = vfx ?? throw new ArgumentNullException(nameof(vfx));
        }

        public int LastFrameSpawnedCount { get; private set; }
        public int LastFrameReleaseSpawnedCount { get; private set; }
        public int LastFrameStoppedCount { get; private set; }
        public int LastFrameResolvedInstanceCount { get; private set; }
        public int ActivePresentationCount => _activeByEntityId.Count;

        /// <remarks>LowFreqAlloc: presentation ticks may update labels, diagnostics, or backend views; simulation hot paths should use lower-level NoAlloc APIs.</remarks>
        public override void Tick(RuntimeTickContext context)
        {
            LastFrameSpawnedCount = 0;
            LastFrameReleaseSpawnedCount = 0;
            LastFrameStoppedCount = 0;
            LastFrameResolvedInstanceCount = 0;
            ResolveSpawnedInstances();

            RuntimeFrame frame = new RuntimeFrame(context.FrameIndex);
            CharacterTestCharacterRuntimeWorld world = _worldProvider();
            if (world == null)
            {
                StopAll(frame);
                return;
            }

            _presentEntityIds.Clear();
            GameplayComponentStore<GameplayStatusComponent> statusStore = null;
            world.GameplayComponentWorld.TryGetStore(out statusStore);

            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == null)
                    continue;

                if (entity.BattleState.LifeState != CharacterTestCharacterLifeState.Alive)
                {
                    StopStatusVfx(entity.Id.Value, frame);
                    continue;
                }

                CharacterControlPresentationState state = ResolveState(statusStore, entity);
                if (TryResolveLoopVfx(
                    state.PrimaryKind,
                    out VfxDefinitionId definitionId,
                    out int lifetimeFrames,
                    out int traceId))
                {
                    _presentEntityIds.Add(entity.Id.Value);
                    EnsureStatusVfx(
                        entity,
                        frame,
                        i,
                        state.PrimaryKind,
                        definitionId,
                        lifetimeFrames,
                        traceId);
                }
                else
                {
                    StopStatusVfx(entity.Id.Value, frame);
                }
            }

            StopRemovedEntities(frame);
        }

        private void EnsureStatusVfx(
            CharacterTestCharacterEntity entity,
            RuntimeFrame frame,
            int entityIndex,
            CharacterControlPresentationKind primaryKind,
            VfxDefinitionId definitionId,
            int lifetimeFrames,
            int traceId)
        {
            VfxPose pose = CreateEntityPose(entity);
            if (_activeByEntityId.TryGetValue(entity.Id.Value, out ActiveStatusVfx existing))
            {
                if (existing.PrimaryKind == primaryKind
                    && existing.DefinitionId == definitionId
                    && existing.TraceId.Sequence == traceId)
                {
                    existing.LastPose = pose;
                    return;
                }

                StopStatusVfx(entity.Id.Value, frame);
            }

            int triggerId = traceId;
            var trace = new RuntimeTraceIdentity(CharacterTestFrameworkTraceKinds.CharacterStatusVfx, entity.Id.Value, frame.Value, traceId);
            int sourceOrder = Math.Max(0, entityIndex) * 1000;
            VfxAnchor anchor = CreateStatusAnchor(primaryKind, entity.Id.Value, pose);
            _vfx.EnqueueSpawnRequest(new VfxSpawnRequest(
                frame,
                VfxSourceDomains.Gameplay,
                frame.Value,
                localFrame: 0,
                sourceOrder,
                triggerId,
                entity.Id.Value,
                entity.Id.Value,
                definitionId,
                anchor,
                new VfxParameterBlock(triggerId),
                lifetimeFrames,
                trace));

            _activeByEntityId.Add(
                entity.Id.Value,
                new ActiveStatusVfx(
                    entity.Id.Value,
                    frame.Value,
                    primaryKind,
                    definitionId,
                    trace,
                    pose));
            LastFrameSpawnedCount++;
        }

        private void StopStatusVfx(int entityId, RuntimeFrame frame)
        {
            if (!_activeByEntityId.TryGetValue(entityId, out ActiveStatusVfx active))
                return;

            if (active.InstanceId.IsValid)
            {
                _vfx.EnqueueStopRequest(new VfxStopRequest(
                    frame,
                    active.InstanceId,
                    VfxStopMode.Complete,
                    VfxStopReasonCode.SourceRemoved,
                    (int)active.PrimaryKind,
                    active.TraceId));
                LastFrameStoppedCount++;
            }

            if (active.PrimaryKind == CharacterControlPresentationKind.Rooted)
                EnqueueReleaseVfx(active, frame);
            _activeByEntityId.Remove(entityId);
        }

        private void EnqueueReleaseVfx(ActiveStatusVfx active, RuntimeFrame frame)
        {
            int triggerId = AreaConfinementCharacterReleaseTraceId;
            _vfx.EnqueueSpawnRequest(new VfxSpawnRequest(
                frame,
                VfxSourceDomains.Gameplay,
                frame.Value,
                localFrame: 0,
                sourceOrder: 0,
                triggerId,
                active.EntityId,
                active.EntityId,
                _areaConfinementCharacterReleaseDefinitionId,
                VfxAnchor.Target(active.EntityId, active.EntityId, active.LastPose),
                new VfxParameterBlock(triggerId),
                CharacterTestVfxIds.AreaConfinementCharacterReleaseLifetimeFrames,
                new RuntimeTraceIdentity(CharacterTestFrameworkTraceKinds.CharacterStatusReleaseVfx, active.EntityId, frame.Value, AreaConfinementCharacterReleaseTraceId)));
            LastFrameReleaseSpawnedCount++;
        }

        private void StopAll(RuntimeFrame frame)
        {
            _removeEntityIds.Clear();
            foreach (int entityId in _activeByEntityId.Keys)
                _removeEntityIds.Add(entityId);

            for (int i = 0; i < _removeEntityIds.Count; i++)
                StopStatusVfx(_removeEntityIds[i], frame);

            _removeEntityIds.Clear();
        }

        private void StopRemovedEntities(RuntimeFrame frame)
        {
            _removeEntityIds.Clear();
            foreach (int entityId in _activeByEntityId.Keys)
            {
                if (!_presentEntityIds.Contains(entityId))
                    _removeEntityIds.Add(entityId);
            }

            for (int i = 0; i < _removeEntityIds.Count; i++)
                StopStatusVfx(_removeEntityIds[i], frame);

            _removeEntityIds.Clear();
            _presentEntityIds.Clear();
        }

        private void ResolveSpawnedInstances()
        {
            _activeInstances.Clear();
            _vfx.CopyActiveInstances(_activeInstances);
            for (int i = 0; i < _activeInstances.Count; i++)
            {
                VfxInstanceSnapshot instance = _activeInstances[i];
                if (instance == null)
                    continue;

                int entityId = instance.TargetId > 0 ? instance.TargetId : instance.SourceId;
                if (!_activeByEntityId.TryGetValue(entityId, out ActiveStatusVfx active)
                    || active.InstanceId.IsValid
                    || instance.StartFrame.Value < active.RequestedFrame
                    || instance.DefinitionId != active.DefinitionId
                    || instance.SourceDomain != VfxSourceDomains.Gameplay
                    || instance.TraceId != active.TraceId)
                {
                    continue;
                }

                active.InstanceId = instance.InstanceId;
                LastFrameResolvedInstanceCount++;
            }

            _activeInstances.Clear();
        }

        private static CharacterControlPresentationState ResolveState(
            GameplayComponentStore<GameplayStatusComponent> statusStore,
            CharacterTestCharacterEntity entity)
        {
            if (statusStore != null
                && entity != null
                && statusStore.TryGet(entity.GameplayEntityId, out GameplayStatusComponent statuses))
            {
                return CharacterTestControlPresentationResolver.Resolve(statuses);
            }

            return CharacterControlPresentationState.None;
        }

        private static bool TryResolveLoopVfx(
            CharacterControlPresentationKind primaryKind,
            out VfxDefinitionId definitionId,
            out int lifetimeFrames,
            out int traceId)
        {
            switch (primaryKind)
            {
                case CharacterControlPresentationKind.Rooted:
                    definitionId = new VfxDefinitionId(CharacterTestVfxIds.AreaConfinementCharacterDefinitionId);
                    lifetimeFrames = CharacterTestVfxIds.AreaConfinementCharacterLifetimeFrames;
                    traceId = AreaConfinementCharacterTraceId;
                    return true;
                case CharacterControlPresentationKind.Stunned:
                    definitionId = new VfxDefinitionId(CharacterTestVfxIds.StunnedStatusDefinitionId);
                    lifetimeFrames = CharacterTestVfxIds.StunnedStatusLifetimeFrames;
                    traceId = StunnedStatusTraceId;
                    return true;
                case CharacterControlPresentationKind.Paralyzed:
                    definitionId = new VfxDefinitionId(CharacterTestVfxIds.ParalyzedStatusDefinitionId);
                    lifetimeFrames = CharacterTestVfxIds.ParalyzedStatusLifetimeFrames;
                    traceId = ParalyzedStatusTraceId;
                    return true;
                default:
                    definitionId = default;
                    lifetimeFrames = 0;
                    traceId = 0;
                    return false;
            }
        }

        private static VfxPose CreateEntityPose(CharacterTestCharacterEntity entity)
        {
            CharacterTestCharacterTransformComponent transform = entity.Transform;
            double yawRadians = transform.YawDegrees * Math.PI / 180.0;
            float forwardX = (float)Math.Sin(yawRadians);
            float forwardZ = (float)Math.Cos(yawRadians);
            if (forwardX * forwardX + forwardZ * forwardZ <= 0.000001f)
            {
                forwardX = 0f;
                forwardZ = 1f;
            }

            return new VfxPose(
                VfxVector3.FromRaw(ToRaw(transform.X), ToRaw(transform.Y), ToRaw(transform.Z)),
                VfxVector3.FromRaw(ToRaw(forwardX), 0L, ToRaw(forwardZ)));
        }

        private static VfxAnchor CreateStatusAnchor(
            CharacterControlPresentationKind primaryKind,
            int entityId,
            VfxPose pose)
        {
            if (primaryKind == CharacterControlPresentationKind.Stunned
                || primaryKind == CharacterControlPresentationKind.Paralyzed)
            {
                return new VfxAnchor(
                    VfxAnchorKind.TargetEntity,
                    entityId,
                    entityId,
                    CharacterTestVfxAnchorResolver.ControlStatusHeadSocketId,
                    pose);
            }

            return VfxAnchor.Target(entityId, entityId, pose);
        }

        private static long ToRaw(float value)
        {
            return (long)Math.Round(value * VfxVector3.Scale);
        }

        private sealed class ActiveStatusVfx
        {
            public ActiveStatusVfx(
                int entityId,
                long requestedFrame,
                CharacterControlPresentationKind primaryKind,
                VfxDefinitionId definitionId,
                RuntimeTraceIdentity traceId,
                VfxPose lastPose)
            {
                EntityId = entityId;
                RequestedFrame = requestedFrame;
                PrimaryKind = primaryKind;
                DefinitionId = definitionId;
                TraceId = traceId;
                LastPose = lastPose;
            }

            public int EntityId { get; }
            public long RequestedFrame { get; }
            public CharacterControlPresentationKind PrimaryKind { get; }
            public VfxDefinitionId DefinitionId { get; }
            public RuntimeTraceIdentity TraceId { get; }
            public VfxInstanceId InstanceId { get; set; }
            public VfxPose LastPose { get; set; }
        }
    }

    internal sealed class CharacterTestVfxPresentationModule : RuntimeModule
    {
        private readonly VfxRuntimeEventFanout _fanout;

        public CharacterTestVfxPresentationModule(VfxRuntimeEventFanout fanout)
            : base("CharacterTest.VfxPresentation", RuntimeTickStage.PostSimulation, priority: 220)
        {
            _fanout = fanout ?? throw new ArgumentNullException(nameof(fanout));
        }

        /// <remarks>LowFreqAlloc: presentation ticks may update labels, diagnostics, or backend views; simulation hot paths should use lower-level NoAlloc APIs.</remarks>
        public override void Tick(RuntimeTickContext context)
        {
            _fanout.DrainFrame(new RuntimeFrame(context.FrameIndex));
        }
    }
}
