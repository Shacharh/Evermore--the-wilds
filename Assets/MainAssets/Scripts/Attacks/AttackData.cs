using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Attack Moves/Create Attack")]
public class AttackData : ScriptableObject
{
    [Header("ID (do NOT change after release)")]
    [SerializeField] private string id;

    [Header("Display")]
    [SerializeField] private string displayName;
    [SerializeField] private string description;

    [Header("Element")]
    [SerializeField] private AttackEnum.ElementType element;

    [Header("Effects")]
    [SerializeField] private List<AttackEffect> effects = new();

    [Header("Stats")]
    [SerializeField] private int maxPP;
    [SerializeField] private int consumeActionPoints = 1;
    [SerializeField] private bool guaranteedHit = false;
    [SerializeField, ShowIf("!guaranteedHit")]
    private int accuracy = 100;

    [Header("Targeting")]
    [SerializeField] private int range = 1;
    [SerializeField] private AttackEnum.AttackTarget target;
    [SerializeField] private AttackEnum.AttackTargetShape targetShape;
    [SerializeField] private int rangeTargetShapeSize = 1;

    [SerializeField] private bool isDirect = true;
    [SerializeField, ShowIf("!isDirect")]
    [Range(0f, 0.99f)] private float inDirectHitPrecent = 0;

    #region Getters
    public string ID => id;
    public string DisplayName => displayName;
    public string Description => description;
    public AttackEnum.ElementType Element => element;
    public IReadOnlyList<AttackEffect> Effects => effects;
    public int MaxPP => maxPP;
    public int ConsumeActionPoints => consumeActionPoints;
    public bool GuaranteedHit => guaranteedHit;
    public int Accuracy => guaranteedHit ? 100 : accuracy;
    public int Range => range;
    public AttackEnum.AttackTarget Target => target;
    public AttackEnum.AttackTargetShape TargetShape => targetShape;
    public int RangeTargetShapeSize => rangeTargetShapeSize;
    public bool IsDirect => isDirect;
    public float InDirectHitPercent => inDirectHitPrecent;
    #endregion

    private void OnValidate()
    {
        maxPP = Mathf.Max(0, maxPP);
        consumeActionPoints = Mathf.Max(0, consumeActionPoints);
        accuracy = Mathf.Clamp(accuracy, 0, 100);

        if (effects == null) return;

        for (int i = effects.Count - 1; i >= 0; i--)
        {
            if (effects[i] == null)
            {
                effects.RemoveAt(i); // optionally remove the null
                continue;
            }

            effects[i].value = Mathf.Max(0, effects[i].value);
            effects[i].duration = Mathf.Max(1, effects[i].duration);
        }

    }
}
