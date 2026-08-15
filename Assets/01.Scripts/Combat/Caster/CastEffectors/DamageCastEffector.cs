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

    public void Cast(in CastHit hit)
    {
        IDamageable damageable = hit.Target.Damageable;
        if (damageable == null) return;

        Agent owner = _caster != null ? _caster.Owner : null;
        Vector2 direction = (hit.Position - (Vector2)transform.position).normalized;

        DamageData damageData = BuildDamageData(owner, direction);
        damageData.damage *= _damageMultiplier * hit.DamageMultiplier;

        damageable.ApplyDamage(damageData);
    }

    private DamageData BuildDamageData(Agent owner, Vector2 direction)
    {
        // 발사자가 위력을 실어 보냈으면 그게 우선이다.
        // 무기 스탯과 탄종 차이는 Agent 스탯에 들어있지 않아서, 이걸 무시하면 전부 사라진다.
        if (_caster != null && _caster.Power.HasValue)
        {
            CastPower power = _caster.Power.Value;

            return DamageHandler.CalculateDamage(power.damage, power.criticalRate, _canSuperCritical)
                .WithSource(owner, direction);
        }

        if (owner != null && owner.AgentStatus != null)
            return DamageHandler.CalculateDamage(owner, direction, _canSuperCritical);

        return DamageHandler.CalculateDamage(_fallbackDamage, 0f, false)
            .WithSource(owner, direction);
    }
}
