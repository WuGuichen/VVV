using System;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public interface ICharacterTestNetworkRunner : IDisposable
    {
        GameSlice Slice { get; }

        void Tick(double deltaTime);

        CharacterTestNetworkLoopbackDiagnostics CaptureDiagnostics();
    }

    public interface ICharacterTestNetworkReplayRunner
    {
        bool IsReplayRecording { get; }

        CharacterTestReplayPlaybackStatus BeginReplayRecording();

        CharacterTestReplayCapture CreateReplayCapture();
    }

    internal interface ICharacterTestNetworkReplayEvidenceProvider
    {
        bool TryGetLatestReplayEvidence(out RuntimeFrame frame, out long resultHash);
    }

    public interface ICharacterTestNetworkReplayController
    {
        CharacterTestReplayPlaybackStatus BeginNetworkReplayRecording();

        CharacterTestReplayPlaybackStatus ValidateNetworkReplay();

        CharacterTestReplayPlaybackStatus CaptureNetworkReplayStatus();
    }

    public interface ICharacterTestNetworkLobbyRunner
    {
        bool IsRoomHost { get; }

        bool LocalRoomReady { get; }

        bool CanSetRoomReady { get; }

        bool CanStartRoomGame { get; }

        bool TrySetLocalRoomReady(bool ready, out string reason);

        bool TryStartRoomGame(out string reason);
    }
}
