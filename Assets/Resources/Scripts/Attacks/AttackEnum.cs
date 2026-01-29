using UnityEngine;

public class AttackEnum : MonoBehaviour
{
    public enum ElementType
    {
        Fire,
        Water,
        Wind,
        Grass,
        Electric
    }

    public enum AttackCategory
    {
        damage,
        heal,
        buff,
        debuff
    }

    public enum AttackTarget
    {
        self,
        singleTarget,
        AOE
    }

    public enum AttackTargetShape
    {
        cube,
        sphere,
        line,
        column
    }

}
