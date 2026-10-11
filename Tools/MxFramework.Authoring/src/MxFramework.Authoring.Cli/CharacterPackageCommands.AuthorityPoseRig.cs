using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class CharacterPackageCommands
{
    private static void ReadAuthorityPoseRig(
        string packagePath,
        JsonSerializerOptions options,
        CharacterAuthoringGeometry geometry)
    {
        string path = Path.Combine(packagePath, "geometry", "authority_pose_rig.json");
        CharacterAuthorityPoseRigLoadResult result = File.Exists(path)
            ? CharacterAuthorityPoseRigDocumentCodec.Read(File.ReadAllText(path), options)
            : CharacterAuthorityPoseRigDocumentCodec.Missing();
        geometry.AuthorityPoseRig = result.Document;
        geometry.AuthorityPoseRigDocumentState = result.State;
        geometry.AuthorityPoseRigRawPayload = result.RawPayload;
    }
}
