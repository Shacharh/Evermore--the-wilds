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

    private int currentPP;
    public int CurrentPP
    {
        get { return currentPP; }
        set { currentPP = Mathf.Clamp(value, 0, maxPP); }
    }
}
