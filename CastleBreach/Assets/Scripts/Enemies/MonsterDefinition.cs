using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// All stats + behavior flags for one monster type (design doc §7.3), as a
/// designer-editable asset file — create via
/// Assets ▸ Create ▸ Castle Breach ▸ Monster Definition.
/// One asset per monster type; the single generic Monster prefab (MonsterAI)
/// reads everything from here, so adding a new monster type needs NO new
/// code and NO new prefab — just a new asset with different numbers.
///
/// Fields are grouped by concern (identity, movement, damage, timing,
/// targeting, economy, then the per-monster specials). An estimated-DPS
/// summary shows at the top of the inspector — see MonsterDefinitionEditor.
/// </summary>
[CreateAssetMenu(menuName = "Castle Breach/Monster Definition", fileName = "NewMonster")]
public class MonsterDefinition : ScriptableObject
{
    // ─────────────────────────────────────────────────────────────
    [Header("Identity / look [Placeholder]")]
    public string displayName = "Zombie";
    public Color bodyColor = new Color(0.25f, 0.50f, 0.20f);
    [Tooltip("Body size in tiles (1-tile monsters ~0.85; the 2x2 Cyclops ~1.7).")]
    public float bodyScale = 0.85f;

    // ─────────────────────────────────────────────────────────────
    [Header("Movement & health (§7.3)")]
    public float moveSpeed = 4f;
    public float maxHealth = 10f;

    // ─────────────────────────────────────────────────────────────
    [Header("Damage dealt (§7.3)")]
    [Tooltip("Damage per hit vs the player.")]
    public float playerDamage = 3f;
    [Tooltip("Damage per hit vs the King — its own value, always used (never falls back to or stacks with Player Damage). Set it equal to Player Damage for a monster that should hit the King the same as the player.")]
    public float kingDamage = 3f;
    [Tooltip("Damage per hit vs player-built structures.")]
    public float structureDamage = 3f;
    [Tooltip("Goblin hits the Praise the King Tower harder (doc: 4 vs its normal 2). 0 = same as Structure Damage.")]
    public float praiseTowerDamage = 0f;
    [Tooltip("Damage per hit vs player-built Walls and Gates specifically. 0 = same as Structure Damage. Separate from Structure Damage so a monster can be tuned to chew through a maze faster or slower than it fights a tower.")]
    public float wallDamage = 0f;

    // ─────────────────────────────────────────────────────────────
    [Header("Attack timing & reach")]
    [Tooltip("Seconds between attacks.")]
    public float attackInterval = 1.5f;
    [Tooltip("How much open space (in tiles) is allowed between this monster's body and its target's body for a hit to land — an edge-to-edge gap, not center-to-center, so it means the same thing regardless of target size (design doc §7.3, e.g. Zombie: 1). This does NOT control how close a monster tries to get — it always keeps advancing as far as physical space allows regardless of this value; it only decides whether an attack can currently land.")]
    public float attackRange = 1f;

    // ─────────────────────────────────────────────────────────────
    [Header("Targeting — player vs King")]
    [Tooltip("Chases the player when they are within this many tiles. 0 = never targets the player (Goblin).")]
    public float playerTargetRange = 6f;
    [Tooltip("If the King is within this many tiles, target the King instead of chasing the player, even if the player is also in range. 0 = off (King stays fallback-only, the original behavior). Usable on ANY monster.")]
    public float kingPriorityRange = 0f;
    [Tooltip("When checked, the King (while inside King Priority Range) also beats STRUCTURE priority, not just the player — so a monster near the King goes for the King instead of a nearby tower.")]
    public bool kingPriorityBeatsStructures = false;
    [Tooltip("If this monster is within this many tiles of the King/structure it's targeting, it IGNORES the player even while being attacked (recent-combat aggro can't pull it off). Helps stop the player from peeling monsters off a target they're right next to. 0 = off.")]
    public float keepTargetWithinRange = 0f;

