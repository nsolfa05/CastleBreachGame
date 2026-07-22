using UnityEngine;

/// <summary>
/// Removes the object from the scene when its Health reaches 0.
/// Used by structures (towers, later walls/gates). Monsters handle their own
/// death (they need to drop currency first), and the player respawns instead.
/// </summary>
[RequireComponent(typeof(Health))]
public class DestroyWhenDead : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Health>().Died += _ => Destroy(gameObject);
    }
}
