using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Single asset that controls the visual style of all auto-generated UI panels.
///
/// HOW TO CREATE:
///   1. In the Project window, navigate to (or create) Assets/Resources/
///   2. Right-click → Create → Evermore → UI Style Config
///   3. Name the asset exactly  UIStyleConfig  (no spaces)
///   4. Assign your sprites and tweak colours — every panel picks them up at runtime.
///
/// WHY Resources/?
///   The panels (PauseMenu, WinLoseManager, etc.) are auto-created singletons —
///   they have no Inspector because they spawn themselves in code.
///   Resources.Load is the standard Unity way to give code-created objects
///   designer-editable assets without requiring scene placement.
/// </summary>
[CreateAssetMenu(fileName = "UIStyleConfig", menuName = "Evermore/UI Style Config")]
public class UIStyleConfig : ScriptableObject
{
    // ── Shared sprites ────────────────────────────────────────────────────────

    [Header("Shared sprites")]
    [Tooltip("Background sprite for every panel. Leave empty for a solid colour.")]
    public Sprite panelSprite;

    [Tooltip("Sprite for every button. Leave empty for a solid colour.")]
    public Sprite buttonSprite;

    // ── Panel layout ──────────────────────────────────────────────────────────

    [Header("Panel layout")]
    [Tooltip("Height in pixels of the coloured header bar inside info panels.\n" +
             "Increase this if your panel sprite's header art is taller than the default 50 px,\n" +
             "so the body text (HP, stats, description) starts below the header art.")]
    public float panelHeaderHeight = 50f;

    // ── Pause Menu ────────────────────────────────────────────────────────────

    [Header("Pause Menu colours")]
    public Color pausePanelColor    = new Color(0.07f, 0.07f, 0.12f, 0.98f);
    public Color resumeButtonColor  = new Color(0.15f, 0.50f, 0.20f, 1f);
    public Color keyBindButtonColor = new Color(0.20f, 0.20f, 0.38f, 1f);

    // ── Win / Lose screen ─────────────────────────────────────────────────────

    [Header("Win / Lose screen colours")]
    public Color winlosePanelColor  = new Color(0.06f, 0.06f, 0.10f, 0.97f);
    public Color playAgainColor     = new Color(0.15f, 0.45f, 0.20f, 1f);
    public Color quitColor          = new Color(0.45f, 0.12f, 0.12f, 1f);

    // ── Monster Info Panel ────────────────────────────────────────────────────

    [Header("Monster Info Panel colours")]
    public Color infoPanelColor     = new Color(0.07f, 0.07f, 0.11f, 0.95f);
    public Color infoHeaderColor    = new Color(0.15f, 0.15f, 0.25f, 1f);
    public Color closeButtonColor   = new Color(0.65f, 0.15f, 0.15f, 1f);

    // ── Attack Info Panel ─────────────────────────────────────────────────────

    [Header("Attack Info Panel colours")]
    public Color attackPanelColor   = new Color(0.07f, 0.07f, 0.11f, 0.95f);
    public Color attackHeaderColor  = new Color(0.10f, 0.15f, 0.25f, 1f);

    // ── UI Toolkit ────────────────────────────────────────────────────────────

    [Header("UI Toolkit — assign in Inspector")]
    [Tooltip("Shared PanelSettings for all code-spawned UI Toolkit panels. " +
             "Drag RadialMenuPanelSettings here.")]
    public PanelSettings panelSettings;

    [Tooltip("UXML for MonsterInfoPanel. Drag Assets/UIDocuments/MonsterInfoPanel/MonsterInfoPanel.uxml here.")]
    public VisualTreeAsset monsterInfoPanelUXML;

    [Tooltip("UXML for AttackInfoPanel. Drag Assets/UIDocuments/AttackInfoPanel/AttackInfoPanel.uxml here.")]
    public VisualTreeAsset attackInfoPanelUXML;

    [Tooltip("UXML for PauseMenu. Drag Assets/UIDocuments/PauseMenu/PauseMenu.uxml here.")]
    public VisualTreeAsset pauseMenuUXML;

    // ── Helper ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Finds the UIStyleConfig anywhere inside any Resources folder.
    /// Logs a console warning if nothing is found so you know exactly what's wrong.
    /// </summary>
    public static UIStyleConfig Load()
    {
        // Fast path — expected location
        var cfg = Resources.Load<UIStyleConfig>("UIStyleConfig");
        if (cfg != null) return cfg;

        // Fallback — search every Resources folder recursively (handles subfolders / different names)
        var all = Resources.LoadAll<UIStyleConfig>("");
        if (all != null && all.Length > 0)
        {
            if (all.Length > 1)
                Debug.LogWarning("[UIStyleConfig] Multiple UIStyleConfig assets found — using the first one.");
            return all[0];
        }

        Debug.LogWarning(
            "[UIStyleConfig] Asset not found in any Resources folder.\n" +
            "Create it via:  Assets → Create → Evermore → UI Style Config\n" +
            "Then move it into any folder named  Resources  inside your Assets folder.\n" +
            "Panels will use built-in default colours until it is found.");
        return null;
    }

    /// <summary>
    /// Applies <paramref name="sprite"/> to <paramref name="img"/> (auto-detects
    /// Sliced vs Simple from the sprite's border settings).
    /// <para>
    /// • Sprite assigned → uses the sprite with no tint (Color.white) so it
    ///   renders exactly as designed. <paramref name="solidFallback"/> is ignored.
    /// • No sprite → fills with <paramref name="solidFallback"/> as a plain colour.
    /// </para>
    /// This means the colour fields in UIStyleConfig are only active when no sprite
    /// is set — you never have to set them to white to "un-tint" a sprite.
    /// </summary>
    public static void ApplySprite(UnityEngine.UI.Image img, Sprite sprite, Color solidFallback)
    {
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type   = sprite.border != Vector4.zero
                ? UnityEngine.UI.Image.Type.Sliced
                : UnityEngine.UI.Image.Type.Simple;
            img.color  = Color.white;
        }
        else
        {
            img.color = solidFallback;
        }
    }

    /// <summary>
    /// UI Toolkit equivalent of ApplySprite.
    /// Applies <paramref name="sprite"/> as a background on a VisualElement,
    /// including 9-slice borders if the sprite has them.
    /// Falls back to a solid background colour if no sprite is assigned.
    /// </summary>
    public static void ApplySprite(VisualElement el, Sprite sprite, Color solidFallback)
    {
        if (sprite != null)
        {
            el.style.backgroundImage = new StyleBackground(sprite);
            el.style.unityBackgroundImageTintColor = Color.white;
            el.style.backgroundColor = Color.clear;   // hide any USS background-color behind the sprite
            if (sprite.border != Vector4.zero)
            {
                el.style.unitySliceLeft   = Mathf.RoundToInt(sprite.border.x);
                el.style.unitySliceBottom = Mathf.RoundToInt(sprite.border.y);
                el.style.unitySliceRight  = Mathf.RoundToInt(sprite.border.z);
                el.style.unitySliceTop    = Mathf.RoundToInt(sprite.border.w);
                el.style.unitySliceScale  = 1f;
            }
        }
        else
        {
            el.style.backgroundColor = solidFallback;
        }
    }
}
