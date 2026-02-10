using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

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

    void Awake()
    {
        Debug.Log($"RadialMenuButton Awake: {gameObject.name}");

        // Auto-assign components if not assigned
        if (button == null) button = GetComponent<Button>();
        if (backgroundImage == null) backgroundImage = GetComponent<Image>();

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
        {
            labelText.color = textHoverColor;
        }

        Debug.Log($">>> HOVER: {actionLabel}");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        targetColor = normalColor;
        targetScale = originalScale;

        if (labelText != null)
        {
            labelText.color = textNormalColor;
        }

        Debug.Log($"<<< EXIT HOVER: {actionLabel}");
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"*** BUTTON CLICKED: {actionLabel} ***");

        // Visual feedback
        StartCoroutine(ClickAnimation());

        // Notify parent menu
        if (parentMenu != null)
        {
            parentMenu.OnButtonClicked(actionLabel);
        }
        else
        {
            Debug.LogError("Parent menu is NULL!");
        }
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