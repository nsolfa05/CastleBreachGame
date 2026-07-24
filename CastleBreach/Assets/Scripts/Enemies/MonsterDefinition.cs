using UnityEngine;

/// <summary>
/// All stats + behavior flags for one monster type (design doc §7.3), as a
/// designer-editable asset file — create via
/// Assets ▸ Create ▸ Castle Breach ▸ Monster Definition.
/// One asset per monster type; the single generic Monster prefab (MonsterAI)
/// reads everything from here, so adding a new monster type needs NO new
/// code and NO new prefab — just a new asset with different numbers.
/// </summary>
[CreateAssetMenu(menuName = "Castle Breach/Monster Definition", fileName = "NewMonster")]
public class MonsterDefinition : ScriptableObject
{
    [Header("Identity / look [Placeholder]")]
    public string displayName = "Zombie";
    public Color bodyColor = new Color(0.25f, 0.50f, 0.20f);
    [Tooltip("Body size in tiles (1-tile monsters ~0.85; the 2x2 Cyclops ~1.7).")]
    public float bodyScale = 0.85f;

    [Header("Movement & health (§7.3)")]
    public float moveSpeed = 4f;
    public float maxHealth = 10f;

    [Header("Attacking the player / King (§7.3)")]
    [Tooltip("Damage per hit vs the player AND the King.")]
    public float playerDamage = 3f;
    [Tooltip("Chases the player when they are within this many tiles. 0 = never targets the player (Goblin).")]
    public float playerTargetRange = 6f;
    [Tooltip("Seconds between attacks.")]
    public float attackInterval = 1.5f;
    [Tooltip("Center-to-center reach of an attack, in tiles.")]
    public float attackRange = 1.2f;

    [Header("Attacking structures (§7.3)")]
    public float structureDamage = 3f;
    [Tooltip("Goblin hits the Praise the King Tower harder (doc: 4 vs its normal 2). 0 = same as Structure Damage.")]
    public float praiseTowerDamage = 0f;

    [Header("Economy (§4 / §7.3)")]
    public int currencyDrop = 3;

    [Header("Tile weight (§7.1 — enforced in the walls/pathfinding phase)")]
    public int tileWeight = 2;

    [Header("Special behaviors")]
    [Tooltip("Goblin: ignores the player entirely and heads straight for the King.")]
    public bool targetsOnlyKing = false;
    [Tooltip("Goblin: if a Praise the King Tower is within this many tiles, target it instead of the King. 0 = off.")]
    public float praiseTowerLureRange = 0f;
    [Tooltip("Goblin: can pass through player-built Gate structures (used once walls/gates exist).")]
    public bool passesThroughGates = false;
    [Tooltip("If the King is within this many tiles, target the King instead of chasing the player, even if the player is also in range. 0 = off (King stays fallback-only, the original behavior). Usable on ANY monster, not just one type.")]
    public float kingPriorityRange = 0f;
    [Tooltip("Attacks structures within Structure Priority Range before anything else (player OR King) — Cyclops uses this, but it's usable on any monster.")]
    public bool prioritizesStructures = false;
    public float structurePriorityRange = 0f;
    [Tooltip("Skeleton: how many times it revives after dying (1 = two lives total).")]
    public int extraLives = 0;
    [Tooltip("Skeleton: seconds spent as an invulnerable bone pile before reviving.")]
    public float reviveDelaySeconds = 6f;
    public Color bonePileColor = new Color(0.90f, 0.90f, 0.85f);
}
