using System;
using System.Linq;
using System.Reflection;
using MxFramework.Editor.CharacterImport;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Rendering
{
    public sealed class WGameBakedSurfaceTests
    {
        [Test]
        public void SurfaceMip_SelectsExistingProfileAndAveragesContinuousData()
        {
            MethodInfo method = typeof(CharacterImportedPackagePrefabBuilder).GetMethod("BuildBakedSurfaceMip", BindingFlags.NonPublic | BindingFlags.Static);
            var source = new[] { new Color32(0, 40, 80, 8), new Color32(40, 80, 120, 248), new Color32(80, 120, 160, 248), new Color32(120, 160, 200, 248) };
            var mip = (Color32[])method.Invoke(null, new object[] { source, 2, 2 });
            Assert.That(mip[0].a, Is.EqualTo(248));
            Assert.That(mip[0].r, Is.EqualTo(60));
            Assert.That(mip[0].g, Is.EqualTo(100));
            Assert.That(mip[0].b, Is.EqualTo(140));
        }

        [Test]
        public void SurfaceMip_TieCannotTurnSkinAndMetalIntoAnotherMaterial()
        {
            MethodInfo method = typeof(CharacterImportedPackagePrefabBuilder).GetMethod("BuildBakedSurfaceMip", BindingFlags.NonPublic | BindingFlags.Static);
            var source = new[] { new Color32(255,255,0,8), new Color32(255,255,0,216), new Color32(255,255,0,8), new Color32(255,255,0,216) };
            var mip = (Color32[])method.Invoke(null, new object[] { source, 2, 2 });
            Assert.That(new byte[] { 8,216 }, Does.Contain(mip[0].a));
        }

        [Test]
        public void BakedMaterial_IsExcludedFromRuntimePaletteBinding()
        {
            var shader = Shader.Find("WGame/CharacterStylizedLit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                MethodInfo method = typeof(MxFramework.CharacterRuntimeSpawn.Unity.CharacterPaletteBinding).GetMethod("IsPaletteCompatible", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That((bool)method.Invoke(null,new object[]{material}),Is.False);
                Assert.That(material.HasProperty("_WgPaletteEnabled"), Is.False);
                Assert.That(material.HasProperty("_WgBakedSurfaceEnabled"), Is.False);
                material.shader = Shader.Find("WGame/CharacterPaletteLegacy");
                Assert.That((bool)method.Invoke(null,new object[]{material}),Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        }
    }
}
