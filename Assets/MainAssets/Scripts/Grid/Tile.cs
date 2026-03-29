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

    // ── Active Highlight (set by Highlight(), cleared by ResetVisuals()) ──────
    // Tracks the colour/height applied by gridManager.HighlightTiles() so that
    // SetHovered(false) can restore it instead of snapping to the raw originalColor.

    /// <summary>The colour most recently applied by Highlight(). Restored when hover ends.</summary>
    private Color activeHighlightColor;
    /// <summary>The height offset applied by the last Highlight() call.</summary>
    private float activeHighlightHeight;
    /// <summary>True while an external Highlight() colour is active on this tile.</summary>
    private bool  hasActiveHighlight;

    // ── Pulse state (used for attack-target colour cycling) ───────────────────

    private bool  isPulsing;
    private float pulseTimer;
    private Color pulseColorA;
    private Color pulseColorB;
    private float pulseSpeed;

    // ── Jitter state (horizontal shimmy for enemy tiles in attack range) ──────

    private bool  isJittering;
    private float jitterTimer;
    private float jitterAmplitude;
    private float jitterFrequency;

    void Awake()
    {
        tileRenderer = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        // MaterialPropertyBlock is not serialized by Unity, so it gets wiped
        // on domain reloads (script recompilation). Re-create it if needed.
        if (propertyBlock == null)
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
        currentTargetColor    = originalColor;
        currentTargetHeight   = 0f;
        activeHighlightColor  = originalColor;
        activeHighlightHeight = 0f;
        hasActiveHighlight    = false;
    }

    void Update()
    {
        // ── Pulse animation ───────────────────────────────────────────────────
        // While pulsing (e.g. attack-target highlight), oscillate between two
        // colours using a smooth sine wave.
        if (isPulsing)
        {
            pulseTimer += Time.deltaTime;
            // t goes 0→1→0→1… at the given speed (full cycles per second)
            float t = (Mathf.Sin(pulseTimer * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            currentTargetColor = Color.Lerp(pulseColorA, pulseColorB, t);
        }

        // Smooth color transition
        tileRenderer.GetPropertyBlock(propertyBlock);
        Color currentColor = propertyBlock.GetColor(BaseColorProperty);
        Color newColor = Color.Lerp(currentColor, currentTargetColor, Time.deltaTime * transitionSpeed);
        propertyBlock.SetColor(BaseColorProperty, newColor);
        tileRenderer.SetPropertyBlock(propertyBlock);

        // Smooth height transition
        float currentHeight = transform.position.y - originalPosition.y;
        float newHeight = Mathf.Lerp(currentHeight, currentTargetHeight, Time.deltaTime * transitionSpeed);

        // ── Jitter animation (horizontal shimmy for enemy attack-target tiles) ─
        // An offset is applied on the X axis using a fast sine wave.
        // When jitter stops the tile snaps back to its originalPosition X/Z.
        if (isJittering)
        {
            jitterTimer += Time.deltaTime;
            float xOffset = Mathf.Sin(jitterTimer * jitterFrequency * Mathf.PI * 2f) * jitterAmplitude;
            transform.position = new Vector3(
                originalPosition.x + xOffset,
                originalPosition.y + newHeight,
                originalPosition.z);
        }
        else
        {
            transform.position = new Vector3(
                originalPosition.x,
                originalPosition.y + newHeight,
                originalPosition.z);
        }
    }

    private void SetColor(Color color)
    {
        if (tileRenderer != null)
        {
            propertyBlock.SetColor(BaseColorProperty, color);
            tileRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    // ── Pulse API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Start a continuous colour pulse between <paramref name="colorA"/> and
    /// <paramref name="colorB"/> at <paramref name="speed"/> full cycles per second.
    /// Call StopPulse() or ResetVisuals() to stop.
    /// </summary>
    public void StartPulse(Color colorA, Color colorB, float speed = 2.5f)
    {
        isPulsing  = true;
        pulseColorA = colorA;
        pulseColorB = colorB;
        pulseSpeed  = speed;
        pulseTimer  = 0f;
        currentTargetHeight = hoverHeightOffset * 0.8f; // slight lift for visibility
    }

    /// <summary>Stops the pulse; tile colour resumes its normal lerp target.</summary>
    public void StopPulse()
    {
        isPulsing = false;
    }

    // ── Jitter API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts a horizontal shimmy on this tile — the tile oscillates left/right
    /// along the X axis at <paramref name="frequency"/> cycles per second with
    /// <paramref name="amplitude"/> world-unit amplitude.
    /// Call <see cref="StopJitter"/> or <see cref="ResetVisuals"/> to stop.
    /// </summary>
    public void StartJitter(float amplitude = 0.06f, float frequency = 10f)
    {
        isJittering     = true;
        jitterAmplitude = amplitude;
        jitterFrequency = frequency;
        jitterTimer     = 0f;
    }

    /// <summary>Stops the jitter; the tile snaps back to its original X/Z position.</summary>
    public void StopJitter()
    {
        isJittering = false;
    }

    public void SetHovered(bool hovered)
    {
        if (isSelected) return; // Don't change color if selected

        // While pulsing (e.g. attack-target highlight), the pulse animation owns
        // the visual state. Hover must not override it — the tile still receives
        // mouse events, but colour/height stays driven by the sine wave.
        if (isPulsing) return;

        isHovered = hovered;
        if (hovered)
        {
            currentTargetColor  = hoverColor;
            currentTargetHeight = hoverHeightOffset;
        }
        else
        {
            // Restore the active highlight colour (e.g. movement-range blue) if
            // one was set by Highlight(), otherwise fall back to the tile's natural colour.
            currentTargetColor  = hasActiveHighlight ? activeHighlightColor  : originalColor;
            currentTargetHeight = hasActiveHighlight ? activeHighlightHeight : 0f;
        }
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (selected)
        {
            currentTargetColor  = selectedColor;
            currentTargetHeight = hoverHeightOffset * 1.5f;
        }
        else
        {
            // Same restore-to-highlight logic as SetHovered(false).
            currentTargetColor  = hasActiveHighlight ? activeHighlightColor  : originalColor;
            currentTargetHeight = hasActiveHighlight ? activeHighlightHeight : 0f;
            isHovered = false;
        }
    }

    public void Highlight(Color color, float heightOffset = 0f)
    {
        // Remember this as the "active highlight" so SetHovered(false) can restore it.
        activeHighlightColor  = color;
        activeHighlightHeight = heightOffset;
        hasActiveHighlight    = true;
        currentTargetColor    = color;
        currentTargetHeight   = heightOffset;
    }

    public void ResetVisuals()
    {
        StopPulse();           // stop colour cycle
        StopJitter();          // stop horizontal shimmy — X/Z restored on next Update
        currentTargetColor    = originalColor;
        currentTargetHeight   = 0f;
        isHovered             = false;
        isSelected            = false;
        hasActiveHighlight    = false;
        activeHighlightColor  = originalColor;
        activeHighlightHeight = 0f;
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
        return OccupyingObject != null ? OccupyingObject.GetComponentInChildren<Monster>() : null;
    }
}
