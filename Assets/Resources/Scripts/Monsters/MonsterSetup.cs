using UnityEngine;

[RequireComponent(typeof(Monster))]
public class MonsterSetup : MonoBehaviour
{
    [SerializeField] private string[] startingAttackIds;
    [SerializeField] private Monster monster;

    private void Start()
    {
        foreach (string id in startingAttackIds)
        {
            AttackEntry entry = FindAttackInMovePool(id);

            if (entry != null)
            {
                monster.LearnAttack(entry.attack);
            }
            else
            {
                Debug.LogWarning($"Attack {id} not found in move pool of {monster.Data.displayName}");
            }
        }
    }

    private AttackEntry FindAttackInMovePool(string attackId)
    {
        foreach (AttackEntry entry in monster.Data.movePool)
        {
            if (entry.attack != null && entry.attack.ID == attackId)
                return entry;
        }

        return null;
    }
}
