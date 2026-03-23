using UnityEngine;

[CreateAssetMenu(menuName = "Attack Moves/Create Status Effect")]
public class StatusEffectData : ScriptableObject
{
    [SerializeField] private AttackEnum.StatusEffect id;
    [SerializeField] private AttackEnum.ElementType element;

    [Header("Behavior")]
    [SerializeField] private int damage;

    [Header("Rules")]
    [SerializeField] private bool stacks;

    // Identity
    public AttackEnum.StatusEffect ID => id;
    public AttackEnum.ElementType Element => element;

    // Behavior
    public int Damage => damage;

    // Rules
    public bool Stacks => stacks;
}
