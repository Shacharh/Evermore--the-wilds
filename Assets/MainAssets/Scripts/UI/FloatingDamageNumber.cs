using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Spawns a world-space floating number above a monster when it takes damage or
/// is healed.  No scene setup required — call FloatingDamageNumber.Spawn() from
/// anywhere and the object manages its own lifetime.
/// </summary>
public class FloatingDamageNumber : MonoBehaviour
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Spawns a floating number at <paramref name="worldPos"/>.
    /// </summary>
    /// <param name="worldPos">World position of the target monster.</param>
    /// <param name="amount">Absolute value to display (positive integer).</param>
    /// <param name="isHeal">True → green "+N" text.</param>
    /// <param name="isCrit">True → gold, larger text with a "!" prefix.</param>
    /// <param name="effectiveness">Type matchup — colours the damage number accordingly.</param>
    public static void Spawn(Vector3 worldPos, int amount, bool isHeal = false, bool isCrit = false,
                             TypeEffectiveness effectiveness = TypeEffectiveness.Normal)
    {
        if (amount <= 0) return;
        var go = new GameObject("FloatingDamageNumber");
        go.AddComponent<FloatingDamageNumber>().Init(worldPos, amount, isHeal, isCrit, effectiveness);
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void Init(Vector3 worldPos, int amount, bool isHeal, bool isCrit,
                      TypeEffectiveness effectiveness)
    {
        // Spawn slightly above the monster's feet
        transform.position = worldPos + Vector3.up * 2.2f;

        var tmp = gameObject.AddComponent<TextMeshPro>();
        tmp.alignment    = TextAlignmentOptions.Center;
        tmp.sortingOrder = 20;

        if (isHeal)
        {
            tmp.text      = $"+{amount}";
            tmp.color     = new Color(0.2f, 1f, 0.35f);
            tmp.fontSize  = 6f;
            tmp.fontStyle = FontStyles.Bold;
        }
        else if (isCrit)
        {
            tmp.text      = $"!{amount}!";
            tmp.color     = new Color(1f, 0.85f, 0f);
            tmp.fontSize  = 8f;
            tmp.fontStyle = FontStyles.Bold;
        }
        else
        {
            tmp.text      = $"-{amount}";
            tmp.fontStyle = FontStyles.Normal;

            // Colour and size scale with type effectiveness.
            // SuperEffective / Effective use HDR values (> 1) so HDRP bloom makes them glow.
            // Weak / SuperWeak stay sub-1 — no bloom, visually muted.
            // Requires the TMP material to use the "Distance Field (Surface)" HDRP shader
            // for HDR values to pass through; the default shader clamps to 0–1.
            switch (effectiveness)
            {
                case TypeEffectiveness.SuperEffective:
                    tmp.color     = new Color(2.5f, 0.9f, 0f);   // HDR orange — blooms hard
                    tmp.fontSize  = 8.5f;
                    tmp.fontStyle = FontStyles.Bold;
                    break;
                case TypeEffectiveness.Effective:
                    tmp.color    = new Color(1.8f, 1.4f, 0.2f);  // HDR gold — soft bloom
                    tmp.fontSize = 7f;
                    break;
                case TypeEffectiveness.Weak:
                    tmp.color    = new Color(0.75f, 0.55f, 0.55f); // muted rose, no bloom
                    tmp.fontSize = 5.5f;
                    break;
                case TypeEffectiveness.SuperWeak:
                    tmp.color    = new Color(0.6f, 0.6f, 0.6f);    // grey, no bloom
                    tmp.fontSize = 5f;
                    break;
                default: // Normal
                    tmp.color    = new Color(1f, 0.3f, 0.3f);      // standard red
                    tmp.fontSize = 6f;
                    break;
            }
        }

        float drift = Random.Range(-0.25f, 0.25f);
        StartCoroutine(Animate(tmp, drift));
    }

    private IEnumerator Animate(TextMeshPro tmp, float horizontalDrift)
    {
        const float duration   = 1.4f;
        const float floatHeight = 1.8f;
        const float fadeStart  = 0.55f; // fraction of duration when fade begins

        Vector3 startPos = transform.position;
        Color   baseColor = tmp.color;
        float   elapsed   = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // unscaled so it works while paused
            float t = elapsed / duration;

            // Float upward with slight horizontal drift
            transform.position = startPos + new Vector3(horizontalDrift * t, floatHeight * t, 0f);

            // Face the camera (billboard)
            if (Camera.main != null)
                transform.forward = Camera.main.transform.forward;

            // Fade out in the latter portion
            float alpha = t < fadeStart
                ? 1f
                : Mathf.Lerp(1f, 0f, (t - fadeStart) / (1f - fadeStart));

            tmp.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

            yield return null;
        }

        Destroy(gameObject);
    }
}
