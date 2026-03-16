using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;

public class RadialMenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI labelText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Button button;
    [SerializeField] private Image backgroundImage;

    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = new Color(0.15f, 0.15f, 0.2f, 0.95f);
    [SerializeField] private Color hoverColor = new Color(0.3f, 0.6f, 1f, 1f);
    [SerializeField] private Color pressedColor = new Color(0.2f, 0.8f, 0.4f, 1f);
    [SerializeField] private float scaleOnHover = 1.15f;
    [SerializeField] private float animationSpeed = 8f;

    [Header("Text Settings")]
    [SerializeField] private Color textNormalColor = Color.white;
    [SerializeField] private Color textHoverColor = Color.yellow;
    [SerializeField] private float textSize = 36f; // Larger for better visibility

    private string actionLabel;
    private RadialMenu parentMenu;
    private Vector3 originalScale;
    private Color targetColor;
    private Vector3 targetScale;
    private bool isHovered = false;
    private RadialActionType actionType;
    private System.Action<RadialActionType> onActionSelected;

    /// <summary>
    /// Optional attack data shown in AttackInfoPanel while this button is hovered.
    /// Null for non-attack buttons (Move / Info etc.).
    /// </summary>
    private AttackData hoverAttackData;

    /// <summary>Realtime seconds when this button was created. Used to ignore
    /// the same click that opened the parent menu (spawn-frame click guard).</summary>
    private float createdAt;

    void Start()
    {
        // Set original scale after the object is fully initialized
        originalScale = transform.localScale;
        targetScale = originalScale;
    }

    void Awake()
    {
        createdAt = Time.realtimeSinceStartup;

        Debug.Log($"RadialMenuButton Awake: {gameObject.name}");

        // Auto-assign components if not assigned
        if (button == null) button = GetComponent<Button>();
        if (backgroundImage == null) backgroundImage = GetComponent<Image>();

        // Hide any legacy Unity UI Text child (the default "Button" label that ships
        // with Unity's built-in Button prefab).  It is never used by this script —
        // we use TextMeshProUGUI — but it can show "Button" on screen if not hidden.
        foreach (var legacyText in GetComponentsInChildren<UnityEngine.UI.Text>())
            legacyText.gameObject.SetActive(false);

        // Get TextMeshPro from children if not assigned
        if (labelText == null)
        {
            labelText = GetComponentInChildren<TextMeshProUGUI>();
        }

        if (labelText != null)
        {
            Debug.Log($"LabelText found: {labelText.gameObject.name}");
        }
        else
        {
            Debug.LogError("LabelText is NULL in Awake!");
        }

        originalScale = transform.localScale;
        targetColor = normalColor;
        targetScale = originalScale;

        // Setup text appearance
        if (labelText != null)
        {
            labelText.fontSize = textSize;
            labelText.color = textNormalColor;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontStyle = FontStyles.Bold;
            labelText.enableAutoSizing = false;

            // Force the text to be visible
            labelText.gameObject.SetActive(true);
            labelText.enabled = true;

            Debug.Log($"Text settings applied. Font size: {labelText.fontSize}, Color: {labelText.color}");
        }

        // Make background visible and interactable
        if (backgroundImage != null)
        {
            backgroundImage.color = normalColor;
            backgroundImage.raycastTarget = true;
            backgroundImage.enabled = true;
            Debug.Log("Background image configured");
        }
        else
        {
            Debug.LogWarning("Background image is NULL!");
        }
    }

    public void Initialize(string label, Sprite icon, RadialMenu menu)
    {
        Debug.Log($"=== Initializing Button with label: '{label}' ===");

        actionLabel = label;
        parentMenu = menu;

        // Set button text
        if (labelText != null)
        {
            labelText.text = label;
            labelText.gameObject.SetActive(true);
            labelText.enabled = true;

            // Force update the text mesh
            labelText.ForceMeshUpdate();

            Debug.Log($"✓ Button text set to: '{label}'. TextMeshPro active: {labelText.gameObject.activeSelf}, enabled: {labelText.enabled}");
            Debug.Log($"  Text content: '{labelText.text}', Font: {labelText.font?.name ?? "NULL"}");
        }
        else
        {
            Debug.LogError("✗ LabelText is NULL! Cannot set button text.");
        }

        // Set icon (if provided)
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = (icon != null);
            Debug.Log($"Icon: {(icon != null ? "Set" : "None")}");
        }

        // Ensure background is visible
        if (backgroundImage != null)
        {
            backgroundImage.enabled = true;
            backgroundImage.color = normalColor;
            Debug.Log($"Background enabled with color: {normalColor}");
        }

        Debug.Log($"=== Button '{label}' initialization complete ===\n");
    }

    /// <summary>
    /// Configures the button. Pass <paramref name="attackData"/> for attack buttons so that
    /// hovering shows the <see cref="AttackInfoPanel"/>. Leave null for non-attack buttons.
    /// </summary>
    public void Setup(string label, Sprite icon, RadialActionType type,
                      System.Action<RadialActionType> callback,
                      AttackData attackData = null)
    {
        actionType       = type;
        onActionSelected = callback;
        hoverAttackData  = attackData;

        if (labelText != null)
        {
            labelText.text     = label;
            // Force a readable font size for ScreenSpaceOverlay.
            // The prefab value (36 pt) was correct for WorldSpace at scale 0.05,
            // but is tiny in screen-space pixels.
            labelText.fontSize = 30f;
            labelText.ForceMeshUpdate();
        }

        if (icon != null && iconImage != null)
            iconImage.sprite = icon;
    }

    void Update()
    {
        // Smooth animations
        if (backgroundImage != null)
        {
            backgroundImage.color = Color.Lerp(backgroundImage.color, targetColor, Time.deltaTime * animationSpeed);
        }

        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * animationSpeed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        targetColor = hoverColor;
        targetScale = originalScale * scaleOnHover;

        if (labelText != null)
            labelText.color = textHoverColor;

        // Show attack info popup if this is an attack button
        if (hoverAttackData != null)
            AttackInfoPanel.Show(hoverAttackData);

        Debug.Log($">>> HOVER: {actionLabel}");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        targetColor = normalColor;
        targetScale = originalScale;

        if (labelText != null)
            labelText.color = textNormalColor;

        // Hide attack info popup when leaving any button
        // (safe to call even on non-attack buttons — Hide() is a no-op when hidden)
        AttackInfoPanel.Hide();

        Debug.Log($"<<< EXIT HOVER: {actionLabel}");
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Guard: ignore clicks that arrive in the same frame this button was created.
        // This prevents the click that opened the parent menu (e.g. "Attack") from
        // immediately triggering a button in the newly-spawned sub-menu.
        if (Time.realtimeSinceStartup - createdAt < 0.15f) return;

        Debug.Log($"*** BUTTON CLICKED: {actionLabel} ***");

        // Invoke the callback with the action type
        onActionSelected?.Invoke(actionType);

        // Visual feedback
        StartCoroutine(ClickAnimation());

        // parentMenu notification removed — radial menu receives the selection via the callback above
    }

    private System.Collections.IEnumerator ClickAnimation()
    {
        // Flash green when clicked
        targetColor = pressedColor;
        yield return new UnityEngine.WaitForSeconds(0.15f);

        if (!isHovered)
        {
            targetColor = normalColor;
        }
        else
        {
            targetColor = hoverColor;
        }
    }
}