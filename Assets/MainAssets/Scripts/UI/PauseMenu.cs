using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Pause menu — toggled by the Pause hotkey (default: Escape).
/// AUTO-SETUP: creates its own Canvas at runtime. No scene placement required.
/// Visual style is driven by a UIStyleConfig asset in Assets/Resources/.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        new GameObject("PauseMenu").AddComponent<PauseMenu>();
    }

    // ── State ─────────────────────────────────────────────────────────────────

    private bool        _isOpen;
    private GameObject  _overlay;   // full-screen dim + panel container
    private Toggle      _edgeScrollToggle;

    // Style — loaded once in BuildUI()
    private Sprite _panelSprite;
    private Sprite _buttonSprite;
    private Color  _panelColor;
    private Color  _resumeColor;
    private Color  _keyBindColor;

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
        bool togglePressed = HotkeyManager.Instance != null
            ? HotkeyManager.Instance.WasPressedThisFrame(HotkeyAction.Pause)
            : Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

        if (togglePressed) Toggle();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Toggle() => SetVisible(!_isOpen);
    public void Open()   => SetVisible(true);
    public void Close()  => SetVisible(false);

    private void SetVisible(bool visible)
    {
        _isOpen = visible;
        _overlay.SetActive(visible);
        Time.timeScale = visible ? 0f : 1f;

        if (visible && _edgeScrollToggle != null)
            _edgeScrollToggle.isOn = GameSettings.EdgeScrollEnabled;
    }

    private void OpenKeyBindings()
    {
        var hotkeyUI = Object.FindFirstObjectByType<HotkeyRebindUI>(FindObjectsInactive.Include);
        if (hotkeyUI == null)
        {
            Debug.LogWarning("[PauseMenu] HotkeyRebindUI not found — " +
                             "make sure HotkeyPanel exists in the scene.");
            return;
        }

        // Hide our overlay entirely (game stays paused — timeScale is still 0).
        // This avoids any canvas sort-order conflict with the hotkey panel.
        _overlay.SetActive(false);

        // When the hotkey panel's Back button is pressed it calls Hide(),
        // which fires onClose — we use that to restore the pause menu.
        hotkeyUI.onClose += RestoreFromKeyBindings;
        hotkeyUI.Show();
    }

    private void RestoreFromKeyBindings()
    {
        // Back was pressed inside key bindings — restore the pause overlay.
        _overlay.SetActive(true);
        // Sync the edge-scroll toggle in case settings changed while away.
        if (_edgeScrollToggle != null)
            _edgeScrollToggle.isOn = GameSettings.EdgeScrollEnabled;
    }

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Load style from the UIStyleConfig asset (falls back to defaults if absent)
        var s        = UIStyleConfig.Load();
        _panelSprite  = s?.panelSprite;
        _buttonSprite = s?.buttonSprite;
        _panelColor   = s?.pausePanelColor    ?? new Color(0.07f, 0.07f, 0.12f, 0.98f);
        _resumeColor  = s?.resumeButtonColor  ?? new Color(0.15f, 0.50f, 0.20f, 1f);
        _keyBindColor = s?.keyBindButtonColor ?? new Color(0.20f, 0.20f, 0.38f, 1f);

        // Canvas
        var canvasGO        = new GameObject("PauseCanvas");
        canvasGO.transform.SetParent(transform);
        var canvas          = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        var scaler                 = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Full-screen dim overlay
        _overlay = MakeChild(canvasGO, "Overlay");
        var overlayRT       = _overlay.GetComponent<RectTransform>();
        overlayRT.anchorMin = Vector2.zero;
        overlayRT.anchorMax = Vector2.one;
        overlayRT.sizeDelta = Vector2.zero;
        _overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

        // Center panel
        var panel   = MakeChild(_overlay, "Panel");
        var panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin        = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax        = new Vector2(0.5f, 0.5f);
        panelRT.pivot            = new Vector2(0.5f, 0.5f);
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.sizeDelta        = new Vector2(480f, 430f);
        UIStyleConfig.ApplySprite(panel.AddComponent<Image>(), _panelSprite, _panelColor);

        float y = 140f;
        AddLabel(panel, "PAUSED",   36f, FontStyles.Bold,   Color.white,                   ref y, 50f);
        AddDivider(panel, ref y, 10f);
        AddLabel(panel, "Settings", 20f, FontStyles.Italic, new Color(0.6f, 0.6f, 0.75f), ref y, 30f);
        _edgeScrollToggle = AddToggle(panel, "Edge Scroll", GameSettings.EdgeScrollEnabled, ref y,
            v => GameSettings.EdgeScrollEnabled = v);
        AddDivider(panel, ref y, 14f);
        AddButton(panel, "Key Bindings", _keyBindColor, ref y, OpenKeyBindings);
        AddDivider(panel, ref y, 6f);
        AddButton(panel, "Resume",       _resumeColor,  ref y, Close);
    }

    // ── Layout helpers ────────────────────────────────────────────────────────

    private void AddLabel(GameObject parent, string text, float fontSize,
                          FontStyles style, Color color, ref float y, float height)
    {
        var go = MakeChild(parent, text);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -y);
        rt.sizeDelta        = new Vector2(420f, height);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = fontSize; tmp.fontStyle = style;
        tmp.color = color; tmp.alignment = TextAlignmentOptions.Center;
        y += height + 6f;
    }

    private void AddDivider(GameObject parent, ref float y, float extra)
    {
        var go = MakeChild(parent, "Divider");
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -y);
        rt.sizeDelta        = new Vector2(400f, 2f);
        go.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.45f, 1f);
        y += 2f + extra;
    }

    private Toggle AddToggle(GameObject parent, string label, bool initial,
                             ref float y, System.Action<bool> onChange)
    {
        const float h = 36f;
        var row = MakeChild(parent, label + "Row");
        var rt  = row.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -y); rt.sizeDelta = new Vector2(420f, h);
        y += h + 8f;

        var lblGO = MakeChild(row, "Label");
        var lblRT = lblGO.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = new Vector2(0.75f, 1f);
        lblRT.sizeDelta = Vector2.zero;
        var lblTmp = lblGO.AddComponent<TextMeshProUGUI>();
        lblTmp.text = label; lblTmp.fontSize = 18f;
        lblTmp.color = new Color(0.88f, 0.88f, 0.88f);
        lblTmp.alignment = TextAlignmentOptions.MidlineLeft;

        var tglGO = MakeChild(row, "Toggle");
        var tglRT = tglGO.GetComponent<RectTransform>();
        tglRT.anchorMin = new Vector2(0.75f, 0.5f); tglRT.anchorMax = new Vector2(0.75f, 0.5f);
        tglRT.pivot = new Vector2(0.5f, 0.5f);
        tglRT.anchoredPosition = new Vector2(20f, 0f); tglRT.sizeDelta = new Vector2(28f, 28f);
        var bg = tglGO.AddComponent<Image>();
        var tgl = tglGO.AddComponent<Toggle>();
        tgl.targetGraphic = bg; tgl.isOn = initial;

        var checkGO = MakeChild(tglGO, "Checkmark");
        var checkRT = checkGO.GetComponent<RectTransform>();
        checkRT.anchorMin = new Vector2(0.1f, 0.1f); checkRT.anchorMax = new Vector2(0.9f, 0.9f);
        checkRT.sizeDelta = Vector2.zero;
        var checkImg = checkGO.AddComponent<Image>();
        checkImg.color = new Color(0.2f, 0.9f, 0.4f);
        tgl.graphic = checkImg;
        tgl.onValueChanged.AddListener(v => {
            onChange?.Invoke(v);
            bg.color = v ? new Color(0.1f, 0.35f, 0.15f) : new Color(0.2f, 0.2f, 0.3f);
        });
        bg.color = initial ? new Color(0.1f, 0.35f, 0.15f) : new Color(0.2f, 0.2f, 0.3f);
        return tgl;
    }

    private void AddButton(GameObject parent, string label, Color bgColor,
                           ref float y, System.Action onClick)
    {
        const float h = 44f;
        var go = MakeChild(parent, label + "Btn");
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -y); rt.sizeDelta = new Vector2(280f, h);
        y += h + 8f;

        var img = go.AddComponent<Image>();
        UIStyleConfig.ApplySprite(img, _buttonSprite, bgColor);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var lblGO = MakeChild(go, "Lbl");
        var lblRT = lblGO.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one; lblRT.sizeDelta = Vector2.zero;
        var tmp = lblGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 20f; tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white; tmp.alignment = TextAlignmentOptions.Center;
    }

    private static GameObject MakeChild(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        return go;
    }
}
