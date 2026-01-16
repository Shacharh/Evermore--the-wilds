using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Battle/Attack Database")]
public class AttackDatabase : ScriptableObject
{
    public AttackData[] attacks;

    private Dictionary<string, AttackData> lookup;

    public void Init()
    {
        lookup = new Dictionary<string, AttackData>();

        foreach (var attack in attacks)
        {
            if (!lookup.ContainsKey(attack.id))
                lookup.Add(attack.id, attack);
            else
                Debug.LogWarning($"Duplicate attack ID: {attack.id}");
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
