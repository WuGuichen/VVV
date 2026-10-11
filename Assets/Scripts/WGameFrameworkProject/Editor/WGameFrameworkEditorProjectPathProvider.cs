using MxFramework.Editor;
using UnityEditor;

namespace MxFramework.Project.Editor
{
    [InitializeOnLoad]
    internal static class WGameFrameworkEditorProjectPathBootstrap
    {
        private static readonly WGameFrameworkEditorProjectPathProvider Provider =
            new WGameFrameworkEditorProjectPathProvider();

        static WGameFrameworkEditorProjectPathBootstrap()
        {
            MxFrameworkEditorProjectPaths.Register(Provider);
        }
    }

    internal sealed class WGameFrameworkEditorProjectPathProvider :
        IMxFrameworkEditorProjectPathProvider
    {
        public bool TryGetProjectRelativePath(
            MxFrameworkEditorProjectPathKind kind,
            out string projectRelativePath)
        {
            switch (kind)
            {
                case MxFrameworkEditorProjectPathKind.AbilityAuthoringJson:
                    projectRelativePath =
                        "Tools/MxFramework.Authoring/samples/ability-character-test/config/ability_authoring.json";
                    return true;
                case MxFrameworkEditorProjectPathKind.ProjectileAuthoringPackage:
                    projectRelativePath =
                        "Tools/MxFramework.Authoring/samples/ability-character-test";
                    return true;
                case MxFrameworkEditorProjectPathKind.ProjectileSourceDirectory:
                    projectRelativePath =
                        "Tools/MxFramework.Authoring/samples/ability-character-test/config/projectiles";
                    return true;
                default:
                    projectRelativePath = string.Empty;
                    return false;
            }
        }
    }
}
