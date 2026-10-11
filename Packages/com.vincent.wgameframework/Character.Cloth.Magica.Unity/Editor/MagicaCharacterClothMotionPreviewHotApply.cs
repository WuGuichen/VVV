using System;
using System.Collections.Generic;
using System.IO;
using MagicaCloth2;
using MxFramework.CharacterCloth.Magica.Unity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MxFramework.CharacterCloth.Magica.Unity.Editor
{
    internal static class MagicaCharacterClothMotionPreviewHotApply
    {
        private const string ClothPrefix = "MagicaCloth_";
        private const string ColliderPrefix = "MagicaCollider_";

        internal static bool TryApply(
            GameObject actor,
            Transform bodyModel,
            MagicaCharacterClothAuthoringDocument document,
            out string error)
        {
            error = string.Empty;
            if (actor == null || bodyModel == null || document == null)
            {
                error = "运动预览热应用缺少角色、BodyModel 或 Cloth 草稿。";
                return false;
            }

            MagicaCharacterClothValidationResult validation =
                MagicaCharacterClothAuthoringValidator.Validate(document, bodyModel);
            if (validation.HasErrors)
            {
                error = validation.Summary;
                return false;
            }
            if (document.enabled && (document.cloths == null || document.cloths.Length == 0))
            {
                error = "启用 Cloth 时至少需要一个 Cloth 条目。";
                return false;
            }

            try
            {
                RemoveGeneratedProjection(bodyModel);
                if (document.enabled)
                {
                    MagicaCharacterClothPrefabPostprocessor.ApplyDocument(
                        actor,
                        bodyModel,
                        document,
                        "motion-preview-hot-apply",
                        logResult: false);
                }
                else
                {
                    RefreshRuntimeComposition(actor);
                }
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is InvalidDataException
                || exception is FileNotFoundException
                || exception is InvalidOperationException)
            {
                error = exception.Message;
                return false;
            }
        }

        private static void RemoveGeneratedProjection(Transform bodyModel)
        {
            Transform[] transforms = bodyModel.GetComponentsInChildren<Transform>(includeInactive: true);
            var clothObjects = new List<GameObject>();
            var colliderObjects = new List<GameObject>();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null || ReferenceEquals(candidate, bodyModel))
                    continue;
                if (candidate.name.StartsWith(ClothPrefix, StringComparison.Ordinal))
                    clothObjects.Add(candidate.gameObject);
                else if (candidate.name.StartsWith(ColliderPrefix, StringComparison.Ordinal))
                    colliderObjects.Add(candidate.gameObject);
            }

            for (int i = 0; i < clothObjects.Count; i++)
                if (clothObjects[i] != null) Object.DestroyImmediate(clothObjects[i]);
            for (int i = 0; i < colliderObjects.Count; i++)
                if (colliderObjects[i] != null) Object.DestroyImmediate(colliderObjects[i]);
        }

        private static void RefreshRuntimeComposition(GameObject actor)
        {
            MagicaCharacterClothController controller =
                actor.GetComponent<MagicaCharacterClothController>();
            controller?.RefreshClothComponents();
            MagicaCharacterClothSystem system = actor.GetComponent<MagicaCharacterClothSystem>();
            system?.RefreshControllers();
        }
    }
}
