using UnityEngine;

public static class DamageHandler
{
    public const float CriticalMultiplier = 2f;
    public const float SuperCriticalMultiplier = 3f;

    /// <summary>
    /// 방어력 1당 감소율. defense 100이면 데미지가 절반이 된다.
    /// </summary>
    public const float DefenseConstant = 100f;

    /// <summary>
    /// 치명타를 굴려 최종 데미지를 만든다.
    /// criticalRate가 1을 넘으면 치명타는 확정이고, 초과분이 극대화 확률이 된다. (1.3 -> 확정 치명타 + 30% 극대화)
    /// </summary>
    public static DamageData CalculateDamage(float damage, float criticalRate, bool canSuperCritical)
    {
        bool isCritical = Random.value < criticalRate;
        bool isSuperCritical = canSuperCritical && isCritical && Random.value < criticalRate - 1f;

        if (isSuperCritical)
            damage *= SuperCriticalMultiplier;
        else if (isCritical)
            damage *= CriticalMultiplier;

        return new DamageData
        {
            damage = damage,
            isCritical = isCritical,
            isSuperCritical = isSuperCritical,
        };
    }

    /// <summary>
    /// 공격자의 스탯을 그대로 사용해 데미지를 계산한다.
    /// </summary>
    public static DamageData CalculateDamage(Agent origin, Vector2 direction, bool canSuperCritical = true)
    {
        AgentStatus status = origin.AgentStatus;
        if (status == null)
        {
            Debug.LogWarning($"[DamageHandler] {origin.name}에 AgentStatus가 없습니다.", origin);
            return default;
        }

        return CalculateDamage(status.Damage.TotalValue, status.CriticalRate.TotalValue, canSuperCritical)
            .WithSource(origin, direction);
    }

    /// <summary>
    /// 방어력을 적용한 최종 피해량. 방어력이 아무리 높아도 0 아래로는 내려가지 않는다.
    /// </summary>
    public static float ApplyDefense(float damage, float defense)
    {
        if (defense <= 0f) return damage;

        return damage * (DefenseConstant / (DefenseConstant + defense));
    }
}
