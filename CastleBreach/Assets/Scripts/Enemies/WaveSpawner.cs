using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns a fixed list of waves (design doc §8 — campaign levels have a fixed
/// number of waves). Each wave is a list of SPAWN GROUPS: (which monster,
/// how many, from which gate, how fast, after what delay) — groups in the
/// same wave run in parallel, so "3 Zombies from the West while 2 Goblins
/// rush the South gate" is one wave with two groups.
///
/// Monsters spawn only on gate tiles (§3.3), pulled from the
/// CastleMapGenerator's gate regions. All monster types share ONE generic
/// Monster prefab — each group picks which MonsterDefinition asset to stamp
/// onto it. When the last wave is cleared the GameManager is told the level
/// is won.
/// </summary>
public class WaveSpawner : MonoBehaviour
{
    public enum GateSide { North, South, East, West }

    [Serializable]
    public class SpawnGroup
    {
        [Tooltip("Which monster type — a MonsterDefinition asset from Assets/Monsters.")]
        public MonsterDefinition monster;

        public int count = 4;
        public GateSide gate = GateSide.West;

        [Tooltip("Seconds between individual spawns in this group.")]
        public float secondsBetweenSpawns = 1.5f;

        [Tooltip("Seconds after the wave starts before this group begins spawning — for staggered assaults.")]
        public float startDelay = 0f;
    }

    [Serializable]
    public class Wave
    {
        public string name = "Wave";
        public List<SpawnGroup> groups = new List<SpawnGroup>();
    }

    [Header("References")]
    [SerializeField] private CastleMapGenerator map;

    [Tooltip("The ONE generic Monster prefab — each spawn stamps a group's MonsterDefinition onto a fresh copy of it.")]
    [SerializeField] private MonsterAI monsterPrefab;

    [Header("Pacing")]
    [SerializeField] private float delayBeforeFirstWave = 5f;
    [SerializeField] private float delayBetweenWaves = 8f;

    [Header("Waves")]
    [SerializeField] private List<Wave> waves = new List<Wave>
    {
        new Wave { name = "Wave 1", groups = new List<SpawnGroup> {
            new SpawnGroup { count = 4, gate = GateSide.West },
        } },
        new Wave { name = "Wave 2", groups = new List<SpawnGroup> {
            new SpawnGroup { count = 3, gate = GateSide.West },
            new SpawnGroup { count = 3, gate = GateSide.East },
        } },
        new Wave { name = "Wave 3", groups = new List<SpawnGroup> {
            new SpawnGroup { count = 3, gate = GateSide.North },
            new SpawnGroup { count = 3, gate = GateSide.South },
            new SpawnGroup { count = 3, gate = GateSide.East, startDelay = 4f },
            new SpawnGroup { count = 3, gate = GateSide.West, startDelay = 4f },
        } },
    };

    /// <summary>1-based; 0 means the first wave hasn't started yet.</summary>
    public int CurrentWaveNumber { get; private set; }
    public int TotalWaves => waves.Count;
    public int AliveCount { get; private set; }

    private int activeGroups;

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

            activeGroups = 0;
            foreach (var group in wave.groups)
            {
                activeGroups++;
                StartCoroutine(RunGroup(group));
            }

            // Wave is over when every group finished spawning AND everything is dead.
            yield return new WaitUntil(() => activeGroups == 0);
            yield return new WaitUntil(() => AliveCount == 0);

            if (i < waves.Count - 1)
                yield return new WaitForSeconds(delayBetweenWaves);
        }

        if (GameManager.Instance != null)
            GameManager.Instance.ReportAllWavesCleared();
    }

    private IEnumerator RunGroup(SpawnGroup group)
    {
        if (group.monster == null)
        {
            Debug.LogWarning("WaveSpawner: a spawn group has no MonsterDefinition assigned — skipping it.");
            activeGroups--;
            yield break;
        }

        if (group.startDelay > 0f)
            yield return new WaitForSeconds(group.startDelay);

        for (int s = 0; s < group.count; s++)
        {
            SpawnMonster(group.monster, group.gate);
            yield return new WaitForSeconds(group.secondsBetweenSpawns);
        }

        activeGroups--;
    }

    private void SpawnMonster(MonsterDefinition definition, GateSide side)
    {
        Vector2 position = RandomGateTileCenter(side);
        var monster = Instantiate(monsterPrefab, position, Quaternion.identity);
        monster.gameObject.SetActive(true); // guard against the prefab being saved inactive
        monster.SetDefinition(definition);
        AliveCount++;
        monster.Killed += _ => AliveCount--;
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
