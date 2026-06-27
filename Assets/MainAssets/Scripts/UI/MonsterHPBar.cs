using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Always-visible world-space HP bar that floats above a monster.
/// Status badges appear above the left edge of the bar, rising in when applied
/// and falling back down when the effect expires.
/// </summary>
public class MonsterHPBar : MonoBehaviour
{
    // ── HP bar layout ─────────────────────────────────────────────────────────

    private const float BarWidth       = 120f;
    private const float BarHeight      = 14f;
    private const float VerticalOffset = 1.8f;
    private const float CanvasScale    = 0.005f;

    // ── Status badge layout ───────────────────────────────────────────────────

    private const float BadgeW      = 44f;
    private const float BadgeH      = 20f;
    private const float BadgeGap    =  4f;
    private const float BadgeBorder =  2f;   // black outline width each side
    private const float BadgeFinalY = BarHeight * 0.5f + BadgeH * 0.5f + 3f;
    private const float AnimDuration = 0.35f;

    // ── References ────────────────────────────────────────────────────────────

    private Monster  monster;
    private Slider   hpSlider;
    private Canvas   barCanvas;
    private Transform _badgeParent;  // the canvas transform badges are parented to

    private readonly Dictionary<AttackEnum.StatusEffect, GameObject> _badges = new();
    private readonly Dictionary<AttackEnum.StatusEffect, Coroutine>  _anims  = new();

    // ── Init ──────────────────────────────────────────────────────────────────

    public void Initialize(Monster m)
    {
        monster = m;
        BuildUI();
        UpdateBar(monster.CurrentHP, monster.MaxHP);
        monster.OnHPChanged     += UpdateBar;
        monster.OnStatusChanged += RefreshStatusBadges;
        RefreshStatusBadges();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnDestroy()
    {
        if (monster == null) return;
        monster.OnHPChanged     -= UpdateBar;
        monster.OnStatusChanged -= RefreshStatusBadges;
    }

    private void Update()
    {
        if (Camera.main == null || barCanvas == null) return;
        Vector3 dir = Camera.main.transform.position - barCanvas.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            barCanvas.transform.rotation = Quaternion.LookRotation(-dir);
    }

    // ── HP update ─────────────────────────────────────────────────────────────

    private void UpdateBar(int current, int max)
    {
        if (hpSlider == null) return;
        hpSlider.maxValue = Mathf.Max(1, max);
        hpSlider.value    = current;
    }

    // ── UI construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        Transform anchor  = transform.Find("HPHandler");
        float     yOffset = anchor != null ? 0f : VerticalOffset;
        if (anchor == null) anchor = transform;

        var canvasGO = new GameObject("HPBarCanvas");
        canvasGO.transform.SetParent(anchor);
        canvasGO.transform.localPosition = new Vector3(0f, yOffset, 0f);
        canvasGO.transform.localRotation = Quaternion.identity;
        canvasGO.transform.localScale    = Vector3.one * CanvasScale;

        barCanvas              = canvasGO.AddComponent<Canvas>();
        barCanvas.renderMode   = RenderMode.WorldSpace;
        barCanvas.sortingOrder = 100;

        var canvasRect       = canvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(BarWidth, BarHeight);

        _badgeParent = canvasGO.transform;

        // Dark background strip
        var bgGO   = MakeChild(canvasGO, "BG");
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgGO.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        // Fill area
        var sliderGO   = MakeChild(canvasGO, "HPSlider");
        var sliderRect = sliderGO.GetComponent<RectTransform>();
        sliderRect.anchorMin = Vector2.zero;
        sliderRect.anchorMax = Vector2.one;
        sliderRect.sizeDelta = Vector2.zero;

        var fillAreaGO   = MakeChild(sliderGO, "FillArea");
        var fillAreaRect = fillAreaGO.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.sizeDelta = Vector2.zero;

        var fillGO   = MakeChild(fillAreaGO, "Fill");
        var fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;

        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color = (monster != null && monster.IsEnemy)
                        ? new Color(0.9f, 0.2f, 0.2f, 1f)
                        : new Color(0.2f, 0.85f, 0.3f, 1f);

        hpSlider              = sliderGO.AddComponent<Slider>();
        hpSlider.direction    = Slider.Direction.LeftToRight;
        hpSlider.minValue     = 0f;
        hpSlider.maxValue     = monster != null ? monster.MaxHP : 100f;
        hpSlider.value        = monster != null ? monster.CurrentHP : 100f;
        hpSlider.wholeNumbers = true;
        hpSlider.interactable = false;
        hpSlider.fillRect     = fillRect;
    }

    // ── Status badges ─────────────────────────────────────────────────────────

    private void RefreshStatusBadges()
    {
        if (_badgeParent == null || monster == null) return;

        var statuses = monster.ActiveStatuses;

        // Build lookup of currently active statuses
        var current = new Dictionary<AttackEnum.StatusEffect, ActiveStatus>();
        foreach (var s in statuses) current[s.data.ID] = s;

        // Animate out badges whose status expired
        var expired = new List<AttackEnum.StatusEffect>();
        foreach (var id in _badges.Keys)
            if (!current.ContainsKey(id)) expired.Add(id);

        foreach (var id in expired)
        {
            var go = _badges[id];
            _badges.Remove(id);
            CancelBadgeAnim(id);
            StartCoroutine(AnimateOut(go));
        }

        // Add new badges and refresh existing ones
        int i = 0;
        foreach (var kvp in current)
        {
            float x = -BarWidth * 0.5f + BadgeW * 0.5f + i * (BadgeW + BadgeGap);

            if (_badges.TryGetValue(kvp.Key, out var existing))
            {
                // Reposition (in case order changed) and update turn count
                existing.GetComponent<RectTransform>().anchoredPosition =
                    new Vector2(x, BadgeFinalY);
                UpdateBadgeTurns(existing, kvp.Key, kvp.Value.remainingTurns);
            }
            else
            {
                var badge = CreateBadge(kvp.Value, x);
                _badges[kvp.Key] = badge;
                CancelBadgeAnim(kvp.Key);
                _anims[kvp.Key] = StartCoroutine(AnimateIn(badge, x));
            }
            i++;
        }
    }

