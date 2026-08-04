using UnityEngine;

/// <summary>
/// 일정 주기로 피해를 주는 상태이상의 공통 구현.
/// </summary>
public abstract class DamageOverTimeCondition : ConditionBase
{
    /// <summary>레벨 1당 한 틱에 들어가는 피해량.</summary>
    protected abstract float DamagePerLevel { get; }
    protected abstract float TickInterval { get; }

    protected float TickDamage => DamagePerLevel * Level;

    private Health _health;
    private float _tickTimer;

    public override void Initialize(Agent owner, object origin, int level, float duration)
    {
        base.Initialize(owner, origin, level, duration);
        _health = owner.GetCompo<Health>();
    }

    public override void OnStart()
    {
        _tickTimer = TickInterval;
    }

    public override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);

        if (_health == null || _health.IsDead) return;

        _tickTimer -= deltaTime;
        if (_tickTimer > 0f) return;

        _tickTimer += TickInterval;
        _health.ApplyDamage(new DamageData
        {
            damage = TickDamage,
            origin = Origin as Agent,
            direction = Vector2.zero,
        });
    }
}
