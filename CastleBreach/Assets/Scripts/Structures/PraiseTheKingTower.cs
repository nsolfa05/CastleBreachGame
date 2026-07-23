using UnityEngine;

/// <summary>
/// Praise the King Tower (design doc §6): an economy building — generates
/// gold on a timer instead of attacking. 30 HP, cost 120, 2x2, generates
/// 3 coins every 4 seconds (all tunable here).
///
/// It matters to the enemy too: a Goblin whose lure range covers this tower
/// targets IT instead of the King (§7.3), and hits it extra hard — so where
/// you place these is a real decision, not free money.
/// </summary>
public class PraiseTheKingTower : MonoBehaviour
{
    [Header("Economy (design doc §6: 3 coins / 4s)")]
    [SerializeField] private int goldPerTick = 3;
    [SerializeField] private float secondsPerTick = 4f;

    private float nextTickTime;

    private void Start()
    {
        nextTickTime = Time.time + secondsPerTick;
    }

    private void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Playing) return;

        if (Time.time >= nextTickTime)
        {
            nextTickTime = Time.time + secondsPerTick;
            gm.AddGold(goldPerTick);
        }
    }
}
