[System.Serializable]
public class MonsterAttack
{
    public AttackData data;
    public int currentPP;

    public MonsterAttack(AttackData data)
    {
        this.data = data;
        currentPP = data.maxPP;
    }
}
