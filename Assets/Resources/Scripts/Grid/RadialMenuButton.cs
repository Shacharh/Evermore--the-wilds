using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RadialMenuButton : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI labelText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Button button;
    [SerializeField] private Image backgroundImage;
    
    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color hoverColor = new Color(0.3f, 0.6f, 1f, 0.9f);
    [SerializeField] private float scaleOnHover = 1.2f;
    
    private string actionLabel;
    private RadialMenu parentMenu;
    private Vector3 originalScale;
    
    void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
        
        if (button != null)
        {
            button.onClick.AddListener(OnClick);
        }
        
        originalScale = transform.localScale;
    }
    
    public void Initialize(string label, Sprite icon, RadialMenu menu)
    {
        actionLabel = label;
        parentMenu = menu;
        
        if (labelText != null)
            labelText.text = label;
        
        if (iconImage != null && icon != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = true;
        }
        else if (iconImage != null)
        {
            iconImage.enabled = false;
        }
        
        if (backgroundImage != null)
            backgroundImage.color = normalColor;
    }
    
    void OnClick()
    {
        if (parentMenu != null)
        {
            parentMenu.OnButtonClicked(actionLabel);
        }
    }
    
    // Mouse hover effects (if using UI)
    public void OnPointerEnter()
    {
        if (backgroundImage != null)
            backgroundImage.color = hoverColor;
        
        transform.localScale = originalScale * scaleOnHover;
    }
    
    public void OnPointerExit()
    {
        if (backgroundImage != null)
            backgroundImage.color = normalColor;
        
        transform.localScale = originalScale;
    }
}