public static class ItemEnum
{
    public enum Archetype
    {
        Healing,
        Revival,
        BuffDebuff,
        APAffecting,
        AcceptanceRateEnhancing,
        DialogAssist
    }

    [System.Flags]
    public enum UsageContext
    {
        Combat      = 1 << 0,
        Exploration = 1 << 1
    }

    public enum HealMode
    {
        Targeted,   // player clicks a specific alive ally monster
        AreaHeal,   // player clicks a tile; all allies in aoeRadius are healed
        PartyHeal   // all allies healed instantly, no targeting
    }

    public enum DialogAssistType
    {
        HintReveal,     // reveals which answer is Correct
        EliminateOption, // removes one non-Correct answer from view
        AllowRetry      // absorbs the next wrong/reallybad answer without penalty
    }
}
