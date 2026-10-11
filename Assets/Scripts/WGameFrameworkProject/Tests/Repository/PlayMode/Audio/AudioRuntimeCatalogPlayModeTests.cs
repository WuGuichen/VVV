using System.Collections;
using System.IO;
using MxFramework.Audio;
using MxFramework.Audio.Config;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MxFramework.Tests.PlayMode.Audio
{
    public sealed class AudioRuntimeCatalogPlayModeTests
    {
        [UnityTest]
        public IEnumerator PlayerMirror_LoadsGameplayCuesWithDedicatedEvents()
        {
            yield return null;
            string path = Path.Combine(Application.streamingAssetsPath, "MxFramework", "Audio", "audio_runtime_catalog.json");
            Assert.IsTrue(File.Exists(path), "Player audio runtime catalog mirror is missing.");
            Assert.IsTrue(AudioRuntimeCatalogJson.TryLoad(File.ReadAllText(path), out AudioDefinitionCatalog catalog, out string error), error);
            Assert.IsTrue(catalog.TryGetCue("sfx.light_attack", out AudioCueDefinition lightAttack));
            Assert.IsTrue(catalog.TryGetCue("sfx.fireball_explosion", out AudioCueDefinition fireball));
            Assert.AreEqual(500312, lightAttack.EventId);
            Assert.AreEqual(500313, fireball.EventId);
            Assert.AreNotEqual(lightAttack.EventId, fireball.EventId, "Gameplay Cues with independently authored audio must keep dedicated Event Definitions.");
            Assert.IsTrue(catalog.TryGetEvent(lightAttack.EventId, out AudioEventDefinition lightAttackEvent));
            Assert.IsTrue(catalog.TryGetEvent(fireball.EventId, out AudioEventDefinition fireballEvent));
            Assert.AreEqual("event:/MxFramework/CharacterTest/PhysicalAudio/LightAttackHit", lightAttackEvent.FmodEventPath);
            Assert.AreEqual("event:/MxFramework/CharacterTest/PhysicalAudio/FireballExplosion", fireballEvent.FmodEventPath);
        }
    }
}
