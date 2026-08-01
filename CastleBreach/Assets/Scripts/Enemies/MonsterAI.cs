using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The one AI script shared by EVERY monster type — all stats and behavior
/// flags come from the assigned MonsterDefinition asset (§7.3), so the same
/// generic Monster prefab plays as a Zombie, Armored Zombie, Skeleton,
/// Goblin, or Cyclops depending on which definition it's given.
///
/// Core behavior: walk straight toward the King; chase the player instead
/// when they come within the definition's target range; attack whatever is
/// in reach, including structures blocking the path. Specials handled here:
/// - targetsOnlyKing / praiseTowerLureRange (Goblin)
/// - kingPriorityRange: beats chasing the player if the King is this close
///   (any monster, not just one type — see ChooseTarget)
/// - Structure Priority Range / Structure Interest Range / Structure Near
///   King Range (Cyclops; also usable on any monster) — see ChooseTarget
/// - extraLives + invulnerable bone-pile revive (Skeleton)
///
/// MOVEMENT: routes around player-built mazes via PathGrid (§6) — see
/// ResolveNavigation and SteeringPoint. With nothing in the way the result is
/// identical to the straight-line movement this used before.
///
/// WHERE a monster stands to attack is decided by an attack-slot reservation
/// (AttackSlots / UpdateSlot): closing on a target, it claims a distinct
/// walkable tile within range and heads there via ApproachPoint, so a crowd
/// fans into a ring around the objective instead of all converging on one
/// point — the real fix for front-rank monsters hogging the only reachable
/// tile, especially at a target boxed into a tight alcove. A slot generated
/// right in a chokepoint could still hold it forever, so a settled slot-holder
/// that's blocking a queued ally MIGRATES to a different free slot
/// (TryMigrateForBlockedAlly) — deterministically vacating its tile while
/// still attacking — or stays put if no other slot exists. HOW a monster gets
/// to its slot, and what slot-LESS overflow does once every slot is taken, is
/// handled by four local steering behaviors layered on top of the seek, never
/// by routing (which only ever accounts for static obstacles):
/// SeparationFromNeighbors (spread out of a pile-up), UpdateStuckRecovery
/// (break a travelling monster free of a wall wedge or doorway deadlock),
/// ShouldYieldToLeader (a monster jammed behind an ally at a 1-wide gap eases
/// back so the leader funnels through first), and GiveWayVelocity (a slot-less
/// arrived monster slides aside to let a queued ally reach the same target).
/// None of them ever make routing aware of other monsters.
///
/// NOTE: the targeting RANGES below (attack range, the structure priority /
/// interest / near-King ranges) still measure straight-line distance via
/// DistanceBetween, not route length. That is deliberate — they are all
/// "how far away is this" questions, they are called many times per frame,
/// and their current values were tuned against straight-line distance.
/// Switching them to route length is a real behavior change to make on
/// purpose, not a side effect of pathfinding existing; see ROADMAP.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Health))]
public class MonsterAI : MonoBehaviour
{
    [Tooltip("Which monster this is — every stat reads from this asset. The WaveSpawner overrides this per spawn group; the value set on the prefab is just the default.")]
    [SerializeField] private MonsterDefinition definition;

    [Header("Scene wiring (identical for every monster type)")]
    [SerializeField] private CurrencyDrop currencyDropPrefab;

    [Tooltip("Layers holding player-built structures — set to the Structure layer.")]
    [SerializeField] private LayerMask structureLayers;

    [Tooltip("The tinted body renderer. Leave empty to use the SpriteRenderer on this object.")]
    [SerializeField] private SpriteRenderer body;

    [Header("Crowd avoidance (shared behavior, not per-monster-type data)")]
    [Tooltip("Monsters within this distance (tiles) push each other apart. A little more than one body-width, so neighbors start spreading before they physically wedge together, but small enough that a crowd still packs in around a target. Applied every physics step as an omnidirectional shove — so a neighbor pressing in from the side or behind counts too, not just one directly ahead.")]
    [SerializeField] private float separationRadius = 0.85f;

    [Tooltip("How hard the crowd-separation shove pushes, relative to a monster's own drive toward its target. Higher spreads a crowd more and frees pile-ups faster; too high and monsters can never close on the same target together. ~0.6 keeps them packing in while still sliding off each other instead of interlocking.")]
    [SerializeField] private float separationStrength = 0.6f;

    [Tooltip("How far (tiles) a monster already attacking its target looks for another monster queued up behind it — one heading for the SAME target but not yet in range. Seeing one makes it slide aside to let that one in (see Give Way Strength). A little over one body-length so it notices the next in line.")]
    [SerializeField] private float giveWayRadius = 1.1f;

    [Tooltip("How firmly a monster attacking its target slides sideways along the target's edge to open the spot for an ally queued directly behind it. It keeps pressing toward the target the whole time and only ever moves while still in attack range, so it never abandons its own target — it just shuffles over enough to let others reach the objective too. Higher makes room faster; too high reads as sidestepping instead of attacking. 0 = off.")]
    [SerializeField] private float giveWayStrength = 0.6f;

    [Tooltip("When a monster travelling to its target gets physically jammed directly behind an ally that's ahead of it and closer to the goal — the classic two-abreast pile-up at the mouth of a 1-wide gap, where there's no room to slide aside — the one behind briefly eases BACK to let the leader funnel through first, then follows. This is how far ahead (tiles) it looks for that leader. 0 = off (no yielding).")]
    [SerializeField] private float yieldProbeRadius = 0.9f;

    [Tooltip("How hard a yielding monster pushes back to clear the opening for the leader ahead of it. Modest on purpose — it's making room, not fleeing.")]
    [SerializeField] private float yieldBackStrength = 0.7f;

    [Tooltip("Seconds a monster keeps easing back once it decides to yield, before re-checking — long enough for the leader to take the gap, short enough to resume promptly. Stops it flickering between backing off and shoving forward every physics step.")]
    [SerializeField] private float yieldHoldSeconds = 0.3f;

    [Tooltip("Within this many tiles of wherever a monster is steering toward (its attack slot, or the target's surface), speed eases down instead of always driving at full moveSpeed. Fixes a real jitter: driving at full speed all the way to an exact point and only getting stopped by collision means tiny position noise right at contact flips direction every physics step, since it's re-normalized to full speed each time — visible as rapid back-and-forth shaking once a monster arrives. Only applies while calmly settling (not while genuinely stuck or yielding, which need full force to actually break free). 0 = off (old always-full-speed behavior).")]
    [SerializeField] private float arrivalRadius = 0.3f;

    [Tooltip("The floor speed (as a fraction of moveSpeed) arrival easing ramps down to, never all the way to 0. A small floor keeps a settled monster's other forces (give-way, separation) able to actually move it a meaningful amount even right at its spot — dropping all the way to a full stop would fight those.")]
    [SerializeField, Range(0f, 1f)] private float arrivalMinSpeedFactor = 0.35f;

    [Tooltip("How hard a monster steers sideways to avoid an imminent collision with a neighbor it's actually closing on (RECIPROCAL, velocity-based avoidance — the ORCA/RVO idea used by most crowd-heavy games, cut down to a cheap per-neighbor form). Predicts a collision from the two monsters' relative velocity (are we actually converging?), and both agents deterministically veer to the same rotational side, so they split the dodge and glide past instead of meeting head-on and bouncing. Distinct from plain separation, which only reacts to how CLOSE bodies are, not where they're heading. Higher = earlier, wider berths; too high = monsters swerve around allies they'd never have hit. 0 = off (falls back to separation + stuck-recovery alone).")]
    [SerializeField] private float avoidStrength = 0.6f;

    [Tooltip("PRESSURE GRADIENT — the 'flood like water & surround' knob. How hard a rear-rank monster throttles its forward speed when boxed in behind allies heading to the SAME target. 0 = off (everyone drives at full force = the old behavior, where the rear compresses the front rank through the structure). Higher = the front line holds and attacks while the ranks behind push in progressively softer, so a crowd spills sideways and flows AROUND to surround a target (and fills a corridor like water) instead of piling on its near face. As front-liners die or move, the pressure behind them drops and the next rank flows into the opening. This is the main dial for 'how much the rear pushes the front into corridors' — turn it UP for more water-like/less shoving.")]
    [SerializeField] private float rearPushFalloff = 2f;

    [Tooltip("The lowest forward-speed fraction the pressure gradient (Rear Push Falloff) can throttle a rear enemy down to. A small floor keeps even a deeply-buried enemy slowly creeping, so it flows into any opening the instant one appears instead of freezing solid. 1 = no throttle at all.")]
    [SerializeField, Range(0f, 1f)] private float rearPushFloor = 0.2f;

    [Tooltip("REAR PRESSURE — 'crowd behind me' (the opposite of the pressure gradient above, which measures crowd AHEAD). Only used while Use Attack Slots is off. Real allies pressing in from behind, still trying to reach the SAME target, above this amount mean: (1) I stay eligible for stuck-recovery's escalation even while nominally 'in range' — without this, a monster wedged right at a doorway can read as settled by plain straight-line distance and permanently disable its own stuck-recovery, freezing indefinitely since give-way alone can't help there (it only slides TANGENTIALLY along the target's edge, which at a narrow gap just presses the monster into the flanking wall); (2) I stop backing off for a leader ahead of me (there's nowhere to back INTO); (3) I skip the gentle arrival-easing slowdown and keep driving at full strength. This is the 'crowd pressure makes you commit and squeeze through' feel — a monster doesn't get shoved by neighbors, it just personally becomes less willing to wait or ease off the more of a crowd is counting on it moving. 0 = a monster with rear allies never gets this urgency boost (falls back to the old behavior).")]
    [SerializeField] private float rearPressureThreshold = 0.4f;

    [Header("Attack slots (shared behavior, not per-monster-type data)")]
    [Tooltip("When on, a monster closing on its target claims a distinct standing spot (\"slot\") around it — one of the real walkable tiles within attack range — and heads there instead of everyone converging on the target's nearest surface. This is what actually spreads a crowd into a ring and stops the front rank hogging the only reachable tile, especially around a target boxed into a tight alcove where there's no room to shuffle sideways. The force behaviors above still handle the journey and any overflow once every slot is taken. Off = the old behavior (approach the target's surface directly).")]
    [SerializeField] private bool useAttackSlots = true;

    [Tooltip("How close (tiles, edge to edge) a monster must get to its target before it claims a slot. Kept short so it streams in normally from a distance and only fans out to a distinct spot for the final approach — which also means it grabs a slot on its own side of the target rather than one it'd have to cross to. Measured from attack range outward.")]
    [SerializeField] private float slotClaimDistance = 4f;

    [Tooltip("Minimum seconds between a settled monster migrating from one attack slot to another to clear the way for a blocked ally (see the alcove/doorway behavior). Prevents a monster from hopping around the ring every frame chasing 'make room'; long enough that each migration is a decisive move to a new spot, short enough that it still responds promptly the next time it's genuinely in someone's way.")]
    [SerializeField] private float slotMigrateCooldown = 0.6f;

    [Tooltip("How long (seconds) an ally must be CONTINUOUSLY blocked behind a monster before that monster gives up its slot to make room — hysteresis that keeps a packed-but-roomy ring from constantly reshuffling. Monsters in a crowd momentarily jostle past each other all the time; without this dwell, every transient brush triggered a migration, which is what made a crowd look like it was about to settle and then re-jumble. A genuine chokepoint block (someone stuck at a doorway) is persistent, so it clears the dwell and still gets a migration — the case this is actually for. Higher = calmer/stickier but slower to clear a real block; too high and a legitimately jammed ally waits noticeably.")]
    [SerializeField] private float migrateBlockedDwell = 0.5f;

    [Tooltip("How far (tiles) a migrating monster PREFERS to reach for its NEW slot — deliberately short, just past one tile over (including diagonally). Migration is meant to be a small local shuffle ('step aside one tile so the blocked ally can get in'), not always a straight-line-nearest search across the whole ring: unbounded by default, it could hand a monster a slot clear on the far side of a crowded target, so it and whoever's now walking toward what used to be its spot end up crossing paths and shoving through each other on the way. So a nearby tile always wins when one exists. But when NOTHING is free within this radius — the doorway case, a monster holding the one tile in a 1-wide wall gap has no nearby alternative by definition, wall on both sides — it falls back to searching the whole ring rather than refusing to move and jamming the doorway shut forever. Only if there's truly no free slot anywhere does it stay put and keep attacking.")]
    [SerializeField] private float migrateSearchRadius = 1.6f;

