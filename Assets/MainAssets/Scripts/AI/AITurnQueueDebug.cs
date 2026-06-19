using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Debug overlay that displays the full enemy turn queue at the start of each enemy turn.
/// Shows every scored action (monster name, action type, score), sorted highest to lowest.
/// The currently executing action is highlighted in gold.
///
/// Visibility is controlled by the "Show Turn Debug Panel" checkbox on EnemyTurnController.
/// When enabled the panel is always visible and refreshes each enemy turn.
///
/// Auto-creates itself â€” no prefab or scene setup required.
///
/// Called by EnemyTurnController:
///   AITurnQueueDebug.SetEnabled(bool);
///   AITurnQueueDebug.ShowQueue(queue);
///   AITurnQueueDebug.SetActiveIndex(i);
///   AITurnQueueDebug.ClearActiveIndex();
/// </summary>
public class AITurnQueueDebug : MonoBehaviour
{
    // â”€â”€ Singleton â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public static AITurnQueueDebug Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        new GameObject("AITurnQueueDebug").AddComponent<AITurnQueueDebug>();
    }

    // â”€â”€ Internal state â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private GameObject      _panelRoot;
    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _queueText;

    private List<string> _lines       = new List<string>();
    private int          _activeIndex = -1;

    // â”€â”€ Lifecycle â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        _panelRoot.SetActive(false); // hidden until EnemyTurnController enables it
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // â”€â”€ Public API â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Shows or hides the panel and sets its height.
    /// Driven by the Inspector fields on EnemyTurnController.
    /// Safe to call from Awake() â€” creates the singleton on demand if it does not exist yet.
    /// </summary>
    public static void SetEnabled(bool enabled, float panelHeight = 800f)
    {
        if (Instance == null)
            new GameObject("AITurnQueueDebug").AddComponent<AITurnQueueDebug>();
        Instance?.DoSetEnabled(enabled, panelHeight);
    }

    /// <summary>
    /// Refreshes the panel with a new turn queue.
    /// Call once at the start of each enemy turn after the queue is sorted.
    /// </summary>
    public static void ShowQueue(List<(Monster monster, AIAction action)> queue)
        => Instance?.DoShowQueue(queue);

    /// <summary>Highlights the entry at <paramref name="index"/> as currently executing.</summary>
    public static void SetActiveIndex(int index)
        => Instance?.DoSetActiveIndex(index);

    /// <summary>Clears the active highlight (call when the enemy turn ends).</summary>
    public static void ClearActiveIndex()
        => Instance?.DoSetActiveIndex(-1);

    // â”€â”€ Implementation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void DoSetEnabled(bool enabled, float panelHeight = 800f)
    {
        var rt = _panelRoot.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, panelHeight);
        _panelRoot.SetActive(enabled);
    }

    private void DoShowQueue(List<(Monster monster, AIAction action)> queue)
    {
        _lines.Clear();
        _activeIndex = -1;

        _titleText.text = "Enemy Turn Queue";

        for (int i = 0; i < queue.Count; i++)
        {
            var (monster, action) = queue[i];
            string monsterName = monster.Data?.displayName ?? monster.gameObject.name;
            string actionDesc  = FormatAction(monster, action);
            string scorePart   = $"<color=#aaaaaa>[{action.Score:F1}]</color>";
            _lines.Add($"<color=#666666>#{i + 1:D2}</color>  {monsterName}  â†’  {actionDesc}  {scorePart}");
        }

        Redraw();
    }

    private void DoSetActiveIndex(int index)
    {
        _activeIndex = index;
        Redraw();
    }

    // â”€â”€ Text Rebuild â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void Redraw()
    {
        if (_lines.Count == 0) { _queueText.text = ""; return; }

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < _lines.Count; i++)
        {
            bool isActive = (i == _activeIndex);
            bool isDone   = (i < _activeIndex);

            if (isActive)
            {
                // Gold + arrow for the action currently executing
                sb.AppendLine($"<color=#FFD700><b>â–º {StripColorTags(_lines[i])}</b></color>");
            }
            else if (isDone)
            {
                // Dim out completed entries (no strikethrough â€” TMP bug with long lines)
                sb.AppendLine($"<color=#3a3a3a>âœ“  {StripColorTags(_lines[i])}</color>");
            }
            else
            {
                sb.AppendLine(_lines[i]);
            }
        }
        _queueText.text = sb.ToString();
    }

    // â”€â”€ Action Formatting â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private string FormatAction(Monster monster, AIAction action)
    {
        switch (action)
        {
            case AttackAction atk:
            {
                string atkName  = GetAttackName(monster, atk.AttackIndex);
                string tgtName  = atk.Target?.Data?.displayName ?? atk.Target?.gameObject.name ?? "?";
                return $"<color=#FF6B6B>Attack</color> <b>{atkName}</b> on <b>{tgtName}</b>";
            }

            case MoveAndAttackAction mva:
            {
                string atkName  = GetAttackName(monster, mva.AttackIndex);
                string tgtName  = mva.Target?.Data?.displayName ?? mva.Target?.gameObject.name ?? "?";
                string dest     = mva.MoveTo != null ? mva.MoveTo.GridPosition.ToString() : "?";
                return $"<color=#6BB5FF>Move</color> to {dest} â†’ <color=#FF6B6B>Attack</color> <b>{atkName}</b> on <b>{tgtName}</b>";
            }

            case MoveAction mv:
            {
                string dest = mv.Destination != null ? mv.Destination.GridPosition.ToString() : "?";
                return $"<color=#6BB5FF>Move</color> to {dest}";
            }

            case PassAction _:
                return "<color=#888888>Pass</color>";

            default:
                return action.GetType().Name;
        }
    }

    private string GetAttackName(Monster monster, int index)
    {
        var attacks = monster.GetAttacks();
        if (attacks == null || index < 0 || index >= attacks.Count) return "?";
        return attacks[index]?.data?.DisplayName ?? "?";
    }

    // Very simple tag stripper â€” removes <color=...> and </color> wrappers
    // so we can re-wrap the whole line in a different colour.
    private string StripColorTags(string s)
    {
        // Use regex-free approach: strip the known tag patterns we generate above.
        // This is debug UI so performance is not critical.
        return System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "");
    }

    // â”€â”€ UI Construction â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void BuildUI()
    {
        // Root canvas
        var canvasGO = new GameObject("AIDebugCanvas");
        canvasGO.transform.SetParent(transform);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // Panel â€” stretches the full screen height on the right edge.
        // RectMask2D clips anything that overflows the panel bounds.
        _panelRoot = new GameObject("QueuePanel");
        _panelRoot.transform.SetParent(canvasGO.transform, false);

        var panelRT = _panelRoot.AddComponent<RectTransform>();
        panelRT.anchorMin        = new Vector2(1f, 1f); // top-right corner â€” grows downward
        panelRT.anchorMax        = new Vector2(1f, 1f);
        panelRT.pivot            = new Vector2(1f, 1f); // pivot at top-right so height expands downward
        panelRT.anchoredPosition = new Vector2(-10f, -10f);
        panelRT.sizeDelta        = new Vector2(540f, 800f); // height overwritten by SetEnabled

        var bg = _panelRoot.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.04f, 0.10f, 0.88f);

        _panelRoot.AddComponent<RectMask2D>(); // clips child text at panel edges

        // Title â€” pinned to top of panel
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(_panelRoot.transform, false);

        var titleRT = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin        = new Vector2(0f, 1f);
        titleRT.anchorMax        = new Vector2(1f, 1f);
        titleRT.pivot            = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -8f);
        titleRT.sizeDelta        = new Vector2(-16f, 34f);

        _titleText                    = titleGO.AddComponent<TextMeshProUGUI>();
        _titleText.fontSize           = 15f;
        _titleText.color              = new Color(1f, 0.85f, 0.2f);
        _titleText.alignment          = TextAlignmentOptions.Center;
        _titleText.fontStyle          = FontStyles.Bold;
        _titleText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        _titleText.overflowMode       = TextOverflowModes.Truncate;

        // Separator line under the title
        var sepGO = new GameObject("Separator");
        sepGO.transform.SetParent(_panelRoot.transform, false);

        var sepRT = sepGO.AddComponent<RectTransform>();
        sepRT.anchorMin        = new Vector2(0f, 1f);
        sepRT.anchorMax        = new Vector2(1f, 1f);
        sepRT.pivot            = new Vector2(0.5f, 1f);
        sepRT.anchoredPosition = new Vector2(0f, -44f);
        sepRT.sizeDelta        = new Vector2(-16f, 2f);

        sepGO.AddComponent<Image>().color = new Color(1f, 0.85f, 0.2f, 0.35f);

        // Queue text â€” fills the rest of the panel below the title.
        // Uses Truncate overflow so TMP never tries to draw outside its rect.
        var textGO = new GameObject("QueueText");
        textGO.transform.SetParent(_panelRoot.transform, false);

        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0f, 0f);
        textRT.anchorMax = new Vector2(1f, 1f);
        textRT.offsetMin = new Vector2(10f, 8f);
        textRT.offsetMax = new Vector2(-10f, -50f); // 50 px gap at top for title + separator

        _queueText                    = textGO.AddComponent<TextMeshProUGUI>();
        _queueText.fontSize           = 13f;
        _queueText.color              = new Color(0.85f, 0.85f, 0.85f);
        _queueText.alignment          = TextAlignmentOptions.TopLeft;
        _queueText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        _queueText.richText           = true;
        _queueText.lineSpacing        = 2f;
        _queueText.overflowMode       = TextOverflowModes.Truncate;
    }
}

