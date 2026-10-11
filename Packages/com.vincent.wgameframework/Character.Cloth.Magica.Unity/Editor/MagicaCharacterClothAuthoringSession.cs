using System;
using UnityEngine;

namespace MxFramework.CharacterCloth.Magica.Unity.Editor
{
    internal sealed class MagicaCharacterClothAuthoringSession : ScriptableObject
    {
        [SerializeField] internal MagicaCharacterClothAuthoringDocument document = new MagicaCharacterClothAuthoringDocument();
        [NonSerialized] internal string packagePath = string.Empty;
        [NonSerialized] internal string packageId = string.Empty;
        [NonSerialized] internal string loadedHash = string.Empty;
        [NonSerialized] internal string cleanDocumentHash = string.Empty;
        [NonSerialized] internal string message = string.Empty;
        [NonSerialized] internal bool configExists;
        [NonSerialized] internal bool prefabRebuildRequired;
        [NonSerialized] internal MagicaCharacterClothValidationResult validation;

        internal bool IsDirty
        {
            get
            {
                if (document == null || string.IsNullOrWhiteSpace(cleanDocumentHash)) return false;
                if (!configExists) return true;
                string serialized = MagicaCharacterClothAuthoringSerializer.Serialize(document);
                return !string.Equals(
                    cleanDocumentHash,
                    MagicaCharacterClothAuthoringRepository.ComputeTextHash(serialized),
                    StringComparison.Ordinal);
            }
        }
    }
}
