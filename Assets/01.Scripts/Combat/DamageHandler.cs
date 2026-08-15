using UnityEngine;

public static class DamageHandler
{
    public const float CriticalMultiplier = 2f;
    public const float SuperCriticalMultiplier = 3f;

    /// <summary>
    /// 방어력으로 아무리 깎여도 이만큼은 들어간다. 원래 데미지가 이보다 작으면 원래 데미지가 하한이다.
    /// </summary>
    public const float MinDamage = 1f;

    /// <summary>
    /// 치명타를 굴려 최종 데미지를 만든다.
    /// criticalRate가 1을 넘으면 치명타는 확정이고, 초과분이 극대화 확률이 된다. (1.3 -> 확정 치명타 + 30% 극대화)
    /// </summary>
    public static DamageData CalculateDamage(float damage, float criticalRate, bool canSuperCritical)
    {
        // 멀티에서 클라마다 결과가 갈리면 안 되므로 시드 난수를 쓴다. UnityEngine.Random 금지.
        IRandomSource random = GameRandom.Combat;

        bool isCritical = random.Value < criticalRate;
        bool isSuperCritical = canSuperCritical && isCritical && random.Value < criticalRate - 1f;

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
    /// 방어력(깡 차감) → 피해 저항(% 감소) 순으로 적용한 최종 피해량.
    /// </summary>
    public static float CalculateFinalDamage(float damage, float defense, float damageResistance)
    {
        if (damage <= 0f) return 0f;

        // 방어력이 아무리 높아도 MinDamage(혹은 원래 데미지)는 들어간다.
        float floor = Mathf.Min(damage, MinDamage);
        float afterDefense = Mathf.Max(damage - defense, floor);

        return afterDefense * (1f - Mathf.Clamp01(damageResistance));
    }
}
