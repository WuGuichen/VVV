using System.Threading;
using MxFramework.Resources;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestEditorAnimationAssetProvider : IResourceProvider
    {
        public const string Id = "characterTest.editorAnimationAsset";

        public string ProviderId => Id;

        public bool CanLoad(ResourceCatalogEntry entry)
        {
            return entry != null
                && string.Equals(entry.ProviderId, ProviderId, System.StringComparison.Ordinal)
                && string.Equals(entry.TypeId, ResourceTypeIds.AnimationClip, System.StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(entry.Address);
        }

        public ResourceLoadResult<object> Load(ResourceLoadContext context)
        {
            if (context == null || context.Entry == null)
            {
                return ResourceLoadResult<object>.Failed(new ResourceError(
                    ResourceErrorCode.ProviderFailed,
                    default,
                    ProviderId,
                    "Editor animation asset load context is missing."));
            }

            if (!string.Equals(context.Entry.TypeId, ResourceTypeIds.AnimationClip, System.StringComparison.Ordinal))
            {
                return ResourceLoadResult<object>.Failed(new ResourceError(
                    ResourceErrorCode.TypeMismatch,
                    context.Key,
                    ProviderId,
                    "Editor animation asset provider only supports AnimationClip.",
                    context.Entry.Address));
            }

#if UNITY_EDITOR
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(context.Entry.Address);
            if (clip == null)
            {
                return ResourceLoadResult<object>.Failed(new ResourceError(
                    ResourceErrorCode.NotFound,
                    context.Key,
                    ProviderId,
                    "Shared project AnimationClip was not found.",
                    context.Entry.Address));
            }

            return ResourceLoadResult<object>.Loaded(clip);
#else
            return ResourceLoadResult<object>.Failed(new ResourceError(
                ResourceErrorCode.ProviderFailed,
                context.Key,
                ProviderId,
                "Editor animation asset provider is only available in the Unity Editor.",
                context.Entry.Address));
#endif
        }

        public IResourceOperation<object> LoadAsync(
            ResourceLoadContext context,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new ImmediateResourceOperation<object>(ResourceLoadResult<object>.Failed(new ResourceError(
                    ResourceErrorCode.Cancelled,
                    context != null ? context.Key : default,
                    ProviderId,
                    "Editor animation asset load was cancelled.")));
            }

            return new ImmediateResourceOperation<object>(Load(context));
        }

        public void Release(ResourceReleaseContext context)
        {
        }
    }
}
