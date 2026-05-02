using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Wires up the battle HUD:
///   • AP_Meter  — fills its Image bar and updates its TMP text with current AP.
///   • End_Turn  — enables/disables the button based on whose turn it is.
///
/// HOW TO USE IN UNITY:
///   1. Select the "CombatGrid > HUD" object in the Hierarchy.
///   2. Add Component → HUDController.
///   3. The script auto-finds AP_Meter, End_Turn, and PlayerTurnController by name.
///      (If auto-find fails, drag the references manually in the Inspector.)
///
/// NOTE: TurnManager, PlayerTurnController and EnemyTurnController are now
/// auto-created at runtime — you do NOT need to add them to the scene manually.
/// The HUD will find the PlayerTurnController automatically once it exists.
/// </summary>
public class HUDController : MonoBehaviour
{
    // ── Inspector fields (auto-filled if left empty) ──────────────────────────

    [Header("AP Meter")]
    [SerializeField] private Image           apBarImage;
    [SerializeField] private TextMeshProUGUI apText;

    [Header("End Turn")]
    [SerializeField] private Button endTurnButton;

    [Header("Pause / Settings Button")]
    [Tooltip("The HUD Button that opens the pause/settings menu.\n" +
             "Leave empty to auto-find by the name in 'Pause Button Name'.")]
    [SerializeField] private Button pauseButton;

    [Tooltip("Name of the HUD GameObject that holds the pause/settings Button component. " +
             "Only used when Pause Button is left empty above.")]
    [SerializeField] private string pauseButtonName = "Settings";

    [Header("References (auto-found if empty)")]
    [SerializeField] private PlayerTurnController playerTurnController;

    // Tracks whether events are already wired (so we don't double-subscribe)
    private bool eventsWired = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        AutoFindUIReferences();
        SetEndTurnInteractable(false); // disabled until player turn starts
        StartCoroutine(RetryConnect());
    }

    /// <summary>
    /// Polls every 0.25 s until PlayerTurnController is available.
    /// Much cheaper than FindFirstObjectByType every frame in Update().
    /// Also retries UI reference discovery in case HUD objects weren't ready yet.
    /// </summary>
    private System.Collections.IEnumerator RetryConnect()
    {
        while (!eventsWired)
        {
            // Re-try UI auto-find if references are still missing
            if (apText == null || endTurnButton == null)
                AutoFindUIReferences();

            TryConnectController();
            yield return new UnityEngine.WaitForSeconds(0.25f);
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

    private void TryConnectController()
    {
        // Find the controller if not yet assigned
        if (playerTurnController == null)
            playerTurnController = FindFirstObjectByType<PlayerTurnController>();

        if (playerTurnController == null) return; // still not available

        // Controller found — wire events and refresh UI
        WireEvents();
        RefreshAP(playerTurnController.CurrentAP, playerTurnController.MaxAP);
        Debug.Log("[HUDController] Connected to PlayerTurnController.");
    }

    // ── Auto-find UI elements ─────────────────────────────────────────────────

    private void AutoFindUIReferences()
    {
        // Pause / Settings button
        if (pauseButton == null && !string.IsNullOrEmpty(pauseButtonName))
        {
            var go = GameObject.Find(pauseButtonName);
            if (go != null)
                pauseButton = go.GetComponent<Button>();
        }

        if (pauseButton != null)
        {
            pauseButton.onClick.RemoveAllListeners();
            pauseButton.onClick.AddListener(() => PauseMenu.Instance?.Toggle());
        }

        // AP_Meter
        if (apBarImage == null)
        {
            GameObject apMeterGO = GameObject.Find("AP_Meter");
            if (apMeterGO != null)
            {
                apBarImage = apMeterGO.GetComponent<Image>();

                if (apBarImage != null)
                {
                    apBarImage.type       = Image.Type.Filled;
                    apBarImage.fillMethod = Image.FillMethod.Horizontal;
                    apBarImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                }

                if (apText == null)
                    apText = apMeterGO.GetComponentInChildren<TextMeshProUGUI>();
            }
            else
            {
                Debug.LogWarning("[HUDController] 'AP_Meter' GameObject not found. " +
                                 "Assign apBarImage and apText manually in the Inspector.");
            }
        }

        // End_Turn button
        if (endTurnButton == null)
        {
            GameObject endTurnGO = GameObject.Find("End_Turn");
            if (endTurnGO != null)
                endTurnButton = endTurnGO.GetComponent<Button>();
            else
                Debug.LogWarning("[HUDController] 'End_Turn' GameObject not found. " +
                                 "Assign endTurnButton manually in the Inspector.");
        }
    }

    // ── Event Wiring ──────────────────────────────────────────────────────────

    private void WireEvents()
    {
        if (eventsWired || playerTurnController == null) return;
        eventsWired = true;

        playerTurnController.onAPChanged.AddListener(OnAPChanged);
        playerTurnController.onTurnStart.AddListener(OnPlayerTurnStart);
        playerTurnController.onTurnEnd  .AddListener(OnPlayerTurnEnd);

        if (endTurnButton != null)
        {
            endTurnButton.onClick.RemoveAllListeners();
            endTurnButton.onClick.AddListener(playerTurnController.OnEndTurnButtonPressed);
        }
    }

    // ── Callbacks ─────────────────────────────────────────────────────────────

    private void OnAPChanged(int newAP)
        => RefreshAP(newAP, playerTurnController.MaxAP);

    private void OnPlayerTurnStart() => SetEndTurnInteractable(true);
    private void OnPlayerTurnEnd()   => SetEndTurnInteractable(false);

    // ── UI Updates ────────────────────────────────────────────────────────────

    private void RefreshAP(int current, int max)
    {
        float ratio = max > 0 ? (float)current / max : 0f;

        if (apBarImage != null)
            apBarImage.fillAmount = ratio;

        // Format: "current / max"  (e.g. "4 / 6")
        if (apText != null)
            apText.text = $"{current} / {max}";
    }

    private void SetEndTurnInteractable(bool interactable)
    {
        if (endTurnButton != null)
            endTurnButton.interactable = interactable;
    }
}
