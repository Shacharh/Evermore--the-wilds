using UnityEngine;

public class Tile : MonoBehaviour
{
    public enum TileType
    {
        PlayerSide,
        EnemySide,
        Neutral
    }

    public Vector2Int GridPosition { get; private set; }
    public bool IsOccupied { get; set; }
    public GameObject OccupyingMonster { get; set; }
    public TileType Type { get; private set; }

    private Renderer tileRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Color originalColor;
    private Color hoverColor = new Color(1f, 1f, 0.7f, 1f);

    [Header("Visual Settings")]
    [SerializeField] private Color baseTileColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    [SerializeField] private float checkerboardDarkenAmount = 0.85f;
    [SerializeField] private float hoverHeightOffset = 0.05f;

    private Vector3 originalPosition;
    private bool isHovered = false;
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        tileRenderer = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
    }

    public void Initialize(int x, int y, int gridHeight)
    {
        GridPosition = new Vector2Int(x, y);
        IsOccupied = false;
        OccupyingMonster = null;
        originalPosition = transform.position;

        // Determine tile type based on position (for gameplay logic)
        if (y < 2)
            Type = TileType.PlayerSide;
        else if (y >= gridHeight - 2)
            Type = TileType.EnemySide;
        else
            Type = TileType.Neutral;

        // Set uniform base color for all tiles
        SetColor(baseTileColor);

        // Add subtle checkerboard pattern
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
    }

    private void SetColor(Color color)
    {
        if (tileRenderer != null)
        {
            propertyBlock.SetColor(BaseColorProperty, color);
            tileRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    public void Highlight(Color color)
    {
        SetColor(color);
    }

    public void ResetColor()
    {
        SetColor(originalColor);
        transform.position = originalPosition;
        isHovered = false;
    }

    void OnMouseEnter()
    {
        if (!isHovered)
        {
            isHovered = true;
            SetColor(hoverColor);
            transform.position = originalPosition + Vector3.up * hoverHeightOffset;
        }
    }

    void OnMouseExit()
    {
        if (isHovered)
        {
            ResetColor();
        }
    }

    void OnMouseDown()
    {
        Debug.Log($"Clicked tile at: ({GridPosition.x}, {GridPosition.y}) - Type: {Type}");
    }
}