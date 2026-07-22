using System.Collections;
using UnityEngine;

/// <summary>
/// Design doc §10.1: on reaching 0 HP the player disappears and respawns at
/// their starting position after a tunable delay. (The "keep or lose upgrades
/// on death" toggle arrives together with the upgrade system, post-slice.)
/// </summary>
[RequireComponent(typeof(Health))]
public class PlayerRespawn : MonoBehaviour
{
    [Tooltip("Seconds until respawn (design doc §10.1 — tunable).")]
    [SerializeField] private float respawnDelay = 3f;

    private Health health;
    private Vector3 spawnPosition;

    private void Awake()
    {
        health = GetComponent<Health>();
        spawnPosition = transform.position;
        health.Died += OnDied;
    }

    private void OnDied(Health _)
    {
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        SetAlive(false);
        yield return new WaitForSeconds(respawnDelay);
        transform.position = spawnPosition;
        health.ResetToFull();
        SetAlive(true);
    }

    private void SetAlive(bool alive)
    {
        foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true))
            renderer.enabled = alive;
        foreach (var collider in GetComponentsInChildren<Collider2D>(true))
            collider.enabled = alive;

        var movement = GetComponent<PlayerMovement>();
        if (movement != null) movement.MovementLocked = !alive;

        var attack = GetComponent<PlayerAttack>();
        if (attack != null) attack.enabled = alive;
    }
}
