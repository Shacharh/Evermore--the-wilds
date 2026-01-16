using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Attack Moves/Attack Database")]
public class AttackDatabase : ScriptableObject
{
    public AttackData[] attacks;

    private Dictionary<string, AttackData> lookup;

    public void Init()
    {
        lookup = new Dictionary<string, AttackData>();

        foreach (var attack in attacks)
        {
            if (!lookup.ContainsKey(attack.ID))
                lookup.Add(attack.ID, attack);
            else
                Debug.LogWarning($"Duplicate attack ID: {attack.ID}");
        }
    }

    public AttackData GetAttackById(string id)
    {
        if (lookup.TryGetValue(id, out var attack))
            return attack;

        Debug.LogError($"Attack not found: {id}");
        return null;
    }
}
