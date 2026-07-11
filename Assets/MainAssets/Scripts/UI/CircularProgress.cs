using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit custom element: draws a circular ring progress indicator.
/// Set via SetValue(current, max). Place in a square-sized container.
/// </summary>
public class CircularProgress : VisualElement
{
    private float _ratio;
    private Label _centerLabel;

    private static readonly Color TrackColor = new Color(0.08f, 0.08f, 0.13f, 0.95f);
    private static readonly Color FillColor  = new Color(0.25f, 0.65f, 1f,   0.95f);
    private const float Thickness = 9f;

    public CircularProgress()
    {
        generateVisualContent += Draw;

        _centerLabel = new Label("--");
        _centerLabel.pickingMode = PickingMode.Ignore;
        _centerLabel.style.position           = Position.Absolute;
        _centerLabel.style.left               = 0; _centerLabel.style.right  = 0;
        _centerLabel.style.top                = 0; _centerLabel.style.bottom = 0;
        _centerLabel.style.unityTextAlign     = TextAnchor.MiddleCenter;
        _centerLabel.style.fontSize           = 13;
        _centerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _centerLabel.style.color              = Color.white;
        Add(_centerLabel);
    }

    public void SetValue(int current, int max, bool initialized)
    {
        _ratio = (initialized && max > 0) ? Mathf.Clamp01((float)current / max) : 0f;
        _centerLabel.text = initialized ? $"{current}\n/{max}" : "--";
        MarkDirtyRepaint();
    }

    private void Draw(MeshGenerationContext ctx)
    {
        var p = ctx.painter2D;
        float cx = contentRect.width  * 0.5f;
        float cy = contentRect.height * 0.5f;
        float r  = Mathf.Min(cx, cy) - Thickness * 0.5f - 2f;

        // Background track ring
        p.strokeColor = TrackColor;
        p.lineWidth   = Thickness;
        p.BeginPath();
        p.Arc(new Vector2(cx, cy), r, 0f, 360f);
        p.Stroke();

        // Fill arc — starts at 12-o'clock (-90°), sweeps clockwise
        if (_ratio > 0.001f)
        {
            p.strokeColor = FillColor;
            p.lineWidth   = Thickness;
            p.BeginPath();
            p.Arc(new Vector2(cx, cy), r, -90f, -90f + _ratio * 360f,
                  ArcDirection.Clockwise);
            p.Stroke();
        }
    }
}