    // ── Badge creation ────────────────────────────────────────────────────────

    private GameObject CreateBadge(ActiveStatus s, float x)
    {
        var root     = MakeChild(_badgeParent.gameObject, s.data.ID.ToString());
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin        = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax        = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta        = new Vector2(BadgeW, BadgeH);
        rootRect.anchoredPosition = new Vector2(x, 0f); // starts at bar level

        // Black border (full background)
        var border = MakeChild(root, "Border");
        var bRect  = border.GetComponent<RectTransform>();
        bRect.anchorMin = Vector2.zero;
        bRect.anchorMax = Vector2.one;
        bRect.sizeDelta = Vector2.zero;
        border.AddComponent<Image>().color = Color.black;

        // Colored fill (inset by BadgeBorder)
        var fill  = MakeChild(root, "Fill");
        var fRect = fill.GetComponent<RectTransform>();
        fRect.anchorMin = Vector2.zero;
        fRect.anchorMax = Vector2.one;
        fRect.sizeDelta = new Vector2(-BadgeBorder * 2f, -BadgeBorder * 2f);
        fill.AddComponent<Image>().color = StatusColor(s.data.ID);

        // Text label
        var lbl      = MakeChild(root, "Lbl");
        var lblRect  = lbl.GetComponent<RectTransform>();
        lblRect.anchorMin = Vector2.zero;
        lblRect.anchorMax = Vector2.one;
        lblRect.sizeDelta = Vector2.zero;

        var txt = lbl.AddComponent<Text>();
        txt.text      = BadgeText(s.data.ID, s.remainingTurns);
        txt.fontSize  = 9;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = Color.white;
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return root;
    }

    private static void UpdateBadgeTurns(GameObject badge,
        AttackEnum.StatusEffect id, int turns)
    {
        var lbl = badge.transform.Find("Lbl")?.GetComponent<Text>();
        if (lbl != null) lbl.text = BadgeText(id, turns);
    }

    private static string BadgeText(AttackEnum.StatusEffect id, int turns)
        => $"{StatusAbbr(id)}\n{turns}";

    // ── Animation ─────────────────────────────────────────────────────────────

    private IEnumerator AnimateIn(GameObject badge, float x)
    {
        var rect  = badge.GetComponent<RectTransform>();
        float t   = 0f;
        float endY = BadgeFinalY;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / AnimDuration;
            float y = Mathf.Lerp(0f, endY, EaseOut(t));
            if (rect != null) rect.anchoredPosition = new Vector2(x, y);
            yield return null;
        }
        if (rect != null) rect.anchoredPosition = new Vector2(x, endY);
    }

    private IEnumerator AnimateOut(GameObject badge)
    {
        var rect = badge.GetComponent<RectTransform>();
        if (rect == null) { Destroy(badge); yield break; }

        float t      = 0f;
        float startY = rect.anchoredPosition.y;
        float startX = rect.anchoredPosition.x;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / AnimDuration;
            float y = Mathf.Lerp(startY, 0f, EaseIn(t));
            if (rect != null) rect.anchoredPosition = new Vector2(startX, y);
            yield return null;
        }
        Destroy(badge);
    }

    private void CancelBadgeAnim(AttackEnum.StatusEffect id)
    {
        if (_anims.TryGetValue(id, out var c) && c != null) StopCoroutine(c);
        _anims.Remove(id);
    }

    private static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }
    private static float EaseIn (float t) { t = Mathf.Clamp01(t); return t * t; }

    // ── Status colour / label lookup ──────────────────────────────────────────

    private static Color StatusColor(AttackEnum.StatusEffect id) => id switch
    {
        AttackEnum.StatusEffect.Burn   => new Color(1f,    0.45f, 0.10f, 1f),
        AttackEnum.StatusEffect.Freeze => new Color(0.30f, 0.85f, 1f,   1f),
        AttackEnum.StatusEffect.Shock  => new Color(1f,    0.90f, 0f,   1f),
        AttackEnum.StatusEffect.Poison => new Color(0.65f, 0.15f, 0.85f,1f),
        AttackEnum.StatusEffect.Sleep  => new Color(0.45f, 0.45f, 0.80f,1f),
        _                              => Color.grey,
    };

    private static string StatusAbbr(AttackEnum.StatusEffect id) => id switch
    {
        AttackEnum.StatusEffect.Burn   => "BRN",
        AttackEnum.StatusEffect.Freeze => "FRZ",
        AttackEnum.StatusEffect.Shock  => "SHK",
        AttackEnum.StatusEffect.Poison => "PSN",
        AttackEnum.StatusEffect.Sleep  => "ZZZ",
        _                              => "???",
    };

    // ── Helper ────────────────────────────────────────────────────────────────

    private static GameObject MakeChild(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        return go;
    }
}
