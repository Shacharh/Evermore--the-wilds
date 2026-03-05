using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Singleton that drives the Player <-> Enemy turn loop.
/// Attach to a persistent GameManager GameObject in the scene.
/// </summary>
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    // -- Controllers -----------------------------------------------------------

    [Header("Controllers")]
    [SerializeField] private PlayerTurnController playerController;
    [SerializeField] private EnemyTurnController enemyController;

    // -- State -----------------------------------------------------------------

    public enum TurnOwner { Player, Enemy }
    public TurnOwner CurrentTurn { get; private set; }

    public bool IsPlayerTurn => CurrentTurn == TurnOwner.Player;
    public bool IsEnemyTurn => CurrentTurn == TurnOwner.Enemy;

    // -- Turn Counter ----------------------------------------------------------

    public int TurnNumber { get; private set; } = 1;

    // -- Events ----------------------------------------------------------------

    [Header("Events")]
    public UnityEvent onPlayerTurnStart;
    public UnityEvent onEnemyTurnStart;
    public UnityEvent<int> onNewRound;   // fires every full Player+Enemy cycle

    // -- Timing ----------------------------------------------------------------

    [Header("Timing")]
    [Tooltip("Seconds to wait between turn handoffs (for animations / UI).")]
    [SerializeField] private float transitionDelay = 0.5f;

    private bool transitionInProgress = false;

    // -- Lifecycle -------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        StartCoroutine(BeginGame());
    }

    private IEnumerator BeginGame()
    {
        yield return new WaitForEndOfFrame(); // let all Start()s finish
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

    // -- Internal Flow ---------------------------------------------------------

    private void BeginTurn(TurnOwner owner)
    {
        CurrentTurn = owner;

        if (owner == TurnOwner.Player)
        {
            Debug.Log($"[TurnManager] == PLAYER TURN {TurnNumber} ==");
            playerController.StartTurn();
            onPlayerTurnStart?.Invoke();
        }
        else
        {
            Debug.Log($"[TurnManager] == ENEMY TURN {TurnNumber} ==");
            enemyController.StartTurn();
            onEnemyTurnStart?.Invoke();
        }
    }

    private IEnumerator Transition()
    {
        transitionInProgress = true;

        // End active controller
        ActiveController.EndTurn();

        // Increment round counter when the enemy's turn ends (full cycle complete)
        if (IsEnemyTurn)
        {
            TurnNumber++;
            onNewRound?.Invoke(TurnNumber);
        }

        yield return new WaitForSeconds(transitionDelay);

        TurnOwner next = IsPlayerTurn ? TurnOwner.Enemy : TurnOwner.Player;
        transitionInProgress = false;
        BeginTurn(next);
    }
}