using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestRuneTurretVfxMode
    {
        Deploy = 0,
        Idle = 1,
        Fire = 2,
        Flight = 3,
        Impact = 4,
        Expire = 5,
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Game/Character Test/Rune Turret VFX View")]
    public sealed class CharacterTestRuneTurretVfxView : MonoBehaviour
    {
        [SerializeField] private CharacterTestRuneTurretVfxMode _mode;

        public CharacterTestRuneTurretVfxMode Mode => _mode;

        public static GameObject CreateRuntimePrefab(CharacterTestRuneTurretVfxMode mode)
        {
            var prefab = new GameObject("CharacterTest_RuneTurret_" + mode + "_Vfx");
            prefab.hideFlags = HideFlags.HideAndDontSave;
            CharacterTestRuneTurretVfxView view = prefab.AddComponent<CharacterTestRuneTurretVfxView>();
            view._mode = mode;
            view.RebuildVisuals();
            prefab.SetActive(false);
            return prefab;
        }

        public void RebuildVisuals()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyUnityObject(transform.GetChild(i).gameObject);

            Material core = CreateRuntimeMaterial(new Color(0.42f, 1f, 0.78f, 0.86f));
            Material beam = CreateRuntimeMaterial(new Color(0.25f, 0.72f, 1f, 0.76f));
            Material rune = CreateRuntimeMaterial(new Color(0.92f, 0.78f, 0.34f, 0.64f));

            switch (_mode)
            {
                case CharacterTestRuneTurretVfxMode.Deploy:
                    CreateParticles("DeployRune", rune, ParticleSystemShapeType.Circle, 72, 0.55f, 0.11f, 1.0f, false, 0f, Vector3.zero);
                    CreateParticles("ColumnWake", beam, ParticleSystemShapeType.Cone, 46, 0.65f, 0.06f, 0.36f, false, 0f, new Vector3(0f, 0.44f, 0f), ParticleSystemRenderMode.Stretch, 1.35f);
                    CreateParticles("CoreIgnition", core, ParticleSystemShapeType.Sphere, 32, 0.42f, 0.07f, 0.18f, false, 0f, new Vector3(0f, 0.28f, 0f));
                    break;
                case CharacterTestRuneTurretVfxMode.Idle:
                    CreateParticles("IdleHalo", rune, ParticleSystemShapeType.Circle, 36, 1.1f, 0.06f, 0.72f, true, 9f, Vector3.zero);
                    CreateParticles("AimingCore", core, ParticleSystemShapeType.Sphere, 16, 0.7f, 0.055f, 0.16f, true, 12f, new Vector3(0f, 0.8f, 0f));
                    CreateParticles("RuneSparks", beam, ParticleSystemShapeType.Sphere, 22, 0.58f, 0.035f, 0.54f, true, 14f, new Vector3(0f, 0.14f, 0f));
                    break;
                case CharacterTestRuneTurretVfxMode.Fire:
                    CreateParticles("MuzzleFlash", core, ParticleSystemShapeType.Cone, 44, 0.24f, 0.085f, 0.18f, false, 0f, new Vector3(0f, 0.02f, 0.38f), ParticleSystemRenderMode.Stretch, 1.55f);
                    CreateParticles("FireRunes", rune, ParticleSystemShapeType.Circle, 30, 0.34f, 0.05f, 0.52f, false, 0f, new Vector3(0f, 0.8f, 0f));
                    break;
                case CharacterTestRuneTurretVfxMode.Flight:
                    CreateParticles("ShotCore", core, ParticleSystemShapeType.Sphere, 16, 0.22f, 0.08f, 0.08f, true, 18f, Vector3.zero);
                    CreateParticles("ShotTrail", beam, ParticleSystemShapeType.Cone, 34, 0.36f, 0.045f, 0.14f, true, 38f, new Vector3(0f, 0f, -0.5f), ParticleSystemRenderMode.Stretch, 1.9f);
                    break;
                case CharacterTestRuneTurretVfxMode.Impact:
                    CreateParticles("ImpactFlash", core, ParticleSystemShapeType.Sphere, 48, 0.28f, 0.13f, 0.22f, false, 0f, Vector3.zero);
                    CreateParticles("ImpactRing", beam, ParticleSystemShapeType.Circle, 58, 0.42f, 0.075f, 0.82f, false, 0f, Vector3.zero, ParticleSystemRenderMode.Stretch, 1.35f);
                    break;
                default:
                    CreateParticles("ExpireRune", rune, ParticleSystemShapeType.Circle, 42, 0.5f, 0.08f, 0.72f, false, 0f, Vector3.zero);
                    CreateParticles("DissolveMotes", beam, ParticleSystemShapeType.Sphere, 44, 0.72f, 0.04f, 0.62f, false, 0f, new Vector3(0f, 0.24f, 0f));
                    break;
            }
        }

        private ParticleSystem CreateParticles(
            string childName,
            Material material,
            ParticleSystemShapeType shapeType,
            int burstCount,
            float lifetime,
            float size,
            float radius,
            bool loop,
            float rateOverTime,
            Vector3 velocity,
            ParticleSystemRenderMode renderMode = ParticleSystemRenderMode.Billboard,
            float lengthScale = 1f)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            ParticleSystem system = child.AddComponent<ParticleSystem>();

            var main = system.main;
            main.duration = Mathf.Max(0.1f, lifetime);
            main.loop = loop;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = size;
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = system.emission;
            emission.rateOverTime = Mathf.Max(0f, rateOverTime);
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, burstCount)) });

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = shapeType;
            shape.radius = radius;
            if (shapeType == ParticleSystemShapeType.Cone)
            {
                shape.angle = 10f;
                shape.length = 0.9f;
            }
            else if (shapeType == ParticleSystemShapeType.Circle)
            {
                shape.arc = 360f;
            }

            var velocityOverLifetime = system.velocityOverLifetime;
            velocityOverLifetime.enabled = velocity.sqrMagnitude > 0.0001f;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
            velocityOverLifetime.x = velocity.x;
            velocityOverLifetime.y = velocity.y;
            velocityOverLifetime.z = velocity.z;

            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(CreateGradient(material.color));

            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = renderMode;
            renderer.lengthScale = lengthScale;
            renderer.sortingFudge = 0.2f;
            renderer.minParticleSize = 0.01f;
            renderer.maxParticleSize = 1f;
            system.Play();
            return system;
        }

        private static Gradient CreateGradient(Color color)
        {
            return new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(Color.white, 0.32f),
                    new GradientColorKey(color, 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(color.a, 0.08f),
                    new GradientAlphaKey(color.a * 0.65f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            };
        }

        private static Material CreateRuntimeMaterial(Color color)
        {
            var material = CharacterTestUnlitMaterials.CreateVfx("CharacterTest_RuneTurret_RuntimeVfx", color, additive: true);
            material.hideFlags = HideFlags.HideAndDontSave;
            return material;
        }

        private static void DestroyUnityObject(Object value)
        {
            if (value == null)
                return;

            if (Application.isPlaying)
                Destroy(value);
            else
                DestroyImmediate(value);
        }
    }
}
