using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One node on the Campaign trail (§2/§3.5). Index is this node's 1-based
/// position in trail order, used for progression gating via
/// CampaignProgress — nodes past the unlocked count render locked and
/// can't be activated. CampaignCameraAndInput owns click detection (it has
/// to, in order to tell a click from a drag-to-pan) and calls Activate()
/// here once it decides this node was genuinely clicked, not dragged past.
///
/// Placeholder wiring: every unlocked node currently loads the same `Game`
/// scene (the vertical slice) regardless of which node — there's no real
/// per-level data yet (Guide 14b). Repoint Activate() at real per-level
/// data once that exists.
/// </summary>
public class CampaignNode : MonoBehaviour
{
    [Tooltip("1-based position in trail order — node 1 is always unlocked by default.")]
    [SerializeField] private int index = 1;

    [SerializeField] private string levelName = "Level 1";
    [SerializeField] private SpriteRenderer icon;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Color unlockedColor = Color.white;
    [SerializeField] private Color lockedColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    [SerializeField] private string gameSceneName = "Game";

    public bool IsUnlocked => CampaignProgress.IsUnlocked(index);

    private void Start() => RefreshVisual();

    private void RefreshVisual()
    {
        if (label != null) label.text = levelName;
        if (icon != null) icon.color = IsUnlocked ? unlockedColor : lockedColor;
    }

    public void Activate()
    {
        if (!IsUnlocked) return;
        SceneManager.LoadScene(gameSceneName);
    }
}
