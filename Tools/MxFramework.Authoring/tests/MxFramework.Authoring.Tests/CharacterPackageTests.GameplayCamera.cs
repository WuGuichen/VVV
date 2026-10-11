using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Tests;

internal static partial class CharacterPackageTests
{
    internal static void RunCharacterGameplayCamera()
    {
        CharacterGameplayCamera_RoundTripCompileAndValidationAreFailClosed();
        CharacterSocketBinding_ModelRootAllowsEmptyPathsAndTypedBindingsFailClosed();
    }

    private static void CharacterGameplayCamera_RoundTripCompileAndValidationAreFailClosed()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        package.Geometry.Sockets.Add(new CharacterSocketProfile
        {
            SocketId = "cameraPivot",
            AbstractBoneId = "body_center",
            BonePath = "bone.spine",
            LocalPose = new CharacterAuthoringLocalPose
            {
                ParentKind = CharacterPoseParentKind.Bone,
                ParentPath = "bone.spine"
            },
            Usage = CharacterSocketUsage.Gameplay
        });
        package.ApplicationConfig.GameplayCameraProfile = CreateGameplayCameraProfile();

        string json = JsonSerializer.Serialize(package.ApplicationConfig, JsonOptions);
        CharacterApplicationAuthoringSummary roundTrip = JsonSerializer.Deserialize<CharacterApplicationAuthoringSummary>(json, JsonOptions);
        Require(roundTrip?.GameplayCameraProfile?.ProfileId == "camera.test.gameplay", "gameplay camera profile should roundtrip without a schema bump.");
        Require(roundTrip.GameplayCameraProfile.FramingBoundsExtents.Z == 3f, "gameplay camera extents should roundtrip.");
        Require(roundTrip.GameplayCameraProfile.Locked.FramingScale == 0.65f, "locked gameplay framing scale should roundtrip.");

        CharacterAuthoringValidationReport valid = CharacterResourcePackageValidator.Validate(package);
        Require(!HasValidationIssue(valid, CharacterAuthoringValidationCodes.CameraProfileInvalid, CharacterAuthoringValidationGate.SpawnBlocked), "valid gameplay camera profile should not block spawn.");
        Require(!HasValidationIssue(valid, CharacterAuthoringValidationCodes.CameraFocusMissing, CharacterAuthoringValidationGate.SpawnBlocked), "resolved gameplay camera focus should not block spawn.");

        CharacterAuthoringCompileResult compiled = Compile(package);
        PatchEntry presentation = compiled.GeneratedConfigPatch.Patch.Entries.Find(entry =>
            entry.Source == CharacterApplicationCompilerTableNames.CharacterPresentationProfileConfig);
        FieldValue cameraField = null;
        Require(presentation != null && presentation.Fields.TryGetValue("GameplayCameraProfile", out cameraField), "compiler should project gameplay camera profile into presentation config.");
        Require(cameraField.Kind == FieldValueKind.Map && cameraField.Map["FocusAbstractBoneId"].Scalar == "body_center", "compiled gameplay camera focus should preserve the abstract bone id.");
        Require(cameraField.Map["Locked"].Map["FramingScale"].Scalar == "0.65", "compiler should project the locked gameplay framing scale.");

        package.ApplicationConfig.GameplayCameraProfile.FramingBoundsExtents.X = 0f;
        CharacterAuthoringValidationReport invalidBounds = CharacterResourcePackageValidator.Validate(package);
        Require(HasValidationIssue(invalidBounds, CharacterAuthoringValidationCodes.CameraBoundsInvalid, CharacterAuthoringValidationGate.SpawnBlocked), "zero gameplay camera extents should block spawn.");

        package.ApplicationConfig.GameplayCameraProfile.FramingBoundsExtents.X = 1f;
        package.ApplicationConfig.GameplayCameraProfile.FocusAbstractBoneId = "missing_camera_focus";
        CharacterAuthoringValidationReport missingFocus = CharacterResourcePackageValidator.Validate(package);
        Require(HasValidationIssue(missingFocus, CharacterAuthoringValidationCodes.CameraAnchorUnresolved, CharacterAuthoringValidationGate.SpawnBlocked), "unresolved gameplay camera abstract bone should block spawn.");

        package.ApplicationConfig.GameplayCameraProfile.FocusAbstractBoneId = string.Empty;
        CharacterAuthoringValidationReport blankFocus = CharacterResourcePackageValidator.Validate(package);
        Require(HasValidationIssue(blankFocus, CharacterAuthoringValidationCodes.CameraFocusMissing, CharacterAuthoringValidationGate.SpawnBlocked), "blank gameplay camera abstract bone should report the dedicated missing-focus diagnostic.");

