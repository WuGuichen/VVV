using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MxFramework.Input
{
    public static class DefaultInputActionAssetFactory
    {
        public static InputActionAsset Create()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = DefaultInputBindingCatalog.AssetName;
            IReadOnlyList<DefaultInputActionMapDefinition> maps = DefaultInputBindingCatalog.GetMaps();
            for (int i = 0; i < maps.Count; i++)
                asset.AddActionMap(CreateMap(maps[i]));
            AddControlSchemes(asset);
            return asset;
        }

        private static void AddControlSchemes(InputActionAsset asset)
        {
            asset.AddControlScheme(DefaultInputBindingCatalog.KeyboardMouseGroup)
                .WithRequiredDevice("<Keyboard>")
                .WithRequiredDevice("<Mouse>");
            asset.AddControlScheme(DefaultInputBindingCatalog.GamepadGroup)
                .WithRequiredDevice("<Gamepad>");
            asset.AddControlScheme(DefaultInputBindingCatalog.TouchGroup)
                .WithRequiredDevice("<Touchscreen>");
        }

        private static InputActionMap CreateMap(DefaultInputActionMapDefinition definition)
        {
            var map = new InputActionMap(definition.Name);
            for (int i = 0; i < definition.Actions.Count; i++)
                AddAction(map, definition.Actions[i]);
            return map;
        }

        private static void AddAction(
            InputActionMap map,
            DefaultInputActionDefinition definition)
        {
            InputAction action = map.AddAction(
                definition.Name,
                definition.Type,
                expectedControlLayout: definition.ExpectedControlLayout);

            InputActionSetupExtensions.CompositeSyntax composite =
                default(InputActionSetupExtensions.CompositeSyntax);
            bool hasComposite = false;
            for (int i = 0; i < definition.Bindings.Count; i++)
            {
                DefaultInputBindingDefinition binding = definition.Bindings[i];
                if (binding.IsComposite)
                {
                    composite = action.AddCompositeBinding(binding.Path);
                    hasComposite = true;
                    continue;
                }

                if (binding.IsPartOfComposite)
                {
                    if (!hasComposite)
                    {
                        Debug.LogWarning(
                            "Default input binding catalog has a composite part without a composite: "
                            + definition.Name
                            + "/"
                            + binding.Name);
                        continue;
                    }

                    composite = composite.With(binding.Name, binding.Path);
                    continue;
                }

                InputActionSetupExtensions.BindingSyntax syntax = action.AddBinding(binding.Path);
                if (!string.IsNullOrEmpty(binding.Name))
                    syntax = syntax.WithName(binding.Name);
                if (!string.IsNullOrEmpty(binding.Group))
                    syntax.WithGroup(binding.Group);
            }
        }
    }
}
