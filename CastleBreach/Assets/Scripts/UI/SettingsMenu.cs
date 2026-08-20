using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Settings screen (§2): Master Volume and Cursor Speed sliders, both
/// backed by GameSettings/PlayerPrefs so they persist between sessions.
/// Wire each Slider's On Value Changed to the matching method here — the
/// Slider's own Min/Max Value fields in the Inspector set the valid range,
/// nothing duplicated here.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Slider cursorSpeedSlider;
    [SerializeField] private string titleSceneName = "Title";

    private void Start()
    {
        if (volumeSlider != null)
            volumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume);

        if (cursorSpeedSlider != null)
            cursorSpeedSlider.SetValueWithoutNotify(GameSettings.CursorSpeed);
    }

    public void OnVolumeChanged(float value) => GameSettings.MasterVolume = value;
    public void OnCursorSpeedChanged(float value) => GameSettings.CursorSpeed = value;
    public void OnBackPressed() => SceneManager.LoadScene(titleSceneName);
}
