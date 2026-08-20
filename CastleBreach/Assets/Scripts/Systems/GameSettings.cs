using UnityEngine;

/// <summary>
/// Player-adjustable settings (§2): Master Volume and Cursor Speed, both
/// persisted via PlayerPrefs so they survive between sessions. Cached in
/// memory after first read so per-frame readers (CustomCursor) aren't
/// hitting PlayerPrefs every frame.
/// </summary>
public static class GameSettings
{
    private const string MasterVolumeKey = "MasterVolume";
    private const string CursorSpeedKey = "CursorSpeed";

    public const float DefaultMasterVolume = 1f;

    [Tooltip("How fast the custom cursor eases toward the mouse — see CustomCursor.")]
    public const float DefaultCursorSpeed = 20f;

    private static float? masterVolume;
    private static float? cursorSpeed;

    public static float MasterVolume
    {
        get => masterVolume ??= PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume);
        set
        {
            masterVolume = value;
            PlayerPrefs.SetFloat(MasterVolumeKey, value);
            AudioListener.volume = value;
        }
    }

    public static float CursorSpeed
    {
        get => cursorSpeed ??= PlayerPrefs.GetFloat(CursorSpeedKey, DefaultCursorSpeed);
        set
        {
            cursorSpeed = value;
            PlayerPrefs.SetFloat(CursorSpeedKey, value);
        }
    }

    /// <summary>
    /// Call once when the game starts (Title scene's Awake) so a returning
    /// player's saved volume takes effect immediately, not just whenever
    /// they next happen to open Settings. Cursor Speed needs no equivalent
    /// push — CustomCursor reads it live every frame.
    /// </summary>
    public static void ApplyOnLoad()
    {
        AudioListener.volume = MasterVolume;
    }
}
