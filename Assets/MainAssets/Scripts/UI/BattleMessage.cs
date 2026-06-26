using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Screen-space toast overlay for brief battle feedback messages.
///
/// Usage from anywhere:
///   BattleMessage.Show("No targets in range!", 2.5f);
///
/// Auto-created singleton — no prefab or scene setup needed.
/// Appears in the upper-center of the screen, fades in then out.
/// Uses UI Toolkit — resolution-independent at any screen size.
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

    private VisualElement _root;
    private Label         _label;
    private Coroutine     _activeCoroutine;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
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
        _label.text = message;
        if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
        _activeCoroutine = StartCoroutine(FadeRoutine(duration));
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
            _root.style.opacity = Mathf.Clamp01(t / fadeInTime);
            yield return null;
        }
        _root.style.opacity = 1f;

        yield return new WaitForSeconds(displayDuration);

        t = 0f;
        while (t < fadeOutTime)
        {
            t += Time.deltaTime;
            _root.style.opacity = Mathf.Clamp01(1f - t / fadeOutTime);
            yield return null;
        }
        _root.style.opacity = 0f;
    }

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        var s   = UIStyleConfig.Load();
        var doc = gameObject.AddComponent<UIDocument>();
        doc.panelSettings = s?.panelSettings;
        doc.sortingOrder  = 700;

        _root = doc.rootVisualElement;
        _root.pickingMode = PickingMode.Ignore;
        _root.style.opacity      = 0f;
        _root.style.position     = Position.Absolute;
        _root.style.left = 0; _root.style.top = 0; _root.style.right = 0; _root.style.bottom = 0;
        // Center the panel horizontally, place it near the top
        _root.style.alignItems     = Align.Center;
        _root.style.justifyContent = Justify.FlexStart;
        _root.style.paddingTop     = new StyleLength(new Length(7f, LengthUnit.Percent));

        var panel = new VisualElement();
        panel.pickingMode = PickingMode.Ignore;
        panel.style.backgroundColor       = new Color(0.04f, 0.04f, 0.10f, 0.90f);
        panel.style.paddingTop            = 12; panel.style.paddingBottom = 12;
        panel.style.paddingLeft           = 24; panel.style.paddingRight  = 24;
        panel.style.borderTopLeftRadius    = 6; panel.style.borderTopRightRadius    = 6;
        panel.style.borderBottomLeftRadius = 6; panel.style.borderBottomRightRadius = 6;

        _label = new Label();
        _label.pickingMode = PickingMode.Ignore;
        _label.style.fontSize                = 26;
        _label.style.unityFontStyleAndWeight = FontStyle.Bold;
        _label.style.color                   = new Color(1f, 0.85f, 0.2f, 1f);
        _label.style.unityTextAlign          = TextAnchor.MiddleCenter;
        _label.style.whiteSpace              = WhiteSpace.NoWrap;

        panel.Add(_label);
        _root.Add(panel);
    }
}
