using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Singleton that drives the Player ↔ Enemy turn loop.
///
/// AUTO-SETUP — No manual scene work needed:
///   • If TurnManager is not in the scene, it creates itself automatically.
///   • If PlayerTurnController / EnemyTurnController are missing, they are
///     created automatically and will discover their monsters from the scene.
///
/// OPTIONAL manual setup (gives Inspector control over AP, monster rosters, etc.):
///   1. Create an empty GameObject → add TurnManager.
///   2. Create two more GameObjects → add PlayerTurnController and EnemyTurnController.
///   3. Drag those controllers into TurnManager's inspector fields.
///   4. Assign monsters to each controller's Monsters list.
/// </summary>
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    // -- Controllers -----------------------------------------------------------

    [Header("Controllers  (auto-created if left empty)")]
    [SerializeField] private PlayerTurnController playerController;
    [SerializeField] private EnemyTurnController  enemyController;

    // -- State -----------------------------------------------------------------

    public enum TurnOwner { Player, Enemy }
    public TurnOwner CurrentTurn { get; private set; }

    public bool IsPlayerTurn => CurrentTurn == TurnOwner.Player;
    public bool IsEnemyTurn  => CurrentTurn == TurnOwner.Enemy;

    // -- Turn Counter ----------------------------------------------------------

    public int TurnNumber { get; private set; } = 1;

    // -- Events ----------------------------------------------------------------

    [Header("Events")]
    public UnityEvent    onPlayerTurnStart;
    public UnityEvent    onEnemyTurnStart;
    public UnityEvent<int> onNewRound;   // fires every full Player+Enemy cycle

    // -- Timing ----------------------------------------------------------------

    [Header("Timing")]
    [Tooltip("Seconds to wait between turn handoffs (for animations / UI).")]
    [SerializeField] private float transitionDelay = 0.5f;

    private bool transitionInProgress = false;

    // -- Auto-Create -----------------------------------------------------------

    /// <summary>
    /// Ensures TurnManager exists even if it was not placed in the scene manually.
    /// Fires after the scene loads — safe to have it in the scene too (duplicate
    /// Awake check will destroy the extra instance).
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        if (FindFirstObjectByType<TurnManager>() != null) return; // already in scene
        new GameObject("TurnManager").AddComponent<TurnManager>();
        Debug.Log("[TurnManager] Auto-created (was not in scene).");
    }

    // -- Lifecycle -------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        EnsureControllers();
    }

    private void Start()
    {
        StartCoroutine(BeginGame());
    }

    // -- Game Start Sequence ---------------------------------------------------

    private IEnumerator BeginGame()
    {
        // MonsterSpawner uses Invoke(0.2 s) to spawn — wait long enough so all
        // monsters exist and their Start() methods have run before we discover them.
        yield return new WaitForSeconds(0.5f);

        // Auto-discover monsters (only fills rosters that were left empty in Inspector)
        playerController?.AutoDiscoverMonsters();
        enemyController?.AutoDiscoverMonsters();

        BeginTurn(TurnOwner.Player);
    }

    // -- Public API ------------------------------------------------------------

    /// <summary>
    /// Ends the current turn and hands control to the other side.
    /// Safe to call from both controllers. Ignores calls during a transition.
    /// </summary>
    public void ForceEndTurn()
    {
        if (transitionInProgress) return;
        StartCoroutine(Transition());
    }

    public TurnController ActiveController =>
        IsPlayerTurn ? (TurnController)playerController : enemyController;

    public PlayerTurnController PlayerController => playerController;
    public EnemyTurnController  EnemyController  => enemyController;

    // -- Internal Flow ---------------------------------------------------------

    private void BeginTurn(TurnOwner owner)
    {
        CurrentTurn = owner;

        if (owner == TurnOwner.Player)
        {
            Debug.Log($"[TurnManager] == PLAYER TURN {TurnNumber} ==");
            playerController?.StartTurn();
            onPlayerTurnStart?.Invoke();
        }
        else
        {
            Debug.Log($"[TurnManager] == ENEMY TURN {TurnNumber} ==");
            enemyController?.StartTurn();
            onEnemyTurnStart?.Invoke();
        }
    }

    private IEnumerator Transition()
    {
        transitionInProgress = true;

        ActiveController?.EndTurn();

        if (IsEnemyTurn)
        {
            TurnNumber++;
            onNewRound?.Invoke(TurnNumber);
        }

        yield return new WaitForSeconds(transitionDelay);

        TurnOwner next       = IsPlayerTurn ? TurnOwner.Enemy : TurnOwner.Player;
        transitionInProgress = false;
        BeginTurn(next);
    }

    // -- Controller Setup Helpers ----------------------------------------------

    private void EnsureControllers()
    {
        // Auto-find in scene if not assigned in Inspector
        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerTurnController>();
        if (enemyController == null)
            enemyController  = FindFirstObjectByType<EnemyTurnController>();

        // Auto-create if still missing
        if (playerController == null)
        {
            playerController = new GameObject("PlayerTurnController")
                .AddComponent<PlayerTurnController>();
            Debug.Log("[TurnManager] Auto-created PlayerTurnController.");
        }
        if (enemyController == null)
        {
            enemyController = new GameObject("EnemyTurnController")
                .AddComponent<EnemyTurnController>();
            Debug.Log("[TurnManager] Auto-created EnemyTurnController.");
        }
    }
}
