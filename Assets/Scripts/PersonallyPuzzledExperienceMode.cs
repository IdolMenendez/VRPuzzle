using System;

public enum PersonallyPuzzledExperienceMode
{
    PlayerManual,
    AutoDemo,
    ValidationSmoke,
}

/// <summary>
/// Single source of truth for editor play-mode routing. Android/PICO players
/// always resolve to PlayerManual, so demo automation can never leak into a
/// release APK.
/// </summary>
public static class PersonallyPuzzledExperienceModeState
{
    public const string EnvironmentVariable = "PPVR_EXPERIENCE_MODE";
    public const PersonallyPuzzledExperienceMode ReleaseDefault =
        PersonallyPuzzledExperienceMode.PlayerManual;

    public static PersonallyPuzzledExperienceMode Current
    {
        get
        {
#if UNITY_EDITOR
            string value = Environment.GetEnvironmentVariable(
                EnvironmentVariable);
            if (Enum.TryParse(
                    value,
                    true,
                    out PersonallyPuzzledExperienceMode mode))
                return mode;
#endif
            return ReleaseDefault;
        }
    }

    public static bool IsPlayerManual =>
        Current == PersonallyPuzzledExperienceMode.PlayerManual;
    public static bool IsAutoDemo =>
        Current == PersonallyPuzzledExperienceMode.AutoDemo;
    public static bool IsValidationSmoke =>
        Current == PersonallyPuzzledExperienceMode.ValidationSmoke;
}
