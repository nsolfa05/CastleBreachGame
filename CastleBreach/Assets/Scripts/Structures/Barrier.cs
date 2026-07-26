using UnityEngine;

/// <summary>
/// Marks a structure as a player-built barrier — a Wall or a Gate (design doc
/// §6) — rather than a building like a tower. PathGrid reads this to answer two
/// questions it cannot get from a collider alone:
///
/// - Is this a Gate? Gates are solid to most monsters but open to any monster
///   whose definition has Passes Through Gates (the Goblin). Unity's layer
///   collision matrix cannot express "block this layer except these specific
///   members" — every monster shares the Enemy layer — so the exception has to
///   be decided in routing code, which is what this flag drives.
/// - Is this a barrier at all? A future flying monster (Flies Over Barriers)
///   passes over Walls and Gates but is still stopped by towers and the King,
///   and the presence of this component is what separates the two.
///
/// Add it to the Wall and Gate prefabs only. Everything else about them —
/// Health, collider, DestroyWhenDead, the Structure layer — is exactly the
/// same setup every other structure already uses.
/// </summary>
public class Barrier : MonoBehaviour
{
    [Tooltip("Gate: monsters whose definition has Passes Through Gates (the Goblin) walk straight through this, everything else is blocked by it. Leave unchecked for a plain Wall, which blocks every monster.")]
    [SerializeField] private bool isGate = false;

    public bool IsGate => isGate;
}
