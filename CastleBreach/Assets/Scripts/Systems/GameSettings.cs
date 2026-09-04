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
    private const string CursorSkinIndexKey = "CursorSkinIndex";
    private const string CursorScaleKey = "CursorScale";
    private const string HideOsCursorKey = "HideOsCursor";

    public const float DefaultMasterVolume = 1f;

    [Tooltip("How fast the custom cursor eases toward the mouse — see CustomCursor.")]
    public const float DefaultCursorSpeed = 20f;

    public const int DefaultCursorSkinIndex = 0;

    [Tooltip("Multiplies every cursor skin's own Base Size — see CursorSkin.")]
    public const float DefaultCursorScale = 1f;

    private static float? masterVolume;
    private static float? cursorSpeed;
    private static int? cursorSkinIndex;
    private static float? cursorScale;
    private static bool? hideOsCursor;

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

    /// <summary>Which entry in CustomCursor's Skins list is active, by index.</summary>
    public static int CursorSkinIndex
    {
        get => cursorSkinIndex ??= PlayerPrefs.GetInt(CursorSkinIndexKey, DefaultCursorSkinIndex);
        set
        {
            cursorSkinIndex = value;
            PlayerPrefs.SetInt(CursorSkinIndexKey, value);
        }
    }

    /// <summary>Multiplies every cursor skin's own Base Size — see CursorSkin.</summary>
    public static float CursorScale
    {
        get => cursorScale ??= PlayerPrefs.GetFloat(CursorScaleKey, DefaultCursorScale);
        set
        {
            cursorScale = value;
            PlayerPrefs.SetFloat(CursorScaleKey, value);
        }
    }

    /// <summary>
    /// Whether the custom cursor should hide the real OS pointer. Off means
    /// the OS arrow shows and the custom sprite hides instead, rather than
    /// both showing at once. PlayerPrefs has no bool type, so stored as 0/1.
    /// </summary>
    public static bool HideOsCursor
    {
        get => hideOsCursor ??= PlayerPrefs.GetInt(HideOsCursorKey, 1) == 1;
        set
        {
            hideOsCursor = value;
            PlayerPrefs.SetInt(HideOsCursorKey, value ? 1 : 0);
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
