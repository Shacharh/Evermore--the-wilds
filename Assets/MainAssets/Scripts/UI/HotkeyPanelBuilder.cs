using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach this to HotkeyPanel alongside HotkeyRebindUI.
/// Point "Row Template" at the single row you built manually (Row_Move).
///
/// On Awake it:
///   • Moves Row_Move into a VerticalLayoutGroup container
///   • Clones it 8 more times (one per remaining action)
///   • Sets each row's action-name label automatically
///   • Creates the Reset All and Back buttons (using your button sprite if assigned)
///   • Creates the "Press any key…" listening overlay (using your panel sprite if assigned)
///   • Calls HotkeyRebindUI.Configure() — no manual Inspector wiring needed
/// </summary>
[RequireComponent(typeof(HotkeyRebindUI))]
public class HotkeyPanelBuilder : MonoBehaviour
{
    [Header("Required")]
    [Tooltip("The single row you built manually (Row_Move). Must have children " +
             "named 'ActionLabel' (TMP), 'KeyLabel' (TMP), and a Button.")]
    [SerializeField] private GameObject rowTemplate;

    [Header("Sprites — drag your assets here")]
    [Tooltip("Sprite used for the Back and Reset All buttons. " +
             "Leave empty to use a plain coloured rectangle.")]
    [SerializeField] private Sprite buttonSprite;

    [Tooltip("Sprite used for the 'Press any key…' overlay panel. " +
             "Leave empty to use a plain semi-transparent black rectangle.")]
    [SerializeField] private Sprite overlayPanelSprite;

    [Header("Layout — adjust to match your panel size")]
    [Tooltip("Pixels from the top of the panel to the first row.")]
    [SerializeField] private float topPadding    = 150f;
    [Tooltip("Height of each row in pixels.")]
    [SerializeField] private float rowHeight     =  48f;
    [Tooltip("Gap between rows in pixels.")]
    [SerializeField] private float rowSpacing    =   4f;
    [Tooltip("Horizontal padding taken off each side of the row container.")]
    [SerializeField] private float sidePadding   =  30f;
    [Tooltip("Width of the key-label column. Must fit the longest key name (e.g. 'Backspace').")]
    [SerializeField] private float keyLabelWidth = 220f;

    [Header("Button appearance")]
    [Tooltip("Tint colour applied on top of the button sprite (or the solid colour when no sprite is set).")]
    [SerializeField] private Color buttonColor   = new Color(0.55f, 0.15f, 0.15f, 1f);

    [Header("Listening overlay appearance")]
    [Tooltip("Tint/colour of the 'Press any key…' overlay. " +
             "When a sprite is assigned this is blended on top of it.")]
    [SerializeField] private Color overlayColor    = new Color(0f, 0f, 0f, 0.82f);
    [SerializeField] private float overlayFontSize = 42f;

    // ── Action list ───────────────────────────────────────────────────────────

