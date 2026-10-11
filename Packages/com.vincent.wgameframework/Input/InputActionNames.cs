namespace MxFramework.Input
{
    public static class InputActionNames
    {
        public const string GameplayMap = "Gameplay";
        public const string UIMap = "UI";
        public const string VehicleMap = "Vehicle";
        public const string PhotoModeMap = "PhotoMode";
        public const string CutsceneMap = "Cutscene";
        public const string DebugMap = "Debug";

        public const string Move = "Move";
        public const string Look = "Look";
        public const string Jump = "Jump";
        public const string AttackPrimary = "AttackPrimary";
        public const string AttackSecondary = "AttackSecondary";
        public const string Interact = "Interact";
        public const string Dodge = "Dodge";
        public const string Sprint = "Sprint";
        public const string Pause = "Pause";
        public const string TargetLock = "TargetLock";
        public const string CameraModeNext = "CameraModeNext";
        public const string GuidancePrevious = "GuidancePrevious";
        public const string GuidanceNext = "GuidanceNext";
        public const string SkillSlot1 = "SkillSlot1";
        public const string SkillSlot2 = "SkillSlot2";
        public const string SkillSlot3 = "SkillSlot3";
        public const string SkillSlot4 = "SkillSlot4";
        public const string SkillSlot5 = "SkillSlot5";
        public const string SkillSlot6 = "SkillSlot6";
        public const string SkillSlot7 = "SkillSlot7";
        public const string SkillSlot8 = "SkillSlot8";
        public const string SkillSlot9 = "SkillSlot9";

        public static readonly string[] SkillSlots =
        {
            SkillSlot1,
            SkillSlot2,
            SkillSlot3,
            SkillSlot4,
            SkillSlot5,
            SkillSlot6,
            SkillSlot7,
            SkillSlot8,
            SkillSlot9
        };

        public const string Navigate = "Navigate";
        public const string Submit = "Submit";
        public const string Cancel = "Cancel";
        public const string Point = "Point";
        public const string Click = "Click";
        public const string RightClick = "RightClick";
        public const string MiddleClick = "MiddleClick";
        public const string ScrollWheel = "ScrollWheel";
        public const string TrackedDevicePosition = "TrackedDevicePosition";
        public const string TrackedDeviceOrientation = "TrackedDeviceOrientation";

        public const string Steer = "Steer";
        public const string Throttle = "Throttle";
        public const string Brake = "Brake";
        public const string ExitVehicle = "ExitVehicle";

        public const string Orbit = "Orbit";
        public const string Zoom = "Zoom";
        public const string TakeShot = "TakeShot";
        public const string Exit = "Exit";

        public const string Skip = "Skip";
        public const string Continue = "Continue";

        public const string ToggleConsole = "ToggleConsole";
        public const string Restart = "Restart";
        public const string DebugPrimary = "DebugPrimary";
        public const string DebugSecondary = "DebugSecondary";
        public const string DebugCycle = "DebugCycle";
        public const string DebugStep = "DebugStep";
        public const string ToggleHud = "ToggleHud";
        public const string AudioPrimary = "AudioPrimary";
        public const string AudioSecondary = "AudioSecondary";

        public static string GetMapName(InputContext context)
        {
            switch (context)
            {
                case InputContext.Gameplay:
                    return GameplayMap;
                case InputContext.UI:
                case InputContext.Rebinding:
                    return UIMap;
                case InputContext.Vehicle:
                    return VehicleMap;
                case InputContext.PhotoMode:
                    return PhotoModeMap;
                case InputContext.Cutscene:
                    return CutsceneMap;
                case InputContext.Debug:
                    return DebugMap;
                default:
                    return string.Empty;
            }
        }
    }
}
