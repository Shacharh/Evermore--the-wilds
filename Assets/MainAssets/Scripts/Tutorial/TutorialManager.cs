using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Drives the tutorial via a strict FSM.
/// Place this MonoBehaviour in the scene. Assign the three tutorial prefabs and
/// GridManager in the Inspector. TurnManager auto-creates itself; this script
/// controls when it starts and what AP the player has at each phase.
///
/// Input restriction works through InputManager.TutorialFilter — each state sets
/// the filter so only allowed actions pass through; blocked ones trigger a
/// friendly redirect message via the TutPanel.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    // ── Inspector refs ────────────────────────────────────────────────────────

    [Header("Tutorial Monster Prefabs")]
    [SerializeField] private GameObject pixiventiTutPrefab;
    [SerializeField] private GameObject nidpettiteTutPrefab;
    [SerializeField] private GameObject sermalTutPrefab;

    [Header("Scene References")]
    [SerializeField] private GridManager gridManager;

    [Header("Phase 1 — Controls")]
    [Tooltip("Tile where Pixiventi spawns for Phase 1.")]
    [SerializeField] private Vector2Int pixiventiPhase1Tile = new Vector2Int(8, 4);
    [Tooltip("Tile the player must move Pixiventi to.")]
    [SerializeField] private Vector2Int targetMoveTile      = new Vector2Int(9, 3);
    [Tooltip("AP for the entire Phase 1 (should be just enough to move once then run out).")]
    [SerializeField] private int phase1AP = 4;

    [Header("Phase 2 — Combat")]
    [SerializeField] private Vector2Int pixiventiPhase2Tile  = new Vector2Int(8, 2);
    [SerializeField] private Vector2Int nidpettitePhase2Tile = new Vector2Int(9, 2);
    [SerializeField] private Vector2Int sermalPhase2Tile     = new Vector2Int(8, 5);
    [Tooltip("AP for Phase 2 (enough for Heal + FireBolt with 2-3 left over).")]
    [SerializeField] private int phase2AP = 8;

    // ── FSM ───────────────────────────────────────────────────────────────────

    private enum State
    {
        Idle,
        AskToPlay,

        // Phase 1
        P1_WaitMonsterClick,
        P1_WaitInfo,
        P1_InfoOpen,
        P1_APLesson,          // AP explanation before movement
        P1_WaitMovement,
        P1_WaitMoveTile,
        P1_WaitCamera,
        P1_WaitEndTurn,
        P1_TurnEnded,
        P1_FadeOut,

        // Phase 2
        P2_Intro,
        P2_ShowMonsterUI,
        P2_WaitHealCommand,
        P2_WaitHealTarget,
        P2_WaitRestAP,        // end-turn rest lesson after heal
        P2_EnemySpawned,
        P2_TypeEffectiveness, // element legend + effectiveness colours
        P2_StatusEffects,     // status effect explanation
        P2_WaitAttackCommand,
        P2_WaitAttackTarget,
        P2_Victory,

        Done
    }

    private State _state = State.Idle;

    /// <summary>True while the tutorial FSM is running (not yet Done or Idle).</summary>
    public bool IsActive => _state != State.Idle && _state != State.Done;

    // ── Runtime monster references ────────────────────────────────────────────
    private Monster _pixiventi;
    private Monster _nidpettite;
    private Monster _sermal;

    // ── UI (built in code) ───────────────────────────────────────────────────
    // No separate UIDocument — we inject into the first existing UIDocument in
    // the scene (HUDController's) to avoid PanelSettings borrowing issues.
    private VisualElement _uiRoot;    // borrowed root from scene's UIDocument
    private VisualElement _panelRoot;
    private Label         _panelText;
    private VisualElement _arrow;
    private VisualElement _fadeOverlay;


    // ── Lifecycle ─────────────────────────────────────────────────────────────

    // Persists across scene reloads (static). Once the tutorial has been played or
    // declined in this session, we never ask again — e.g. after a victory restart.
    private static bool _tutorialPlayed = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }

        if (_tutorialPlayed)
        {
            // Tutorial was already played or skipped this session — do nothing.
            // Don't set Instance so the real battle starts normally.
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // Block all normal spawners NOW (before their Start() fires) so the
        // battle doesn't start until the player answers the tutorial prompt.
        MonsterSpawner.HoldSpawn = true;
    }

    void Start()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        StartCoroutine(StartAfterFrame());
    }

    private System.Collections.IEnumerator StartAfterFrame()
    {
        // Wait one frame so all other MonoBehaviours (HUDController, etc.)
        // have run their Start() and their UIDocuments exist with PanelSettings.
        yield return null;
        BuildUI();
        SetState(State.AskToPlay);
    }

    void OnDestroy()
    {
        InputManager.TutorialFilter  = null;
        InputManager.OnBlockedAction = null;
        Instance = null;
    }

    // ── State Machine ─────────────────────────────────────────────────────────

    private void SetState(State next)
    {
        _state = next;
        switch (next)
        {
            case State.AskToPlay:         EnterAskToPlay();        break;
            case State.P1_WaitMonsterClick: EnterP1MonsterClick(); break;
            case State.P1_WaitInfo:         EnterP1WaitInfo();     break;
            case State.P1_InfoOpen:         EnterP1InfoOpen();     break;
            case State.P1_APLesson:         EnterP1APLesson();     break;
            case State.P1_WaitMovement:     EnterP1WaitMovement(); break;
            case State.P1_WaitMoveTile:     EnterP1WaitMoveTile(); break;
            case State.P1_WaitCamera:       EnterP1WaitCamera();   break;
            case State.P1_WaitEndTurn:      EnterP1WaitEndTurn();  break;
            case State.P1_TurnEnded:        EnterP1TurnEnded();    break;
            case State.P1_FadeOut:          StartCoroutine(Phase1FadeAndTransition()); break;
            case State.P2_Intro:            EnterP2Intro();        break;
            case State.P2_ShowMonsterUI:    EnterP2ShowMonsterUI(); break;
            case State.P2_WaitHealCommand:  EnterP2WaitHealCommand(); break;
            case State.P2_WaitHealTarget:      EnterP2WaitHealTarget();    break;
            case State.P2_WaitRestAP:          EnterP2WaitRestAP();        break;
            case State.P2_EnemySpawned:        EnterP2EnemySpawned();      break;
            case State.P2_TypeEffectiveness:   EnterP2TypeEffectiveness(); break;
            case State.P2_StatusEffects:       EnterP2StatusEffects();     break;
            case State.P2_WaitAttackCommand:   EnterP2WaitAttackCommand(); break;
            case State.P2_WaitAttackTarget: EnterP2WaitAttackTarget(); break;
            case State.P2_Victory:          EnterVictory();        break;
        }
    }

    // ── Ask To Play ───────────────────────────────────────────────────────────

    private VisualElement _askOverlay;

    private void EnterAskToPlay()
    {
        // No input lock needed — no turn has started yet.
        ShowAskOverlay();
    }

    private void ShowAskOverlay()
    {
        var s    = UIStyleConfig.Load();
        var root = _uiRoot;

        if (root == null) { Debug.LogError("[TutorialManager] _uiRoot is null in ShowAskOverlay."); return; }

        _askOverlay = new VisualElement();
        _askOverlay.style.position       = Position.Absolute;
        _askOverlay.style.left = 0; _askOverlay.style.top = 0;
        _askOverlay.style.right = 0; _askOverlay.style.bottom = 0;
        _askOverlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.72f);
        _askOverlay.style.alignItems     = Align.Center;
        _askOverlay.style.justifyContent = Justify.Center;

        var box = new VisualElement();
        box.style.width   = 480;
        box.style.paddingLeft = 40; box.style.paddingRight  = 40;
        box.style.paddingTop  = 36; box.style.paddingBottom = 36;
        box.style.alignItems  = Align.Center;
        box.style.borderTopLeftRadius     = 12;
        box.style.borderTopRightRadius    = 12;
        box.style.borderBottomLeftRadius  = 12;
        box.style.borderBottomRightRadius = 12;
        UIStyleConfig.ApplySprite(box, s?.panelSprite, new Color(0.06f, 0.06f, 0.10f, 0.97f));

        var title = new Label("Tutorial");
        title.style.fontSize                = 28;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.color                   = new Color(0.25f, 0.85f, 1f, 1f);
        title.style.unityTextAlign          = TextAnchor.MiddleCenter;
        title.style.marginBottom            = 14;

        var body = new Label("Would you like to play the Tutorial?");
        body.style.fontSize       = 18;
        body.style.color          = Color.white;
        body.style.unityTextAlign = TextAnchor.MiddleCenter;
        body.style.whiteSpace     = WhiteSpace.Normal;
        body.style.marginBottom   = 28;

        var btnRow = new VisualElement();
        btnRow.style.flexDirection  = FlexDirection.Row;
        btnRow.style.justifyContent = Justify.Center;

        var yesBtn = MakeButton("YES", new Color(0.12f, 0.45f, 0.18f, 1f));
        yesBtn.style.width       = 140;
        yesBtn.style.height      = 50;
        yesBtn.style.fontSize    = 18;
        yesBtn.style.marginRight = 16;
        yesBtn.clicked += () =>
        {
            _askOverlay.style.display = DisplayStyle.None;
            SetState(State.P1_WaitMonsterClick);
        };

        var noBtn = MakeButton("NO", new Color(0.45f, 0.10f, 0.10f, 1f));
        noBtn.style.width    = 140;
        noBtn.style.height   = 50;
        noBtn.style.fontSize = 18;
        noBtn.clicked += () =>
        {
            _askOverlay.style.display = DisplayStyle.None;
            ExitTutorial();
        };

        btnRow.Add(yesBtn);
        btnRow.Add(noBtn);
        box.Add(title);
        box.Add(body);
        box.Add(btnRow);
        _askOverlay.Add(box);
        root.Add(_askOverlay);
    }

    // ── Phase 1 ───────────────────────────────────────────────────────────────

    private void EnterP1MonsterClick()
    {
        // Spawn Pixiventi and force AP to phase1AP
        SpawnPlayerMonster(pixiventiTutPrefab, pixiventiPhase1Tile, ref _pixiventi);
        StartCoroutine(DelayedOverrideAP(phase1AP));

        // Vibrate Pixiventi's tile
        _vibrateCoroutine = StartCoroutine(VibrateTile(pixiventiPhase1Tile));

        ShowPanel("Press on your monster to command it!");

        // Only allow clicking that specific tile
        SetFilter((action, tile) =>
        {
            if (action == InputManager.TutorialAction.MonsterClick)
                return tile != null && tile.GridPosition == pixiventiPhase1Tile;
            return false;
        }, "Press on your monster to command it!");

        // Listen for the monster being clicked (radial menu opens)
        StartCoroutine(WaitForMonsterSelected(_pixiventi, () => SetState(State.P1_WaitInfo)));
    }

    private void EnterP1WaitInfo()
    {
        StopVibrate();
        ShowPanel("Use [I] for Info, [M] for Movement, or [A] to Attack!\nFor now, press [I] or choose 'Info' to inspect your monster.");

        // Allow only monster click + info; block movement and attack
        SetFilter((action, tile) =>
        {
            if (action == InputManager.TutorialAction.MovementMode) return false;
            if (action == InputManager.TutorialAction.AttackMode)   return false;
            return true;
        }, "Here, try casting the 'Info' command first!");

        StartCoroutine(WaitForInfoOpened(() => SetState(State.P1_InfoOpen)));
    }

    private void EnterP1InfoOpen()
    {
        ShowPanel("Here you can view information about this monster.\nTo continue, exit the info screen.");
        StartCoroutine(WaitForInfoClosed(() => SetState(State.P1_APLesson)));
    }

    private void EnterP1APLesson()
    {
        ShowPanel("Every action costs Action Points (AP)!\nYou can see your remaining AP at the bottom of the screen.\nWhen your AP runs out, your turn ends — spend wisely!\n\nClick Continue to try moving.");
        ShowArrowAtAP();
        ShowContinueButton(() =>
        {
            HideArrow();
            SetState(State.P1_WaitMovement);
        });
    }

    private void EnterP1WaitMovement()
    {
        ShowPanel("Let's make Pixiventi move!\nCast the 'Movement' command.");

        SetFilter((action, tile) =>
        {
            if (action == InputManager.TutorialAction.AttackMode) return false;
            return true;
        }, "Let's learn movement first!");

        StartCoroutine(WaitForMovementModeEntered(() => SetState(State.P1_WaitMoveTile)));
    }

    private void EnterP1WaitMoveTile()
    {
        ShowPanel("Those teal tiles show where you can move!\nMove to the tile with the arrow — the highlighted one!");
        _vibrateCoroutine = StartCoroutine(VibrateTile(targetMoveTile));
        ShowArrowAtTile(targetMoveTile);

        InputManager.TutorialFilter = (action, tile) =>
        {
            if (action == InputManager.TutorialAction.MoveTile)
                return tile != null && tile.GridPosition == targetMoveTile;
            return true;
        };
        InputManager.OnBlockedAction = _ => ShowRedirectMessage("Head to the highlighted tile!");

        StartCoroutine(WaitForMonsterMovedTo(targetMoveTile, () =>
        {
            StopVibrate();
            HideArrow();
            SetState(State.P1_WaitCamera);
        }));
    }

    private void EnterP1WaitCamera()
    {
        ShowPanel("Hmm, let's look around!\nYou can move your camera with the Arrow Keys and Middle Mouse Button.\nYou can also rotate with Right Mouse Button.");

        // Allow everything during camera tutorial
        InputManager.TutorialFilter  = null;
        InputManager.OnBlockedAction = null;

        // Detect any camera action (pan / rotate / zoom)
        StartCoroutine(WaitForCameraAction(() => SetState(State.P1_WaitEndTurn)));
    }

    private void EnterP1WaitEndTurn()
    {
        ShowPanel("It seems you're low on AP!\nLet's end the turn here and rest.\nYou can also use [Spacebar].");
        ShowArrowAtEndTurn();

        SetFilter((action, tile) =>
        {
            if (action == InputManager.TutorialAction.EndTurn)   return true;
            if (action == InputManager.TutorialAction.MonsterClick) return false;
            if (action == InputManager.TutorialAction.MovementMode) return false;
            if (action == InputManager.TutorialAction.AttackMode)   return false;
            return true;
        }, "End the turn first!");

        StartCoroutine(WaitForTurnEnded(TurnManager.TurnOwner.Enemy, () => SetState(State.P1_TurnEnded)));
    }

    private void EnterP1TurnEnded()
    {
        HideArrow();
        InputManager.TutorialFilter  = null;
        InputManager.OnBlockedAction = null;
        ShowPanel("Ah, so much better! Now we have the energy for another round!\nA round can also end when you run out of AP — so watch how you spend it!");
        ShowContinueButton(() => SetState(State.P1_FadeOut));
    }

    private Button _continueBtn;

    private void ShowContinueButton(System.Action then)
    {
        if (_panelRoot == null) return;
        _continueBtn?.RemoveFromHierarchy();
        _continueBtn = MakeButton("Continue →", new Color(0.12f, 0.40f, 0.18f, 1f));
        _continueBtn.style.marginTop  = 14;
        _continueBtn.style.alignSelf  = Align.FlexEnd;
        _continueBtn.clicked += () =>
        {
            _continueBtn.RemoveFromHierarchy();
            _continueBtn = null;
            then?.Invoke();
        };
        _panelRoot.Add(_continueBtn);
    }

    // ── Phase 1 → Phase 2 Transition ─────────────────────────────────────────

    private IEnumerator Phase1FadeAndTransition()
    {
        LockAll();
        HidePanel();
        yield return StartCoroutine(Fade(0f, 1f, 1f));
        yield return new WaitForSeconds(0.5f);

        // Despawn Phase 1 monster
        DespawnMonster(ref _pixiventi);

        yield return StartCoroutine(Fade(1f, 0f, 1f));
        SetState(State.P2_Intro);
    }

    // ── Phase 2 ───────────────────────────────────────────────────────────────

    private void EnterP2Intro()
    {
        EnemyTurnController.TutorialSkipTurn = true;
        MovePanelToLeftMid();
        SpawnPlayerMonster(pixiventiTutPrefab,  pixiventiPhase2Tile,  ref _pixiventi);
        SpawnPlayerMonster(nidpettiteTutPrefab, nidpettitePhase2Tile, ref _nidpettite);

        // Set Nídpettite to half HP
        StartCoroutine(DelayThen(0.3f, () =>
        {
            if (_nidpettite != null)
                _nidpettite.DevSetHP(_nidpettite.MaxHP / 2);
            HUDController.RefreshRosters();
        }));

        StartCoroutine(DelayedOverrideAP(phase2AP));
        SetState(State.P2_ShowMonsterUI);
    }

    private void EnterP2ShowMonsterUI()
    {
        LockAll();
        ShowPanel("The side panel shows all your monsters!\nIt displays HP, level, and current status — keep an eye on it!");
        ShowContinueButton(() => SetState(State.P2_WaitHealCommand));
    }

    private void EnterP2WaitHealCommand()
    {
        // Clear any lingering movement highlights from Phase 1
        InputManager.RequestCancel();
        MovePanelToTopRight();
        ShowPanel("That Nídpettite looks hurt!\nBefore we learn to attack, let's heal it.\nCommand Pixiventi to Attack with 'Heal'.");

        // Allow only Pixiventi's attack mode (which shows Heal)
        SetFilter((action, tile) =>
        {
            if (action == InputManager.TutorialAction.MonsterClick)
                return tile != null && (tile.GridPosition == pixiventiPhase2Tile ||
                                        (_pixiventi != null && _pixiventi.CurrentTile?.GridPosition == tile.GridPosition));
            if (action == InputManager.TutorialAction.AttackMode)  return true;
            if (action == InputManager.TutorialAction.MovementMode) return false;
            return true;
        }, "We need to heal Nídpettite! Command Pixiventi to use Heal.");

        StartCoroutine(WaitForAttackRangeShown(() => SetState(State.P2_WaitHealTarget)));
    }

    private void EnterP2WaitHealTarget()
    {
        ShowPanel("Here you can see all monsters Pixiventi can heal.\nPress on Nídpettite!");

        SetFilter((action, tile) =>
        {
            if (action == InputManager.TutorialAction.AttackTarget)
                return tile != null && _nidpettite != null &&
                       tile.GridPosition == _nidpettite.CurrentTile?.GridPosition;
            return true;
        }, "Press on Nídpettite to heal it!");

        StartCoroutine(WaitForMonsterHealed(_nidpettite, () =>
        {
            // Drain AP so the rest lesson makes sense — Nídpettite's Fire Bolt costs more
            FindFirstObjectByType<PlayerTurnController>()?.DevSetAP(1);
            ShowPanel("Nídpettite is healed!");
            StartCoroutine(DelayThen(1.5f, () => SetState(State.P2_WaitRestAP)));
        }));
    }

    private void EnterP2WaitRestAP()
    {
        ShowPanel("Nídpettite can't use Fire Bolt yet — not enough AP!\nEnd your turn to rest and restore AP.\nYou can also press [Spacebar]!");
        ShowArrowAtEndTurn();

        SetFilter((action, tile) =>
        {
            if (action == InputManager.TutorialAction.EndTurn)      return true;
            if (action == InputManager.TutorialAction.MonsterClick)  return false;
            if (action == InputManager.TutorialAction.MovementMode)  return false;
            if (action == InputManager.TutorialAction.AttackMode)    return false;
            return true;
        }, "End your turn to rest and regain AP!");

        StartCoroutine(WaitForPlayerTurnRestarted(() =>
        {
            HideArrow();
            InputManager.TutorialFilter  = null;
            InputManager.OnBlockedAction = null;
            ShowPanel("Much better — AP is restored!\nNow Nídpettite is ready to fight.");
            ShowContinueButton(() => SetState(State.P2_EnemySpawned));
        }));
    }

    private IEnumerator WaitForPlayerTurnRestarted(System.Action then)
    {
        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => TurnManager.Instance != null && TurnManager.Instance.IsEnemyTurn);
        yield return new WaitUntil(() => TurnManager.Instance != null && TurnManager.Instance.IsPlayerTurn);
        yield return new WaitForSeconds(0.5f);
        then?.Invoke();
    }

    private void EnterP2EnemySpawned()
    {
        SpawnEnemyMonster(sermalTutPrefab, sermalPhase2Tile, ref _sermal);
        if (_sermal != null)
        {
            FindFirstObjectByType<EnemyTurnController>()?.AutoDiscoverMonsters();
            HUDController.RefreshRosters();
        }
        // Enemy roster card now visible top-right — shift panel left to avoid overlap.
        if (_panelRoot != null) _panelRoot.style.right = 230;
        LockAll();
        ShowPanel("Hey! A wild Sermal has slithered in!\nTime to test Nídpettite's power!");
        ShowContinueButton(() => SetState(State.P2_StatusEffects));
    }

    private void EnterP2StatusEffects()
    {
        ShowPanel("Attacks can also apply Status Effects!\nBurned enemies take damage each turn.\nOther effects can weaken stats or cause misses.\nWatch for status icons next to monster HP bars!\n\nReady to fight?");
        ShowContinueButton(() => SetState(State.P2_WaitAttackCommand));
    }

    private void EnterP2WaitAttackCommand()
    {
        ShowPanel("Command Nídpettite to attack with 'Fire Bolt'!\nSelect Nídpettite, then choose Attack.");

        SetFilter((action, tile) =>
        {
            if (action == InputManager.TutorialAction.MonsterClick)
                return tile != null && (_nidpettite != null &&
                       tile.GridPosition == _nidpettite.CurrentTile?.GridPosition);
            if (action == InputManager.TutorialAction.AttackMode)   return true;
            if (action == InputManager.TutorialAction.MovementMode) return false;
            return true;
        }, "Command Nídpettite to use Fire Bolt on the enemy!");

        // Wait until the attack range is visible — THEN teach effectiveness colours
        // (aura highlights only appear once targeting mode is active).
        StartCoroutine(WaitForAttackRangeShown(() => SetState(State.P2_TypeEffectiveness)));
    }

    private void EnterP2TypeEffectiveness()
    {
        // Lock game input (keeps attack-range overlay visible) but Continue button
        // is a UI element — it is not gated by TutorialFilter.
        LockAll();
        ElementLegend.ShowLegend(_uiRoot);
        ShowPanel("See the Golden Aura on Sermal?\nGold = Super Effective — bonus damage!\nGray = Resisted — reduced damage.\nNídpettite's Fire is super effective here!\n\nOpen the Element Legend for details, then Continue.");
        ShowContinueButton(() =>
        {
            ElementLegend.HideLegend();
            SetState(State.P2_WaitAttackTarget);
        });
    }

    private void EnterP2WaitAttackTarget()
    {
        ShowPanel("Nídpettite is locked on — finish Sermal!");

        // Attack range is already displayed; only allow selecting Sermal's tile.
        SetFilter((action, tile) =>
        {
            if (action == InputManager.TutorialAction.AttackTarget)
                return tile != null && _sermal != null &&
                       tile.GridPosition == _sermal.CurrentTile?.GridPosition;
            return true;
        }, "Attack the highlighted enemy!");

        StartCoroutine(WaitForMonsterKilled(_sermal, () => SetState(State.P2_Victory)));
    }

    // ── Victory ───────────────────────────────────────────────────────────────

    private void EnterVictory()
    {
        LockAll();
        HidePanel();
        ShowVictoryScreen();
    }

    private VisualElement _victoryOverlay;

    private void ExitTutorial()
    {
        _tutorialPlayed = true; // suppress re-ask on scene reload within this session
        InputManager.TutorialFilter  = null;
        InputManager.OnBlockedAction = null;
        _state = State.Done;
        // Stop all pending WaitFor coroutines so they can't fire tutorial callbacks during the real battle.
        StopAllCoroutines();
        StartCoroutine(ExitSequence());
    }

    private IEnumerator ExitSequence()
    {
        // Clear all tutorial input restrictions immediately.
        InputManager.TutorialFilter  = null;
        InputManager.OnBlockedAction = null;

        // Fade to black
        if (_fadeOverlay != null) _fadeOverlay.pickingMode = PickingMode.Position;
        yield return StartCoroutine(Fade(0f, 1f, 1f));

        // Hide all tutorial UI
        HidePanel();
        if (_victoryOverlay != null) _victoryOverlay.style.display = DisplayStyle.None;

        // Despawn tutorial monsters
        DespawnMonster(ref _pixiventi);
        DespawnMonster(ref _nidpettite);
        DespawnMonster(ref _sermal);

        yield return new WaitForSeconds(0.3f);

        // Fade back in
        yield return StartCoroutine(Fade(1f, 0f, 1f));
        if (_fadeOverlay != null) _fadeOverlay.pickingMode = PickingMode.Ignore;

        EnemyTurnController.TutorialSkipTurn = false;

        // Reset _isFirstTurn so RestartGame grants startingAP (not additive).
        FindFirstObjectByType<PlayerTurnController>()?.ResetForNewGame();
        FindFirstObjectByType<EnemyTurnController>()?.ResetForNewGame();

        // Release the hold so normal MonsterSpawners fire.
        MonsterSpawner.HoldSpawn = false;
        foreach (var spawner in FindObjectsByType<MonsterSpawner>(FindObjectsSortMode.None))
            spawner.ForceSpawnAll();

        // Give spawners one frame to finish so Monster.Start() has run.
        yield return null;

        // Stop the tutorial-era turn loop and restart fresh:
        // RestartGame resets TurnNumber=1, re-discovers real monsters, and begins Player Turn 1.
        var tm = TurnManager.Instance;
        if (tm != null)
        {
            tm.StopAllCoroutines();
            tm.RestartGame();
        }
    }

    // ── Coroutine Waiters ─────────────────────────────────────────────────────

    private IEnumerator WaitForMonsterSelected(Monster monster, System.Action then)
    {
        while (true)
        {
            yield return null;
            // RadialMenu opens when a monster is selected — check if the
            // monster's tile is currently selected in InputManager (indirect check via camera focus)
            if (monster == null) yield break;
            // Poll: check if a RadialMenu exists in scene (monster was clicked)
            if (FindFirstObjectByType<RadialMenu>() != null)
            {
                then?.Invoke();
                yield break;
            }
        }
    }

    private IEnumerator WaitForInfoOpened(System.Action then)
    {
        bool opened = false;
        System.Action handler = () => opened = true;
        MonsterInfoPanel.OnOpened += handler;
        yield return new WaitForSeconds(0.3f); // debounce: ignore any open that happens before user can act
        opened = false;                         // reset in case it fired during debounce
        while (!opened) yield return null;
        MonsterInfoPanel.OnOpened -= handler;
        then?.Invoke();
    }

    private IEnumerator WaitForInfoClosed(System.Action then)
    {
        bool closed = false;
        System.Action handler = () => closed = true;
        MonsterInfoPanel.OnClosed += handler;
        while (!closed) yield return null;
        MonsterInfoPanel.OnClosed -= handler;
        then?.Invoke();
    }

    private IEnumerator WaitForMovementModeEntered(System.Action then)
    {
        _movementModeEntered = false;
        while (!_movementModeEntered) yield return null;
        _movementModeEntered = false;
        then?.Invoke();
    }

    private static bool _movementModeEntered;
    public static void NotifyMovementModeEntered() => _movementModeEntered = true;

    private IEnumerator WaitForMonsterMovedTo(Vector2Int targetTile, System.Action then)
    {
        while (true)
        {
            yield return new WaitForSeconds(0.3f);
            if (_pixiventi == null) yield break;
            if (_pixiventi.CurrentTile?.GridPosition == targetTile)
            {
                yield return new WaitForSeconds(0.5f); // let animation finish
                then?.Invoke();
                yield break;
            }
        }
    }

    private IEnumerator WaitForCameraAction(System.Action then)
    {
        // Require the player to both PAN (arrow keys / middle-mouse) and ROTATE (right-drag).
        // CameraController exposes its orbit yaw; we detect a yaw change as rotation.
        yield return new WaitForSeconds(0.5f);

        bool panned  = false;
        bool rotated = false;

        var cam = FindFirstObjectByType<CameraController>();
        Vector3 startPos = cam != null ? cam.transform.position : Vector3.zero;
        float   startYaw = cam != null ? cam.OrbitYaw : 0f;

        while (!panned || !rotated)
        {
            yield return null;
            if (cam == null) { cam = FindFirstObjectByType<CameraController>(); continue; }

            if (!panned && Vector3.Distance(cam.transform.position, startPos) > 0.15f)
                panned = true;

            if (!rotated && Mathf.Abs(Mathf.DeltaAngle(cam.OrbitYaw, startYaw)) > 3f)
                rotated = true;
        }
        yield return new WaitForSeconds(0.5f);
        then?.Invoke();
    }

    private IEnumerator WaitForTurnEnded(TurnManager.TurnOwner waitForOwner, System.Action then)
    {
        yield return new WaitForSeconds(0.3f);
        while (true)
        {
            yield return null;
            if (TurnManager.Instance == null) continue;
            // Wait until the enemy turn begins (meaning player ended their turn)
            if (TurnManager.Instance.CurrentTurn == waitForOwner)
            {
                // Wait for enemy turn to finish too (it'll be quick with no enemies)
                yield return new WaitForSeconds(1.5f);
                then?.Invoke();
                yield break;
            }
        }
    }

    private IEnumerator WaitForAttackRangeShown(System.Action then)
    {
        _attackRangeShown = false;
        while (!_attackRangeShown) yield return null;
        _attackRangeShown = false;
        then?.Invoke();
    }
    private static bool _attackRangeShown;
    public static void NotifyAttackRangeShown() => _attackRangeShown = true;

    private IEnumerator WaitForMonsterHealed(Monster monster, System.Action then)
    {
        if (monster == null) { then?.Invoke(); yield break; }
        int startHP = monster.CurrentHP;
        while (true)
        {
            yield return null;
            if (monster == null) { then?.Invoke(); yield break; }
            if (monster.CurrentHP > startHP || monster.CurrentHP == monster.MaxHP)
            {
                then?.Invoke();
                yield break;
            }
        }
    }

    private IEnumerator WaitForMonsterKilled(Monster monster, System.Action then)
    {
        if (monster == null) { then?.Invoke(); yield break; }
        while (true)
        {
            yield return null;
            if (monster == null || !monster.IsAlive)
            {
                yield return new WaitForSeconds(1.5f);
                then?.Invoke();
                yield break;
            }
        }
    }

    private IEnumerator DelayThen(float seconds, System.Action then)
    {
        yield return new WaitForSeconds(seconds);
        then?.Invoke();
    }

    // ── Monster Spawning ──────────────────────────────────────────────────────

    private void SpawnPlayerMonster(GameObject prefab, Vector2Int gridPos, ref Monster outMonster)
        => SpawnMonster(prefab, gridPos, false, ref outMonster);

    private void SpawnEnemyMonster(GameObject prefab, Vector2Int gridPos, ref Monster outMonster)
        => SpawnMonster(prefab, gridPos, true, ref outMonster);

    private void SpawnMonster(GameObject prefab, Vector2Int gridPos, bool isEnemy, ref Monster outMonster)
    {
        if (prefab == null || gridManager == null) return;
        Tile tile = gridManager.GetTile(gridPos.x, gridPos.y);
        if (tile == null) { Debug.LogWarning($"[TutorialManager] Tile {gridPos} not found."); return; }

        Quaternion rot = isEnemy ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
        GameObject go  = Instantiate(prefab, tile.transform.position, rot);

        tile.SetOccupation(Tile.OccupationType.Monster, go);

        Monster m = go.GetComponentInChildren<Monster>();
        if (m != null)
        {
            m.CurrentTile = tile;
            MonsterHPBar bar = go.AddComponent<MonsterHPBar>();
            StartCoroutine(InitHPBarNextFrame(bar, m));
        }

        if (go.GetComponent<Collider>() == null)
        {
            var cap = go.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0f, 0.75f, 0f);
            cap.radius = 0.4f;
            cap.height = 1.5f;
        }
        go.layer = LayerMask.NameToLayer("Monster");

        var outline = go.GetComponent<Outline>() ?? go.AddComponent<Outline>();
        outline.OutlineMode  = Outline.Mode.SilhouetteOnly;
        outline.OutlineColor = isEnemy
            ? new Color(1f, 0.25f, 0.25f, 1f)
            : new Color(0.25f, 0.85f, 1f, 1f);
        outline.OutlineWidth = 8f;

        outMonster = m;
        Debug.Log($"[TutorialManager] Spawned {prefab.name} at {gridPos} (enemy={isEnemy})");
    }

    private void DespawnMonster(ref Monster monster)
    {
        if (monster == null) return;
        monster.CurrentTile?.ClearOccupation();
        Destroy(monster.transform.root.gameObject);
        monster = null;
    }

    private IEnumerator InitHPBarNextFrame(MonsterHPBar bar, Monster monster)
    {
        yield return null;
        if (bar != null && monster != null) bar.Initialize(monster);
    }

    // ── AP Override ───────────────────────────────────────────────────────────

    private IEnumerator DelayedOverrideAP(int targetAP)
    {
        // Wait for TurnManager and PlayerTurnController to initialise
        float t = 0f;
        while (TurnManager.Instance == null && t < 5f)
        {
            yield return new WaitForSeconds(0.1f);
            t += 0.1f;
        }

        PlayerTurnController ptc = FindFirstObjectByType<PlayerTurnController>();
        if (ptc == null) yield break;

        // Wait for the first turn to start
        while (!ptc.IsActive && t < 10f)
        {
            yield return new WaitForSeconds(0.1f);
            t += 0.1f;
        }

        ptc.DevSetAP(targetAP);
        Debug.Log($"[TutorialManager] AP set to {targetAP} (was {ptc.CurrentAP}).");
    }

    // ── Input Filter Helpers ──────────────────────────────────────────────────

    private void SetFilter(System.Func<InputManager.TutorialAction, Tile, bool> filter, string redirectMsg)
    {
        InputManager.TutorialFilter  = filter;
        InputManager.OnBlockedAction = _ =>
        {
            // Close any open radial menu so the player can read the redirect message.
            InputManager.RequestCancel();
            ShowRedirectMessage(redirectMsg);
        };
    }

    private void LockAll()
    {
        SetFilter((_, __) => false, "");
    }

    // ── UI ────────────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        var s = UIStyleConfig.Load();

        // Borrow the root VisualElement from any existing UIDocument in the scene
        // (HUDController's is ideal). This avoids creating a second UIDocument with
        // a borrowed PanelSettings that can become stale.
        UIDocument hostDoc = null;
        foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
        {
            if (doc.rootVisualElement != null)
            {
                hostDoc = doc;
                break;
            }
        }
        if (hostDoc == null)
        {
            Debug.LogError("[TutorialManager] No UIDocument found in scene — tutorial UI will not render.");
            return;
        }
        _uiRoot = hostDoc.rootVisualElement;

        // ── Wrapper (positioned, full-screen, pointer-transparent) ────────
        var wrapper = new VisualElement();
        wrapper.name = "TutorialOverlayRoot";
        wrapper.pickingMode = PickingMode.Ignore;
        wrapper.style.position = Position.Absolute;
        wrapper.style.left = 0; wrapper.style.top = 0;
        wrapper.style.right = 0; wrapper.style.bottom = 0;
        _uiRoot.Add(wrapper);

        // ── Fade overlay ──────────────────────────────────────────────────
        _fadeOverlay = new VisualElement();
        _fadeOverlay.pickingMode = PickingMode.Ignore;
        _fadeOverlay.style.position = Position.Absolute;
        _fadeOverlay.style.left = 0; _fadeOverlay.style.top = 0;
        _fadeOverlay.style.right = 0; _fadeOverlay.style.bottom = 0;
        _fadeOverlay.style.backgroundColor = Color.black;
        _fadeOverlay.style.opacity = 0f;
        wrapper.Add(_fadeOverlay);

        // ── Tutorial panel (top-right) ────────────────────────────────────
        _panelRoot = new VisualElement();
        _panelRoot.pickingMode = PickingMode.Ignore;
        _panelRoot.style.position = Position.Absolute;
        _panelRoot.style.right  = 20;
        _panelRoot.style.top    = 20;
        _panelRoot.style.width  = 340;
        _panelRoot.style.paddingLeft   = 20; _panelRoot.style.paddingRight  = 20;
        _panelRoot.style.paddingTop    = 16; _panelRoot.style.paddingBottom = 16;
        _panelRoot.style.borderTopLeftRadius    = 8;
        _panelRoot.style.borderTopRightRadius   = 8;
        _panelRoot.style.borderBottomLeftRadius = 8;
        _panelRoot.style.borderBottomRightRadius= 8;
        UIStyleConfig.ApplySprite(_panelRoot, s?.panelSprite, new Color(0.06f, 0.06f, 0.10f, 0.96f));
        _panelRoot.style.display = DisplayStyle.None;

        _panelText = new Label("");
        _panelText.pickingMode = PickingMode.Ignore;
        _panelText.style.fontSize   = 20;
        _panelText.style.color      = Color.white;
        _panelText.style.whiteSpace = WhiteSpace.Normal;
        _panelText.style.unityTextAlign = TextAnchor.UpperLeft;
        _panelRoot.Add(_panelText);
        wrapper.Add(_panelRoot);

        // ── Animated arrow ────────────────────────────────────────────────
        _arrow = new VisualElement();
        _arrow.pickingMode = PickingMode.Ignore;
        _arrow.style.position = Position.Absolute;
        _arrow.style.width    = 40;
        _arrow.style.height   = 40;
        _arrow.style.display  = DisplayStyle.None;
        _arrow.generateVisualContent += DrawArrow;
        wrapper.Add(_arrow);

        StartCoroutine(AnimateArrow());
    }

    private void ShowPanel(string text)
    {
        if (_panelRoot == null) return;
        _panelText.text          = text;
        _panelRoot.style.display = DisplayStyle.Flex;
    }

    private void MovePanelToLeftMid()
    {
        if (_panelRoot == null) return;
        // Sit to the right of the roster card strip (cards are ~100px wide + margin).
        // Vertically: upper-middle, clear of the cards.
        _panelRoot.style.right  = StyleKeyword.Auto;
        _panelRoot.style.bottom = StyleKeyword.Auto;
        _panelRoot.style.left   = 120;
        _panelRoot.style.top    = new StyleLength(new Length(30, LengthUnit.Percent));
        _panelRoot.style.width  = 300;
    }

    private void MovePanelToTopRight()
    {
        if (_panelRoot == null) return;
        _panelRoot.style.left   = StyleKeyword.Auto;
        _panelRoot.style.bottom = StyleKeyword.Auto;
        _panelRoot.style.right  = 20;
        _panelRoot.style.top    = 20;
        _panelRoot.style.width  = 340;
    }

    private void HidePanel()
    {
        if (_panelRoot != null) _panelRoot.style.display = DisplayStyle.None;
    }

    private void ShowRedirectMessage(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        ShowPanel(msg);
    }

    private void ShowArrowAtEndTurn()
    {
        if (_arrow == null) return;
        _arrow.style.right   = 210;
        _arrow.style.bottom  = 90;
        _arrow.style.left    = StyleKeyword.Auto;
        _arrow.style.top     = StyleKeyword.Auto;
        _arrow.style.display = DisplayStyle.Flex;
    }

    private void ShowArrowAtAP()
    {
        if (_arrow == null) return;
        // AP circle sits to the left of the END TURN button (~230px from right edge).
        // Place the arrow above it so it points down onto the AP circle.
        _arrow.style.right   = 195;
        _arrow.style.bottom  = 130;
        _arrow.style.left    = StyleKeyword.Auto;
        _arrow.style.top     = StyleKeyword.Auto;
        _arrow.style.display = DisplayStyle.Flex;
    }

    private void ShowArrowAtTile(Vector2Int gridPos)
    {
        // World-to-screen projection for tile position
        if (gridManager == null || Camera.main == null) return;
        Tile tile = gridManager.GetTile(gridPos.x, gridPos.y);
        if (tile == null) return;
        StartCoroutine(TrackTileArrow(tile));
    }

    private IEnumerator TrackTileArrow(Tile tile)
    {
        _arrow.style.display = DisplayStyle.Flex;
        while (_state == State.P1_WaitMoveTile)
        {
            if (Camera.main != null && tile != null)
            {
                Vector3 screen = Camera.main.WorldToScreenPoint(tile.transform.position + Vector3.up * 0.5f);
                float x = screen.x - 20f;
                float y = Screen.height - screen.y - 20f;
                _arrow.style.left   = x;
                _arrow.style.top    = y;
                _arrow.style.right  = StyleKeyword.Auto;
                _arrow.style.bottom = StyleKeyword.Auto;
            }
            yield return null;
        }
        _arrow.style.display = DisplayStyle.None;
    }

    private void HideArrow()
    {
        if (_arrow != null) _arrow.style.display = DisplayStyle.None;
        // arrow hidden
    }

    private IEnumerator AnimateArrow()
    {
        float t = 0f;
        while (true)
        {
            yield return null;
            if (_arrow == null || _arrow.style.display == DisplayStyle.None) continue;
            t += Time.deltaTime * 3f;
            float offset = Mathf.Sin(t) * 8f;
            _arrow.style.marginBottom = offset;
            _arrow.MarkDirtyRepaint();
        }
    }

    private void DrawArrow(MeshGenerationContext ctx)
    {
        var p  = ctx.painter2D;
        float w = 40f, h = 40f;
        p.fillColor = new Color(0.25f, 0.85f, 1f, 0.95f);
        p.BeginPath();
        p.MoveTo(new Vector2(w * 0.5f, h));
        p.LineTo(new Vector2(w,        0));
        p.LineTo(new Vector2(w * 0.7f, 0));
        p.LineTo(new Vector2(w * 0.7f, -h * 0.4f));
        p.LineTo(new Vector2(w * 0.3f, -h * 0.4f));
        p.LineTo(new Vector2(w * 0.3f, 0));
        p.LineTo(new Vector2(0,        0));
        p.ClosePath();
        p.Fill();
    }

    private void ShowVictoryScreen()
    {
        var s    = UIStyleConfig.Load();
        var root = _uiRoot;
        if (root == null) return;

        _victoryOverlay = new VisualElement();
        var overlay = _victoryOverlay;
        overlay.style.position        = Position.Absolute;
        overlay.style.left = 0; overlay.style.top = 0;
        overlay.style.right = 0; overlay.style.bottom = 0;
        overlay.style.alignItems      = Align.Center;
        overlay.style.justifyContent  = Justify.Center;
        overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.75f);

        var box = new VisualElement();
        box.style.width   = 500;
        box.style.paddingLeft = 40; box.style.paddingRight  = 40;
        box.style.paddingTop  = 40; box.style.paddingBottom = 40;
        box.style.alignItems  = Align.Center;
        box.style.borderTopLeftRadius    = 12;
        box.style.borderTopRightRadius   = 12;
        box.style.borderBottomLeftRadius = 12;
        box.style.borderBottomRightRadius= 12;
        UIStyleConfig.ApplySprite(box, s?.panelSprite, new Color(0.06f, 0.06f, 0.10f, 0.97f));

        var title = new Label("Congratulations!");
        title.style.fontSize                = 34;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.color                   = new Color(0.25f, 0.9f, 0.5f, 1f);
        title.style.unityTextAlign          = TextAnchor.MiddleCenter;
        title.style.marginBottom            = 12;

        var sub = new Label("You've completed the tutorial.\nTime to face the real battle!");
        sub.style.fontSize      = 18;
        sub.style.color         = Color.white;
        sub.style.unityTextAlign = TextAnchor.MiddleCenter;
        sub.style.whiteSpace    = WhiteSpace.Normal;
        sub.style.marginBottom  = 28;

        var continueBtn = MakeButton("CONTINUE", new Color(0.15f, 0.45f, 0.20f, 1f));
        continueBtn.style.width  = 200;
        continueBtn.style.height = 54;
        continueBtn.style.fontSize = 20;
        continueBtn.clicked += ExitTutorial;

        box.Add(title);
        box.Add(sub);
        box.Add(continueBtn);
        overlay.Add(box);
        root.Add(overlay);
    }

    // ── Fade Coroutine ────────────────────────────────────────────────────────

    private IEnumerator Fade(float from, float to, float duration)
    {
        _fadeOverlay.pickingMode = to > 0.5f ? PickingMode.Position : PickingMode.Ignore;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            _fadeOverlay.style.opacity = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        _fadeOverlay.style.opacity = to;
    }

    // ── Tile Vibration ────────────────────────────────────────────────────────

    private Coroutine _vibrateCoroutine;

    private IEnumerator VibrateTile(Vector2Int gridPos)
    {
        Tile tile = gridManager?.GetTile(gridPos.x, gridPos.y);
        if (tile == null) yield break;
        Vector3 origin = tile.transform.position;
        while (true)
        {
            float t = Time.time * 8f;
            tile.transform.position = origin + new Vector3(Mathf.Sin(t) * 0.08f, Mathf.Abs(Mathf.Sin(t)) * 0.06f, 0f);
            yield return null;
        }
    }

    private void StopVibrate()
    {
        if (_vibrateCoroutine != null)
        {
            StopCoroutine(_vibrateCoroutine);
            _vibrateCoroutine = null;
        }
        // Snap all tiles back
        if (gridManager == null) return;
        Tile t1 = gridManager.GetTile(pixiventiPhase1Tile.x, pixiventiPhase1Tile.y);
        Tile t2 = gridManager.GetTile(targetMoveTile.x, targetMoveTile.y);
        if (t1 != null) t1.transform.position = SnapToGrid(t1);
        if (t2 != null) t2.transform.position = SnapToGrid(t2);
    }

    private Vector3 SnapToGrid(Tile tile)
    {
        // Round to nearest 0.1 to cancel accumulated float drift
        Vector3 p = tile.transform.position;
        return new Vector3(Mathf.Round(p.x * 10f) / 10f, Mathf.Round(p.y * 10f) / 10f, Mathf.Round(p.z * 10f) / 10f);
    }

    // ── Button Factory ────────────────────────────────────────────────────────

    private Button MakeButton(string label, Color color)
    {
        var btn = new Button { text = label };
        btn.style.fontSize                = 16;
        btn.style.unityFontStyleAndWeight = FontStyle.Bold;
        btn.style.color                   = Color.white;
        btn.style.backgroundColor         = color;
        btn.style.paddingLeft  = 20; btn.style.paddingRight  = 20;
        btn.style.paddingTop   = 10; btn.style.paddingBottom = 10;
        btn.style.borderTopLeftRadius    = 6;
        btn.style.borderTopRightRadius   = 6;
        btn.style.borderBottomLeftRadius = 6;
        btn.style.borderBottomRightRadius = 6;
        btn.style.borderTopWidth = 0; btn.style.borderBottomWidth = 0;
        btn.style.borderLeftWidth = 0; btn.style.borderRightWidth = 0;
        return btn;
    }
}
