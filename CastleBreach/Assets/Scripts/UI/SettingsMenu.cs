using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Settings screen (§2): Master Volume, Cursor Speed, and a Cursor Skin
/// picker, all backed by GameSettings/PlayerPrefs so they persist between
/// sessions. Wire each control's On Value Changed to the matching method
/// here — the Slider's own Min/Max Value fields in the Inspector set the
/// valid range, nothing duplicated here.
///
/// The skin dropdown's options come from the CustomCursor already present
/// in this scene (the CursorCanvas prefab, per 13a/13e) rather than being
/// typed in here — so the two can never list different skins, and it
/// previews live since Settings has its own cursor instance too.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Slider cursorSpeedSlider;
    [SerializeField] private TMP_Dropdown cursorSkinDropdown;
    [SerializeField] private string titleSceneName = "Title";

    private CustomCursor customCursor;

    private void Start()
    {
        if (volumeSlider != null)
            volumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume);

        if (cursorSpeedSlider != null)
            cursorSpeedSlider.SetValueWithoutNotify(GameSettings.CursorSpeed);

        customCursor = FindFirstObjectByType<CustomCursor>();
        if (cursorSkinDropdown != null && customCursor != null && customCursor.Skins.Count > 0)
        {
            var names = new List<string>();
            foreach (var skin in customCursor.Skins) names.Add(skin.displayName);

            cursorSkinDropdown.ClearOptions();
            cursorSkinDropdown.AddOptions(names);
            cursorSkinDropdown.SetValueWithoutNotify(GameSettings.CursorSkinIndex);
        }
    }

    public void OnVolumeChanged(float value) => GameSettings.MasterVolume = value;
    public void OnCursorSpeedChanged(float value) => GameSettings.CursorSpeed = value;

    public void OnCursorSkinChanged(int index)
    {
        if (customCursor != null) customCursor.ApplySkin(index);
    }

    public void OnBackPressed() => SceneManager.LoadScene(titleSceneName);
}