    // ─────────────────────────────────────────────────────────────
    [Header("Targeting — structures")]
    [Tooltip("Hard cutoff: always prioritize a structure within this many tiles, no comparison needed — beats the player too (the only escape valve is Recent Player Combat Window, and only before this monster has already committed to a specific structure) and beats the King unconditionally. Cyclops uses this, but it's usable on any monster. 0 = off.")]
    public float structurePriorityRange = 0f;
    [Tooltip("Softer than Structure Priority Range: within this many tiles, still prefer a structure over trekking to the King — but NEVER over actively chasing the player (this tier is skipped entirely whenever the player is already the target, no exceptions). The candidate structure must also be closer to the King than this monster currently is, so it can never get lured sideways or backward away from the King. 0 = off.")]
    public float structureInterestRange = 0f;
    [Tooltip("If a structure this monster would otherwise target (from either range above) is within this many tiles of the King, skip it and go straight for the King instead — for structures built defensively close to the King, where attacking them isn't worth ignoring the real target. 0 = off.")]
    public float structureNearKingRange = 0f;
    [Tooltip("If this monster attacked the player, or was attacked BY the player, within this many seconds, it stays engaged with the player instead of switching to a nearby structure. Does NOT apply retroactively — once already committed to attacking a structure, getting hit doesn't pull it back off (unless it's stuck and can't reach that structure). 0 = off.")]
    public float recentPlayerCombatWindow = 3f;

    // ─────────────────────────────────────────────────────────────
    [Header("Knockback & stun dealt (Guide 11)")]
    [Tooltip("What this monster's attack does on TOP of damage — knockback and/or stun, both off by default. Mainly meaningful against the player (who has a KnockbackReceiver); the King and structures don't react. The knockback shoves the player directly away from this monster. Example: give a big enemy some knockback + a short stun so a hit from it actually staggers the player.")]
    public HitEffects attackEffects;

    // ─────────────────────────────────────────────────────────────
    [Header("Economy")]
    public int currencyDrop = 3;

    // ─────────────────────────────────────────────────────────────
    [Header("Personal Defenses")]
    [Tooltip("Weight (§7.1) — heavier monsters are knocked back LESS by the same hit (the combat framework's knockback speed scales by 1/weight). MonsterAI copies this into the monster's KnockbackReceiver at spawn. A decimal value, so knockback distance can be tuned precisely rather than only in whole steps.")]
    [FormerlySerializedAs("tileWeight")]
    [InspectorLabel("Weight Impact Lvl")]
    public float weight = 2f;
    [Tooltip("How much THIS monster resists being stunned by the player's weapons — 0 = fully stunnable, 1 = stun-immune, 0.5 = stuns on it last half as long. Lets a big enemy shrug off a stun that would freeze a small one, even though the weapon's Stun Duration is the same for both. Does NOT affect knockback distance — see Weight for that.")]
    [InspectorLabel("Stun Impact Lvl")]
    [Range(0f, 1f)] public float stunResistance = 0f;

    // ─────────────────────────────────────────────────────────────
    [Header("Special: Goblin")]
    [Tooltip("Goblin: ignores the player entirely and heads straight for the King.")]
    public bool targetsOnlyKing = false;
    [Tooltip("Goblin: if a Praise the King Tower is within this many tiles, target it instead of the King. 0 = off.")]
    public float praiseTowerLureRange = 0f;
    [Tooltip("Goblin: can pass through player-built Gate structures — routes straight through them instead of around, and never needs to break one.")]
    public bool passesThroughGates = false;

    // ─────────────────────────────────────────────────────────────
    [Header("Movement — walls & gates (§6)")]
    [Tooltip("Flying: routes straight over player-built Walls and Gates as if they weren't there, so it never gets maze'd and never needs to break one. Still blocked by towers, the King, and the castle's own border walls. Off for every current monster — here for a future flying type.")]
    public bool fliesOverBarriers = false;

    // ─────────────────────────────────────────────────────────────
    [Header("Special: Skeleton")]
    [Tooltip("Skeleton: how many times it revives after dying (1 = two lives total).")]
    public int extraLives = 0;
    [Tooltip("Skeleton: seconds spent as an invulnerable bone pile before reviving.")]
    public float reviveDelaySeconds = 6f;
    public Color bonePileColor = new Color(0.90f, 0.90f, 0.85f);

    // ─────────────────────────────────────────────────────────────
    [Header("Special: Cyclops — telegraphed area attack")]
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
    [Tooltip("If checked, movement eases to a full stop once the telegraph wind-up begins (and eases back up to speed once the cooldown starts). If unchecked, the monster keeps walking normally through the whole attack — only the locked box position is affected.")]
    public bool pausesDuringTelegraph = true;
    [Tooltip("Seconds to decelerate to a stop when the wind-up begins. 0 = instant stop. Only used when Pauses During Telegraph is checked.")]
    public float telegraphStopDuration = 0.2f;
    [Tooltip("Seconds to accelerate back to normal move speed once the cooldown (post-slam) begins. 0 = instant resume. Only used when Pauses During Telegraph is checked.")]
    public float telegraphResumeDuration = 0.3f;
}