        package.ApplicationConfig.GameplayCameraProfile.FocusAbstractBoneId = "body_center";
        package.Geometry.Sockets.Add(new CharacterSocketProfile
        {
            SocketId = "duplicateCameraPivot",
            AbstractBoneId = "body_center",
            BonePath = "bone.spine",
            LocalPose = new CharacterAuthoringLocalPose
            {
                ParentKind = CharacterPoseParentKind.Bone,
                ParentPath = "bone.spine"
            },
            Usage = CharacterSocketUsage.Gameplay
        });
        CharacterAuthoringValidationReport duplicateFocus = CharacterResourcePackageValidator.Validate(package);
        Require(HasValidationIssue(duplicateFocus, CharacterAuthoringValidationCodes.CameraAnchorUnresolved, CharacterAuthoringValidationGate.SpawnBlocked), "duplicate gameplay camera abstract bone mapping should block spawn.");
    }

    private static void CharacterSocketBinding_ModelRootAllowsEmptyPathsAndTypedBindingsFailClosed()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        var socket = new CharacterSocketProfile
        {
            SocketId = "stableCameraFocus",
            LocalPose = new CharacterAuthoringLocalPose
            {
                ParentKind = CharacterPoseParentKind.ModelRoot,
                ParentPath = string.Empty,
                Position = new CharacterAuthoringVector3(0f, 3.72f, 0f)
            },
            Usage = CharacterSocketUsage.Camera
        };
        package.Geometry.Sockets.Add(socket);

        CharacterAuthoringValidationReport modelRoot = CharacterResourcePackageValidator.Validate(package);
        Require(
            !HasValidationIssue(modelRoot, CharacterAuthoringValidationCodes.MissingSocketLocator, CharacterAuthoringValidationGate.SpawnBlocked),
            "ModelRoot sockets should be valid without bone, locator, body-part or parent paths.");
        CharacterAuthoringCompileResult compiled = Compile(package);
        CharacterSocketBinding compiledSocket = compiled.GeometryBinding.Sockets.Find(item => item.SocketLabel == socket.SocketId);
        Require(compiledSocket != null
            && compiledSocket.LocalPose.ParentKind == CharacterPoseParentKind.ModelRoot
            && string.IsNullOrWhiteSpace(compiledSocket.LocalPose.ParentPath)
            && compiledSocket.LocalPose.Position.X == 0f
            && compiledSocket.LocalPose.Position.Y == 3.72f
            && compiledSocket.LocalPose.Position.Z == 0f,
            "compiler should preserve the stable root-local camera focus pose exactly.");

        socket.LocalPose.ParentKind = CharacterPoseParentKind.Bone;
        CharacterAuthoringValidationReport missingBone = CharacterResourcePackageValidator.Validate(package);
        Require(
            HasValidationIssue(missingBone, CharacterAuthoringValidationCodes.MissingSocketLocator, CharacterAuthoringValidationGate.SpawnBlocked),
            "Bone sockets should fail closed when bonePath is missing.");

        socket.LocalPose.ParentKind = CharacterPoseParentKind.Locator;
        CharacterAuthoringValidationReport missingLocator = CharacterResourcePackageValidator.Validate(package);
        Require(
            HasValidationIssue(missingLocator, CharacterAuthoringValidationCodes.MissingSocketLocator, CharacterAuthoringValidationGate.SpawnBlocked),
            "Locator sockets should fail closed when locatorPath is missing.");

        socket.LocalPose.ParentKind = CharacterPoseParentKind.BodyPart;
        CharacterAuthoringValidationReport missingBodyPart = CharacterResourcePackageValidator.Validate(package);
        Require(
            HasValidationIssue(missingBodyPart, CharacterAuthoringValidationCodes.MissingSocketLocator, CharacterAuthoringValidationGate.SpawnBlocked),
            "BodyPart sockets should fail closed when parentPartId is missing.");
    }

    private static CharacterGameplayCameraProfileAuthoring CreateGameplayCameraProfile()
    {
        return new CharacterGameplayCameraProfileAuthoring
        {
            ProfileId = "camera.test.gameplay",
            FocusAbstractBoneId = "body_center",
            FocusOffset = new CharacterAuthoringVector3(0f, 0.1f, 0f),
            FramingBoundsCenter = new CharacterAuthoringVector3(0f, 1f, 0f),
            FramingBoundsExtents = new CharacterAuthoringVector3(1f, 2f, 3f),
            Normal = new CharacterGameplayCameraNormalAuthoring
            {
                Distance = 8f,
                MinDistance = 5f,
                MaxDistance = 12f,
                FieldOfView = 60f,
                Pitch = 20f,
                TargetPadding = 0.5f
            },
            Locked = new CharacterGameplayCameraLockedAuthoring
            {
                MinDistance = 8f,
                MaxDistance = 20f,
                TargetScreenY = 0.65f,
                TargetPadding = 1f,
                FramingScale = 0.65f
            },
            Obstruction = new CharacterGameplayCameraObstructionAuthoring
            {
                Radius = 0.4f,
                Padding = 0.2f,
                MinDistance = 2f,
                FocusClearance = 0.5f
            }
        };
    }
}
