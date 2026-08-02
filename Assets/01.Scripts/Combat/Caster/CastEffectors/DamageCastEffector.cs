using UnityEngine;

public class DamageCastEffector : MonoBehaviour, ICastEffector
{
    [SerializeField] private float _damageMultiplier = 1f;
    [SerializeField] private bool _canSuperCritical = true;

    [Tooltip("Owner가 없을 때(스탯 없는 함정 등) 사용할 고정 데미지")]
    [SerializeField] private float _fallbackDamage = 10f;

    private CasterBase _caster;

    public void Initialize()
    {
        _caster = GetComponentInParent<CasterBase>();
    }

    public void Cast(Collider2D target)
    {
        if (!target.TryGetComponent(out IDamageable damageable))
            damageable = target.GetComponentInParent<IDamageable>();

        if (damageable == null || damageable.IsDead) return;

        Agent owner = _caster != null ? _caster.Owner : null;

        // 자기 자신은 때리지 않는다.
        if (owner != null && target.GetComponentInParent<Agent>() == owner) return;

        Vector2 direction = ((Vector2)(target.transform.position - transform.position)).normalized;
        DamageData damageData = BuildDamageData(owner, direction);
        damageData.damage *= _damageMultiplier;

        damageable.ApplyDamage(damageData);
    }

    private DamageData BuildDamageData(Agent owner, Vector2 direction)
    {
        if (owner != null && owner.AgentStatus != null)
            return DamageHandler.CalculateDamage(owner, direction, _canSuperCritical);

        return DamageHandler.CalculateDamage(_fallbackDamage, 0f, false)
            .WithSource(owner, direction);
    }
}
