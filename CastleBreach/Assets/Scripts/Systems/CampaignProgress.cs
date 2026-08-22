using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Campaign progression gating (§2/§3.5): how many nodes, in trail order,
/// are unlocked. Persisted via PlayerPrefs like GameSettings. Starts with
/// a few unlocked so locked/unlocked visuals are both testable immediately
/// — real progression (win a level, unlock the next) wires in via
/// Guide 13c's win-screen work, once there's an actual win event to hook.
/// </summary>
public static class CampaignProgress
{
    private const string UnlockedCountKey = "UnlockedLevelCount";
    public const int DefaultUnlockedCount = 3;

    private static int? unlockedCount;

    public static int UnlockedCount
    {
        get => unlockedCount ??= PlayerPrefs.GetInt(UnlockedCountKey, DefaultUnlockedCount);
        set
        {
            unlockedCount = Mathf.Max(1, value);
            PlayerPrefs.SetInt(UnlockedCountKey, unlockedCount.Value);
        }
    }

    /// <summary>nodeIndex is 1-based — a node's position in campaign order.</summary>
    public static bool IsUnlocked(int nodeIndex) => nodeIndex <= UnlockedCount;

    /// <summary>Call on a win once Guide 13c wires up the win screen.</summary>
    public static void UnlockNext() => UnlockedCount++;

#if UNITY_EDITOR
    [MenuItem("Tools/Castle Breach/Campaign - Unlock Next Level")]
    private static void UnlockNextMenuItem() => UnlockNext();

    [MenuItem("Tools/Castle Breach/Campaign - Reset Progress")]
    private static void ResetProgressMenuItem() => UnlockedCount = DefaultUnlockedCount;
#endif
}
