using UnityEngine;

public class AttackCommandManager : MonoBehaviour
{
    private static AttackCommandManager _instance;
    public static AttackCommandManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<AttackCommandManager>();
            return _instance;
        }
    }

    private Monster currentAttacker;
    private Monster currentTarget;
    private int currentAttackIndex;
    private bool currentIsDirect;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    /// <summary>
    /// Call this when the player selects an attack and target
    /// This stores the attack context before playing the animation
    /// </summary>
    public void SetupAttack(Monster attacker, Monster target, int attackIndex, bool isDirect)
    {
        currentAttacker = attacker;
        currentTarget = target;
        currentAttackIndex = attackIndex;
        currentIsDirect = isDirect;

        Debug.Log($"Attack setup: {attacker.Data.displayName} -> {target.Data.displayName}, Attack Index: {attackIndex}");
    }

    /// <summary>
    /// Called from animation events - only needs the effect index
    /// </summary>
    public void TriggerAttackEffect(int effectIndex)
    {
        if (currentAttacker == null || currentTarget == null)
        {
            Debug.LogError("Attack not properly setup! Call SetupAttack first.");
            return;
        }

        currentAttacker.UseAttackEffect(
            currentAttacker,
            currentTarget,
            currentAttackIndex,
            effectIndex,
            currentIsDirect
        );
    }

    /// <summary>
    /// Optional: Clear the attack context after animation completes
    /// </summary>
    public void ClearAttack()
    {
        currentAttacker = null;
        currentTarget = null;
        currentAttackIndex = -1;
        currentIsDirect = false;
    }
}