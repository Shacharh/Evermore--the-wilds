using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Small persistent overlay that shows the AP cost of moving to the currently
/// hovered tile while the player is in movement mode.
///
/// Appears in the top-left corner (away from the monster info panel which sits
/// bottom-right). Instantly shows/hides — no fade animation — because it must
/// update every frame as the player hovers different tiles.
///
/// Usage (from InputManager):
///   MoveCostHint.Show("Move cost: 2 AP");
///   MoveCostHint.Hide();
///
/// Auto-created singleton — no prefab or scene setup needed.
/// </summary>
public class MoveCostHint : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────

    public static MoveCostHint Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        new GameObject("MoveCostHint").AddComponent<MoveCostHint>();
    }

    // ── UI References ─────────────────────────────────────────────────────────

    private GameObject      panelRoot;
    private TextMeshProUGUI hintText;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        panelRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Shows the hint panel with the given text. Safe to call every frame.</summary>
    public static void Show(string text)
    {
        if (Instance == null) return;
        Instance.hintText.text = text;
        Instance.panelRoot.SetActive(true);
    }

    /// <summary>Hides the hint panel.</summary>
    public static void Hide()
    {
        if (Instance == null) return;
        Instance.panelRoot.SetActive(false);
    }

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Root canvas — ScreenSpaceOverlay, sort order 600 (above game world, below battle messages)
        var canvasGO = new GameObject("MoveCostCanvas");
        canvasGO.transform.SetParent(transform);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        // Non-interactive — just a display label
        var cg = canvasGO.AddComponent<CanvasGroup>();
        cg.interactable   = false;
        cg.blocksRaycasts = false;

        // ── Panel — top-left corner, 260 × 52 px in 1920×1080 reference space ─
        panelRoot = MakeChild(canvasGO, "HintPanel");
        var rt = panelRoot.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(20f, -20f);   // 20 px from top-left edge
        rt.sizeDelta        = new Vector2(280f, 52f);

        var bg = panelRoot.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.10f, 0.04f, 0.92f);   // dark green tint

        // ── Text ──────────────────────────────────────────────────────────────
        var textGO = MakeChild(panelRoot, "Text");
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(12f, 6f);
        textRT.offsetMax = new Vector2(-12f, -6f);

        hintText                        = textGO.AddComponent<TextMeshProUGUI>();
        hintText.fontSize               = 22f;
        hintText.color                  = new Color(0.6f, 1f, 0.6f, 1f);  // light green
        hintText.alignment              = TextAlignmentOptions.Center;
        hintText.fontStyle              = FontStyles.Bold;
        hintText.enableWordWrapping     = false;
    }

    private static GameObject MakeChild(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        return go;
    }
}
