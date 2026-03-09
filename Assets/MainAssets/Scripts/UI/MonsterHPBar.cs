using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Always-visible world-space HP bar that floats above a monster.
/// Built entirely in code — no prefab required (prefab extraction comes later).
///
/// Usage (done by MonsterSpawner after spawning):
///   MonsterHPBar bar = spawnedGO.AddComponent<MonsterHPBar>();
///   bar.Initialize(monster);
/// </summary>
public class MonsterHPBar : MonoBehaviour
{
    // ── Configuration ─────────────────────────────────────────────────────────

    private const float BarWidth      = 120f;
    private const float BarHeight     = 14f;
    private const float VerticalOffset = 1.8f;   // world units above monster origin
    private const float CanvasScale   = 0.005f;  // world-space canvas scale

    // ── References ────────────────────────────────────────────────────────────

    private Monster  monster;
    private Image    fillImage;
    private Canvas   barCanvas;

    // ── Init ──────────────────────────────────────────────────────────────────

    /// <summary>Call once, immediately after AddComponent, to wire up the bar.</summary>
    public void Initialize(Monster m)
    {
        monster = m;
        BuildUI();
        UpdateBar(monster.CurrentHP, monster.MaxHP); // set initial fill
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
        // Billboard: rotate to face camera (y-axis only so the bar stays upright)
        if (Camera.main == null || barCanvas == null) return;

        Vector3 dir = Camera.main.transform.position - barCanvas.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            barCanvas.transform.rotation = Quaternion.LookRotation(-dir);
    }

    // ── HP Update ─────────────────────────────────────────────────────────────

    private void UpdateBar(int current, int max)
    {
        if (fillImage == null) return;
        fillImage.fillAmount = max > 0 ? (float)current / max : 0f;
    }

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        // ── World-space canvas parented to the monster ─────────────────────────
        GameObject canvasGO = new GameObject("HPBarCanvas");
        canvasGO.transform.SetParent(transform);
        canvasGO.transform.localPosition = new Vector3(0f, VerticalOffset, 0f);
        canvasGO.transform.localRotation = Quaternion.identity;
        canvasGO.transform.localScale    = Vector3.one * CanvasScale;

        barCanvas = canvasGO.AddComponent<Canvas>();
        barCanvas.renderMode  = RenderMode.WorldSpace;
        barCanvas.sortingOrder = 100;
        canvasGO.AddComponent<CanvasScaler>();

        // ── Background (dark strip) ───────────────────────────────────────────
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        bgGO.AddComponent<CanvasRenderer>();

        RectTransform bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(BarWidth, BarHeight);

        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        // ── Fill (green for player, red for enemy) ────────────────────────────
        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(bgGO.transform, false);
        fillGO.AddComponent<CanvasRenderer>();

        RectTransform fillRect = fillGO.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        fillRect.pivot     = new Vector2(0f, 0.5f); // fill left-to-right

        // Set pivot so the fill shrinks from the right
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot     = new Vector2(0f, 0.5f);
        fillRect.sizeDelta = new Vector2(BarWidth, 0f);

        fillImage = fillGO.AddComponent<Image>();
        // Green for player monsters, red for enemy monsters
        fillImage.color      = (monster != null && monster.IsEnemy)
                               ? new Color(0.9f, 0.2f, 0.2f, 1f)
                               : new Color(0.2f, 0.85f, 0.3f, 1f);
        fillImage.type       = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 1f;
    }
}
