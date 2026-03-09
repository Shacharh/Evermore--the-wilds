public class ActiveStatus
{
    public StatusEffectData data;
    public int remainingTurns;


    public ActiveStatus(StatusEffectData data, int duration)
    {
        this.data = data;
        this.remainingTurns = duration;
    }
}