    private static readonly (HotkeyAction action, string label)[] ActionDefs =
    {
        (HotkeyAction.Move,    "Move"),
        (HotkeyAction.Attack,  "Attack"),
        (HotkeyAction.Info,    "Info"),
        (HotkeyAction.Attack1, "Attack 1"),
        (HotkeyAction.Attack2, "Attack 2"),
        (HotkeyAction.Attack3, "Attack 3"),
        (HotkeyAction.EndTurn, "End Turn"),
        (HotkeyAction.Cancel,  "Cancel"),
        (HotkeyAction.Pause,   "Pause"),
    };

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (rowTemplate == null)
        {
            Debug.LogError("[HotkeyPanelBuilder] Assign Row_Move to 'Row Template' in the Inspector.");
            return;
        }
        Build();
    }

    // ── Builder ───────────────────────────────────────────────────────────────

    void Build()
    {
        var rebindUI  = GetComponent<HotkeyRebindUI>();
        var builtRows = new List<HotkeyRebindUI.HotkeyRow>();

        // ── 1. Row container ──────────────────────────────────────────────────
        var container = new GameObject("RowContainer", typeof(RectTransform)).transform;
        container.SetParent(transform, false);

        var crt = (RectTransform)container;
        crt.anchorMin        = new Vector2(0f, 1f);
        crt.anchorMax        = new Vector2(1f, 1f);
        crt.pivot            = new Vector2(0.5f, 1f);
        crt.anchoredPosition = new Vector2(0f, -topPadding);
        crt.sizeDelta        = new Vector2(-sidePadding * 2f, 0f);

        var vlg = container.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing              = rowSpacing;
        vlg.childAlignment       = TextAnchor.UpperCenter;
        vlg.childControlWidth    = true;
        vlg.childControlHeight   = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        container.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        // ── 2. Spawn one row per action ────────────────────────────────────────
        for (int i = 0; i < ActionDefs.Length; i++)
        {
            var (action, label) = ActionDefs[i];

            GameObject rowGO = (i == 0)
                ? rowTemplate
                : Instantiate(rowTemplate, container);

            if (i == 0)
                rowGO.transform.SetParent(container, false);

            rowGO.name = "Row_" + action;
            rowGO.SetActive(true);

            var rt = (RectTransform)rowGO.transform;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, rowHeight);

            var actionLabel = FindChildTMP(rowGO, "ActionLabel");
            var keyLabel    = FindChildTMP(rowGO, "KeyLabel");
            var rebindBtn   = rowGO.GetComponentInChildren<Button>();

            if (actionLabel != null)
                actionLabel.text = label;
            else
                Debug.LogWarning($"[HotkeyPanelBuilder] '{rowGO.name}' has no child named 'ActionLabel'.");

            if (keyLabel != null)
            {
                var klRT = (RectTransform)keyLabel.transform;
                klRT.sizeDelta        = new Vector2(keyLabelWidth, klRT.sizeDelta.y);
                keyLabel.enableAutoSizing = true;
                keyLabel.fontSizeMin      = 12f;
                keyLabel.fontSizeMax      = keyLabel.fontSize;
                keyLabel.overflowMode     = TextOverflowModes.Overflow;
            }
            else
            {
                Debug.LogWarning($"[HotkeyPanelBuilder] '{rowGO.name}' has no child named 'KeyLabel'.");
            }

            builtRows.Add(new HotkeyRebindUI.HotkeyRow
            {
                action       = action,
                displayName  = label,
                keyLabel     = keyLabel,
                rebindButton = rebindBtn,
            });
        }

        // ── 3. Reset All button ────────────────────────────────────────────────
        var resetGO = MakeButton("ResetAllButton", "Reset All", transform, buttonSprite, buttonColor);
        var resetRT = (RectTransform)resetGO.transform;
        resetRT.anchorMin        = new Vector2(0.5f, 0f);
        resetRT.anchorMax        = new Vector2(0.5f, 0f);
        resetRT.pivot            = new Vector2(0.5f, 0f);
        resetRT.anchoredPosition = new Vector2(0f, 20f);
        resetRT.sizeDelta        = new Vector2(220f, 50f);

        // ── 4. Listening overlay ───────────────────────────────────────────────
        var overlay = new GameObject("ListeningOverlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(transform, false);
        var ort = (RectTransform)overlay.transform;
        ort.anchorMin = Vector2.zero;
        ort.anchorMax = Vector2.one;
        ort.offsetMin = Vector2.zero;
        ort.offsetMax = Vector2.zero;

        var overlayImg = overlay.GetComponent<Image>();
        if (overlayPanelSprite != null)
        {
            overlayImg.sprite = overlayPanelSprite;
            overlayImg.type   = overlayPanelSprite.border != Vector4.zero
                ? Image.Type.Sliced
                : Image.Type.Simple;
        }
        overlayImg.color = overlayColor;   // tint blended on top of the sprite (or solid colour)

        var listTextGO = new GameObject("ListeningText", typeof(RectTransform));
        listTextGO.transform.SetParent(overlay.transform, false);
        var lrt = (RectTransform)listTextGO.transform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        var listText = listTextGO.AddComponent<TextMeshProUGUI>();
        listText.text      = "Press any key…\n<size=70%>Escape to cancel</size>";
        listText.fontSize  = overlayFontSize;
        listText.alignment = TextAlignmentOptions.Center;
        listText.color     = Color.white;

        overlay.SetActive(false);

        // ── 5. Back button ─────────────────────────────────────────────────────
        var backGO = MakeButton("BackButton", "← Back", transform, buttonSprite, buttonColor);
        var backRT = (RectTransform)backGO.transform;
        backRT.anchorMin        = new Vector2(0f, 1f);
        backRT.anchorMax        = new Vector2(0f, 1f);
        backRT.pivot            = new Vector2(0f, 1f);
        backRT.anchoredPosition = new Vector2(12f, -12f);
        backRT.sizeDelta        = new Vector2(130f, 44f);
        // Call Hide() (not SetActive directly) so the onClose event fires
        // and PauseMenu knows to restore itself.
        backGO.GetComponent<Button>().onClick.AddListener(() => rebindUI.Hide());

        // ── 6. Hand everything to HotkeyRebindUI ──────────────────────────────
        rebindUI.Configure(
            builtRows.ToArray(),
            resetGO.GetComponent<Button>(),
            overlay,
            listText);

        Debug.Log("[HotkeyPanelBuilder] Panel built successfully.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TextMeshProUGUI FindChildTMP(GameObject root, string childName)
    {
        Transform child = root.transform.Find(childName);
        return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
    }

    /// <summary>
    /// Creates a Button with a TMP label.
    /// If <paramref name="sprite"/> is assigned it is used as the button background
    /// (auto-detected as Sliced or Simple based on the sprite's border settings).
    /// If null, a plain rectangle in <paramref name="color"/> is used instead.
    /// </summary>
    private static GameObject MakeButton(string name, string label, Transform parent,
                                         Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type   = sprite.border != Vector4.zero
                ? Image.Type.Sliced   // 9-slice — borders set in Sprite editor
                : Image.Type.Simple;
        }
        img.color = color;            // tint on top of sprite, or solid colour if no sprite

        var textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);
        var trt = (RectTransform)textGO.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 26f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;

        return go;
    }
}
