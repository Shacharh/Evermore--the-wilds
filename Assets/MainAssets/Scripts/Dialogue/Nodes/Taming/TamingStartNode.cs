using XNode;
using UnityEngine;

[CreateNodeMenu("Dialogue/Taming/Taming Start")]
[NodeTint(200, 140, 40)]
public class TamingStartNode : Node
{
    [Header("Session Settings")]
    [Min(1)] public int questionsPerSession = 3;

    [Tooltip("Overrides species baseAcceptance. Set to -1 to use MonsterData value.")]
    [Range(-1f, 1f)] public float baseAcceptanceOverride = -1f;
}
