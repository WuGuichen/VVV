using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace MxFramework.Tests
{
    public sealed class CharacterTestUnlitMaterialTests
    {
        [Test]
        public void GeometryMarkerRendersWithoutUvOrVertexColors()
        {
            Material material = CharacterTestUnlitMaterials.CreateMarker("Marker test", Color.white);
            Mesh mesh = Quad();
            try
            {
                var tint = new Color(.2f, .4f, .8f, 1f);
                material.SetColor("_WgMarkerColor", tint);
                Color expected = QualitySettings.activeColorSpace == ColorSpace.Linear ? tint.linear : tint;
                Color pixel = Render(material, mesh);
                Assert.That(material.shader.name, Is.EqualTo(CharacterTestUnlitMaterials.MarkerShaderName));
                Assert.That(pixel.r, Is.EqualTo(expected.r).Within(.002f));
                Assert.That(pixel.g, Is.EqualTo(expected.g).Within(.002f));
                Assert.That(pixel.b, Is.EqualTo(expected.b).Within(.002f));
                Assert.That(pixel.a, Is.EqualTo(1f).Within(.002f));
            }
            finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(material); }
        }

        [Test]
        public void TexturedVfxPreservesVertexColorLifetimeAlphaAndHdr()
        {
            Material material = CharacterTestUnlitMaterials.CreateVfx("Particle test", Color.white);
            Mesh mesh = Quad();
            try
            {
                mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                var color = new Color(.25f, .5f, .75f, .5f);
                mesh.colors = new[] { color, color, color, color };
                material.SetVector("_BaseColor", new Vector4(2f, 2f, 2f, .8f));
                material.SetTexture("_BaseMap", Texture2D.whiteTexture);
                material.SetInt("_SrcBlend", (int)BlendMode.One);
                material.SetInt("_DstBlend", (int)BlendMode.Zero);
                material.SetInt("_SrcBlendAlpha", (int)BlendMode.One);
                material.SetInt("_DstBlendAlpha", (int)BlendMode.Zero);
                Color pixel = Render(material, mesh);
                Assert.That(pixel.r, Is.EqualTo(.5f).Within(.002f));
                Assert.That(pixel.g, Is.EqualTo(1f).Within(.002f));
                Assert.That(pixel.b, Is.EqualTo(1.5f).Within(.002f));
                Assert.That(pixel.a, Is.EqualTo(.4f).Within(.002f));
            }
            finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(material); }
        }

        [Test]
        public void ShaderAdoptionPreservesAuthoredTextureTintAndRenderState()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            try
            {
                material.SetVector("_BaseColor", new Vector4(2f, .5f, .2f, .7f));
                material.SetTexture("_BaseMap", Texture2D.whiteTexture);
                material.SetTextureScale("_BaseMap", new Vector2(2f, .5f));
                material.SetTextureOffset("_BaseMap", new Vector2(.1f, .2f));
                material.SetInt("_SrcBlend", (int)BlendMode.One);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 1);
                material.SetInt("_Cull", (int)CullMode.Back);
                material.renderQueue = 3105;
                CharacterTestUnlitMaterials.AdoptVfxShader(material);
                Assert.That(material.shader.name, Is.EqualTo(CharacterTestUnlitMaterials.VfxShaderName));
                Assert.That(material.IsKeywordEnabled("_WG_VFX_TEXTURED"), Is.True);
                Assert.That(material.GetVector("_BaseColor"), Is.EqualTo(new Vector4(2f, .5f, .2f, .7f)));
                Assert.That(material.GetTexture("_BaseMap"), Is.SameAs(Texture2D.whiteTexture));
                Assert.That(material.GetTextureScale("_BaseMap"), Is.EqualTo(new Vector2(2f, .5f)));
                Assert.That(material.GetTextureOffset("_BaseMap"), Is.EqualTo(new Vector2(.1f, .2f)));
                Assert.That(material.GetInt("_SrcBlend"), Is.EqualTo((int)BlendMode.One));
                Assert.That(material.GetInt("_DstBlend"), Is.EqualTo((int)BlendMode.OneMinusSrcAlpha));
                Assert.That(material.GetInt("_ZWrite"), Is.EqualTo(1));
                Assert.That(material.GetInt("_Cull"), Is.EqualTo((int)CullMode.Back));
                Assert.That(material.renderQueue, Is.EqualTo(3105));
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void MigratedParticleMatchesOriginalRgbAndAlpha()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            Mesh mesh = Quad();
            try
            {
                mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                var color = new Color(.4f, .7f, 1f, .25f);
                mesh.colors = new[] { color, color, color, color };
                material.SetColor("_BaseColor", new Color(2.5f, .7f, .2f, .6f));
                material.SetTexture("_BaseMap", Texture2D.whiteTexture);
                material.SetFloat("_Surface", 1f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetInt("_SrcBlend", (int)BlendMode.One);
                material.SetInt("_DstBlend", (int)BlendMode.Zero);
                material.SetInt("_SrcBlendAlpha", (int)BlendMode.One);
                material.SetInt("_DstBlendAlpha", (int)BlendMode.Zero);
                Color original = Render(material, mesh);
                CharacterTestUnlitMaterials.AdoptVfxShader(material);
                Color migrated = Render(material, mesh);
                Assert.That(migrated.r, Is.EqualTo(original.r).Within(.01f));
                Assert.That(migrated.g, Is.EqualTo(original.g).Within(.01f));
                Assert.That(migrated.b, Is.EqualTo(original.b).Within(.01f));
                Assert.That(migrated.a, Is.EqualTo(original.a).Within(.01f));
            }
            finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(material); }
        }

        private static Mesh Quad()
        {
            var mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0),
                new Vector3(1, 1, 0), new Vector3(-1, 1, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            return mesh;
        }

        private static Color Render(Material material, Mesh mesh)
        {
            Assume.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            var target = new RenderTexture(8, 8, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(8, 8, TextureFormat.RGBAFloat, false, true);
            RenderTexture previous = RenderTexture.active;
            var command = new CommandBuffer();
            try
            {
                target.Create();
                command.SetRenderTarget(target);
                command.ClearRenderTarget(true, true, Color.clear);
                command.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                command.DrawMesh(mesh, Matrix4x4.identity, material, 0, 0);
                Graphics.ExecuteCommandBuffer(command);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 8, 8), 0, 0);
                readback.Apply();
                return readback.GetPixel(4, 4);
            }
            finally
            {
                RenderTexture.active = previous;
                command.Dispose(); target.Release();
                Object.DestroyImmediate(readback); Object.DestroyImmediate(target);
            }
        }
    }
}
