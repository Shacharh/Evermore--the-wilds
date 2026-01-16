using System.Collections.Generic;
using UnityEngine;

public class Monster : MonoBehaviour
{
    [SerializeField] private MonsterData data;

    private int currentHP;

    // Runtime learned attacks
    [SerializeField] // for debug add [SerializeField], for prodaction remove it
    private List<AttackData> learnedAttacks = new List<AttackData>();
    private const int MaxAttacks = 2;

    private void Start()
    {
        currentHP = data.maxHP;
    }

    public void LearnAttack(AttackData attack)
    {
        if (attack == null || learnedAttacks.Contains(attack)) return;

        if (learnedAttacks.Count >= MaxAttacks)
        {
            Debug.Log($"{gameObject.name} cannot learn more than {MaxAttacks} attacks!");
            return;
        }

        learnedAttacks.Add(attack);

        Debug.Log(
            $"{gameObject.name} learned attack: {attack.displayName}"
        );
    }


    public void ForgetAttack(AttackData attack)
    {
        learnedAttacks.Remove(attack);
    }

    public IReadOnlyList<AttackData> GetAttacks()
    {
        return learnedAttacks;
    }

    public void UseAttack(int index, int usePP)
    {
        if (index < 0 || index >= learnedAttacks.Count)
        {
            Debug.LogWarning("Invalid attack index!");
            return;
        }

        if (usePP <= 0)
        {
            Debug.LogWarning("usePP must be greater than 0!");
            return;
        }

        var attack = learnedAttacks[index];
        Debug.Log($"{data.displayName} uses {attack.displayName}");
        attack.CurrentPP -= usePP;
    }
}
