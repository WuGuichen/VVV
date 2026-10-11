using System;
using System.Collections.Generic;
using MxFramework.Game.CharacterTest;
using MxFramework.Input;
using MxFramework.Network;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    internal sealed class CharacterTestSliceTestHarness : IDisposable
    {
        public CharacterTestSliceTestHarness(CharacterTestSliceOptions options = null)
        {
            Slice = new GameSlice(options ?? new CharacterTestSliceOptions
            {
                Logger = NullRuntimeLogger.Instance,
                CreateCharacterViews = false
            });
            Services = Slice.Services;
        }

        public GameSlice Slice { get; }
        public CharacterTestSliceServices Services { get; }
        public CharacterTestSliceModules Modules => Services.Modules;
        public CharacterTestSliceCommandServices Commands => Services.Commands;
        public CharacterTestSlicePresentationServices Presentation => Services.Presentation;
        public CharacterTestSliceStoryFlowServices StoryFlow => Services.StoryFlow;
        public CharacterTestSliceNetworkServices Network => Services.Network;
        public RuntimeFrame CurrentFrame => Slice.CurrentFrame;

        public IReadOnlyList<CharacterTestCharacterEntity> CharacterEntities => Modules.CharacterEntities;
        public CharacterTestCharacterEntity LocalPlayer => FindLocalPlayer();

        public RuntimeCommandValidationResult Enqueue(RuntimeCommand command)
        {
            return Commands.EnqueueRuntimeCommand(command);
        }

        public CharacterTestCharacterEntity SpawnLocalPlayer(
            CharacterTestCharacterSpawnSpec? spec = null,
            RuntimeTraceIdentity traceId = default)
        {
            CharacterTestCharacterSpawnSpec resolved = spec ?? CharacterTestCharacterSpawnSpec.TemporaryPlayer;
            RuntimeCommandValidationResult spawn = Enqueue(CharacterTestRuntimeCommandFactory.SpawnCharacter(
                CurrentFrame,
                CharacterTestRuntimeCommandSources.System,
                resolved,
                traceId));
            Assert.IsTrue(spawn.Success, spawn.Error.Message);
            Tick();
            CharacterTestCharacterEntity entity = FindLocalPlayer();
            Assert.NotNull(entity);
            return entity;
        }

        public CharacterTestCharacterEntity FindLocalPlayer()
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = CharacterEntities;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer)
                    return entity;
            }

            return null;
        }

        public CharacterTestCharacterEntity FindEntityById(int entityId)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = CharacterEntities;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity.Id.Value == entityId)
                    return entity;
            }

            return null;
        }

        public void SetLocalInputProvider(IInputProvider provider)
        {
            Commands.SetLocalInputProvider(provider);
        }

        public CharacterTestNetworkSnapshotParticipant CreateSnapshotParticipant()
        {
            return Network.CreateSnapshotParticipant();
        }

        public CharacterTestNetworkRollbackParticipant CreateRollbackParticipant(
            CharacterTestNetworkSnapshotParticipant snapshotParticipant)
        {
            return Network.CreateRollbackParticipant(snapshotParticipant);
        }

        public void Tick(double deltaTime = 0d)
        {
            Slice.Tick(deltaTime);
        }

        public void Tick(int count, double deltaTime = 0d)
        {
            for (int i = 0; i < count; i++)
                Tick(deltaTime);
        }

        public void Dispose()
        {
            Slice.Dispose();
        }
    }
}
