using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Pause menu opened/closed with the Escape key.
///
/// AUTO-SETUP — No scene work required.  The menu creates its own Canvas and
/// UI elements at runtime.
///
/// Features:
///   • Pauses game time (Time.timeScale = 0) while open
///   • Settings panel with edge-scroll toggle (persisted via GameSettings)
///   • Resume button / Escape key to close
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }

    // ── Auto-Create ───────────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        new GameObject("PauseMenu").AddComponent<PauseMenu>();
    }

    // ── State ─────────────────────────────────────────────────────────────────

    private bool _isOpen;

    // ── UI References ─────────────────────────────────────────────────────────

    private GameObject _overlay;
    private Toggle     _edgeScrollToggle;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        SetVisible(false);
    }

    private void Update()
    {
        // Escape key: toggle pause (works even when timeScale = 0 because Update
        // is frame-based, not time-based)
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Toggle();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Toggle() => SetVisible(!_isOpen);

    public void Open()  => SetVisible(true);
    public void Close() => SetVisible(false);

    // ── Private helpers ───────────────────────────────────────────────────────

    private void SetVisible(bool visible)
    {
        _isOpen = visible;
        _overlay.SetActive(visible);
        Time.timeScale = visible ? 0f : 1f;

        // Sync toggle to current setting each time the menu opens
        if (visible && _edgeScrollToggle != null)
            _edgeScrollToggle.isOn = GameSettings.EdgeScrollEnabled;
    }

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Canvas
        var canvasGO = new GameObject("PauseCanvas");
        canvasGO.transform.SetParent(transform);
        var canvas          = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900; // above everything else

        var scaler                 = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Dim overlay (full screen semi-transparent black) ──────────────────
        _overlay = MakeChild(canvasGO, "Overlay");
        var overlayRT = _overlay.GetComponent<RectTransform>();
        overlayRT.anchorMin = Vector2.zero;
        overlayRT.anchorMax = Vector2.one;
        overlayRT.sizeDelta = Vector2.zero;
        _overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

        // ── Center panel ─────────────────────────────────────────────────────
        var panel    = MakeChild(_overlay, "Panel");
        var panelRT  = panel.GetComponent<RectTransform>();
        panelRT.anchorMin        = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax        = new Vector2(0.5f, 0.5f);
        panelRT.pivot            = new Vector2(0.5f, 0.5f);
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.sizeDelta        = new Vector2(480f, 360f);
        panel.AddComponent<Image>().color = new Color(0.07f, 0.07f, 0.12f, 0.98f);

        float y = 140f; // top of content area, stepping down

        // ── Title ─────────────────────────────────────────────────────────────
        AddLabel(panel, "PAUSED", 36f, FontStyles.Bold, Color.white, ref y, 50f);

        // ── Divider ───────────────────────────────────────────────────────────
        AddDivider(panel, ref y, 10f);

        // ── Settings header ───────────────────────────────────────────────────
        AddLabel(panel, "Settings", 20f, FontStyles.Italic,
                 new Color(0.6f, 0.6f, 0.75f), ref y, 30f);

        // ── Edge Scroll toggle ────────────────────────────────────────────────
        _edgeScrollToggle = AddToggle(panel, "Edge Scroll", GameSettings.EdgeScrollEnabled, ref y,
            isOn => GameSettings.EdgeScrollEnabled = isOn);

        // ── Divider ───────────────────────────────────────────────────────────
        AddDivider(panel, ref y, 14f);

        // ── Resume button ─────────────────────────────────────────────────────
        AddButton(panel, "Resume", new Color(0.15f, 0.5f, 0.2f), ref y, Close);
    }

    // ── Layout helpers ────────────────────────────────────────────────────────

    private void AddLabel(GameObject parent, string text, float fontSize,
                          FontStyles style, Color color, ref float y, float height)
    {
        var go = MakeChild(parent, text);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -y);
        rt.sizeDelta        = new Vector2(420f, height);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.fontStyle = style;
        tmp.color     = color;
        tmp.alignment = TextAlignmentOptions.Center;

        y += height + 6f;
    }

    private void AddDivider(GameObject parent, ref float y, float extraPadding)
    {
        var go = MakeChild(parent, "Divider");
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -y);
        rt.sizeDelta        = new Vector2(400f, 2f);
        go.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.45f, 1f);
        y += 2f + extraPadding;
    }

    private Toggle AddToggle(GameObject parent, string label, bool initialValue,
                             ref float y, System.Action<bool> onChange)
    {
        const float height = 36f;

        var row = MakeChild(parent, label + "Row");
        var rt  = row.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -y);
        rt.sizeDelta        = new Vector2(420f, height);
        y += height + 8f;

        // Label text (left side)
        var lblGO = MakeChild(row, "Label");
        var lblRT = lblGO.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = new Vector2(0.75f, 1f);
        lblRT.sizeDelta = Vector2.zero;
        var lblTmp = lblGO.AddComponent<TextMeshProUGUI>();
        lblTmp.text      = label;
        lblTmp.fontSize  = 18f;
        lblTmp.color     = new Color(0.88f, 0.88f, 0.88f);
        lblTmp.alignment = TextAlignmentOptions.MidlineLeft;

        // Toggle (right side)
        var tglGO = MakeChild(row, "Toggle");
        var tglRT = tglGO.GetComponent<RectTransform>();
        tglRT.anchorMin        = new Vector2(0.75f, 0.5f);
        tglRT.anchorMax        = new Vector2(0.75f, 0.5f);
        tglRT.pivot            = new Vector2(0.5f, 0.5f);
        tglRT.anchoredPosition = new Vector2(20f, 0f);
        tglRT.sizeDelta        = new Vector2(28f, 28f);

        var bg = tglGO.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.2f, 0.3f);

        var tgl = tglGO.AddComponent<Toggle>();
        tgl.targetGraphic = bg;
        tgl.isOn = initialValue;

        // Checkmark
        var checkGO = MakeChild(tglGO, "Checkmark");
        var checkRT = checkGO.GetComponent<RectTransform>();
        checkRT.anchorMin = new Vector2(0.1f, 0.1f);
        checkRT.anchorMax = new Vector2(0.9f, 0.9f);
        checkRT.sizeDelta = Vector2.zero;
        var checkImg = checkGO.AddComponent<Image>();
        checkImg.color = new Color(0.2f, 0.9f, 0.4f);

        tgl.graphic = checkImg;
        tgl.onValueChanged.AddListener(v =>
        {
            onChange?.Invoke(v);
            bg.color = v ? new Color(0.1f, 0.35f, 0.15f) : new Color(0.2f, 0.2f, 0.3f);
        });

        // Set initial bg color
        bg.color = initialValue ? new Color(0.1f, 0.35f, 0.15f) : new Color(0.2f, 0.2f, 0.3f);

        return tgl;
    }

    private void AddButton(GameObject parent, string label, Color bgColor,
                           ref float y, System.Action onClick)
    {
        const float height = 44f;

        var go = MakeChild(parent, label + "Btn");
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -y);
        rt.sizeDelta        = new Vector2(220f, height);
        y += height + 8f;

        var img = go.AddComponent<Image>();
        img.color = bgColor;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var colors = btn.colors;
        colors.highlightedColor = new Color(bgColor.r + 0.15f, bgColor.g + 0.15f, bgColor.b + 0.15f);
        colors.pressedColor     = new Color(bgColor.r - 0.1f,  bgColor.g - 0.1f,  bgColor.b - 0.1f);
        btn.colors = colors;

        var lblGO = MakeChild(go, "Lbl");
        var lblRT = lblGO.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.sizeDelta = Vector2.zero;
        var tmp = lblGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 20f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
    }

    private static GameObject MakeChild(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        return go;
    }
}
