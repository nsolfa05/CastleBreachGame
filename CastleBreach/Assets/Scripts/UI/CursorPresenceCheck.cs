using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The "make sure the real cursor is hidden" check (§2 follow-up): this
/// project has repeatedly hit scenes where CursorCanvas was never dragged
/// in, or its prefab instance got corrupted, leaving the OS arrow visible
/// with nothing in the Console explaining why. This catches that class of
/// bug automatically, in every scene, with no GameObject or wiring needed
/// — [RuntimeInitializeOnLoadMethod] runs it on its own at startup.
///
/// Only warns when GameSettings.HideOsCursor is on, i.e. only when the OS
/// arrow being visible would actually be wrong. It checks for a
/// CustomCursor instance rather than reading Cursor.visible directly,
/// because right after a scene loads no CustomCursor has run its
/// LateUpdate yet — reading Cursor.visible at that exact moment would
/// misfire on every single scene load, hidden cursor or not.
/// </summary>
public static class CursorPresenceCheck
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded += (scene, mode) => Check(scene.name);
        Check(SceneManager.GetActiveScene().name);
    }

    private static void Check(string sceneName)
    {
        if (!GameSettings.HideOsCursor) return;

        if (Object.FindFirstObjectByType<CustomCursor>() == null)
        {
            Debug.LogWarning($"CursorPresenceCheck: scene '{sceneName}' has no CustomCursor " +
                "(the CursorCanvas prefab is missing, or its instance is broken) — the real " +
                "OS pointer will show instead of the custom one, even though Hide OS Cursor " +
                "is on in Settings.");
        }
    }
}
