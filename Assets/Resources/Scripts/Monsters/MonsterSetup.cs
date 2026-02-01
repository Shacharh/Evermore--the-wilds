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

    private void Start()
    {
        // Check if the global Instance exists first
        if (GameInitializer.Instance == null)
        {
            Debug.LogError("GameInitializer Instance is null! Make sure the GameInitializer script is on a GameObject in your scene.");
            return;
        }

        foreach (string id in startingAttackIds)
        {
            // Now it's safe to access the database
            AttackData attack = GameInitializer.Instance.attackDatabase.GetAttackById(id);

            if (attack != null)
                monster.LearnAttack(attack);
        }
    }
}
