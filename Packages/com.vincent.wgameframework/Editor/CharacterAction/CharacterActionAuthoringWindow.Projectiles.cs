namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private CharacterActionProjectileAuthoringStore LoadProjectileAuthoringStoreForWindow()
        {
            CharacterActionProjectileAuthoringStore store = CharacterActionProjectileAuthoringStore.LoadDefault(
                _model?.ActionSet?.CharacterStableId ?? string.Empty);
            CharacterActionProjectileRequestMembership.Refresh(store);
            return store;
        }

        private void ApplyProjectilePreviewCatalogOverride()
        {
            // CharacterAction previews consume the last formally imported catalogs.
            // Draft projection and projectile write ownership belong exclusively to Projectile Authoring.
            _timelinePreviewSession?.ClearProjectileCatalogOverride();
        }
    }
}
