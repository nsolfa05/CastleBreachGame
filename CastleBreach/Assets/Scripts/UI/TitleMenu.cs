using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Button handlers for the Title scene (§2): Campaign, Survival (button
/// only — its Button component stays non-interactable in the Inspector
/// until Survival mode itself exists, so it deliberately has no handler
/// here), Test (jumps straight into the existing vertical slice, skipping
/// campaign selection), and Settings. Wire each Button's On Click to the
/// matching method here.
/// </summary>
public class TitleMenu : MonoBehaviour
{
    [Tooltip("Built in Guide 13b — until then this button errors on click, expected.")]
    [SerializeField] private string campaignSceneName = "Campaign";

    [SerializeField] private string testSceneName = "Game";
    [SerializeField] private string settingsSceneName = "Settings";

    private void Awake()
    {
        GameSettings.ApplyOnLoad();
    }

    public void OnCampaignPressed() => SceneManager.LoadScene(campaignSceneName);
    public void OnTestPressed() => SceneManager.LoadScene(testSceneName);
    public void OnSettingsPressed() => SceneManager.LoadScene(settingsSceneName);
}
