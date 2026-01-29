using UnityEngine;

[CreateAssetMenu(menuName = "Attack Moves/Create Attack")]
public class AttackData : ScriptableObject
{
    [Header("ID (do NOT change after release)")]
    [SerializeField] private string id;
    public string ID => id;

    [Header("Display")]
    public string displayName;
    public string description;

    [Header("Attack Type and Category")]
    public AttackEnum.AttackCategory category;
    public AttackEnum.ElementType element;

    [Header("Stats")]
    public int power;
    public int maxPP;
    public int consumeActionPoints = 1;
    public bool guaranteedHit = false;
    [SerializeField, ShowIf("!guaranteedHit")]
    public int accuracy = 100;

    [Header("Attributes")]
    public int range = 1;
    public AttackEnum.AttackTarget target;
    public AttackEnum.AttackTargetShape targetShape;
    public int rangeTargetShapeSize = 1;
    public bool isDirect = true;
    [SerializeField, ShowIf("!isDirect")]
    [Range(0f, 0.99f)]public float inDirectHitPrecent = 0;

    private void OnValidate()
    {
        power = Mathf.Max(0, power);
        maxPP = Mathf.Max(0, maxPP);
        consumeActionPoints = Mathf.Max(0, consumeActionPoints);
        accuracy = Mathf.Clamp(accuracy, 0, 100);
    }

}
