using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace MxFramework.Input
{
    public static class DefaultInputBindingCatalog
    {
        public const string AssetName = "MxFrameworkInputActions";
        public const string KeyboardMouseGroup = "Keyboard&Mouse";
        public const string GamepadGroup = "Gamepad";
        public const string TouchGroup = "Touch";
        public const string KeyboardEscapePath = "<Keyboard>/escape";
        public const string KeyboardPPath = "<Keyboard>/p";
        public const string KeyboardVPath = "<Keyboard>/v";
        public const string KeyboardLeftBracketPath = "<Keyboard>/leftBracket";
        public const string KeyboardRightBracketPath = "<Keyboard>/rightBracket";

        private static readonly DefaultInputActionMapDefinition[] Maps =
        {
            new DefaultInputActionMapDefinition(
                InputActionNames.GameplayMap,
                new[]
                {
                    Action(InputActionNames.Move, InputActionType.Value, "Vector2",
                        Wasd(),
                        Arrows(),
                        Binding("<Gamepad>/leftStick", GamepadGroup)),
                    Action(InputActionNames.Look, InputActionType.Value, "Vector2",
                        Binding("<Mouse>/delta", KeyboardMouseGroup),
                        Binding("<Gamepad>/rightStick", GamepadGroup)),
                    Button(InputActionNames.Jump,
                        Binding("<Keyboard>/space", KeyboardMouseGroup),
                        Binding("<Gamepad>/buttonSouth", GamepadGroup)),
                    Button(InputActionNames.AttackPrimary,
                        Binding("<Mouse>/leftButton", KeyboardMouseGroup),
                        Binding("<Gamepad>/rightTrigger", GamepadGroup)),
                    Button(InputActionNames.AttackSecondary,
                        Binding("<Mouse>/rightButton", KeyboardMouseGroup),
                        Binding("<Gamepad>/leftTrigger", GamepadGroup)),
                    Button(InputActionNames.Interact,
                        Binding("<Keyboard>/e", KeyboardMouseGroup),
                        Binding("<Gamepad>/buttonWest", GamepadGroup)),
                    Button(InputActionNames.Dodge,
                        Binding("<Keyboard>/leftShift", KeyboardMouseGroup),
                        Binding("<Gamepad>/buttonEast", GamepadGroup)),
                    Button(InputActionNames.Sprint,
                        Binding("<Keyboard>/leftCtrl", KeyboardMouseGroup),
                        Binding("<Gamepad>/leftStickPress", GamepadGroup)),
                    Button(InputActionNames.Pause,
                        Binding(KeyboardEscapePath, KeyboardMouseGroup),
                        Binding("<Gamepad>/start", GamepadGroup)),
                    Button(InputActionNames.TargetLock,
                        Binding("<Keyboard>/l", KeyboardMouseGroup),
                        Binding("<Mouse>/middleButton", KeyboardMouseGroup),
                        Binding("<Gamepad>/rightStickPress", GamepadGroup)),
                    Button(InputActionNames.CameraModeNext,
                        Binding(KeyboardVPath, KeyboardMouseGroup)),
                    Button(InputActionNames.GuidancePrevious,
                        Binding(KeyboardLeftBracketPath, KeyboardMouseGroup)),
                    Button(InputActionNames.GuidanceNext,
                        Binding(KeyboardRightBracketPath, KeyboardMouseGroup)),
                    Button(InputActionNames.SkillSlot1, Binding("<Keyboard>/1", KeyboardMouseGroup)),
                    Button(InputActionNames.SkillSlot2, Binding("<Keyboard>/2", KeyboardMouseGroup)),
                    Button(InputActionNames.SkillSlot3, Binding("<Keyboard>/3", KeyboardMouseGroup)),
                    Button(InputActionNames.SkillSlot4, Binding("<Keyboard>/4", KeyboardMouseGroup)),
                    Button(InputActionNames.SkillSlot5, Binding("<Keyboard>/5", KeyboardMouseGroup)),
                    Button(InputActionNames.SkillSlot6, Binding("<Keyboard>/6", KeyboardMouseGroup)),
                    Button(InputActionNames.SkillSlot7, Binding("<Keyboard>/7", KeyboardMouseGroup)),
                    Button(InputActionNames.SkillSlot8, Binding("<Keyboard>/8", KeyboardMouseGroup)),
                    Button(InputActionNames.SkillSlot9, Binding("<Keyboard>/9", KeyboardMouseGroup)),
                    Action(InputActionNames.Point, InputActionType.PassThrough, "Vector2",
                        Binding("<Mouse>/position", KeyboardMouseGroup),
                        Binding("<Touchscreen>/primaryTouch/position", TouchGroup)),
                    Button(InputActionNames.Click,
                        Binding("<Mouse>/leftButton", KeyboardMouseGroup),
                        Binding("<Touchscreen>/primaryTouch/press", TouchGroup)),
                    Button(InputActionNames.RightClick,
                        Binding("<Mouse>/rightButton", KeyboardMouseGroup)),
                    Action(InputActionNames.ScrollWheel, InputActionType.PassThrough, "Vector2",
                        Binding("<Mouse>/scroll", KeyboardMouseGroup))
                }),
            new DefaultInputActionMapDefinition(
                InputActionNames.UIMap,
                new[]
                {
                    Action(InputActionNames.Navigate, InputActionType.Value, "Vector2",
                        Wasd(),
                        Arrows(),
                        Binding("<Gamepad>/leftStick", GamepadGroup),
                        Binding("<Gamepad>/dpad", GamepadGroup)),
                    Button(InputActionNames.Submit,
                        Binding("<Keyboard>/enter", KeyboardMouseGroup),
                        Binding("<Gamepad>/buttonSouth", GamepadGroup)),
                    Button(InputActionNames.Cancel,
                        Binding(KeyboardEscapePath, KeyboardMouseGroup),
                        Binding("<Gamepad>/buttonEast", GamepadGroup)),
                    Action(InputActionNames.Point, InputActionType.PassThrough, "Vector2",
                        Binding("<Mouse>/position", KeyboardMouseGroup),
                        Binding("<Touchscreen>/primaryTouch/position", TouchGroup)),
                    Button(InputActionNames.Click,
                        Binding("<Mouse>/leftButton", KeyboardMouseGroup),
                        Binding("<Touchscreen>/primaryTouch/press", TouchGroup)),
                    Button(InputActionNames.RightClick,
                        Binding("<Mouse>/rightButton", KeyboardMouseGroup)),
                    Button(InputActionNames.MiddleClick,
                        Binding("<Mouse>/middleButton", KeyboardMouseGroup)),
                    Action(InputActionNames.ScrollWheel, InputActionType.PassThrough, "Vector2",
                        Binding("<Mouse>/scroll", KeyboardMouseGroup))
                }),
            new DefaultInputActionMapDefinition(
                InputActionNames.DebugMap,
                new[]
                {
                    Button(InputActionNames.ToggleConsole, Binding("<Keyboard>/backquote", KeyboardMouseGroup)),
                    Button(InputActionNames.ToggleHud, Binding("<Keyboard>/f1", KeyboardMouseGroup)),
                    Button(InputActionNames.DebugCycle, Binding("<Keyboard>/f2", KeyboardMouseGroup)),
                    Button(InputActionNames.DebugStep, Binding("<Keyboard>/f3", KeyboardMouseGroup)),
                    Button(InputActionNames.Restart, Binding("<Keyboard>/f4", KeyboardMouseGroup)),
                    Button(InputActionNames.DebugPrimary, Binding("<Keyboard>/f5", KeyboardMouseGroup)),
                    Button(InputActionNames.DebugSecondary, Binding("<Keyboard>/f6", KeyboardMouseGroup)),
                    Button(InputActionNames.AudioPrimary, Binding("<Keyboard>/f7", KeyboardMouseGroup)),
                    Button(InputActionNames.AudioSecondary, Binding("<Keyboard>/f8", KeyboardMouseGroup))
                })
        };

        private static readonly string[][] ConflictDomains =
        {
            new[] { InputActionNames.GameplayMap },
            new[] { InputActionNames.UIMap },
            new[] { InputActionNames.DebugMap },
            new[] { InputActionNames.GameplayMap, InputActionNames.DebugMap },
            new[] { InputActionNames.UIMap, InputActionNames.DebugMap }
        };

        public static IReadOnlyList<DefaultInputActionMapDefinition> GetMaps()
        {
            return Maps;
        }

        public static IReadOnlyList<DefaultInputBindingConflict> FindConflicts()
        {
            var conflicts = new List<DefaultInputBindingConflict>();
            for (int i = 0; i < ConflictDomains.Length; i++)
                FindConflicts(ConflictDomains[i], conflicts);
            return conflicts;
        }

        public static DefaultInputActionMapDefinition FindMap(string mapName)
        {
            for (int i = 0; i < Maps.Length; i++)
            {
                if (string.Equals(Maps[i].Name, mapName, StringComparison.Ordinal))
                    return Maps[i];
            }

            return null;
        }

        private static void FindConflicts(string[] mapNames, List<DefaultInputBindingConflict> conflicts)
        {
            var used = new Dictionary<string, BindingOwner>(StringComparer.OrdinalIgnoreCase);
            for (int mapIndex = 0; mapIndex < mapNames.Length; mapIndex++)
            {
                DefaultInputActionMapDefinition map = FindMap(mapNames[mapIndex]);
                if (map == null)
                    continue;

                for (int actionIndex = 0; actionIndex < map.Actions.Count; actionIndex++)
                {
                    DefaultInputActionDefinition action = map.Actions[actionIndex];
                    for (int bindingIndex = 0; bindingIndex < action.Bindings.Count; bindingIndex++)
                    {
                        DefaultInputBindingDefinition binding = action.Bindings[bindingIndex];
                        if (!IsConflictTrackedPath(binding.Path))
                            continue;

                        string key = NormalizeBindingPath(binding.Path);
                        var owner = new BindingOwner(map.Name, action.Name, binding.Path);
                        if (used.TryGetValue(key, out BindingOwner existing))
                        {
                            if (!string.Equals(existing.MapName, owner.MapName, StringComparison.Ordinal)
                                || !string.Equals(existing.ActionName, owner.ActionName, StringComparison.Ordinal))
                            {
                                conflicts.Add(new DefaultInputBindingConflict(
                                    string.Join("+", mapNames),
                                    binding.Path,
                                    existing.MapName + "/" + existing.ActionName,
                                    owner.MapName + "/" + owner.ActionName));
                            }
                        }
                        else
                        {
                            used.Add(key, owner);
                        }
                    }
                }
            }
        }

        private static bool IsConflictTrackedPath(string path)
        {
            return !string.IsNullOrEmpty(path)
                && (path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith("<Gamepad>/", StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeBindingPath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Trim().ToLowerInvariant();
        }

        private static DefaultInputActionDefinition Action(
            string name,
            InputActionType type,
            string expectedControlLayout,
            params object[] bindingItems)
        {
            DefaultInputBindingDefinition[] bindings = FlattenBindings(bindingItems);
            return new DefaultInputActionDefinition(name, type, expectedControlLayout, bindings);
        }

        private static DefaultInputActionDefinition Button(
            string name,
            params object[] bindingItems)
        {
            return Action(name, InputActionType.Button, "Button", bindingItems);
        }

        private static DefaultInputBindingDefinition Binding(string path, string group)
        {
            return new DefaultInputBindingDefinition(path, string.Empty, group, false, false);
        }

        private static DefaultInputBindingDefinition Composite(string name, string group)
        {
            return new DefaultInputBindingDefinition("2DVector", name, group, true, false);
        }

        private static DefaultInputBindingDefinition Part(string name, string path, string group)
        {
            return new DefaultInputBindingDefinition(path, name, group, false, true);
        }

        private static DefaultInputBindingDefinition[] Wasd()
        {
            return new[]
            {
                Composite("WASD", KeyboardMouseGroup),
                Part("up", "<Keyboard>/w", KeyboardMouseGroup),
                Part("down", "<Keyboard>/s", KeyboardMouseGroup),
                Part("left", "<Keyboard>/a", KeyboardMouseGroup),
                Part("right", "<Keyboard>/d", KeyboardMouseGroup)
            };
        }

        private static DefaultInputBindingDefinition[] Arrows()
        {
            return new[]
            {
                Composite("Arrows", KeyboardMouseGroup),
                Part("up", "<Keyboard>/upArrow", KeyboardMouseGroup),
                Part("down", "<Keyboard>/downArrow", KeyboardMouseGroup),
                Part("left", "<Keyboard>/leftArrow", KeyboardMouseGroup),
                Part("right", "<Keyboard>/rightArrow", KeyboardMouseGroup)
            };
        }

        private static DefaultInputBindingDefinition[] FlattenBindings(object[] items)
        {
            if (items == null || items.Length == 0)
                return Array.Empty<DefaultInputBindingDefinition>();

            var result = new List<DefaultInputBindingDefinition>();
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] is DefaultInputBindingDefinition binding)
                {
                    result.Add(binding);
                }
                else if (items[i] is DefaultInputBindingDefinition[] bindings)
                {
                    result.AddRange(bindings);
                }
                else if (items[i] != null)
                {
                    throw new ArgumentException(
                        "Unsupported default input binding catalog item: " + items[i].GetType().FullName + ".");
                }
            }

            return result.ToArray();
        }

        private readonly struct BindingOwner
        {
            public BindingOwner(string mapName, string actionName, string path)
            {
                MapName = mapName ?? string.Empty;
                ActionName = actionName ?? string.Empty;
                Path = path ?? string.Empty;
            }

            public string MapName { get; }
            public string ActionName { get; }
            public string Path { get; }
        }
    }

    public sealed class DefaultInputActionMapDefinition
    {
        public DefaultInputActionMapDefinition(
            string name,
            IReadOnlyList<DefaultInputActionDefinition> actions)
        {
            Name = name ?? string.Empty;
            Actions = actions ?? Array.Empty<DefaultInputActionDefinition>();
        }

        public string Name { get; }
        public IReadOnlyList<DefaultInputActionDefinition> Actions { get; }
    }

    public sealed class DefaultInputActionDefinition
    {
        public DefaultInputActionDefinition(
            string name,
            InputActionType type,
            string expectedControlLayout,
            IReadOnlyList<DefaultInputBindingDefinition> bindings)
        {
            Name = name ?? string.Empty;
            Type = type;
            ExpectedControlLayout = expectedControlLayout ?? string.Empty;
            Bindings = bindings ?? Array.Empty<DefaultInputBindingDefinition>();
        }

        public string Name { get; }
        public InputActionType Type { get; }
        public string ExpectedControlLayout { get; }
        public IReadOnlyList<DefaultInputBindingDefinition> Bindings { get; }
    }

    public readonly struct DefaultInputBindingDefinition
    {
        public DefaultInputBindingDefinition(
            string path,
            string name,
            string group,
            bool isComposite,
            bool isPartOfComposite)
        {
            Path = path ?? string.Empty;
            Name = name ?? string.Empty;
            Group = group ?? string.Empty;
            IsComposite = isComposite;
            IsPartOfComposite = isPartOfComposite;
        }

        public string Path { get; }
        public string Name { get; }
        public string Group { get; }
        public bool IsComposite { get; }
        public bool IsPartOfComposite { get; }
    }

    public readonly struct DefaultInputBindingConflict
    {
        public DefaultInputBindingConflict(
            string domain,
            string bindingPath,
            string firstAction,
            string secondAction)
        {
            Domain = domain ?? string.Empty;
            BindingPath = bindingPath ?? string.Empty;
            FirstAction = firstAction ?? string.Empty;
            SecondAction = secondAction ?? string.Empty;
        }

        public string Domain { get; }
        public string BindingPath { get; }
        public string FirstAction { get; }
        public string SecondAction { get; }

        public override string ToString()
        {
            return Domain + " " + BindingPath + " " + FirstAction + " <-> " + SecondAction;
        }
    }
}
