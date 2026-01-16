using UnityEngine;

[CreateAssetMenu(menuName = "Battle/Attack")]
public class AttackData : ScriptableObject
{
    [Header("ID (do NOT change after release)")]
    public string id;

    [Header("Display")]
    public string displayName;

    [Header("Stats")]
    public AttackEnum.ElementType element;
    public int power;
    public int maxPP;
}
