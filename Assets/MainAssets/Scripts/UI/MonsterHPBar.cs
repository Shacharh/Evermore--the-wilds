using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Always-visible world-space HP bar that floats above a monster.
/// Built entirely in code — no prefab required yet (the brother will supply
/// a proper prefab later via MonsterSpawner).
///
/// Uses a Unity <see cref="Slider"/> so HP changes are handled cleanly:
///   • Slider.maxValue  = monster.MaxHP
///   • Slider.value     = monster.CurrentHP
///   • No manual fill-width math needed.
///
/// Usage (done by MonsterSpawner after spawning):
///   MonsterHPBar bar = spawnedGO.AddComponent&lt;MonsterHPBar&gt;();
///   bar.Initialize(monster);
/// </summary>
public class MonsterHPBar : MonoBehaviour
{
    // ── Configuration ─────────────────────────────────────────────────────────

    private const float BarWidth       = 120f;
    private const float BarHeight      = 14f;
    private const float VerticalOffset = 1.8f;   // world units above monster root
    private const float CanvasScale    = 0.005f;  // world-space canvas scale

    // ── References ────────────────────────────────────────────────────────────

    private Monster monster;
    private Slider  hpSlider;
    private Canvas  barCanvas;

    // ── Init ──────────────────────────────────────────────────────────────────

    /// <summary>Call once, immediately after AddComponent, to wire up the bar.</summary>
    public void Initialize(Monster m)
    {
        monster = m;
        BuildUI();
        UpdateBar(monster.CurrentHP, monster.MaxHP);  // set initial fill
        monster.OnHPChanged += UpdateBar;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnDestroy()
    {
        if (monster != null)
            monster.OnHPChanged -= UpdateBar;
    }

    private void Update()
    {
        // Billboard: rotate canvas to always face the camera (y-axis only).
        if (Camera.main == null || barCanvas == null) return;

        Vector3 dir = Camera.main.transform.position - barCanvas.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            barCanvas.transform.rotation = Quaternion.LookRotation(-dir);
    }

    // ── HP Update ─────────────────────────────────────────────────────────────

    private void UpdateBar(int current, int max)
    {
        if (hpSlider == null) return;
        hpSlider.maxValue = Mathf.Max(1, max);  // avoid division-by-zero inside Slider
        hpSlider.value    = current;
    }

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        // ── Find HPHandler anchor ─────────────────────────────────────────────
        // If the brother added an "HPHandler" child to the prefab, parent the
        // canvas to it (y-offset = 0 — HPHandler is already at the right height).
        // Falls back to the monster root + the default vertical offset.
        Transform anchor  = transform.Find("HPHandler");
        float     yOffset = anchor != null ? 0f : VerticalOffset;
        if (anchor == null) anchor = transform;

        // ── World-space canvas ────────────────────────────────────────────────
        var canvasGO = new GameObject("HPBarCanvas");
        canvasGO.transform.SetParent(anchor);
        canvasGO.transform.localPosition = new Vector3(0f, yOffset, 0f);
        canvasGO.transform.localRotation = Quaternion.identity;
        canvasGO.transform.localScale    = Vector3.one * CanvasScale;

        barCanvas              = canvasGO.AddComponent<Canvas>();
        barCanvas.renderMode   = RenderMode.WorldSpace;
        barCanvas.sortingOrder = 100;

        // Give the canvas an explicit size so child RectTransforms anchor correctly
        var canvasRect       = canvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(BarWidth, BarHeight);

        // ── Dark background strip ─────────────────────────────────────────────
        var bgGO   = MakeChild(canvasGO, "BG");
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgGO.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        // ── Slider container ──────────────────────────────────────────────────
        var sliderGO   = MakeChild(canvasGO, "HPSlider");
        var sliderRect = sliderGO.GetComponent<RectTransform>();
        sliderRect.anchorMin = Vector2.zero;
        sliderRect.anchorMax = Vector2.one;
        sliderRect.sizeDelta = Vector2.zero;

        // Fill Area — Unity's Slider sets fillRect.anchorMax.x = value/maxValue
        var fillAreaGO   = MakeChild(sliderGO, "FillArea");
        var fillAreaRect = fillAreaGO.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.sizeDelta = Vector2.zero;

        // Fill image: green for allies, red for enemies
        var fillGO   = MakeChild(fillAreaGO, "Fill");
        var fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;   // Slider overrides anchorMax.x each update
        fillRect.sizeDelta = Vector2.zero;

        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color = (monster != null && monster.IsEnemy)
                        ? new Color(0.9f, 0.2f, 0.2f, 1f)   // red   — enemy
                        : new Color(0.2f, 0.85f, 0.3f, 1f);  // green — ally

        // ── Wire the Slider ───────────────────────────────────────────────────
        hpSlider              = sliderGO.AddComponent<Slider>();
        hpSlider.direction    = Slider.Direction.LeftToRight;
        hpSlider.minValue     = 0f;
        hpSlider.maxValue     = monster != null ? monster.MaxHP : 100f;
        hpSlider.value        = monster != null ? monster.CurrentHP : 100f;
        hpSlider.wholeNumbers = true;
        hpSlider.interactable = false;  // display-only; not draggable
        hpSlider.fillRect     = fillRect;
        // handleRect left null intentionally — no visible handle needed
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GameObject MakeChild(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        return go;
    }
}
