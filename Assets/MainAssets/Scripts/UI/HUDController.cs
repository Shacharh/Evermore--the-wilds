using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Wires up the battle HUD:
///   • AP bar — fills its progress bar and updates text with current AP.
///   • End Turn button — enabled/disabled based on whose turn it is.
///   • Menu button — opens the pause/settings menu.
///
/// HOW TO USE IN UNITY:
///   The script is placed on the "HUD" GameObject in the scene.
///   It auto-finds PlayerTurnController at runtime.
///   The old uGUI Canvas (UI_Canvas) is disabled automatically on Start.
///
/// NOTE: TurnManager, PlayerTurnController and EnemyTurnController are
/// auto-created at runtime — you do NOT need to add them to the scene manually.
/// The HUD will find the PlayerTurnController automatically once it exists.
/// </summary>
public class HUDController : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    [SerializeField] private PlayerTurnController playerTurnController;

    // ── UI Elements ───────────────────────────────────────────────────────────

    private VisualElement _apFill;
    private Label         _apText;
    private Button        _endTurnBtn;
    private bool          _eventsWired;

    // True once we have received a real AP value (or connected to an already-active turn).
    // Prevents showing "0 / max" during the brief startup window before BeginTurn fires.
    private bool          _apInitialized;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        // Disable the old uGUI Canvas — this HUD is now fully UI Toolkit
        var oldCanvas = GetComponentInParent<Canvas>();
        if (oldCanvas != null) oldCanvas.enabled = false;

        Debug.Log($"[HUDController] Start — frame {Time.frameCount}, instance {GetInstanceID()}");
        BuildUI();
        SetEndTurnInteractable(false);
        StartCoroutine(RetryConnect());
    }

    // Every LateUpdate, verify the HUD state matches PlayerTurnController's actual state.
    // Also detects if the subscribed PTC was destroyed mid-scene-transition and reconnects.
    private void LateUpdate()
    {
        // If PTC was destroyed (scene-reload race: HUD subscribed to the old PTC before it
        // was torn down), reset and let RetryConnect pick up the new one.
        if (_eventsWired && playerTurnController == null)
        {
            Debug.LogWarning("[HUDController] Subscribed PTC was destroyed — resetting to reconnect to new scene's PTC.");
            _eventsWired = false;
            _apInitialized = false;
            StartCoroutine(RetryConnect());
            return;
        }

        if (!_eventsWired || playerTurnController == null) return;

        bool shouldBeActive = playerTurnController.IsActive;

        if (_endTurnBtn != null && _endTurnBtn.enabledSelf != shouldBeActive)
        {
            Debug.LogWarning($"[HUDController] End Turn button desynced " +
                             $"(was {_endTurnBtn.enabledSelf}, should be {shouldBeActive}) — correcting.");
            SetEndTurnInteractable(shouldBeActive);
        }

        if (shouldBeActive && _apText != null && _apText.text == "-- / --")
        {
            Debug.LogWarning("[HUDController] AP text desynced on active turn — correcting.");
            _apInitialized = true;
            RefreshAP(playerTurnController.CurrentAP, playerTurnController.MaxAP);
        }
    }

    private void OnDestroy()
    {
        if (playerTurnController == null) return;
        playerTurnController.onAPChanged.RemoveListener(OnAPChanged);
        playerTurnController.onTurnStart.RemoveListener(OnPlayerTurnStart);
        playerTurnController.onTurnEnd  .RemoveListener(OnPlayerTurnEnd);
    }

    // ── Controller Connection ─────────────────────────────────────────────────

    /// <summary>Polls every 0.25 s until PlayerTurnController is available.</summary>
    private System.Collections.IEnumerator RetryConnect()
    {
        int attempt = 0;
        while (!_eventsWired)
        {
            attempt++;
            Debug.Log($"[HUDController] RetryConnect attempt {attempt} — frame {Time.frameCount}, timeScale={Time.timeScale}");
            TryConnectController();
            yield return new UnityEngine.WaitForSeconds(0.25f);
        }
        Debug.Log($"[HUDController] RetryConnect done after {attempt} attempt(s) — frame {Time.frameCount}");
    }

    private void TryConnectController()
    {
        if (playerTurnController == null)
            playerTurnController = FindFirstObjectByType<PlayerTurnController>();
        if (playerTurnController == null) return;
        if (_eventsWired) return;
        _eventsWired = true;

        playerTurnController.onAPChanged.AddListener(OnAPChanged);
        playerTurnController.onTurnStart.AddListener(OnPlayerTurnStart);
        playerTurnController.onTurnEnd  .AddListener(OnPlayerTurnEnd);

        if (_endTurnBtn != null)
            _endTurnBtn.clicked += playerTurnController.OnEndTurnButtonPressed;

        Debug.Log($"[HUDController] Connected to PlayerTurnController — " +
                  $"IsActive={playerTurnController.IsActive}, " +
                  $"CurrentAP={playerTurnController.CurrentAP}, frame={Time.frameCount}");

        // If connecting AFTER BeginTurn has already fired (late Start()-ordering on scene reload),
        // the onAPChanged / onTurnStart events were already sent and won't fire again — sync now.
        // If connecting during the startup window (IsActive=false), leave the HUD at "--/--";
        // the imminent onAPChanged / onTurnStart events will update it correctly.
        if (playerTurnController.IsActive)
        {
            _apInitialized = true;
            RefreshAP(playerTurnController.CurrentAP, playerTurnController.MaxAP);
            SetEndTurnInteractable(true);
        }
    }

    // ── Callbacks ─────────────────────────────────────────────────────────────

    private void OnAPChanged(int newAP)
    {
        _apInitialized = true;
        RefreshAP(newAP, playerTurnController.MaxAP);
    }

    private void OnPlayerTurnStart()
    {
        Debug.Log($"[HUDController] OnPlayerTurnStart — frame {Time.frameCount}");
        SetEndTurnInteractable(true);
    }

    private void OnPlayerTurnEnd()
    {
        Debug.Log($"[HUDController] OnPlayerTurnEnd — frame {Time.frameCount}");
        SetEndTurnInteractable(false);
    }

    // ── UI Updates ────────────────────────────────────────────────────────────

    private void RefreshAP(int current, int max)
    {
        float ratio = (_apInitialized && max > 0) ? (float)current / max : 0f;
        if (_apFill != null)
            _apFill.style.width = new StyleLength(new Length(ratio * 100f, LengthUnit.Percent));
        if (_apText != null)
            _apText.text = _apInitialized ? $"{current} / {max}" : "-- / --";
    }

    private void SetEndTurnInteractable(bool interactable)
    {
        if (_endTurnBtn != null)
            _endTurnBtn.SetEnabled(interactable);
    }

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        var s   = UIStyleConfig.Load();
        var doc = gameObject.AddComponent<UIDocument>();
        doc.panelSettings = s?.panelSettings;
        doc.sortingOrder  = 100;

        var root = doc.rootVisualElement;
        root.pickingMode = PickingMode.Ignore;
        root.style.position = Position.Absolute;
        root.style.left = 0; root.style.top = 0; root.style.right = 0; root.style.bottom = 0;

        // ── Menu button (top-left) ────────────────────────────────────────
        var menuBtn = new Button(() => PauseMenu.Instance?.Toggle());
        menuBtn.text = "MENU";
        menuBtn.style.position = Position.Absolute;
        menuBtn.style.left = 24; menuBtn.style.top = 24;
        menuBtn.style.width = 160; menuBtn.style.height = 50;
        StyleHUDButton(menuBtn, s?.buttonSprite, new Color(0.12f, 0.12f, 0.20f, 1f));
        root.Add(menuBtn);

        // ── AP section (top-center) ───────────────────────────────────────
        // Uses a full-width absolutely-positioned container with centered children
        // so the bar sits in the exact horizontal center regardless of resolution.
        var apSection = new VisualElement();
        apSection.pickingMode = PickingMode.Ignore;
        apSection.style.position      = Position.Absolute;
        apSection.style.left          = 0; apSection.style.right = 0; apSection.style.top = 20;
        apSection.style.flexDirection = FlexDirection.Column;
        apSection.style.alignItems    = Align.Center;

        // Outer bar track
        var barOuter = new VisualElement();
        barOuter.pickingMode = PickingMode.Ignore;
        barOuter.style.width  = 220; barOuter.style.height = 22;
        barOuter.style.backgroundColor       = new Color(0.05f, 0.05f, 0.08f, 0.92f);
        barOuter.style.borderTopLeftRadius    = 4; barOuter.style.borderTopRightRadius    = 4;
        barOuter.style.borderBottomLeftRadius = 4; barOuter.style.borderBottomRightRadius = 4;
        barOuter.style.overflow = Overflow.Hidden;

        // Inner fill — width set as a percentage in RefreshAP()
        _apFill = new VisualElement();
        _apFill.pickingMode = PickingMode.Ignore;
        _apFill.style.height          = new StyleLength(new Length(100f, LengthUnit.Percent));
        _apFill.style.width           = new StyleLength(new Length(0f,   LengthUnit.Percent));
        _apFill.style.backgroundColor = new Color(0.25f, 0.65f, 1f, 0.95f);
        barOuter.Add(_apFill);

        // "current / max" label below the bar
        _apText = new Label("-- / --");
        _apText.pickingMode = PickingMode.Ignore;
        _apText.style.fontSize                = 18;
        _apText.style.unityFontStyleAndWeight = FontStyle.Bold;
        _apText.style.color                   = new Color(0.92f, 0.92f, 0.92f, 1f);
        _apText.style.unityTextAlign          = TextAnchor.MiddleCenter;
        _apText.style.marginTop               = 4;

        apSection.Add(barOuter);
        apSection.Add(_apText);
        root.Add(apSection);

        // ── End Turn button (bottom-right) ────────────────────────────────
        _endTurnBtn = new Button();
        _endTurnBtn.text = "END TURN";
        _endTurnBtn.style.position = Position.Absolute;
        _endTurnBtn.style.right  = 24; _endTurnBtn.style.bottom = 24;
        _endTurnBtn.style.width  = 200; _endTurnBtn.style.height = 54;
        StyleHUDButton(_endTurnBtn, s?.buttonSprite, new Color(0.12f, 0.12f, 0.20f, 1f));
        root.Add(_endTurnBtn);
    }

    private static void StyleHUDButton(Button btn, Sprite sprite, Color fallback)
    {
        btn.style.fontSize                = 20;
        btn.style.unityFontStyleAndWeight = FontStyle.Bold;
        btn.style.color                   = Color.white;
        btn.style.borderTopWidth          = 0; btn.style.borderBottomWidth = 0;
        btn.style.borderLeftWidth         = 0; btn.style.borderRightWidth  = 0;
        btn.style.borderTopLeftRadius     = 0; btn.style.borderTopRightRadius     = 0;
        btn.style.borderBottomLeftRadius  = 0; btn.style.borderBottomRightRadius  = 0;
        UIStyleConfig.ApplySprite(btn, sprite, fallback);
    }
}
