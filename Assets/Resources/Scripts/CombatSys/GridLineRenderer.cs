using UnityEngine;

public class GridLineRenderer : MonoBehaviour
{
    [Header("Grid References")]
    [SerializeField] private GridManager gridManager;

    [Header("Line Settings")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private float lineWidth = 0.02f;
    [SerializeField] private float lineHeight = 0.02f; // Height above tiles
    [SerializeField] private Color lineColor = new Color(0.3f, 0.8f, 1f, 1f); // Cyan glow
    [SerializeField] private float emissionIntensity = 2f;

    [Header("Animation")]
    [SerializeField] private bool animateFlow = true;
    [SerializeField] private float flowSpeed = 0.5f;

    private LineRenderer[] horizontalLines;
    private LineRenderer[] verticalLines;
    private float flowOffset = 0f;

    void Start()
    {
        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<GridManager>();
        }

        // Wait for grid to be generated
        Invoke(nameof(GenerateGridLines), 0.1f);
    }

    void GenerateGridLines()
    {
        if (gridManager == null)
        {
            Debug.LogError("GridManager not found!");
            return;
        }

        int width = gridManager.GridWidth;
        int height = gridManager.GridHeight;
        float spacing = gridManager.TileSpacing;

        // Create parent object for organization
        GameObject linesParent = new GameObject("GridLines");
        linesParent.transform.parent = transform;

        // Create horizontal lines (running along X axis)
        horizontalLines = new LineRenderer[height + 1];
        for (int y = 0; y <= height; y++)
        {
            GameObject lineObj = new GameObject($"HorizontalLine_{y}");
            lineObj.transform.parent = linesParent.transform;

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            SetupLineRenderer(lr);

            Vector3 start = new Vector3(-spacing * 0.5f, lineHeight, y * spacing - spacing * 0.5f);
            Vector3 end = new Vector3((width - 0.5f) * spacing, lineHeight, y * spacing - spacing * 0.5f);

            lr.positionCount = 2;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);

            horizontalLines[y] = lr;
        }

        // Create vertical lines (running along Z axis)
        verticalLines = new LineRenderer[width + 1];
        for (int x = 0; x <= width; x++)
        {
            GameObject lineObj = new GameObject($"VerticalLine_{x}");
            lineObj.transform.parent = linesParent.transform;

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            SetupLineRenderer(lr);

            Vector3 start = new Vector3(x * spacing - spacing * 0.5f, lineHeight, -spacing * 0.5f);
            Vector3 end = new Vector3(x * spacing - spacing * 0.5f, lineHeight, (height - 0.5f) * spacing);

            lr.positionCount = 2;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);

            verticalLines[x] = lr;
        }

        Debug.Log($"Generated {horizontalLines.Length + verticalLines.Length} grid lines");
    }

    void SetupLineRenderer(LineRenderer lr)
    {
        lr.material = lineMaterial;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.startColor = lineColor;
        lr.endColor = lineColor;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.alignment = LineAlignment.TransformZ;

        // Set emission for glow
        if (lineMaterial != null)
        {
            lineMaterial.SetColor("_EmissiveColor", lineColor * emissionIntensity);
        }
    }

    void Update()
    {
        if (animateFlow && lineMaterial != null)
        {
            flowOffset += flowSpeed * Time.deltaTime;
            lineMaterial.SetFloat("_TimeOffset", flowOffset);
        }
    }
}