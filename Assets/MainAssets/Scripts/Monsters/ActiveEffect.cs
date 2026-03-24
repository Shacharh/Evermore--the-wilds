public class ActiveEffect
{
    public AttackEnum.AttackBuffType stat;
    public int value;
    public int remainingTurns;

    public ActiveEffect(
        AttackEnum.AttackBuffType stat,
        int value,
        int duration)
    {
        this.stat = stat;
        this.value = value;
        this.remainingTurns = duration;
    }
}
