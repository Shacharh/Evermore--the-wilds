using UnityEngine;

[System.Serializable]
public class MonsterAttack
{
    public AttackData data;
    private int currentPP;

    public MonsterAttack(AttackData data)
    {
        this.data = data;
        currentPP = data.MaxPP;
    }

    public int CurrentPP => currentPP;

    public void UsePP()
    {
        currentPP = Mathf.Max(0, currentPP - 1);
    }
}
