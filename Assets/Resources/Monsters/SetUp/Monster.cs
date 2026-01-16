using System.Collections.Generic;
using UnityEngine;

public class Monster : MonoBehaviour
{
    [SerializeField] private MonsterData data;

    private int currentHP;

    // Runtime learned attacks
    // for debug change this to public but for production use private with a getter
    public List<AttackData> learnedAttacks = new List<AttackData>();

    private void Start()
    {
        currentHP = data.maxHP;
    }

    public void LearnAttack(AttackData attack)
    {
        if (attack == null || learnedAttacks.Contains(attack))
            return;

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

    public void UseAttack(int index)
    {
        if (index < 0 || index >= learnedAttacks.Count)
            return;

        Debug.Log($"{data.displayName} uses {learnedAttacks[index].displayName}");
    }
}
