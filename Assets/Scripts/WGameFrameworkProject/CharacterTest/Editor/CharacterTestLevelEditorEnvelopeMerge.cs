using MxFramework.Config.Levels;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static class CharacterTestLevelEditorEnvelopeMerge
    {
        public static MxLevelAuthoringDocument MergeWorldObjects(
            MxLevelAuthoringDocument latest,
            LevelWorldObjectAuthoringDocument current,
            Transform root)
        {
            return CharacterTestWorldObjectAuthoringAdapter.UpsertWorldObjectsModule(
                latest,
                current,
                root);
        }

        public static MxLevelAuthoringDocument MergeCharacters(
            MxLevelAuthoringDocument latest,
            CharacterTestSceneCharacterAuthoringDocument current,
            Transform root)
        {
            MxLevelAuthoringDocument merged = CharacterTestSceneCharacterAuthoringAdapter.UpsertCharactersModule(
                latest,
                current,
                root);
            if (root != null)
            {
                // Both modules share the same header; character saves must retain the root identity.
                LevelTransformReference rootRef = CharacterTestWorldObjectAuthoringAdapter.CreateTransformRef(root, root);
                merged.RootTransformRef = new LevelTransformReference(root.name, rootRef.GlobalObjectId);
            }
            return merged;
        }
    }
}
