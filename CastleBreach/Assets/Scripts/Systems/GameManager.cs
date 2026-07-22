using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum GameState { Playing, Won, Lost }

/// <summary>
/// Central game state: references to the player and King, the gold wallet
/// (design doc §4), and the win/lose conditions (§10.2). Lose = King dies;
/// win = the WaveSpawner reports all waves cleared. Press R after either
/// to restart the level.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Scene references (drag from the Hierarchy)")]
    [SerializeField] private Transform player;
    [SerializeField] private Health playerHealth;
    [SerializeField] private Transform king;
    [SerializeField] private Health kingHealth;

    [Header("Economy (§4)")]
    [Tooltip("Gold at the start of the level. 200 is enough for one Archer Tower while testing.")]
    [SerializeField] private int startingGold = 200;

    public Transform Player => player;
    public Health PlayerHealth => playerHealth;
    public Transform King => king;
    public Health KingHealth => kingHealth;

    public GameState State { get; private set; } = GameState.Playing;
    public int Gold { get; private set; }

    private void Awake()
    {
        Instance = this;
        Gold = startingGold;
        Time.timeScale = 1f; // in case the previous run ended on the frozen win/lose screen
    }

    private void OnEnable()
    {
        if (kingHealth != null) kingHealth.Died += OnKingDied;
    }

    private void OnDisable()
    {
        if (kingHealth != null) kingHealth.Died -= OnKingDied;
        if (Instance == this) Instance = null;
    }

    private void OnKingDied(Health _) => EndGame(GameState.Lost);

    /// <summary>Called by the WaveSpawner when the last wave is cleared (§10.2).</summary>
    public void ReportAllWavesCleared() => EndGame(GameState.Won);

    private void EndGame(GameState result)
    {
        if (State != GameState.Playing) return;
        State = result;
        Time.timeScale = 0f; // freeze the battlefield behind the result message
    }

    public bool TrySpendGold(int amount)
    {
        if (Gold < amount) return false;
        Gold -= amount;
        return true;
    }

    public void AddGold(int amount) => Gold += amount;

    private void Update()
    {
        if (State == GameState.Playing) return;

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
