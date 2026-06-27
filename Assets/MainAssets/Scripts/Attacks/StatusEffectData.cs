using UnityEngine;

[CreateAssetMenu(menuName = "Attack Moves/Create Status Effect")]
public class StatusEffectData : ScriptableObject
{
    [SerializeField] private AttackEnum.StatusEffect id;
    [SerializeField] private AttackEnum.ElementType element;
    [SerializeField] private bool dispel = false;

    [ShowIf("!dispel", "id", "Burn|Poison")]
    [Header("Behavior")]
    [SerializeField] private int damage = 0;

    [ShowIf("!dispel", "id", "Shock")]
    [Tooltip("Extra AP cost added to all actions while this status is active.")]
    [SerializeField] private int extraAPCost = 0;

    [ShowIf("!dispel", "id", "Burn|Poison")]
    [Header("Rules")]
    [SerializeField] private bool stacks = false;

    [ShowIf("dispel")]
    [Header("Dispel Rules")] 
    [SerializeField] private bool dispelAll = false;

    // Identity
    public AttackEnum.StatusEffect ID => id;
    public AttackEnum.ElementType Element => element;

    // Behavior
    public int Damage => damage;
    public int ExtraAPCost => extraAPCost;

    // Rules
    public bool Stacks => stacks;

    // Dispel Rules
    public bool Dispel => dispel;
    public bool DispelAll => dispelAll;

    /// <summary>Dev/QA use only — sets the status ID at runtime for test-injected instances.</summary>
    public void DevInit(AttackEnum.StatusEffect statusId) => id = statusId;
}
