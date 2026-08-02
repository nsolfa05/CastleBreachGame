using UnityEngine;

/// <summary>
/// Marker for a SpriteRenderer that a combat effect (a charge-indicator bar,
/// a swing flash) owns and toggles itself. PlayerRespawn's "make everything
/// visible again" pass on respawn skips anything carrying this — forcing it
/// back on would show whatever on/off state it was frozen in at the exact
/// moment of death (e.g. a charge bar stuck mid-fill), instead of staying
/// hidden until the effect legitimately triggers again.
/// </summary>
public class CombatFxVisual : MonoBehaviour
{
}
