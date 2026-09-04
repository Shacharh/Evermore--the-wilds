using UnityEngine;

[CreateAssetMenu(menuName = "Evermore/Item")]
public class ItemData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;

    [Header("Usage")]
    [SerializeField] private ItemEnum.Archetype archetype;
    [SerializeField] private ItemEnum.UsageContext usageContext = ItemEnum.UsageContext.Combat;
    [SerializeField] private int apCost = 1;
    [SerializeField] private int maxHeld = 9;

    // ── Healing ────────────────────────────────────────────────────────────────
    [Header("Healing  (Healing archetype only)")]
    [SerializeField] private ItemEnum.HealMode healMode;
    [SerializeField] private int healAmount;
    [SerializeField, Range(0f, 1f)] private float healPercent;
    [SerializeField] private int aoeRadius = 1;
    [SerializeField] private bool clearsStatusEffects;

    // ── Revival ────────────────────────────────────────────────────────────────
    [Header("Revival  (Revival archetype only)")]
    [SerializeField, Range(0f, 1f)] private float revivePercent = 0.5f;

    // ── Buff / Debuff ──────────────────────────────────────────────────────────
    [Header("Buff / Debuff  (BuffDebuff archetype only)")]
    [SerializeField] private StatusEffectData statusEffect;
    [SerializeField] private int statusDuration = 3;
    [SerializeField] private bool isDebuff; // true = targets enemy, false = targets ally

    // ── AP-Affecting ───────────────────────────────────────────────────────────
    [Header("AP Affecting  (APAffecting archetype only)")]
    [SerializeField] private int apDelta = 2;

    // ── Acceptance Rate Enhancing ──────────────────────────────────────────────
    [Header("Acceptance Rate Enhancing  (AcceptanceRateEnhancing archetype only)")]
    [SerializeField, Range(0f, 0.5f)] private float acceptanceBonus = 0.1f;
    [SerializeField] private bool appliesToEntireAttempt;

    // ── Dialog Assist ──────────────────────────────────────────────────────────
    [Header("Dialog Assist  (DialogAssist archetype only)")]
    [SerializeField] private ItemEnum.DialogAssistType assistType;
    [SerializeField, Min(1)] private int usesPerSession = 1;

    // ── Getters ────────────────────────────────────────────────────────────────
    public string                ID             => id;
    public string                DisplayName    => string.IsNullOrEmpty(displayName) ? name : displayName;
    public string                Description    => description;
    public Sprite                Icon           => icon;
    public ItemEnum.Archetype    Archetype      => archetype;
    public ItemEnum.UsageContext UsageContext   => usageContext;
    public int                   APCost         => apCost;
    public int                   MaxHeld        => Mathf.Max(1, maxHeld);

    public ItemEnum.HealMode     HealMode            => healMode;
    public int                   HealAmount          => healAmount;
    public float                 HealPercent         => healPercent;
    public int                   AoeRadius           => Mathf.Max(1, aoeRadius);
    public bool                  ClearsStatusEffects => clearsStatusEffects;

    public float                 RevivePercent  => revivePercent;

    public StatusEffectData      StatusEffect   => statusEffect;
    public int                   StatusDuration => statusDuration;
    public bool                  IsDebuff       => isDebuff;

    public int                   APDelta        => apDelta;

    public float                      AcceptanceBonus        => acceptanceBonus;
    public bool                       AppliesToEntireAttempt => appliesToEntireAttempt;
    public ItemEnum.DialogAssistType  AssistType             => assistType;
    public int                        UsesPerSession         => Mathf.Max(1, usesPerSession);

    private void OnValidate()
    {
        apCost         = Mathf.Max(0, apCost);
        maxHeld        = Mathf.Max(1, maxHeld);
        aoeRadius      = Mathf.Max(1, aoeRadius);
        statusDuration = Mathf.Max(1, statusDuration);
        healAmount     = Mathf.Max(0, healAmount);
    }
}
