using UnityEngine;


[System.Serializable]
public class MonsterAttack
{
    public AttackData data;
    private int currentPP;

    public MonsterAttack(AttackData data)
    {
        this.data = data;
        currentPP = data.maxPP;
    }


    public int getPP() 
    {
        return this.currentPP;
    }

    public void usePP()
    {
        this.currentPP--;
    }
}
