using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Screen-space toast overlay for brief battle feedback messages.
///
/// Usage from anywhere:
///   BattleMessage.Show("No targets in range!", 2.5f);
///
/// Auto-created singleton — no prefab or scene setup needed.
/// Appears in the upper-center of the screen, fades in then out.
/// </summary>
public class BattleMessage : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────

    public static BattleMessage Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        new GameObject("BattleMessage").AddComponent<BattleMessage>();
    }

    // ── UI references ─────────────────────────────────────────────────────────

    private TextMeshProUGUI messageText;
    private CanvasGroup     canvasGroup;
    private Coroutine       activeCoroutine;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        canvasGroup.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Shows <paramref name="message"/> for <paramref name="duration"/> seconds,
    /// then fades it out automatically.
    /// </summary>
    public static void Show(string message, float duration = 2.5f)
        => Instance?.ShowMessage(message, duration);

    private void ShowMessage(string message, float duration)
    {
        messageText.text = message;
        if (activeCoroutine != null) StopCoroutine(activeCoroutine);
        activeCoroutine = StartCoroutine(FadeRoutine(duration));
    }

    // ── Fade Coroutine ────────────────────────────────────────────────────────

    private IEnumerator FadeRoutine(float displayDuration)
    {
        const float fadeInTime  = 0.25f;
        const float fadeOutTime = 0.4f;

        float t = 0f;
        while (t < fadeInTime)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(t / fadeInTime);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        yield return new WaitForSeconds(displayDuration);

        t = 0f;
        while (t < fadeOutTime)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(1f - t / fadeOutTime);
            yield return null;
        }
        canvasGroup.alpha = 0f;
    }

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        var canvasGO = new GameObject("MessageCanvas");
        canvasGO.transform.SetParent(transform);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 700;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        canvasGroup = canvasGO.AddComponent<CanvasGroup>();
        canvasGroup.interactable   = false;
        canvasGroup.blocksRaycasts = false;

        var panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(canvasGO.transform, false);

        var panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin        = new Vector2(0.5f, 0.8f);
        panelRT.anchorMax        = new Vector2(0.5f, 0.8f);
        panelRT.pivot            = new Vector2(0.5f, 0.5f);
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.sizeDelta        = new Vector2(680f, 72f);

        panelGO.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.10f, 0.90f);

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(panelGO.transform, false);

        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(14f, 6f);
        textRT.offsetMax = new Vector2(-14f, -6f);

        messageText                   = textGO.AddComponent<TextMeshProUGUI>();
        messageText.fontSize          = 26f;
        messageText.color             = new Color(1f, 0.85f, 0.2f, 1f);
        messageText.alignment         = TextAlignmentOptions.Center;
        messageText.fontStyle         = FontStyles.Bold;
        messageText.textWrappingMode  = TMPro.TextWrappingModes.NoWrap;
    }
}
