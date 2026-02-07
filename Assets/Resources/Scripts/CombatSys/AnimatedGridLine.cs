using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class AnimatedGridLine : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float minIntensity = 0.5f;
    [SerializeField] private float maxIntensity = 2f;
    [SerializeField] private AnimationCurve pulseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private LineRenderer lineRenderer;
    private Material lineMaterial;
    private Color baseColor;
    private float time = 0f;
    private static readonly int EmissiveColorProperty = Shader.PropertyToID("_EmissiveColor");

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();

        // Create material instance to avoid affecting all lines
        if (lineRenderer.material != null)
        {
            lineMaterial = new Material(lineRenderer.material);
            lineRenderer.material = lineMaterial;
            baseColor = lineMaterial.GetColor(EmissiveColorProperty);
        }

        // Randomize start time for variety
        time = Random.Range(0f, Mathf.PI * 2f);
    }

    void Update()
    {
        if (lineMaterial == null) return;

        time += Time.deltaTime * pulseSpeed;

        // Calculate pulsing intensity
        float normalizedTime = (Mathf.Sin(time) + 1f) / 2f; // 0 to 1
        float intensity = Mathf.Lerp(minIntensity, maxIntensity, pulseCurve.Evaluate(normalizedTime));

        // Apply to emission
        Color emissiveColor = baseColor * intensity;
        lineMaterial.SetColor(EmissiveColorProperty, emissiveColor);
    }

    void OnDestroy()
    {
        if (lineMaterial != null)
        {
            Destroy(lineMaterial);
        }
    }
}