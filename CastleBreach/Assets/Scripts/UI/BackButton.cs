using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Minimal reusable "go to this scene" handler for a Back (or similar)
/// button that only needs to load one fixed scene — avoids writing a
/// dedicated menu script for every screen that just needs a Back button
/// and nothing else. TitleMenu/SettingsMenu stay as they are (each owns
/// more than one button/control); this is for the simple cases like
/// Campaign's Back button.
/// </summary>
public class BackButton : MonoBehaviour
{
    [SerializeField] private string sceneName = "Title";

    public void OnPressed() => SceneManager.LoadScene(sceneName);
}
