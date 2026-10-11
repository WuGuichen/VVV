using MxFramework.Game.CharacterTest.TacticalCard;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class TacticalPreparationCameraTests
    {
        [Test]
        public void NearbyDeploymentIsCenteredWithoutPullingInDistantUnits()
        {
            var setup = new TacticalBattleSetup { PlayerControlledEntityId = "player" };
            setup.Entities.Add(new TacticalBattleEntitySetup { EntityId = "player", X = -2, Y = 0, Z = -1 });
            setup.Entities.Add(new TacticalBattleEntitySetup { EntityId = "opponent", X = 2, Y = 0, Z = 3 });
            setup.Entities.Add(new TacticalBattleEntitySetup { EntityId = "distant", X = 60, Y = 0, Z = 40 });
            Assert.AreEqual(new Vector3(0, 1, 1), TacticalBattlePreparationController.ResolvePreparationFocus(setup));
        }

        [TestCase(45f)] [TestCase(60f)]
        public void BodyAndFeetAreSelectableButDistantAndBehindCameraPointsAreNot(float fov)
        {
            var owner = new GameObject("Preparation picking test");
            try
            {
                var camera = owner.AddComponent<UnityEngine.Camera>(); camera.fieldOfView = fov;
                camera.transform.position = new Vector3(0, 6, -10); camera.transform.LookAt(Vector3.up);
                Vector3 bottom = Vector3.zero, top = Vector3.up * 1.86f;
                foreach (var world in new[] { bottom, Vector3.up, top })
                {
                    Vector3 screen = camera.WorldToScreenPoint(world);
                    Assert.Less(TacticalBattlePreparationController.ScreenBodyPickDistance(camera, bottom, top, .35f,
                        new Vector2(screen.x, screen.y)), 1f);
                }
                Vector3 outside = camera.WorldToScreenPoint(Vector3.right * 4);
                Assert.Greater(TacticalBattlePreparationController.ScreenBodyPickDistance(camera, bottom, top, .35f,
                    new Vector2(outside.x, outside.y)), 1f);
                Vector3 behind = camera.transform.position - camera.transform.forward * 2;
                Assert.IsTrue(float.IsPositiveInfinity(TacticalBattlePreparationController.ScreenBodyPickDistance(
                    camera, behind, behind + Vector3.up, .35f, Vector2.zero)));
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void EmptyDeploymentAndMissingPlayerHaveStableFocus()
        {
            Assert.AreEqual(Vector3.up, TacticalBattlePreparationController.ResolvePreparationFocus(null));
            var setup = new TacticalBattleSetup();
            Assert.AreEqual(Vector3.up, TacticalBattlePreparationController.ResolvePreparationFocus(setup));
            setup.Entities.Add(new TacticalBattleEntitySetup { EntityId = "unit", X = 20, Y = 1, Z = -8 });
            Assert.AreEqual(new Vector3(20, 2, -8), TacticalBattlePreparationController.ResolvePreparationFocus(setup));
        }
    }
}

