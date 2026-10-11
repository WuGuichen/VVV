using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestArcMineVfxMode
    {
        Flight = 0,
        Deploy = 1,
        Fuse = 2,
        Trigger = 3,
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Game/Character Test/Arc Mine VFX View")]
    public sealed class CharacterTestArcMineVfxView : MonoBehaviour
    {
        [SerializeField] private CharacterTestArcMineVfxMode _mode;

        public CharacterTestArcMineVfxMode Mode => _mode;

        public static GameObject CreateRuntimePrefab(CharacterTestArcMineVfxMode mode)
        {
            var prefab = new GameObject("CharacterTest_ArcMine_" + mode + "_Vfx");
            prefab.hideFlags = HideFlags.HideAndDontSave;
            CharacterTestArcMineVfxView view = prefab.AddComponent<CharacterTestArcMineVfxView>();
            view._mode = mode;
            view.RebuildVisuals();
            prefab.SetActive(false);
            return prefab;
        }

        public void RebuildVisuals()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyUnityObject(transform.GetChild(i).gameObject);

            Material core = CreateRuntimeMaterial(new Color(0.72f, 0.95f, 1f, 0.88f));
            Material arc = CreateRuntimeMaterial(new Color(0.42f, 0.55f, 1f, 0.72f));
            Material rune = CreateRuntimeMaterial(new Color(0.82f, 0.42f, 1f, 0.62f));

            switch (_mode)
            {
                case CharacterTestArcMineVfxMode.Flight:
                    CreateParticles("RuneCore", core, ParticleSystemShapeType.Sphere, 18, 0.28f, 0.1f, 0.1f, true, 18f, Vector3.zero);
                    CreateParticles("ArcTrail", arc, ParticleSystemShapeType.Cone, 34, 0.5f, 0.055f, 0.18f, true, 40f, new Vector3(0f, 0.02f, -0.42f), ParticleSystemRenderMode.Stretch, 1.8f);
                    CreateParticles("RuneMotes", rune, ParticleSystemShapeType.Sphere, 16, 0.36f, 0.035f, 0.26f, true, 16f, Vector3.zero);
                    break;
                case CharacterTestArcMineVfxMode.Deploy:
                    CreateParticles("GroundRune", rune, ParticleSystemShapeType.Circle, 64, 0.58f, 0.12f, 1.15f, false, 0f, Vector3.zero);
                    CreateParticles("DeployShockRing", arc, ParticleSystemShapeType.Circle, 42, 0.4f, 0.075f, 1.45f, false, 0f, Vector3.zero, ParticleSystemRenderMode.Stretch, 1.25f);
                    CreateParticles("ArmingSparks", core, ParticleSystemShapeType.Sphere, 38, 0.5f, 0.045f, 0.5f, false, 0f, new Vector3(0f, 0.12f, 0f), ParticleSystemRenderMode.Stretch, 1.5f);
                    break;
                case CharacterTestArcMineVfxMode.Fuse:
                    CreateParticles("FuseRunePulse", rune, ParticleSystemShapeType.Circle, 44, 0.9f, 0.1f, 1.2f, true, 8f, Vector3.zero);
                    CreateParticles("CountdownArcs", arc, ParticleSystemShapeType.Circle, 34, 0.7f, 0.055f, 1.0f, true, 14f, Vector3.zero, ParticleSystemRenderMode.Stretch, 1.35f);
                    CreateParticles("WarningMotes", core, ParticleSystemShapeType.Sphere, 26, 0.55f, 0.04f, 0.46f, true, 18f, new Vector3(0f, 0.18f, 0f));
                    break;
                default:
                    CreateParticles("RuneFlash", core, ParticleSystemShapeType.Sphere, 72, 0.32f, 0.18f, 0.35f, false, 0f, Vector3.zero);
                    CreateParticles("ElectricBurstRing", arc, ParticleSystemShapeType.Circle, 88, 0.48f, 0.09f, 2.25f, false, 0f, Vector3.zero, ParticleSystemRenderMode.Stretch, 1.65f);
                    CreateParticles("AfterglowMotes", rune, ParticleSystemShapeType.Sphere, 54, 0.72f, 0.05f, 1.25f, false, 0f, new Vector3(0f, 0.22f, 0f));
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
                shape.angle = 12f;
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
                    new GradientColorKey(Color.white, 0.36f),
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
            var material = CharacterTestUnlitMaterials.CreateVfx("CharacterTest_ArcMine_RuntimeVfx", color, additive: true);
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
