using UnityEngine;

public class Tile : MonoBehaviour
{
    public enum TileType
    {
        PlayerSide,
        EnemySide,
        Neutral
    }
    
    public enum OccupationType
    {
        Empty,
        Monster,
        Obstruction,
        Trap
    }
    
    // Core Properties
    public Vector2Int GridPosition { get; private set; }
    public TileType Type { get; private set; }
    public OccupationType Occupation { get; private set; }
    public GameObject OccupyingObject { get; private set; }
    
    // Visual Components
    private Renderer tileRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Color originalColor;
    private Vector3 originalPosition;
    private bool isHovered = false;
    
    [Header("Visual Settings")]
    [SerializeField] private Color baseTileColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    [SerializeField] private Color hoverColor = new Color(1f, 1f, 0.7f, 1f);
    [SerializeField] private Color selectedColor = new Color(0.3f, 1f, 0.3f, 1f);
    [SerializeField] private float checkerboardDarkenAmount = 0.85f;
    [SerializeField] private float hoverHeightOffset = 0.05f;
    [SerializeField] private float transitionSpeed = 10f;
    
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private Color currentTargetColor;
    private float currentTargetHeight;
    private bool isSelected = false;
    
    void Awake()
    {
        tileRenderer = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
    }
    
    public void Initialize(int x, int y, int gridHeight)
    {
        GridPosition = new Vector2Int(x, y);
        Occupation = OccupationType.Empty;
        OccupyingObject = null;
        originalPosition = transform.position;
        
        // Determine tile type based on position
        if (y < 2)
            Type = TileType.PlayerSide;
        else if (y >= gridHeight - 2)
            Type = TileType.EnemySide;
        else
            Type = TileType.Neutral;
        
        // Set base color
        SetColor(baseTileColor);
        
        // Add checkerboard pattern
        if ((x + y) % 2 == 0)
        {
            tileRenderer.GetPropertyBlock(propertyBlock);
            Color currentColor = propertyBlock.GetColor(BaseColorProperty);
            Color darkened = currentColor * checkerboardDarkenAmount;
            darkened.a = 1f;
            SetColor(darkened);
        }
        
        // Store original color
        tileRenderer.GetPropertyBlock(propertyBlock);
        originalColor = propertyBlock.GetColor(BaseColorProperty);
        currentTargetColor = originalColor;
        currentTargetHeight = 0f;
    }
    
    void Update()
    {
        // Smooth color transition
        tileRenderer.GetPropertyBlock(propertyBlock);
        Color currentColor = propertyBlock.GetColor(BaseColorProperty);
        Color newColor = Color.Lerp(currentColor, currentTargetColor, Time.deltaTime * transitionSpeed);
        propertyBlock.SetColor(BaseColorProperty, newColor);
        tileRenderer.SetPropertyBlock(propertyBlock);
        
        // Smooth height transition
        float currentHeight = transform.position.y - originalPosition.y;
        float newHeight = Mathf.Lerp(currentHeight, currentTargetHeight, Time.deltaTime * transitionSpeed);
        transform.position = originalPosition + Vector3.up * newHeight;
    }
    
    private void SetColor(Color color)
    {
        if (tileRenderer != null)
        {
            propertyBlock.SetColor(BaseColorProperty, color);
            tileRenderer.SetPropertyBlock(propertyBlock);
        }
    }
    
    public void SetHovered(bool hovered)
    {
        if (isSelected) return; // Don't change color if selected
        
        isHovered = hovered;
        if (hovered)
        {
            currentTargetColor = hoverColor;
            currentTargetHeight = hoverHeightOffset;
        }
        else
        {
            currentTargetColor = originalColor;
            currentTargetHeight = 0f;
        }
    }
    
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (selected)
        {
            currentTargetColor = selectedColor;
            currentTargetHeight = hoverHeightOffset * 1.5f;
        }
        else
        {
            currentTargetColor = originalColor;
            currentTargetHeight = 0f;
            isHovered = false;
            //currentTargetColor = isHovered ? hoverColor : originalColor;
            //currentTargetHeight = isHovered ? hoverHeightOffset : 0f;
        }
    }
    
    public void Highlight(Color color, float heightOffset = 0f)
    {
        currentTargetColor = color;
        currentTargetHeight = heightOffset;
    }
    
    public void ResetVisuals()
    {
        currentTargetColor = originalColor;
        currentTargetHeight = 0f;
        isHovered = false;
        isSelected = false;
    }
    
    // Occupation Management
    public bool SetOccupation(OccupationType type, GameObject occupyingObject = null)
    {
        if (Occupation != OccupationType.Empty && type != OccupationType.Empty)
        {
            Debug.LogWarning($"Tile {GridPosition} is already occupied by {Occupation}");
            return false;
        }
        
        Occupation = type;
        OccupyingObject = occupyingObject;
        return true;
    }
    
    public void ClearOccupation()
    {
        Occupation = OccupationType.Empty;
        OccupyingObject = null;
    }
    
    public bool IsWalkable()
    {
        return Occupation == OccupationType.Empty || Occupation == OccupationType.Trap;
    }

    public Monster GetMonster()
    {
        return OccupyingObject != null ? OccupyingObject.GetComponent<Monster>() : null;
    }
}