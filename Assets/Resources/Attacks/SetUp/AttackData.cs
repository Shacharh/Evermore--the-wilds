using UnityEngine;

[CreateAssetMenu(menuName = "Attack Moves/Create Attack")]
public class AttackData : ScriptableObject
{
    [Header("ID (do NOT change after release)")]
    [SerializeField] private string id;
    public string ID => id;

    [Header("Display")]
    public string displayName;

    [Header("Stats")]
    public AttackEnum.ElementType element;
    public int power;
    public int maxPP;
    public int consumeActionPoints = 1;

    private void OnValidate()
    {
        power = Mathf.Max(0, power);
        maxPP = Mathf.Max(0, maxPP);
        consumeActionPoints = Mathf.Max(0, consumeActionPoints);
    }

}
