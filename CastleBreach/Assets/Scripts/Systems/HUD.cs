using TMPro;
using UnityEngine;

/// <summary>
/// On-screen text [Placeholder]: gold, wave progress, King health, and the
/// win/lose message. All fields are optional — anything left unassigned is
/// simply skipped, so this works even before every UI element exists.
/// </summary>
public class HUD : MonoBehaviour
{
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text kingHealthText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private WaveSpawner waveSpawner;

    private void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        if (goldText != null)
            goldText.text = $"Gold: {gm.Gold}";

        if (kingHealthText != null && gm.KingHealth != null)
            kingHealthText.text = $"King: {Mathf.CeilToInt(gm.KingHealth.Current)} / {Mathf.CeilToInt(gm.KingHealth.Max)}";

        if (waveText != null && waveSpawner != null)
            waveText.text = waveSpawner.CurrentWaveNumber == 0
                ? "Get ready..."
                : $"Wave {waveSpawner.CurrentWaveNumber} / {waveSpawner.TotalWaves}";

        if (messageText != null)
        {
            switch (gm.State)
            {
                case GameState.Won:
                    messageText.text = "VICTORY! The King survived.\nPress R to play again";
                    break;
                case GameState.Lost:
                    messageText.text = "GAME OVER — the King has fallen.\nPress R to retry";
                    break;
                default:
                    messageText.text = "";
                    break;
            }
        }
    }
}
