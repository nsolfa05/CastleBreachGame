using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns a fixed list of waves (design doc §8 — campaign levels have a
/// fixed number of waves). Monsters spawn only on gate tiles (§3.3), pulled
/// from the CastleMapGenerator's gate regions. When the last wave is cleared
/// the GameManager is told the level is won.
///
/// The wave list is plain Inspector data — tweak counts, pacing, and gates
/// without touching code.
/// </summary>
public class WaveSpawner : MonoBehaviour
{
    public enum GateSide { North, South, East, West }

    [Serializable]
    public class Wave
    {
        public string name = "Wave";

        [Tooltip("Gates this wave spawns from — spawns rotate through the list.")]
        public List<GateSide> gates = new List<GateSide> { GateSide.West };

        public int zombieCount = 5;

        [Tooltip("Seconds between individual spawns.")]
        public float secondsBetweenSpawns = 1.5f;
    }

    [Header("References")]
    [SerializeField] private CastleMapGenerator map;
    [SerializeField] private ZombieAI zombiePrefab;

    [Header("Pacing")]
    [SerializeField] private float delayBeforeFirstWave = 5f;
    [SerializeField] private float delayBetweenWaves = 8f;

    [Header("Waves")]
    [SerializeField] private List<Wave> waves = new List<Wave>
    {
        new Wave { name = "Wave 1", zombieCount = 4,
                   gates = new List<GateSide> { GateSide.West } },
        new Wave { name = "Wave 2", zombieCount = 6,
                   gates = new List<GateSide> { GateSide.West, GateSide.East } },
        new Wave { name = "Wave 3", zombieCount = 10,
                   gates = new List<GateSide> { GateSide.North, GateSide.South, GateSide.East, GateSide.West } },
    };

    /// <summary>1-based; 0 means the first wave hasn't started yet.</summary>
    public int CurrentWaveNumber { get; private set; }
    public int TotalWaves => waves.Count;
    public int AliveCount { get; private set; }

    private void Start()
    {
        StartCoroutine(RunWaves());
    }

    private IEnumerator RunWaves()
    {
        yield return new WaitForSeconds(delayBeforeFirstWave);

        for (int i = 0; i < waves.Count; i++)
        {
            CurrentWaveNumber = i + 1;
            var wave = waves[i];

            for (int s = 0; s < wave.zombieCount; s++)
            {
                SpawnZombie(wave.gates[s % wave.gates.Count]);
                yield return new WaitForSeconds(wave.secondsBetweenSpawns);
            }

            yield return new WaitUntil(() => AliveCount == 0);

            if (i < waves.Count - 1)
                yield return new WaitForSeconds(delayBetweenWaves);
        }

        if (GameManager.Instance != null)
            GameManager.Instance.ReportAllWavesCleared();
    }

    private void SpawnZombie(GateSide side)
    {
        Vector2 position = RandomGateTileCenter(side);
        var zombie = Instantiate(zombiePrefab, position, Quaternion.identity);
        AliveCount++;
        zombie.GetComponent<Health>().Died += _ => AliveCount--;
    }

    private Vector2 RandomGateTileCenter(GateSide side)
    {
        if (map != null)
        {
            foreach (var region in map.GateRegions)
            {
                if (!string.Equals(region.name, side.ToString(), StringComparison.OrdinalIgnoreCase))
                    continue;

                var tiles = new List<Vector2Int>(region.Tiles());
                if (tiles.Count == 0) break;
                var tile = tiles[UnityEngine.Random.Range(0, tiles.Count)];
                return GridMath.TileCenterWorld(tile);
            }
        }

        Debug.LogWarning($"WaveSpawner: no gate region named '{side}' found on the map generator — spawning at map center.");
        return GridMath.MapCenterWorld;
    }
}
