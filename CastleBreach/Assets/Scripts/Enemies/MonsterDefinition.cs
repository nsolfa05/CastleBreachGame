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
    [Tooltip("Damage per hit vs the player (and the King too, unless Use Unique King Damage is checked below).")]
    public float playerDamage = 3f;
    [Tooltip("If checked, the King takes King Damage instead of Player Damage (does NOT stack — it replaces). For when a monster should hurt the King more or less than the player/structures.")]
    public bool useUniqueKingDamage = false;
    [Tooltip("Damage per hit vs the King specifically. Only used when Use Unique King Damage is checked.")]
    public float kingDamage = 3f;
    [Tooltip("Chases the player when they are within this many tiles. 0 = never targets the player (Goblin).")]
    public float playerTargetRange = 6f;
    [Tooltip("Seconds between attacks.")]
    public float attackInterval = 1.5f;
    [Tooltip("How much open space (in tiles) is allowed between this monster's body and its target's body for a hit to land — an edge-to-edge gap, not center-to-center, so it means the same thing regardless of target size (design doc §7.3, e.g. Zombie: 1). This does NOT control how close a monster tries to get — it always keeps advancing as far as physical space allows regardless of this value; it only decides whether an attack can currently land.")]
    public float attackRange = 1f;

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
    [Tooltip("Hard cutoff: always prioritize a structure within this many tiles, no comparison needed.")]
    public float structurePriorityRange = 0f;
    [Tooltip("Beyond Structure Priority Range, still prefer a structure over the King if the King is at least this many TIMES farther away than the structure (e.g. 3 = a tower at 3 tiles beats a King at 12+ tiles). 0 = off — only the hard cutoff above applies.")]
    public float structureFarKingRatio = 0f;
    [Tooltip("Max distance (tiles) at which a structure can even be considered for the Far-King-Ratio comparison — a structure beyond this is never a candidate no matter how favorable the ratio. Only matters if Structure Far King Ratio > 0.")]
    public float structureNoticeRadius = 10f;
    [Tooltip("If this monster attacked the player, or was attacked BY the player, within this many seconds, it stays engaged with the player instead of switching to a nearby structure (both structure-priority tiers). Does NOT apply retroactively — once already committed to attacking a structure, getting hit doesn't pull it back off. 0 = off.")]
    public float recentPlayerCombatWindow = 3f;
    [Tooltip("Skeleton: how many times it revives after dying (1 = two lives total).")]
    public int extraLives = 0;
    [Tooltip("Skeleton: seconds spent as an invulnerable bone pile before reviving.")]
    public float reviveDelaySeconds = 6f;
    public Color bonePileColor = new Color(0.90f, 0.90f, 0.85f);

    [Header("Telegraphed area attack (Cyclops §7.3)")]
    [Tooltip("Cyclops: instead of an instant hit, wind up a telegraphed box attack that damages EVERYTHING inside it (player, King, structures) when it lands. Needs a Telegraphed Area Attack component on the Monster prefab for the visuals.")]
    public bool usesTelegraphedAreaAttack = false;
    [Tooltip("Attack box size in tiles (width, depth). Design doc Cyclops: 2x2.")]
    public Vector2 attackBoxSize = new Vector2(2f, 2f);
    [Tooltip("Seconds of wind-up before the slam lands — the telegraph 'charge up' time. Editable.")]
    public float telegraphTime = 1.5f;
    [Tooltip("The base box shown for the whole wind-up — the attack AREA. Placeholder: transparent gray.")]
    public Color telegraphBaseColor = new Color(0.5f, 0.5f, 0.5f, 0.25f);
    [Tooltip("The darker box that fills in over the wind-up time — the 'charging' indicator. Placeholder: darker transparent gray.")]
    public Color telegraphFillColor = new Color(0.15f, 0.15f, 0.15f, 0.4f);
    [Tooltip("Flashed at the moment of impact — the actual hit. Placeholder: transparent black.")]
    public Color slamColor = new Color(0f, 0f, 0f, 0.55f);
    [Tooltip("How long the impact box stays visible, in seconds.")]
    public float slamFlashSeconds = 0.2f;
}