    [Tooltip("Seconds after a monster drops its slot because it got physically stuck reaching it (still mid-approach, not yet arrived) during which it only reclaims something within Migrate Search Radius — the same 'local shuffle, not a ring-wide swap' rule Migrate Search Radius applies to an arrived holder, extended to cover a still-approaching one too. Without this, the reclaim right after release used the same unbounded nearest-search that produced the stuck/crossed assignment in the first place, so two mutually-stuck monsters could just re-pick each other's tile and keep crossing paths indefinitely.")]
    [SerializeField] private float stuckReclaimLocalWindow = 1.5f;

    [Tooltip("FUNNEL BREACH. When a target (King, tower) is walled in tight — fewer than Funnel Min Open Slots standing positions exist at all, with a breakable wall within Funnel Breach Distance — overflow monsters proactively widen the breach by at least two ADJACENT tiles instead of trickling through (or eventually opening) a single knife-edge 1-wide gap. A 1-wide gap is inherently jam-prone no matter how well the crowd behaviors are tuned (two monsters can never pass each other in it); this fixes that at the geometry level instead of asking the crowd AI to squeeze harder through it. Off = the old behavior: walls near a target only ever break one at a time, purely as overflow once every open slot is already taken. Turn this off if you'd rather a player be able to wall the King down to a permanent single-file chokepoint.")]
    [SerializeField] private bool funnelBreach = true;

    [Tooltip("How close (tiles) a breakable wall must be to a target for Funnel Breach to count it as part of that target's tight defenses. Only matters together with Funnel Min Open Slots.")]
    [SerializeField] private float funnelBreachDistance = 3f;

    [Tooltip("Funnel Breach only kicks in when a target has FEWER than this many open (standable) attack slots in total — not \"free right now\", the actual count that exists. 2 means: the moment a target's defenses would only ever offer a single-file opening, monsters proactively widen it instead of settling for a knife-edge gap.")]
    [SerializeField] private int funnelMinOpenSlots = 2;

    [Tooltip("Actual physics velocity below this (units/sec) counts as \"stuck\" — used to detect a monster that's committed to attacking a structure but is physically blocked (e.g. trapped behind another monster) from ever reaching it.")]
    [SerializeField] private float stuckVelocityThreshold = 0.15f;

    [Tooltip("Seconds between checks for whether a monster is actually making progress toward wherever it's steering. Also the minimum time between escalations below, so a fresh escalation gets a full window to work before trying again.")]
    [SerializeField] private float stuckCheckInterval = 0.4f;

    [Tooltip("Below this much movement (tiles) between checks counts as \"stuck\" this check.")]
    [SerializeField] private float stuckProgressThreshold = 0.15f;

    [Tooltip("Seconds a monster still TRAVELLING to its target can make no progress before recovery escalates — its push shifts from mostly-forward toward mostly-sideways, sliding it off whatever it's caught on (a wall corner or another monster). Exists for the cases ordinary crowding can't resolve: a body wedged on a wall corner, or two monsters holding each other in a doorway. (A monster that has already ARRIVED and is just making room for allies is handled separately by Give Way Strength, not this.) Escalating breaks a wedge instead of leaving them frozen.")]
    [SerializeField] private float stuckEscalationDelay = 0.8f;

    [Header("Routing around walls (shared behavior, not per-monster-type data)")]
    [Tooltip("Seconds between route recalculations. A route is also recalculated immediately whenever the target changes or something is built/destroyed, so this only paces the routine refresh that tracks a moving target.")]
    [SerializeField] private float repathInterval = 0.5f;

    [Tooltip("How close (tiles) a monster must get to a route waypoint before switching to aim at the next one. Smaller = hugs the intended tile-by-tile path more closely, which matters most at corners: too generous a radius lets a monster start swinging toward the next leg of the route well before it's actually rounded the corner, cutting across the inside edge of the wall forming it. Too small makes it harder to ever count a waypoint as \"reached\" while jostled in a crowd, which can stall progress instead. 0.25 keeps a monster within about a quarter-tile of the intended line.")]
    [SerializeField] private float waypointArrivalRadius = 0.25f;

    /// <summary>Fired exactly once, when the monster is permanently dead
    /// (a Skeleton's first death does NOT fire this — it revives).</summary>
    public event Action<MonsterAI> Killed;

    public MonsterDefinition Definition => definition;

    /// <summary>What this monster is currently moving toward (its objective —
    /// the King, a tower, the player, or a wall it must break). Read by another
    /// monster's give-way check to tell whether they share a target.</summary>
    public Transform CurrentTarget => currentTarget;

    /// <summary>True once this monster is within attack range of its target —
    /// i.e. it has arrived and no longer needs to push forward. A monster
    /// queued behind a settled one is what triggers that one to give way.</summary>
    public bool IsSettledAtTarget => settledAtTarget;

    private enum TelegraphPhase { Idle, Winding, Cooldown }

    private Rigidbody2D rb;
    private Health health;
    private TelegraphedAreaAttack telegraph;
    private KnockbackReceiver knockback; // may be null if the prefab has no receiver
    private float nextAttackTime;
    private int livesRemaining;
    private bool bonePileActive;
    private Vector3 activeScale;
    private float avoidSide; // -1 or +1, flips on each stuck escalation to break a symmetric deadlock — see UpdateStuckRecovery
    private float standDownKey; // random, assigned once in Awake — stable tiebreak for ShouldStandDownForStuckNeighbor, deliberately NOT Object.GetInstanceID()/GetEntityId() so this never depends on whichever identity API a given Unity version currently favors
    private float stuckPush; // 0 normally; grows while genuinely stuck, shifting MoveToward's push from forward toward sideways
    private Vector2 stuckSamplePosition;
    private float nextStuckCheckTime;
    private float stuckSeconds;

    // Shared per-frame neighbor scan (see RefreshNeighbors). Every crowd
    // behavior below (separation, yield, give-way, ally-queued-behind,
    // stand-down, head-on avoidance) used to run its OWN Physics2D.OverlapCircle
    // every frame — up to six broad-phase queries per monster per frame, each
    // scanning the same small patch of space. With hundreds of monsters on
    // screen that's a lot of redundant physics work for identical results. Now
    // there's exactly one query per monster per frame, sized to the largest of
    // the individual radii below; each behavior reads from the shared result and
    // applies its OWN (possibly smaller) radius as a plain distance check
    // instead of re-querying. Neither NeighborBuffer's contents nor
    // neighborCount are touched between FixedUpdate calls for different
    // monsters — Unity runs one GameObject's FixedUpdate to completion before
    // the next starts, so nothing can interleave and overwrite them mid-frame.
    private int neighborCount;

    // Debug-only (see PathGrid.DrawTargetingDebug / OnDrawGizmos below): when
    // and where this monster's last successful hit landed, so the gizmo can
    // flash it briefly instead of drawing a permanent marker.
    private float lastHitTime = float.NegativeInfinity;
    private Vector2 lastHitPosition;
    private const float HitFlashDuration = 0.2f;
    private float yieldUntilTime; // while Time.time < this, keep easing back for a leader — see ShouldYieldToLeader

    // Attack-slot reservation (see AttackSlots). While hasSlot, this monster
    // steers at claimedSlot's center instead of the target's surface, and holds
    // that tile against every other monster until it dies or retargets.
    private bool hasSlot;
    private Vector2Int claimedSlot;
    private Transform slotTarget;
    private float nextSlotMigrateTime; // throttle on migrating slots to clear a blocked ally — see TryMigrateForBlockedAlly
    private float preferLocalClaimUntil; // while Time.time < this, the next reclaim is bounded to migrateSearchRadius — see UpdateSlot's stuck-release branch and stuckReclaimLocalWindow
    private float blockedAllySince; // Time.time an ally first became continuously blocked behind me (0 = none right now) — migration hysteresis, see migrateBlockedDwell
    private Vector2 blockingAllyPos; // centroid of allies crowding in behind me, set by AllyQueuedBehind; migration steps AWAY from it (deeper) — see TryMigrateForBlockedAlly

    // What this monster is currently heading for, and whether it's arrived
    // (in attack range). Read by OTHER monsters' give-way check — a settled
    // monster only slides aside for an ally that shares its target and hasn't
    // arrived yet. See GiveWayVelocity / IsSettledAtTarget.
    private Transform currentTarget;
    private bool settledAtTarget;

    // Reused across every monster so per-frame crowd queries allocate nothing.
    private static readonly Collider2D[] NeighborBuffer = new Collider2D[16];
    private ContactFilter2D crowdFilter;

    // Crowd-detection mask covers BOTH monster layers, not just this instance's
    // own one. A monster with Passes Through Gates gets moved onto the
    // GatePasser layer at spawn (see Start) so Gate's collider can tell it
    // apart from ordinary monsters — but that's purely a Gate-collision trick,
    // not a "these are different kinds of crowd" distinction, so
    // SeparationFromNeighbors still needs to see every monster regardless of
    // which of the two layers it landed on. Computed in Awake, not as a field
    // initializer — LayerMask.GetMask calls into Unity's layer system, which
    // isn't allowed to run before a MonoBehaviour's lifecycle actually starts.
    private static int EnemyCrowdMask;
    private float lastAttackedPlayerTime = float.NegativeInfinity;
    private Transform committedStructureTarget; // non-null while already committed to attacking a specific structure
    private TelegraphPhase telegraphPhase = TelegraphPhase.Idle;
    private float telegraphPhaseEndTime;
    private Vector2 lockedBoxCenter;
    private float currentSpeedScale = 1f; // telegraph-attack movement ramp (1 = full speed, 0 = fully stopped)

    // Routing state (Phase 4). The path is a list of tiles to walk; blockedByStructure
    // is set when every route to the real target is sealed and something has to be
    // broken to get through.
    private readonly List<Vector2Int> path = new List<Vector2Int>();
    private int pathIndex;
    private int pathGridVersion = -1;
    private Transform pathTarget;
    private Vector2Int pathGoalTile;
    private float nextRepathTime;
    private Transform blockedByStructure;

