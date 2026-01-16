using UnityEngine;


/* 
 * This component sets up a Monster with its starting attacks
 * note that in this version it is used as a debug to see if the learn
 * function works but can be expended on fucher to load from save files or other sources
 */

[RequireComponent(typeof(Monster))]
public class MonsterSetup : MonoBehaviour
{
    [SerializeField] private string[] startingAttackIds;
    [SerializeField] private Monster monster;

    private void Awake()
    {
        foreach (string id in startingAttackIds)
        {
            AttackData attack =
                GameInitializer.Instance.attackDatabase.GetAttackById(id);

            if (attack != null)
                monster.LearnAttack(attack);
        }
    }
}