    /// <summary>Called by the WaveSpawner right after Instantiate, before the first frame.</summary>
    public void SetDefinition(MonsterDefinition newDefinition) => definition = newDefinition;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // This is a top-down game, but the project's global 2D gravity is
        // (0, -9.81) and the prefab ships gravityScale 1, so every monster was
        // being pulled downward each physics step. MoveToward sets linearVelocity
        // at the START of the step while the solver applies gravity DURING it, so
        // that downward pull is never fully cancelled — it drifts a parked monster
        // off its slot (→ re-seek → jitter) and biases every path. Zero it here so
        // the prefab's serialized value can't reintroduce the bug. A little linear
        // damping then lets any velocity left over from a collision die out
        // instead of carrying into the next step as an overshoot/bounce — kept
        // small (MoveToward sets velocity each step, so damping only shaves a few
        // percent off the effective speed) but enough to calm crowd jostling.
        rb.gravityScale = 0f;
        rb.linearDamping = 2f;
        health = GetComponent<Health>();
        telegraph = GetComponent<TelegraphedAreaAttack>();
        knockback = GetComponent<KnockbackReceiver>();
        health.Died += OnDied;
        if (body == null) body = GetComponent<SpriteRenderer>();
        avoidSide = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        standDownKey = UnityEngine.Random.value;
        stuckSamplePosition = transform.position;
        EnemyCrowdMask = LayerMask.GetMask("Enemy", "GatePasser");
        crowdFilter = new ContactFilter2D { useLayerMask = true, useTriggers = false };
        crowdFilter.SetLayerMask(EnemyCrowdMask);
    }

    /// <summary>
    /// Gap between this monster's collider and the target's collider — NOT
    /// center-to-center distance. A big target (the King at 1.8x scale, or
    /// any 2x2 tower) physically stops a monster's collider well outside 1
    /// world unit from its CENTER, so comparing center-to-center against a
    /// ~1-tile Attack Range meant monsters could walk up, get physically
    /// stopped by collision, and STILL never register as "in range" —
    /// visibly "attacking" while dealing zero damage, forever. Measuring to
    /// the collider surface instead makes Attack Range mean the same thing
    /// regardless of how big the target is.
    /// </summary>
    private float DistanceToTarget(Transform target) => DistanceBetween(transform, target);

    /// <summary>
    /// Edge-to-edge distance between any two colliders (falls back to raw
    /// transform distance if either side lacks a Collider2D). This is the ONE
    /// place every distance-based targeting decision measures "how far apart
    /// are these two things", and it is straight-line by design.
    ///
    /// Routing (PathGrid) deliberately did NOT take this over. Doing so would
    /// mean a graph search behind every attack-range and priority-range check,
    /// many times per frame per monster, and would silently retune every
    /// existing range value from "as the crow flies" to "as the monster walks".
    /// The one rule with a real appetite for route length is the King-progress
    /// guard inside Structure Interest Range (a structure the far side of a
    /// wall can read as closer-to-the-King than it is to walk to). Upgrading
    /// just that one is cheap — a shared breadth-first sweep out from the King
    /// per movement class, refreshed only when PathGrid.Version changes, gives
    /// route distance for every tile at once — and is tracked in ROADMAP as its
    /// own deliberate change rather than folded in here.
    /// </summary>
    private static float DistanceBetween(Transform a, Transform b)
    {
        var colliderA = a.GetComponentInParent<Collider2D>();
        var colliderB = b.GetComponentInParent<Collider2D>();
        if (colliderA != null && colliderB != null)
            return colliderA.Distance(colliderB).distance;
        return Vector2.Distance(a.position, b.position);
    }

    private void Start()
    {
        if (definition == null)
        {
            Debug.LogError($"MonsterAI on '{name}': no MonsterDefinition assigned — monster will do nothing.");
            return;
        }

        name = definition.displayName;
        transform.localScale = Vector3.one * definition.bodyScale;
        activeScale = transform.localScale;
        if (body != null) body.color = definition.bodyColor;
        health.SetMax(definition.maxHealth, refill: true);
        if (knockback != null) knockback.SetWeight(definition.tileWeight); // heavy types resist knockback
        livesRemaining = definition.extraLives;

        // Gate is solid to ordinary monsters (a real physical backstop, so a
        // crowd pressing against one can never shove someone through it) but
        // must not be solid to a monster that's allowed through — and physics
        // layers can't express "block this layer except these specific
        // members" since every monster otherwise shares Enemy. Moving a
        // Passes Through Gates monster onto its own GatePasser layer (with a
        // collision-matrix exception against Gate) sidesteps that instead of
        // fighting it. EnemyCrowdMask above is what keeps this from also
        // splitting the crowd into two groups that ignore each other.
        if (definition.passesThroughGates)
        {
            int gatePasserLayer = LayerMask.NameToLayer("GatePasser");
            if (gatePasserLayer >= 0) gameObject.layer = gatePasserLayer;
        }
    }

    private void FixedUpdate()
    {
        var gm = GameManager.Instance;
        if (definition == null || bonePileActive || gm == null || gm.State != GameState.Playing)
        {
            if (rb.simulated) rb.linearVelocity = Vector2.zero; // avoid warning while a bone pile has physics off
            return;
        }

        // Being knocked back or stunned: the KnockbackReceiver owns the body this
        // frame, and a stunned monster neither moves NOR attacks — so bail out
        // entirely and let the receiver drive velocity (see KnockbackReceiver).
        if (knockback != null && knockback.ControlSuppressed) return;

        Transform objective = ChooseTarget(gm);

        if (objective == null)
        {
            currentTarget = null;
            settledAtTarget = false;
            ReleaseSlot();
            rb.linearVelocity = Vector2.zero;
            if (telegraph != null && telegraphPhase == TelegraphPhase.Winding) { telegraph.Cancel(); telegraphPhase = TelegraphPhase.Idle; }
            return;
        }

        // One shared neighbor scan for every crowd behavior this frame — see
        // neighborCount's field comment. Must run before UpdateSlot, since
        // AllyQueuedBehind (called from it) is one of the readers.
        RefreshNeighbors();

        // Reserve a distinct standing spot ("slot") around the real objective so
        // the crowd fans into a ring instead of converging on one tile. Done
        // BEFORE routing, and keyed to the objective (not whatever wall we might
        // end up breaking to reach it), so navigation below can route straight
        // to the claimed slot. Sets the approach point used by MoveToward.
        UpdateSlot(objective);

        // Route toward the objective (or the slot around it). If every route is
        // sealed, whatever is doing the sealing becomes the thing to attack
        // instead (§6, "breakable if no path around it") — so `target` is the
        // objective when reachable, or a wall to break when not. Attack range,
        // damage and the telegraph attack below all treat it uniformly.
        Transform target = ResolveNavigation(objective);
        currentTarget = objective; // publish the real objective for cross-monster slot/give-way coordination

        if (definition.usesTelegraphedAreaAttack)
        {
            UpdateTelegraphedAttack(gm, target);
            return;
        }

        // Attack Range only decides whether a hit can land right now — it is
        // NOT how close a monster is willing to get. A monster keeps trying
        // to advance every frame regardless of whether it's already in
        // range; physical collision (not this check) is what actually stops
        // it once there's nowhere left to go. This lets Attack Range stay a
        // real gameplay stat (matching the design doc's numbers) instead of
        // secretly doubling as "how close before it gives up approaching."
        float distanceToTarget = DistanceToTarget(target);
        if (distanceToTarget <= definition.attackRange)
        {
            TryAttack(target, gm);
        }
        else
        {
            // Not yet close enough to the real target — but if a structure
            // is literally blocking the direct path, hit that instead of
            // trying to walk through it.
            var blocking = NearestStructureWithin(definition.attackRange);
            if (blocking != null)
                TryAttack(blocking, gm);
        }

        MoveToward(target);
    }

    /// <summary>
    /// One Physics2D.OverlapCircle for this monster's crowd awareness this
    /// frame, sized to whichever individual crowd radius currently reaches
    /// farthest (separation / yield / give-way — see their own tooltips for
    /// what each actually means). Every crowd behavior below then reads
    /// neighborCount and NeighborBuffer instead of querying for itself,
    /// filtering down to its OWN radius with a plain squared-distance check.
    /// Recomputing the max each call (not caching it once) keeps this correct
    /// if those radii are ever live-tuned in the Inspector during Play Mode.
    /// </summary>
    private void RefreshNeighbors()
    {
        float radius = Mathf.Max(separationRadius, Mathf.Max(yieldProbeRadius, giveWayRadius));
        neighborCount = Physics2D.OverlapCircle(transform.position, radius, crowdFilter, NeighborBuffer);
    }

    private void MoveToward(Transform target, float speedScale = 1f)
    {
        // "Settled" = in attack range of the target — this governs ATTACKING and
        // slot-less give-way, and is deliberately about the objective, not a slot.
        settledAtTarget = target != null && DistanceBetween(transform, target) <= definition.attackRange;

        // "Arrived" = actually at the spot I'm trying to occupy: my own slot tile
        // if I hold one, otherwise just in range. This — NOT settledAtTarget — is
        // what gates the movement-recovery behaviors, because a slot-holder jammed
        // a tile short of its slot is still near the King (settled) yet very much
        // NOT where it's going: it must stay eligible for stuck-recovery and
        // yielding, or it sits wedged in the ring forever reading as "home".
        bool arrived = hasSlot ? IsPlaced() : settledAtTarget;

        // Rear pressure only matters for slot-less monsters — see its tooltip.
        // A slot holder's IsPlaced() is already a strict exact-tile check with no
        // looseness to patch, and migration is its own correct answer for "someone
        // needs my spot" there.
        float rearPressure = !hasSlot ? RearCrowdPressure(target) : 0f;
        bool pressured = rearPressureThreshold > 0f && rearPressure >= rearPressureThreshold;

        // Slot-less "arrived" is just settledAtTarget — a loose straight-line
        // check that can be satisfied while wedged right at a doorway/pinch
        // point, not actually well positioned. Once true it permanently disables
        // stuck-recovery below (which resets to 0 every single frame arrived is
        // true), and the only other responder, GiveWayVelocity, only slides
        // TANGENTIALLY along the target's surface — at a narrow gap that just
        // presses the monster into the flanking wall instead of helping it
        // advance. Left alone, a monster can freeze indefinitely the instant it
        // merely reads as close enough. Real rear pressure is the tell that this
        // isn't a comfortable, uncontested settle: stay eligible for
        // stuck-recovery's escalation whenever a crowd genuinely still needs the
        // room, even while nominally "arrived".
        UpdateStuckRecovery(arrived && !pressured);

        Vector2 steeringPoint = SteeringPoint(target);
        Vector2 toSteering = steeringPoint - (Vector2)transform.position;
        Vector2 seekDirection = toSteering.sqrMagnitude > 0.0001f ? toSteering.normalized : Vector2.zero;

        // Omnidirectional shove away from every crowding neighbor — the shove
        // is what actually breaks a pile-up, pushing out of wherever the crowd
        // is densest instead of only reacting to a single body straight ahead.
        // Kept as its own term so it still applies below regardless of whether
        // this monster is currently stuck. See SeparationFromNeighbors.
        Vector2 separation = SeparationFromNeighbors() * separationStrength;

        float extraSpeedScale = 1f;
        float arrivalSpeedFactor = 1f;
        float crowdScale = 1f; // pressure-gradient throttle for rear ranks — set in the calm branch below
        Vector2 steer;

        if (!arrived && ShouldYieldToLeader(seekDirection, steeringPoint, rearPressure))
        {
            // Jammed directly behind an ally that's ahead of me and closer to
            // where we're both going — the two-abreast pile-up at the mouth of
            // a 1-wide gap. Ease BACK (not sideways — a one-wide gap has no
            // room sideways, only walls) so the leader funnels through first;
            // I follow once it's clear. Self-yield, not the leader shoving me:
            // each monster stays the sole author of its own velocity, which
            // physics resolves far more predictably than one body flinging
            // another. Takes priority over the stuck sideways-push below,
            // because when the blocker is a passable ally the answer is
            // ordering (single-file), not grinding sideways into a wall.
            steer = separation - seekDirection * yieldBackStrength;
            extraSpeedScale = 0.6f; // ease back gently — making room, not fleeing
        }
        else if (hasSlot && arrived)
        {
            // Parked in my own slot with nothing else to do (not yielding, and
            // stuck-recovery has been reset by arriving). Hold position: apply
            // ONLY separation, and un-normalized, so it's ~zero when well spaced
            // instead of the full-speed seek that otherwise jitters a monster back
            // and forth across its slot centre at the arrival-speed floor. This is
            // the "if everyone's already on a slot, just stay put" behavior — a
            // monster only leaves when it genuinely needs to (a migrate hands it a
            // new far slot, dropping IsPlaced, or it's pushed clear off the tile).
            rb.linearVelocity = separation * (definition.moveSpeed * speedScale);
            return;
        }
        else if (stuckPush > 0f && ShouldStandDownForStuckNeighbor())
        {
            // Two monsters jammed head-on (the 1-wide-ring lock) both read as
            // stuck and both lean in, holding each other in place forever — no
            // sideways room to slip past, so escalation alone can't break it.
            // Break the symmetry deterministically: the lower-priority one (by
            // stable instance id) eases BACK to clear room, letting the other
            // move through first, then advances once that one is no longer stuck.
            // Exactly one of any locked pair stands down, so they don't both back
            // off (re-locking) or both push (staying locked).
            steer = separation - seekDirection * yieldBackStrength;
            extraSpeedScale = 0.6f;
        }
        else if (stuckPush > 0f)
        {
            // While genuinely stuck (UpdateStuckRecovery), shift the push from
            // "forward" toward "sideways" as it escalates — forward is exactly
            // the direction that's already proven blocked (a wall corner,
            // another monster, or a target's own solid body), so continuing to
            // lean into it wastes the push. A monster planted flush against a
            // target (e.g. queued behind whoever's already attacking) has a
            // seekDirection that points straight into that target every frame;
            // without this shift the sideways component stayed a shallow ~20°
            // lean even at full escalation, which a solid body can just
            // absorb — never enough to actually slide the monster along the
            // target's edge and free the spot for whoever's behind it.
            // avoidSide (flipped on each escalation) breaks the symmetry of
            // two monsters holding each other in a doorway. The forward
            // component never drops to zero — a floor keeps a little seeking
            // alive for the genuinely-diagonal case, where "forward" isn't
            // fully blocked, just partially.
            Vector2 sideways = new Vector2(-seekDirection.y, seekDirection.x) * avoidSide;
            float forwardWeight = Mathf.Max(0.25f, 1f - stuckPush * 0.25f);
            steer = seekDirection * forwardWeight + sideways * stuckPush + separation;
        }
        else
        {
            // Reciprocal avoidance is added here — not just left to separation —
            // so a converging pair starts peeling apart BEFORE they physically
            // meet, rather than colliding first and only correcting once
            // stuck-recovery notices a few checks later. See AvoidNeighbors.
            steer = seekDirection + separation + AvoidNeighbors() * avoidStrength;

            // Pressure gradient (flood-and-surround): throttle forward speed by
            // how boxed-in this monster is behind same-target allies ahead of it.
            // A front-rank monster (nothing ahead) keeps full speed and holds the
            // line; rear ranks slow down so they don't compress the front into the
            // structure, and separation (unthrottled here — this only scales the
            // final speed) turns their slow drive sideways so they flow around and
            // surround rather than pile on the near face. See rearPushFalloff.
            if (rearPushFalloff > 0f)
            {
                float pressure = ForwardCrowdPressure(seekDirection);
                crowdScale = Mathf.Max(rearPushFloor, 1f / (1f + pressure * rearPushFalloff));
            }

            // Arrival easing — see the Arrival Radius tooltip for why this
            // exists. Only in this calm-settling branch: a monster that's
            // stuck or yielding needs full-strength force to actually break
            // free, so damping there would undermine the fix, not help it.
            // Also skipped while pressured: easing off is for a monster gently
            // settling in with nothing behind it — one with real rear pressure
            // isn't done yet and should keep driving at full strength instead of
            // coasting in, the "crowd pressure keeps you committed" effect.
            if (arrivalRadius > 0f && !pressured)
                arrivalSpeedFactor = Mathf.Clamp(toSteering.magnitude / arrivalRadius, arrivalMinSpeedFactor, 1f);
        }

        // Give way: a monster already in range of its target slides along the
        // target's edge to open the front spot for an ally queued behind it,
        // while seekDirection keeps pressing it into the target so it never
        // leaves range. Only ever runs when settled, so it can't fight
        // stuck-recovery (which is disabled while settled). See GiveWayVelocity.
        // Only for SLOT-LESS monsters (overflow, or slots switched off): a
        // slot-holder makes room the deterministic way instead — it migrates to
        // a different free slot in UpdateSlot (TryMigrateForBlockedAlly) — and
        // force-sliding it here would just fight the slot-seek pulling it back
        // to the tile it's meant to vacate.
        if (settledAtTarget && giveWayStrength > 0f && !hasSlot)
            steer += GiveWayVelocity(target);

        steer = steer.sqrMagnitude > 0.0001f ? steer.normalized : seekDirection;
        rb.linearVelocity = steer * (definition.moveSpeed * speedScale * extraSpeedScale * arrivalSpeedFactor * crowdScale);
    }

    /// <summary>
    /// True when this monster should ease back to let an ally funnel in ahead
    /// of it — the pile-up at the mouth of a 1-wide gap, where sliding aside
    /// (separation / stuck-recovery) can't help because the only sideways room
    /// is wall. Fires when an ally is both in my forward cone (roughly ahead of
    /// me toward the goal) AND closer to that goal than I am: that ally is the
    /// leader, I'm the follower, so I yield. The leader — closer to the goal,
    /// with no one ahead-and-closer of its own — never yields, so a clump
    /// resolves itself into single file without any monster reaching in to move
    /// another. (Two monsters pinned exactly side by side, neither in the
    /// other's forward cone, is left to separation to perturb apart.)
    ///
    /// Only ever fires while PHYSICALLY jammed right now (near-zero velocity
    /// from last step) — so in the open, monsters still pack together as a
    /// crowd instead of politely trailing each other. Once triggered it holds
    /// for yieldHoldSeconds, a decisive step back rather than a per-frame
    /// flicker between backing off and shoving forward; by the time it
    /// re-checks, the leader has usually taken the gap and this monster simply
    /// advances (no longer jammed → no longer yields).
    ///
    /// Suppressed while under real rearPressure (see its tooltip): backing off
    /// with a crowd pressing in from behind just rams them, so a pressured
    /// monster holds/pushes forward instead of retreating for the leader ahead —
    /// the "crowd pressure makes you commit" half of that feature.
    /// </summary>
    private bool ShouldYieldToLeader(Vector2 seekDirection, Vector2 steeringPoint, float rearPressure)
    {
        if (yieldProbeRadius <= 0f || seekDirection.sqrMagnitude < 0.0001f) return false;
        if (rearPressureThreshold > 0f && rearPressure >= rearPressureThreshold) return false;

        // Still committed to a yield decided within the last yieldHoldSeconds.
        if (Time.time < yieldUntilTime) return true;

        // Only consider yielding when actually blocked. rb.linearVelocity is
        // last physics step's post-collision result, so near-zero means
        // something physically stopped this monster despite it trying to move.
        if (rb.linearVelocity.sqrMagnitude >= stuckVelocityThreshold * stuckVelocityThreshold) return false;

        float myGoalDistSqr = ((Vector2)transform.position - steeringPoint).sqrMagnitude;
        float radiusSqr = yieldProbeRadius * yieldProbeRadius;
        for (int i = 0; i < neighborCount; i++)
        {
            var collider = NeighborBuffer[i];
            if (collider == null || collider.gameObject == gameObject) continue;
            var other = collider.GetComponentInParent<MonsterAI>();
            if (other == null) continue;

            Vector2 toOther = (Vector2)other.transform.position - (Vector2)transform.position;
            float toOtherSqr = toOther.sqrMagnitude;
            if (toOtherSqr < 0.0001f || toOtherSqr > radiusSqr) continue; // outside my own probe radius
            if (Vector2.Dot(toOther.normalized, seekDirection) < 0.5f) continue; // not ahead of me toward the goal

            // Ahead of me AND closer to where I'm heading = the leader for this
            // pinch; I'm the follower, so I yield. Strict "<" means two exactly
            // equidistant monsters both hold (separation breaks the tie), never
            // a mutual back-off.
            float otherGoalDistSqr = ((Vector2)other.transform.position - steeringPoint).sqrMagnitude;
            if (otherGoalDistSqr < myGoalDistSqr)
            {
                yieldUntilTime = Time.time + yieldHoldSeconds;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// A monster in attack range of its target slides sideways along the
    /// target's edge to make room for an ally queued up directly behind it —
    /// the fix for a front-rank monster hogging the only reachable tile while
    /// allies pile up uselessly behind, unable to reach the same King / tower /
    /// wall. Returns a tangential (perpendicular-to-target) push, so combined
    /// with the inward seek in MoveToward the monster slides ALONG the target's
    /// surface while staying pressed against it — it keeps attacking the whole
    /// time and never gives up its own target to make room.
    ///
    /// Triggered only when an actual ally is queued behind: another monster
    /// sharing this one's target (CurrentTarget), not yet in range itself
    /// (IsSettledAtTarget false), sitting on the far side of this monster from
    /// the target (inside a cone directly behind it). No one behind → zero, so
    /// a lone attacker never fidgets. Which way it slides is chosen toward the
    /// less-crowded side, so a stack fans out into a balanced arc around the
    /// target instead of everyone peeling the same way; ties break on the
    /// per-instance avoidSide so a perfectly balanced pair doesn't jitter.
    /// </summary>
    private Vector2 GiveWayVelocity(Transform target)
    {
        Vector2 approach = ApproachPoint(target);
        Vector2 radialOut = (Vector2)transform.position - approach;
        if (radialOut.sqrMagnitude < 0.0001f) return Vector2.zero; // sitting on the surface point — no meaningful "behind"
        radialOut.Normalize();
        Vector2 tangent = new Vector2(-radialOut.y, radialOut.x);

        float radiusSqr = giveWayRadius * giveWayRadius;
        bool someoneBehind = false;
        float tangentialCrowd = 0f;
        for (int i = 0; i < neighborCount; i++)
        {
            var collider = NeighborBuffer[i];
            if (collider == null || collider.gameObject == gameObject) continue;
            var other = collider.GetComponentInParent<MonsterAI>();
            if (other == null) continue;

            Vector2 toOther = (Vector2)other.transform.position - (Vector2)transform.position;
            if (toOther.sqrMagnitude > radiusSqr) continue; // outside my own give-way radius
            tangentialCrowd += Vector2.Dot(toOther, tangent); // which side the crowd leans, for picking a slide direction

            // Queued behind me = shares my target, hasn't arrived, and sits in
            // the cone on the far side of me from the target (so it's genuinely
            // blocked BY me rather than just standing nearby).
            if (other.currentTarget == target && !other.settledAtTarget &&
                toOther.sqrMagnitude > 0.0001f &&
                Vector2.Dot(toOther.normalized, radialOut) > 0.5f)
                someoneBehind = true;
        }

        if (!someoneBehind) return Vector2.zero;

        float side = tangentialCrowd > 0.01f ? -1f : tangentialCrowd < -0.01f ? 1f : avoidSide;
        return tangent * side * giveWayStrength;
    }

    /// <summary>
    /// True when a higher-priority monster is ALSO stuck and pressed right up
    /// against me — the signal to stand down (ease back) so it can move through,
    /// rather than the two of us leaning into each other and holding the jam in
    /// place. This is the deterministic tiebreak the 1-wide-ring head-on lock
    /// needs, where there's no sideways room for escalation to work: priority is
    /// just standDownKey, a random value assigned once per monster in Awake
    /// (deliberately not any Object identity API — those vary by Unity version),
    /// so of any locked pair EXACTLY ONE stands down and the other advances —
    /// which is what actually drains the jam (in a bigger cluster the single
    /// lowest-key monster keeps moving while the rest defer, then the next, and
    /// so on). Which monster wins is arbitrary; all that matters is that the
    /// choice is consistent frame to frame.
    /// </summary>
    private bool ShouldStandDownForStuckNeighbor()
    {
        float radiusSqr = separationRadius * separationRadius;
        for (int i = 0; i < neighborCount; i++)
        {
            var collider = NeighborBuffer[i];
            if (collider == null || collider.gameObject == gameObject) continue;
            var other = collider.GetComponentInParent<MonsterAI>();
            if (other == null) continue;
            if (((Vector2)other.transform.position - (Vector2)transform.position).sqrMagnitude > radiusSqr) continue;

            // Another monster also jammed, right up against me, that outranks me.
            if (other.stuckPush > 0f && other.standDownKey < standDownKey)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Notices when a monster STILL TRAVELLING to its target genuinely isn't
    /// getting anywhere and grows stuckPush so MoveToward shifts its push from
    /// forward toward sideways, instead of waiting on crowd separation that
    /// clearly isn't resolving the situation.
    ///
    /// Separation alone handles ordinary crowding, but two failure modes slip
    /// past it: a body wedged on a convex wall corner (no neighbor to push off,
    /// just geometry), and two monsters holding each other in a one-wide
    /// doorway (their pushes cancel). This breaks both — measured, not assumed:
    /// real position is sampled every Stuck Check Interval and only counts as
    /// stuck if it genuinely hasn't moved. Once stuck for Stuck Escalation
    /// Delay, stuckPush grows and avoidSide flips so the next attempt tries the
    /// other way past whatever it's caught on, escalating each further check,
    /// capped so the monster never stops trying to reach its target — see
    /// MoveToward for how stuckPush reshapes the push.
    ///
    /// A monster that has ARRIVED (settled — in attack range) is exempt: it
    /// isn't stuck, it's home, so it resets rather than escalating. That both
    /// stops a lone attacker from pointlessly fidgeting against its target and
    /// hands the "someone's queued behind me" case cleanly to the give-way
    /// system (GiveWayVelocity), which handles it more precisely — sliding only
    /// when an ally actually needs the room and only while staying in range.
    /// Resets the moment real progress is measured too, so normal movement is
    /// never affected.
    /// </summary>
    private void UpdateStuckRecovery(bool settled)
    {
        if (settled)
        {
            // Arrived — not stuck. Clear state and re-baseline so leaving the
            // target later doesn't instantly read as a stall.
            stuckSeconds = 0f;
            stuckPush = 0f;
            stuckSamplePosition = transform.position;
            nextStuckCheckTime = Time.time + stuckCheckInterval;
            return;
        }

        if (Time.time < nextStuckCheckTime) return;
        nextStuckCheckTime = Time.time + stuckCheckInterval;

        float progressed = Vector2.Distance(transform.position, stuckSamplePosition);
        stuckSamplePosition = transform.position;

        if (progressed >= stuckProgressThreshold)
        {
            stuckSeconds = 0f;
            stuckPush = 0f;
            return;
        }

        stuckSeconds += stuckCheckInterval;
        if (stuckSeconds < stuckEscalationDelay) return;

        stuckSeconds = 0f;
        avoidSide = -avoidSide;                        // try slipping past the other way next
        stuckPush = Mathf.Min(stuckPush + 1f, 3f);     // see MoveToward — capped so a little seeking always survives
    }

    /// <summary>
    /// The point to actually steer at this frame — the next corner of the
    /// route, or the target itself when it's in plain sight.
    ///
    /// The line-of-sight check first is what keeps open ground feeling
    /// identical to before pathfinding existed: with nothing in the way a
    /// monster heads straight for the target's nearest surface point (so the
    /// crowd-spreading in ApproachPoint still works), and only walks the
    /// tile-by-tile route when something is genuinely between them.
    /// </summary>
    private Vector2 SteeringPoint(Transform target)
    {
        Vector2 approachPoint = ApproachPoint(target);
        var grid = PathGrid.Instance;
        if (grid == null || path.Count == 0) return approachPoint;

        // In plain sight — head straight for it. The route is kept rather than
        // discarded so it's still there the instant something comes between
        // them again (getting shoved back behind a corner, a wall going up)
        // instead of straight-lining into a wall until the next recalculation.
        if (grid.HasClearLine(transform.position, approachPoint, definition, target))
            return approachPoint;

        // Advance past waypoints already reached. Stopping one short of the end
        // keeps a waypoint to steer at until the target itself comes into
        // sight, which the check above then takes over for the final approach.
        float arrivalSqr = waypointArrivalRadius * waypointArrivalRadius;
        while (pathIndex < path.Count - 1 &&
               ((Vector2)transform.position - GridMath.TileCenterWorld(path[pathIndex])).sqrMagnitude < arrivalSqr)
            pathIndex++;

        if (pathIndex >= path.Count) return approachPoint;
        return GridMath.TileCenterWorld(path[pathIndex]);
    }

    /// <summary>
    /// Keeps this monster's route current, and answers the one question routing
    /// exists to answer: can it actually get where it's going?
    ///
    /// Routes to the monster's claimed attack-SLOT tile when it holds one (so a
    /// far-side slot is reached by pathing around the objective, not by pressing
    /// its near face), otherwise to the objective itself. Returns the objective
    /// when a route exists; when every route is sealed, returns the wall that
    /// has to be broken to open one — see PathGrid.Solve for why that's always
    /// the wall genuinely sealing the objective (nearest the goal), so monsters
    /// walk the maze to it rather than chewing the nearest side wall. A slot
    /// that turns out to be unreachable is abandoned (routing falls back to the
    /// objective, i.e. break-in); UpdateSlot then drops the claim next frame.
    ///
    /// Searches are rate-limited two ways: each monster only re-routes when
    /// something relevant changed (objective, goal tile, or the obstacle
    /// layout) or its interval elapses, and PathGrid caps how many monsters may
    /// search in the same frame. A monster over that cap keeps its current route.
    /// </summary>
    private Transform ResolveNavigation(Transform objective)
    {
        var grid = PathGrid.Instance;
        if (grid == null) return objective; // no routing in the scene — straight-line, as before

        // A structure being broken through can die mid-approach.
        if (blockedByStructure == null || !blockedByStructure.gameObject.activeInHierarchy)
            blockedByStructure = null;

        // Route to the claimed slot's TILE when we hold one — not the objective's
        // own tile — so a monster assigned a slot on the far side of the King
        // actually paths AROUND it to get there (the King solid, not walked
        // through), instead of pressing the near face forever. Without a slot,
        // route to the objective as before.
        bool toSlot = hasSlot && slotTarget == objective;
        Vector2Int goalTile = toSlot ? claimedSlot : GridMath.WorldToTile(objective.position);

        bool stale = pathTarget != objective ||
                     pathGridVersion != grid.Version ||
                     goalTile != pathGoalTile ||
                     Time.time >= nextRepathTime;

        if (stale && grid.TryBeginSearch())
        {
            pathTarget = objective;
            pathGridVersion = grid.Version;
            pathGoalTile = goalTile;
            nextRepathTime = Time.time + repathInterval;
            pathIndex = 0;

            Vector2Int from = GridMath.WorldToTile(transform.position);
            PathGrid.PathOutcome outcome;
            Transform blocker;
            if (toSlot)
                // Slot tile is the goal; the objective is solid (route around it)
                // but must never be chosen as the wall to break.
                outcome = grid.Solve(from, goalTile, definition, null, path, out blocker, objective);
            else
                outcome = grid.Solve(from, goalTile, definition, objective, path, out blocker);

            // An OPEN slot we can't actually reach (sealed on the far side of the
            // objective): abandon routing to it and head straight for the
            // objective instead, which will target the sealing wall if one is in
            // the way. The slot claim itself is dropped in UpdateSlot next frame
            // once it sees the objective isn't reachable via the slot.
            //
            // A WALLED slot is the opposite case: "not PathFound" there means the
            // route came back as "break the wall to reach this slot," which is the
            // whole point — the monster is meant to go break that ring wall open
            // and take the spot. So DON'T fall back for a walled slot; keep that
            // MustBreak, or it would wrongly re-route to the objective and just
            // squeeze the one open gap while its wall goes untouched.
            bool walledSlot = AttackSlots.SlotIsCurrentlyWalled(objective, claimedSlot, definition);
            if (toSlot && !walledSlot && outcome != PathGrid.PathOutcome.PathFound && grid.TryBeginSearch())
                outcome = grid.Solve(from, GridMath.WorldToTile(objective.position),
                                     definition, objective, path, out blocker);

            blockedByStructure = outcome == PathGrid.PathOutcome.MustBreak ? blocker : null;
        }

        return blockedByStructure != null ? blockedByStructure : objective;
    }

    /// <summary>
    /// Eases currentSpeedScale toward targetScale over rampSeconds (0 = snap
    /// instantly). Used to smoothly decelerate/accelerate the Cyclops around
    /// its telegraph wind-up instead of a stiff instant stop/start.
    /// </summary>
    private void UpdateSpeedScale(float targetScale, float rampSeconds)
    {
        if (rampSeconds <= 0f) { currentSpeedScale = targetScale; return; }
        float rate = 1f / rampSeconds;
        currentSpeedScale = Mathf.MoveTowards(currentSpeedScale, targetScale, rate * Time.fixedDeltaTime);
    }

    /// <summary>
    /// Telegraphed box attack (Cyclops §7.3): wind up (telegraph) → slam →
    /// cooldown → repeat. The box is aimed at and LOCKED over the target's
    /// position when the wind-up starts, so the target can dodge out of it.
    /// The slam damages everything caught inside the box.
    ///
    /// Movement ramp: when Pauses During Telegraph is on, currentSpeedScale
    /// eases from 1→0 over Telegraph Stop Duration as winding begins, and
    /// 0→1 over Telegraph Resume Duration once cooldown begins — deliberately
    /// its own timer, independent of Telegraph Time (the visual wind-up) and
    /// Attack Interval (the cooldown length), so tuning one never distorts
    /// the others. When the toggle is off, currentSpeedScale is just held at
    /// 1 and the Cyclops keeps walking normally through the whole attack —
    /// only the locked box position is affected.
    /// </summary>
    private void UpdateTelegraphedAttack(GameManager gm, Transform target)
    {
        switch (telegraphPhase)
        {
            case TelegraphPhase.Idle:
                if (DistanceToTarget(target) <= definition.attackRange)
                {
                    lockedBoxCenter = target.position; // aim + lock here
                    telegraph?.BeginTelegraph(lockedBoxCenter, definition.attackBoxSize,
                        definition.telegraphTime, definition.telegraphBaseColor, definition.telegraphFillColor);
                    telegraphPhase = TelegraphPhase.Winding;
                    telegraphPhaseEndTime = Time.time + definition.telegraphTime;
                }
                else
                {
                    if (!definition.pausesDuringTelegraph) currentSpeedScale = 1f;
                    else UpdateSpeedScale(1f, definition.telegraphResumeDuration);
                    MoveToward(target, currentSpeedScale); // not in range yet — approach
                }
                break;

            case TelegraphPhase.Winding:
                if (!definition.pausesDuringTelegraph)
                {
                    currentSpeedScale = 1f;
                    MoveToward(target, currentSpeedScale); // keeps walking; only the box is locked
                }
                else
                {
                    UpdateSpeedScale(0f, definition.telegraphStopDuration);
                    if (currentSpeedScale > 0.001f)
                        MoveToward(target, currentSpeedScale);
                    else
                        rb.linearVelocity = Vector2.zero;
                }

                if (Time.time >= telegraphPhaseEndTime)
                {
                    Slam(gm);
                    telegraph?.TriggerSlam(definition.slamColor, definition.slamFlashSeconds);
                    telegraphPhase = TelegraphPhase.Cooldown;
                    telegraphPhaseEndTime = Time.time + definition.attackInterval;
                }
                break;

            case TelegraphPhase.Cooldown:
                if (!definition.pausesDuringTelegraph) currentSpeedScale = 1f;
                else UpdateSpeedScale(1f, definition.telegraphResumeDuration);
                MoveToward(target, currentSpeedScale); // free to reposition between attacks
                if (Time.time >= telegraphPhaseEndTime)
                    telegraphPhase = TelegraphPhase.Idle;
                break;
        }
    }

    /// <summary>The slam: damage every non-monster Health inside the locked box once.</summary>
    private void Slam(GameManager gm)
    {
        var hits = Physics2D.OverlapBoxAll(lockedBoxCenter, definition.attackBoxSize, 0f);
        var damaged = new System.Collections.Generic.HashSet<Health>();
        foreach (var hit in hits)
        {
            if (hit.GetComponentInParent<MonsterAI>() != null) continue; // never hit other monsters (or self)
            var targetHealth = hit.GetComponentInParent<Health>();
            if (targetHealth == null || !damaged.Add(targetHealth)) continue;

            if (gm != null && targetHealth == gm.PlayerHealth) lastAttackedPlayerTime = Time.time;
            targetHealth.TakeDamage(DamageForHealth(targetHealth, hit.transform, gm));
            // A telegraphed slam (Cyclops = the "large enemy") carries the same
            // knockback/stun as a normal attack; knockback radiates from the box
            // centre, so everything caught in the slam is thrown outward from it.
            definition.attackEffects.ApplyTo(hit, lockedBoxCenter);
        }
    }

    /// <summary>
    /// The point THIS monster should walk toward — the closest point on the
    /// target's own collider surface to its current position, not the
    /// target's center. Monsters approaching from different angles then
    /// naturally head to different points around a big target's perimeter
    /// instead of all converging on the exact same spot and piling up.
    /// </summary>
    private Vector2 ApproachPoint(Transform target)
    {
        // Holding a slot for this exact target — steer at the slot's tile center
        // instead of the target's surface, so monsters fan out to distinct spots
        // around it rather than all piling onto the nearest point. (slotTarget
        // guards against a stale slot from a previous target still in flight.)
        if (hasSlot && slotTarget == target)
            return GridMath.TileCenterWorld(claimedSlot);

        var targetCollider = target.GetComponentInParent<Collider2D>();
        return targetCollider != null ? targetCollider.ClosestPoint(transform.position) : (Vector2)target.position;
    }

    /// <summary>
    /// Keeps this monster's attack-slot reservation current: claim one as it
    /// closes on the target, hold it while attacking, drop it when the target
    /// changes, the tile stops being valid (a wall built on it, say), or — this
    /// is the important one — an ally is genuinely blocked behind it. Claiming
    /// only within slotClaimDistance keeps the long approach unchanged and means
    /// the slot grabbed is on this monster's own side of the target, not one it
    /// would have to cross the target to reach. When every slot is taken this
    /// simply holds no slot and approaches the surface as before, leaving the
    /// force behaviors (separation / give-way / yield) to queue the overflow.
    ///
    /// The blocked-ally case is what stops a slot from turning into a permanent
    /// roadblock: slots are generated from raw walkability + range + line of
    /// sight (AttackSlots.Candidates), with no idea whether a given tile also
    /// happens to be the only way through a narrow gap into the target. The
    /// first monster to arrive can and does claim exactly that tile — and
    /// without this, it would hold it forever, since it's neither stuck (it's
    /// successfully attacking) nor unsettled (give-way requires settled). This
    /// releases the slot the moment an ally is genuinely queued behind it (the
    /// same detection GiveWayVelocity uses), which is also what lets give-way's
    /// tangential slide actually work below in MoveToward — holding a fixed
    /// slot tile while trying to slide off it would just fight the slot-seek
    /// pulling it straight back. A short cooldown before reclaiming stops it
    /// simply grabbing the same tile right back, since it would otherwise still
    /// be the nearest free one to itself.
    /// </summary>
    private void UpdateSlot(Transform objective)
    {
        var gm = GameManager.Instance;
        bool eligible = useAttackSlots && PathGrid.Instance != null && objective != null &&
                        (gm == null || objective != gm.Player) &&   // the player moves — slots are for fixed objectives
                        objective.GetComponentInParent<Collider2D>() != null;

        if (!eligible) { ReleaseSlot(); return; }

        if (slotTarget != objective) ReleaseSlot(); // switched targets — give up the old slot

        // Drop a slot that's no longer valid (wall placed on it, target changed
        // shape); it'll be re-claimed below if still appropriate.
        if (hasSlot && !AttackSlots.IsValidClaim(objective, claimedSlot, definition, this))
            ReleaseSlot();

        // Opportunistic re-anchor ("claim where you stand"): if I'm physically on
        // a valid, free open slot for this objective right now — whether I hold a
        // different one I'm still shoving toward, or none at all — just take THIS
        // one and be done. This is the big stabilizer: a monster adopts wherever
        // it has already drifted instead of crossing back through the crowd to
        // reach a tile it reserved earlier, which is most of what made monsters
        // push past each other and re-jumble right as they were about to settle.
        // On success there's nothing left to reconcile, so it returns before the
        // release/reclaim/migrate paths below even run. Gated on being near the
        // target, the same closeness gate claiming uses.
        if (DistanceToTarget(objective) <= definition.attackRange + slotClaimDistance)
        {
            Vector2Int here = GridMath.WorldToTile(transform.position);
            if ((!hasSlot || claimedSlot != here) &&
                AttackSlots.TryClaimTile(objective, definition, here, this))
            {
                if (hasSlot && claimedSlot != here) AttackSlots.Release(objective, claimedSlot, this);
                claimedSlot = here;
                slotTarget = objective;
                hasSlot = true;
                blockedByStructure = null; // standing on it — nothing to break to reach it
                return;
            }
        }

        // The slot proved unreachable last frame — routing to it fell through to
        // breaking a wall (blockedByStructure set while we held a slot). Give it
        // up so we go break in unencumbered rather than clinging to a spot we
        // can't get to, and hold off reclaiming briefly so we don't just grab
        // the same unreachable tile straight back. EXCEPT when the slot is itself
        // a walled would-be slot we deliberately claimed to break open: then the
        // "must break a wall to reach my slot" routing is the whole point, not a
        // sign the slot is lost — keep it and break in.
        if (hasSlot && blockedByStructure != null &&
            !AttackSlots.SlotIsCurrentlyWalled(objective, claimedSlot, definition))
        {
            ReleaseSlot();
            nextSlotMigrateTime = Time.time + slotMigrateCooldown;
        }

        // Stuck on the way to a claimed slot (e.g. it turned out to be awkward to
        // reach) — let it go and try for a better one next. But NOT while breaking
        // open a walled slot: shouldering through the crowd toward the wall reads
        // as "stuck" every frame, and dropping the claim there would just make it
        // oscillate instead of committing to breach that spot.
        //
        // preferLocalClaimUntil is the important part: this release happens while
        // still mid-approach (not yet IsPlaced), which is exactly the case
        // TryMigrateForBlockedAlly's own bounded search doesn't cover — that one
        // only fires for an ALREADY-ARRIVED holder. Without a bound here, the
        // reclaim below runs the same unbounded nearest-search that produced the
        // stuck/crossed assignment in the first place, off the monster's CURRENT
        // (still-jammed) position — which can just re-pick the same or an
        // equally-crossed tile, and two mutually-stuck monsters can ping-pong
        // between each other's tiles indefinitely. Marking a short window here
        // makes the reclaim below prefer something genuinely nearby first,
        // exactly like a migration, giving separation/avoidance a chance to
        // actually pull the two apart before either locks in a new claim.
        if (hasSlot && !settledAtTarget && stuckPush > 0f &&
            !AttackSlots.SlotIsCurrentlyWalled(objective, claimedSlot, definition))
        {
            ReleaseSlot();
            preferLocalClaimUntil = Time.time + stuckReclaimLocalWindow;
        }

        // An ally is genuinely blocked behind this slot — try to migrate to a
        // DIFFERENT free slot so this monster's current tile opens up for it. If
        // none exists, stay put and keep attacking (see TryMigrateForBlockedAlly).
        // Gated on IsPlaced (actually sitting in our slot) rather than merely
        // settled-near-the-King, so only an established holder yields; suppressed
        // while breaking in (blockedByStructure), where "behind me" is meaningless.
        //
        // HYSTERESIS (the stability lever): don't migrate the instant an ally
        // brushes past — require it to have been continuously blocked behind me
        // for migrateBlockedDwell. A packed-but-roomy ring is full of monsters
        // momentarily jostling past each other; migrating on every transient bump
        // is exactly what made the crowd "almost settle, then re-jumble". A
        // genuine chokepoint block (a doorway) is PERSISTENT, so it clears the
        // dwell and still triggers — the case migration is actually for.
        if (hasSlot && IsPlaced() && blockedByStructure == null && AllyQueuedBehind(objective))
        {
            if (blockedAllySince <= 0f) blockedAllySince = Time.time;
            if (Time.time - blockedAllySince >= migrateBlockedDwell && Time.time >= nextSlotMigrateTime)
                TryMigrateForBlockedAlly(objective);
        }
        else
        {
            blockedAllySince = 0f; // the block cleared (or I'm not eligible) — reset the dwell
        }

        // Claim a slot once close enough. allowWalled lets an overflow monster
        // that finds every open slot taken grab a walled would-be slot instead
        // and go break it open — widening the breach around a boxed-in target
        // (see AttackSlots.ClaimNearestSlot). The claim-distance gate keeps this
        // to monsters already at the target, so distant ones still walk the maze.
        //
        // Bounded to migrateSearchRadius while still inside the window set by a
        // stuck-triggered release just above — the "local shuffle, not a
        // ring-wide swap" rule applied to reclaiming, not just migrating. A
        // monster that never held a slot at all (the ordinary long approach)
        // still searches freely, which is what spreads an incoming crowd into a
        // full ring in the first place.
        if (!hasSlot && Time.time >= nextSlotMigrateTime &&
            DistanceToTarget(objective) <= definition.attackRange + slotClaimDistance)
        {
            float claimReach = Time.time < preferLocalClaimUntil ? migrateSearchRadius : float.PositiveInfinity;

            // Funnel breach: a target walled in tight gets a WIDER hole forced
            // open — an adjacent second (or third...) tile, not a single
            // knife-edge gap that jams no matter how good the crowd steering is.
            // See funnelBreach's tooltip. Tried first, falling straight through
            // to the ordinary claim below if it finds nothing (not under siege,
            // feature off, or genuinely nothing free) — same net result as before.
            Vector2Int? claimed = null;
            if (funnelBreach && AttackSlots.NeedsWiderBreach(objective, definition, funnelMinOpenSlots, funnelBreachDistance))
                claimed = AttackSlots.ClaimAdjacentWalledSlot(objective, definition, transform.position, this, claimReach);

            claimed = claimed ?? AttackSlots.ClaimNearestSlot(objective, definition, transform.position, this,
                                                               allowWalled: true, maxDistance: claimReach);
            if (claimed.HasValue)
            {
                claimedSlot = claimed.Value;
                slotTarget = objective;
                hasSlot = true;
            }
        }
    }

    /// <summary>
    /// A settled monster whose slot is blocking a queued ally moves to a
    /// DIFFERENT free slot — deterministically relocating rather than
    /// force-sliding — so its current tile (often the one chokepoint into a
    /// walled objective) opens up for the ally, while it goes on attacking the
    /// same target from its new spot.
    ///
    /// Steps to a NEARBY free tile, preferring the one furthest from the crowd
    /// pressing in behind me (blockingAllyPos) — i.e. one tile DEEPER — so the
    /// shuffle propagates toward the back of a pocket and the far slots actually
    /// fill, instead of me hopping to a tile right beside the newcomer and
    /// everyone staying clustered at the mouth. Still bounded to
    /// migrateSearchRadius, so it's a one-tile shuffle, not a walk across the
    /// ring (see that tooltip for the crossing-paths problem an unbounded search
    /// caused). If nothing is free within that radius, it falls back to searching
    /// the WHOLE ring rather than giving up: a monster holding the one tile in a
    /// 1-wide wall gap has no nearby alternative BY DEFINITION (wall on both
    /// sides), and refusing to move there reintroduced the exact "one monster
    /// permanently blocks the only doorway" bug this mechanic exists to solve.
    /// Only when neither search finds anything does it keep its slot and stay put
    /// — every reachable slot is genuinely full. Claiming the new slot before
    /// releasing the old guarantees the monster is never briefly slot-less (which
    /// would drop it into the surface-approach fallback for a frame); the migrate
    /// cooldown then stops it hopping around every time any ally is behind it.
    /// </summary>
    private void TryMigrateForBlockedAlly(Transform target)
    {
        var newSlot = AttackSlots.ClaimSlotAwayFrom(target, definition, transform.position,
                                                    blockingAllyPos, this, excludeTile: claimedSlot,
                                                    maxDistance: migrateSearchRadius)
                   ?? AttackSlots.ClaimNearestSlot(target, definition, transform.position, this,
                                                    excludeTile: claimedSlot);
        if (!newSlot.HasValue) return; // no free slot anywhere — stay put, keep attacking

        AttackSlots.Release(target, claimedSlot, this); // free the tile that was in the ally's way
        claimedSlot = newSlot.Value;                    // slotTarget unchanged; still holding a slot for the same target
        nextSlotMigrateTime = Time.time + slotMigrateCooldown;
    }

    /// <summary>
    /// True if an ally sharing this monster's target is genuinely queued
    /// behind it — blocked from reaching the target by this monster's own
    /// body, not just standing nearby. Same geometry GiveWayVelocity uses (a
    /// cone on the far side of this monster from the target), kept as its own
    /// small query here because UpdateSlot needs a plain yes/no to decide
    /// whether to release a slot, before GiveWayVelocity runs later in
    /// MoveToward and separately decides which way to actually slide.
    /// </summary>
    private bool AllyQueuedBehind(Transform target)
    {
        Vector2 approach = ApproachPoint(target);
        Vector2 radialOut = (Vector2)transform.position - approach;
        if (radialOut.sqrMagnitude < 0.0001f) return false;
        radialOut.Normalize();

        // A neighbor pressing against me counts as "bumping" — used to widen the
        // detection cone below for side-on contention.
        const float bumpDistance = 0.9f;

        float radiusSqr = giveWayRadius * giveWayRadius;
        Vector2 blockerSum = Vector2.zero;
        int blockerCount = 0;
        for (int i = 0; i < neighborCount; i++)
        {
            var collider = NeighborBuffer[i];
            if (collider == null || collider.gameObject == gameObject) continue;
            var other = collider.GetComponentInParent<MonsterAI>();
            if (other == null) continue;

            Vector2 toOther = (Vector2)other.transform.position - (Vector2)transform.position;
            float distSqr = toOther.sqrMagnitude;
            if (distSqr < 0.0001f || distSqr > radiusSqr) continue; // outside my own give-way radius
            // Same objective, and not yet parked in its own slot — i.e. genuinely
            // still trying to get in (IsPlaced, not merely near the King).
            if (other.currentTarget != target || other.IsPlaced()) continue;

            // Two ways an ally counts. (1) Queued in the cone behind me — on the
            // far side from the target, the ones I'm plainly blocking from getting
            // in. (2) Simply pressed up against me (bumping) from ANY side while
            // still trying to reach a spot. Case (2) is the user-described chain:
            // it fires between an established holder and whoever's shoving to get
            // past, and because a holder that migrates then becomes unsettled
            // itself while travelling to its new slot, it in turn trips the next
            // holder — so the shuffle propagates up a packed line. Only unsettled
            // same-target allies trigger it (filtered above), and the migrate
            // cooldown keeps it from thrashing.
            float dot = Vector2.Dot(toOther.normalized, radialOut);
            bool bumping = distSqr <= bumpDistance * bumpDistance;
            if (dot > 0.5f || bumping)
            {
                blockerSum += (Vector2)other.transform.position;
                blockerCount++;
            }
        }

        if (blockerCount == 0) return false;
        // Centroid of the allies crowding in on me — TryMigrateForBlockedAlly
        // steps AWAY from this, i.e. deeper, so the shuffle fills the far slots.
        blockingAllyPos = blockerSum / blockerCount;
        return true;
    }

    /// <summary>
    /// True once this monster has actually reached the spot it's trying to
    /// occupy — its claimed slot tile if it holds one, or just "in attack range
    /// of the objective" if it doesn't. Deliberately different from
    /// settledAtTarget, which only measures range to the OBJECTIVE and so reads
    /// true for ANY monster merely near the King — even one still jammed short of
    /// its own slot behind a wall of allies. The yield/migration logic keys off
    /// this instead: a monster that isn't placed genuinely still needs to get in,
    /// so a holder in its way should shuffle aside. It's also what lets the
    /// shuffle chain: a holder that migrates is briefly un-placed (heading to its
    /// new slot) and so, in turn, trips the next holder up a packed line.
    /// </summary>
    private bool IsPlaced()
    {
        if (hasSlot)
        {
            // Forgiving enough that separation nudging a settled monster off its
            // exact tile centre doesn't read as "not placed", tight enough that a
            // monster still a tile or more short of its slot does.
            const float placedRadius = 0.6f;
            Vector2 slotCenter = GridMath.TileCenterWorld(claimedSlot);
            return ((Vector2)transform.position - slotCenter).sqrMagnitude <= placedRadius * placedRadius;
        }
        return settledAtTarget;
    }

    /// <summary>Give up any held slot so another monster can take it.</summary>
    private void ReleaseSlot()
    {
        if (!hasSlot) return;
        AttackSlots.Release(slotTarget, claimedSlot, this);
        hasSlot = false;
        slotTarget = null;
    }

    /// <summary>
    /// Omnidirectional crowd separation: a shove away from every nearby monster,
    /// weighted so closer ones push harder (linear falloff to zero at
    /// separationRadius). Summed across all neighbors, so a monster surrounded
    /// on several sides pushes out of the gap between them rather than into
    /// anyone.
    ///
    /// This replaced a single forward CircleCast, whose one probe could only
    /// ever see a neighbor directly in the desired direction — blind to anyone
    /// pressing in from the side or behind, which is exactly the pile-up it was
    /// meant to resolve, and why monsters kept wedging together no matter how
    /// much the old sideways nudge escalated. Uses the non-allocating overlap
    /// (shared NeighborBuffer) so a screen full of monsters churns no garbage.
    /// </summary>
    private Vector2 SeparationFromNeighbors()
    {
        Vector2 push = Vector2.zero;
        for (int i = 0; i < neighborCount; i++)
        {
            var neighbor = NeighborBuffer[i];
            if (neighbor == null || neighbor.gameObject == gameObject) continue;

            Vector2 away = (Vector2)transform.position - (Vector2)neighbor.transform.position;
            float distance = away.magnitude;
            if (distance < 0.0001f)
            {
                // Spawned exactly on top of each other — shove out along this
                // monster's stable side so they don't sit fused forever.
                push += new Vector2(avoidSide, 0f);
                continue;
            }
            // Clamp01 already zeroes this out beyond separationRadius, so a
            // neighbor picked up only because it's within some OTHER behavior's
            // larger radius (see RefreshNeighbors) naturally contributes nothing.
            push += away / distance * (1f - Mathf.Clamp01(distance / separationRadius));
        }
        return push;
    }

    /// <summary>
    /// A sideways bias, proportional to how directly an ally is moving toward
    /// this monster (not just standing nearby, like plain separation), so two
    /// monsters on a collision course start peeling apart before they actually
    /// meet. Without this the only response to an oncoming ally was reactive:
    /// physically collide, then wait for stuck-recovery to measure "no
    /// progress" over stuckEscalationDelay and only then start pushing
    /// sideways — which showed up as a monster visibly bouncing off an ally a
    /// couple of times before finally sliding past (e.g. two monsters crossing
    /// paths on the way to breaking their own ring walls). Reads a neighbor's
    /// Reciprocal, velocity-based collision avoidance — a cut-down take on the
    /// ORCA/RVO approach most crowd-heavy games use, without the full
    /// linear-programming solver. For each nearby neighbor it asks the RVO
    /// question — "given our two velocities, are we actually converging?" — using
    /// relative velocity, not just how close we are (that's what plain separation
    /// already covers). When two are genuinely closing, BOTH veer to the same
    /// rotational side: each steers to its own right relative to the line to the
    /// other. Because that line points opposite ways for the two of them, "right"
    /// resolves to opposite WORLD directions, so they split the avoidance and
    /// slide past each other instead of meeting head-on and bouncing — the
    /// reciprocity is the whole point, and why it doesn't need one agent to
    /// "win". A neighbor we're moving APART from contributes nothing (no
    /// imminent collision), and the nudge scales with how close and how head-on
    /// the approach is, so a glancing pass barely registers while a dead-on
    /// closing does. Returns an un-normalized bias; MoveToward folds it into the
    /// steer alongside seek and separation.
    /// </summary>
    private Vector2 AvoidNeighbors()
    {
        if (avoidStrength <= 0f) return Vector2.zero;

        Vector2 myVelocity = rb.linearVelocity;
        Vector2 bias = Vector2.zero;
        float radiusSqr = separationRadius * separationRadius;

        for (int i = 0; i < neighborCount; i++)
        {
            var collider = NeighborBuffer[i];
            if (collider == null || collider.gameObject == gameObject) continue;
            var other = collider.GetComponentInParent<MonsterAI>();
            if (other == null || other.rb == null) continue;

            Vector2 relPos = (Vector2)other.transform.position - (Vector2)transform.position;
            float distSqr = relPos.sqrMagnitude;
            if (distSqr < 0.0001f || distSqr > radiusSqr) continue;

            // Converging only: my velocity RELATIVE to theirs must point toward
            // them. Two monsters both flowing the same way (a crowd streaming to
            // the King together) have near-zero relative velocity and don't
            // trigger this — it's specifically for crossing/oncoming paths.
            Vector2 relVel = myVelocity - other.rb.linearVelocity;
            float closing = Vector2.Dot(relVel, relPos);
            if (closing <= 0f) continue;

            float dist = Mathf.Sqrt(distSqr);
            Vector2 toOther = relPos / dist;
            // "Right" of the direction to the other agent. The other computes the
            // same off ITS toOther (which points the opposite way), so its right
            // is our left — the two veer to opposite world sides and pass cleanly.
            Vector2 right = new Vector2(toOther.y, -toOther.x);

            // Urgency: closer neighbors and more head-on closings push harder.
            // relVelMag normalizes 'closing' back into "how head-on" (0..1).
            float relVelMag = relVel.magnitude;
            float headOn = relVelMag > 0.0001f ? closing / (relVelMag * dist) : 0f;
            float proximity = 1f - dist / separationRadius;
            bias += right * (headOn * proximity);
        }
        return bias;
    }

    /// <summary>
    /// How boxed-in toward the target this monster is by same-objective allies
    /// directly AHEAD of it — 0 for a front-rank monster with a clear path,
    /// growing as ranks stack up in front. MoveToward turns this into a
    /// forward-speed throttle (rearPushFalloff): the front holds and attacks
    /// while rear ranks push in softer, so a crowd floods and surrounds a target
    /// instead of the back compressing the front through it. Same-objective only
    /// (an unrelated monster crossing my path isn't "the rank ahead"), and only
    /// neighbors actually in front of me toward where I'm steering count.
    /// </summary>
    private float ForwardCrowdPressure(Vector2 seekDir)
    {
        if (seekDir.sqrMagnitude < 0.0001f) return 0f;

        float radiusSqr = giveWayRadius * giveWayRadius;
        float pressure = 0f;
        for (int i = 0; i < neighborCount; i++)
        {
            var collider = NeighborBuffer[i];
            if (collider == null || collider.gameObject == gameObject) continue;
            var other = collider.GetComponentInParent<MonsterAI>();
            if (other == null || other.currentTarget != currentTarget) continue; // same objective only

            Vector2 toOther = (Vector2)other.transform.position - (Vector2)transform.position;
            float distSqr = toOther.sqrMagnitude;
            if (distSqr < 0.0001f || distSqr > radiusSqr) continue;

            float dist = Mathf.Sqrt(distSqr);
            float ahead = Vector2.Dot(toOther / dist, seekDir); // 1 = directly ahead of me toward the target
            if (ahead > 0.25f)
                pressure += ahead * (1f - dist / giveWayRadius); // nearer + more-ahead blocks more
        }
        return pressure;
    }

    /// <summary>
    /// The opposite of ForwardCrowdPressure: how much real crowd is pressing in
    /// from BEHIND me, still trying to reach the SAME target I'm defending or
    /// approaching. Uses the same target-relative "behind" geometry as
    /// GiveWayVelocity/AllyQueuedBehind (a cone on the far side of me from the
    /// target) rather than my instantaneous heading, since that's a more stable
    /// read of "who's counting on me making room" while I might be jittering in
    /// place at a pinch point.
    ///
    /// Deliberately does NOT filter out a neighbor just because ITS OWN
    /// IsPlaced()/settledAtTarget reads true — a first version did, and that's
    /// exactly the bug this whole feature exists to route around: with slots
    /// off, "arrived" is a loose straight-line distance check, and an entire
    /// cluster jammed at a doorway can ALL satisfy it simultaneously (everyone's
    /// within straight-line attack range of the King even though nobody's
    /// actually gotten anywhere) — which made every monster read every other
    /// monster as "already placed" and exclude it, so pressure came out ~0 for
    /// the whole group, in precisely the scenario meant to detect it.
    ///
    /// Instead, reliability comes from tight PROXIMITY: this only counts a
    /// same-objective neighbor within separationRadius (not the wider
    /// giveWayRadius), because that's evidence a plain "arrived" check can't
    /// fake. A comfortably-settled, non-jammed group spaces itself out beyond
    /// separation's own push radius once the forces balance — two same-target
    /// bodies staying persistently THIS close only happens when there's
    /// genuinely nowhere for separation to push them apart to, i.e. a real jam.
    /// Compares against currentTarget (each monster's real objective, always
    /// accurate) rather than the target parameter (which can be a wall mid-break
    /// for either monster), matching ForwardCrowdPressure's convention.
    /// See rearPressureThreshold's tooltip for what MoveToward does with this.
    /// </summary>
    private float RearCrowdPressure(Transform target)
    {
        Vector2 approach = ApproachPoint(target);
        Vector2 radialOut = (Vector2)transform.position - approach;
        if (radialOut.sqrMagnitude < 0.0001f) return 0f;
        radialOut.Normalize();

        float radiusSqr = separationRadius * separationRadius;
        float pressure = 0f;
        for (int i = 0; i < neighborCount; i++)
        {
            var collider = NeighborBuffer[i];
            if (collider == null || collider.gameObject == gameObject) continue;
            var other = collider.GetComponentInParent<MonsterAI>();
            if (other == null || other.currentTarget != currentTarget) continue;

            Vector2 toOther = (Vector2)other.transform.position - (Vector2)transform.position;
            float distSqr = toOther.sqrMagnitude;
            if (distSqr < 0.0001f || distSqr > radiusSqr) continue;

            float dist = Mathf.Sqrt(distSqr);
            float behind = Vector2.Dot(toOther / dist, radialOut); // 1 = directly behind me, target-relative
            if (behind > 0.5f)
                pressure += behind * (1f - dist / separationRadius);
        }
        return pressure;
    }

    /// <summary>
    /// Full target selection, in strict priority order (no distance-based
    /// tiebreak between them — each rule either applies or it doesn't):
    /// 0. Recent-player-combat guard — if this monster attacked, or was
    ///    attacked by, the player within Recent Player Combat Window
    ///    seconds, structure-priority is skipped entirely this frame (both
    ///    tiers below), keeping it engaged with the player. NOT retroactive
    ///    in general: if the monster was ALREADY committed to a specific
    ///    structure target (committedStructureTarget), this guard normally
    ///    doesn't apply — getting hit mid-structure-attack doesn't pull it
    ///    back off. EXCEPTION: if it's also STUCK — physically blocked
    ///    (e.g. trapped behind another monster) from ever reaching that
    ///    committed structure, so it isn't actually accomplishing anything
    ///    by staying committed — the guard applies anyway, since there's no
    ///    real cost to switching to the player it can actually reach.
    /// 1a. Structure Priority Range (hard cutoff) — unconditional, always
    ///     wins outright when a structure is within range, beating both the
    ///     player AND the King. Cyclops's original behavior, unaffected by
    ///     anything else here.
    /// 1b. Structure Interest Range — a softer pull: prefer a structure over
    ///     heading to the King, but this ONLY ever competes with heading to
    ///     the King — if the base choice is already the player, this tier is
    ///     skipped entirely, no exceptions. The candidate must also be
    ///     closer to the King than this monster currently is (DistanceBetween),
    ///     so a detour can only ever be a step TOWARD the King, never
    ///     sideways or backward around the map.
    /// Both structure tiers also skip any candidate within Structure Near
    /// King Range of the King — too close to the King to bother attacking,
    /// go for the King directly instead.
    /// 2. King-priority — only ever a tiebreaker against CHASING THE PLAYER.
    ///    If a structure already won above, or the base choice wasn't the
    ///    player anyway, this never comes into play.
    /// 3. The base choice (PickTarget): player if in range, else the King.
    /// </summary>
    private Transform ChooseTarget(GameManager gm)
    {
        Transform baseTarget = PickTarget(gm);
        bool recentCombat = HasRecentPlayerCombat();

        bool kingInPriorityRange = gm.King != null && definition.kingPriorityRange > 0f &&
            DistanceToTarget(gm.King) <= definition.kingPriorityRange;

        // King-over-structures override: if enabled and the King is within
        // King Priority Range, the King beats structure-priority entirely.
        // Still yields to recent player combat UNLESS the monster is already
        // essentially on the King (within Keep Target range).
        if (definition.kingPriorityBeatsStructures && kingInPriorityRange &&
            !(recentCombat && !WithinKeepRange(gm.King)))
        {
            committedStructureTarget = null;
            return gm.King;
        }

        // "Stuck" = still committed to a structure, still outside attack
        // range of it (so not just successfully pressed up against it and
        // attacking), but actual physics velocity is near zero — meaning
        // something else (typically another monster) is blocking the way
        // and no progress is actually being made.
        bool isStuckOnCommittedStructure = committedStructureTarget != null &&
            rb.linearVelocity.sqrMagnitude < stuckVelocityThreshold * stuckVelocityThreshold &&
            DistanceToTarget(committedStructureTarget) > definition.attackRange;

        // Recent combat pulls the monster to the player instead of a
        // structure — but NOT if it's already committed (and not stuck), and
        // NOT if it's within Keep Target range of that structure (see below,
        // checked per-candidate since it needs the specific structure).
        bool recentCombatOverridesStructure = recentCombat &&
            (committedStructureTarget == null || isStuckOnCommittedStructure);

        if (definition.structurePriorityRange > 0f)
        {
            var closeStructure = NearestStructureWithin(definition.structurePriorityRange);
            if (closeStructure != null && !IsNearKing(closeStructure, gm) &&
                !(recentCombatOverridesStructure && !WithinKeepRange(closeStructure)))
            {
                committedStructureTarget = closeStructure;
                return closeStructure;
            }
        }

        if (baseTarget != gm.Player && definition.structureInterestRange > 0f && gm.King != null)
        {
            var nearbyStructure = NearestStructureWithin(definition.structureInterestRange);
            if (nearbyStructure != null && !IsNearKing(nearbyStructure, gm) &&
                !(recentCombatOverridesStructure && !WithinKeepRange(nearbyStructure)))
            {
                float structureDistanceToKing = DistanceBetween(nearbyStructure, gm.King);
                float monsterDistanceToKing = DistanceToTarget(gm.King);
                if (structureDistanceToKing < monsterDistanceToKing)
                {
                    committedStructureTarget = nearbyStructure;
                    return nearbyStructure;
                }
            }
        }

        committedStructureTarget = null;

        // King-Priority (vs chasing the player): also holds off during recent
        // player combat — otherwise a monster at the King's doorstep would get
        // yanked back to the King every frame even while actively fighting the
        // player hitting it. Exception: if it's within Keep Target range of the
        // King, it ignores the player aggro and stays on the King.
        if (baseTarget == gm.Player && kingInPriorityRange &&
            !(recentCombat && !WithinKeepRange(gm.King)))
            return gm.King;

        return baseTarget;
    }

    /// <summary>
    /// True if this monster is within Keep Target Within Range tiles (edge to
    /// edge) of the given target — used so recent-player-combat aggro can't
    /// pull a monster off a King/structure it's essentially already on.
    /// </summary>
    private bool WithinKeepRange(Transform target) =>
        definition.keepTargetWithinRange > 0f && target != null &&
        DistanceToTarget(target) <= definition.keepTargetWithinRange;

    /// <summary>
    /// True if the given structure is within Structure Near King Range of
    /// the King — used so a monster doesn't bother attacking something
    /// built right at the King's doorstep and goes straight for the King
    /// instead.
    /// </summary>
    private bool IsNearKing(Transform structure, GameManager gm) =>
        definition.structureNearKingRange > 0f && gm.King != null &&
        DistanceBetween(structure, gm.King) <= definition.structureNearKingRange;

    private Transform PickTarget(GameManager gm)
    {
        if (definition.targetsOnlyKing)
        {
            // Goblin: a Praise the King Tower within lure range wins over the King.
            if (definition.praiseTowerLureRange > 0f)
            {
                var hits = Physics2D.OverlapCircleAll(transform.position, definition.praiseTowerLureRange, structureLayers);
                Transform bestLure = null;
                float bestSqrDistance = float.MaxValue;
                foreach (var hit in hits)
                {
                    if (hit.GetComponentInParent<PraiseTheKingTower>() == null) continue;
                    float sqrDistance = ((Vector2)(hit.transform.position - transform.position)).sqrMagnitude;
                    if (sqrDistance < bestSqrDistance)
                    {
                        bestSqrDistance = sqrDistance;
                        bestLure = hit.transform;
                    }
                }
                if (bestLure != null) return bestLure;
            }
            return gm.King;
        }

        var player = gm.Player;
        bool playerAlive = gm.PlayerHealth != null && !gm.PlayerHealth.IsDead;
        if (player != null && playerAlive && definition.playerTargetRange > 0f &&
            Vector2.Distance(transform.position, player.position) <= definition.playerTargetRange)
            return player;

        return gm.King;
    }

    /// <summary>
    /// Nearest structure whose collider-to-collider GAP (not center distance,
    /// same edge-to-edge measure as DistanceToTarget) is within radius.
    /// Query a generously wide circle first (cheap broad-phase, still
    /// center-based but only used to shortlist candidates), then rank by the
    /// real edge distance so this agrees with the main attack-range check —
    /// otherwise a monster could think a big structure is "blocking" long
    /// before it's actually within its real attack range, or vice versa.
    ///
    /// Player-built Walls and Gates (anything with a Barrier component) are
    /// deliberately NOT candidates here. This feeds every DISCRETIONARY reason
    /// to attack a structure — the priority/interest ranges, and the
    /// "something's in my way" check — and barriers are exactly the thing a
    /// monster is supposed to route around rather than attack. Letting them in
    /// would have monsters grinding down the sides of a corridor simply for
    /// walking through it, which would make mazes pointless. A barrier only
    /// ever becomes a target through the no-route-exists fallback (§6,
    /// "breakable if no path around it"). A future monster designed to smash
    /// walls on sight would opt back in here rather than change that rule.
    /// </summary>
    private Transform NearestStructureWithin(float radius)
    {
        float queryRadius = radius + 3f; // margin generous enough to catch a 2x2 structure's far edge
        var hits = Physics2D.OverlapCircleAll(transform.position, queryRadius, structureLayers);
        Transform best = null;
        float bestDistance = float.MaxValue;
        foreach (var hit in hits)
        {
            if (hit.GetComponentInParent<Barrier>() != null) continue;
            float distance = DistanceBetween(transform, hit.transform);
            if (distance > radius) continue;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = hit.transform;
            }
        }
        return best;
    }

    private void TryAttack(Transform target, GameManager gm)
    {
        if (Time.time < nextAttackTime) return;
        nextAttackTime = Time.time + definition.attackInterval;

        if (gm != null && target == gm.Player)
            lastAttackedPlayerTime = Time.time;

        var targetHealth = target.GetComponentInParent<Health>();
        if (targetHealth != null)
        {
            targetHealth.TakeDamage(DamageForHealth(targetHealth, target, gm));
            // Knockback/stun on top of damage (off unless enabled on the definition).
            // Only the player has a KnockbackReceiver, so this is a no-op vs the
            // King/structures; the shove pushes the player away from this monster.
            definition.attackEffects.ApplyTo(target, transform.position);
            lastHitTime = Time.time;
            lastHitPosition = ApproachPoint(target); // debug-only, for the hit flash gizmo
        }
    }

    /// <summary>
    /// True if this monster attacked the player, or the player attacked it,
    /// within Recent Player Combat Window seconds. Checked in ChooseTarget to
    /// keep a monster engaged with the player instead of letting it switch to
    /// a nearby structure mid-fight.
    /// </summary>
    private bool HasRecentPlayerCombat()
    {
        float window = definition.recentPlayerCombatWindow;
        if (window <= 0f) return false;

        if (Time.time - lastAttackedPlayerTime <= window) return true;
        if (health.LastDamageFromPlayer && Time.time - health.LastDamageTime <= window) return true;
        return false;
    }

    /// <summary>
    /// Damage this monster deals to a given target's Health. Compares against
    /// the King/Player Health objects (robust whether the hit landed on the
    /// root or a child collider). King Damage is its own value — always
    /// used, never a fallback to Player Damage — so a monster can hurt the
    /// King more or less than it hurts the player or structures.
    /// </summary>
    private float DamageForHealth(Health targetHealth, Transform hitTransform, GameManager gm)
    {
        if (gm != null && targetHealth == gm.KingHealth)
            return definition.kingDamage;

        if (gm != null && targetHealth == gm.PlayerHealth)
            return definition.playerDamage;

        if (definition.wallDamage > 0f && hitTransform.GetComponentInParent<Barrier>() != null)
            return definition.wallDamage;

        if (definition.praiseTowerDamage > 0f && hitTransform.GetComponentInParent<PraiseTheKingTower>() != null)
            return definition.praiseTowerDamage;

        return definition.structureDamage;
    }

    private void OnDied(Health _)
    {
        // Free any slot immediately so a living monster can take it, whether
        // this is a temporary bone-pile death or the final one.
        ReleaseSlot();

        // Skeleton rule (§7.3): first death becomes an invulnerable bone pile
        // that revives — only the final death pays out and removes the monster.
        if (livesRemaining > 0)
        {
            livesRemaining--;
            StartCoroutine(BonePileRoutine());
            return;
        }

        if (currencyDropPrefab != null && definition != null)
        {
            var drop = Instantiate(currencyDropPrefab, transform.position, Quaternion.identity);
            drop.SetValue(definition.currencyDrop);
        }

        Killed?.Invoke(this);
        Destroy(gameObject);
    }

    private IEnumerator BonePileRoutine()
    {
        bonePileActive = true;
        health.Invulnerable = true;

        // Freeze physics entirely: rb.simulated = false zeroes its velocity,
        // stops it being shoved by other monsters, AND removes it from
        // physics queries so towers/the sword don't target the pile. This is
        // what actually keeps it from drifting/creeping while "invulnerable".
        rb.simulated = false;
        foreach (var col in GetComponentsInChildren<Collider2D>())
            col.enabled = false;

        // Hide the health bar while piled (otherwise a 0-width sliver lingers),
        // and show the squashed, recolored bone pile.
        SetHealthBarsVisible(false);
        if (body != null) body.color = definition.bonePileColor;
        transform.localScale = new Vector3(activeScale.x, activeScale.y * 0.35f, activeScale.z);

        yield return new WaitForSeconds(definition.reviveDelaySeconds);

        transform.localScale = activeScale;
        if (body != null) body.color = definition.bodyColor;
        foreach (var col in GetComponentsInChildren<Collider2D>())
            col.enabled = true;
        rb.simulated = true;
        health.ResetToFull();
        health.Invulnerable = false;
        SetHealthBarsVisible(true);
        bonePileActive = false;
    }

    private void SetHealthBarsVisible(bool visible)
    {
        foreach (var bar in GetComponentsInChildren<HealthBar>(true))
            bar.gameObject.SetActive(visible);
    }

    /// <summary>
    /// Debug-only, behind PathGrid's Draw Targeting Debug toggle: a thin line
    /// to whatever this monster is currently aiming at — yellow for the
    /// player, cyan for anything else (King, tower, wall) — plus a brief red
    /// flash right at the point of impact the instant a hit actually lands
    /// (fading out over HitFlashDuration, not a permanent marker). Answers
    /// "is this monster targeting/hitting the player or a structure" by eye,
    /// without needing to click through the Inspector while it's playing.
    /// </summary>
    private void OnDrawGizmos()
    {
        var grid = PathGrid.Instance;
        if (grid == null || !grid.DrawTargetingDebug || currentTarget == null) return;

        var gm = GameManager.Instance;
        bool targetingPlayer = gm != null && currentTarget == gm.Player;
        Gizmos.color = targetingPlayer ? new Color(1f, 0.9f, 0.2f, 0.6f) : new Color(0.2f, 0.8f, 1f, 0.5f);
        Gizmos.DrawLine(transform.position, ApproachPoint(currentTarget));

        if (Time.time - lastHitTime < HitFlashDuration)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(lastHitPosition, 0.25f);
        }
    }
}
