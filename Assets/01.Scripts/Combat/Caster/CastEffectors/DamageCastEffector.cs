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

        Vector2 origin = _caster != null ? _caster.HitOrigin : (Vector2)transform.position;
        Vector2 direction = _caster != null
            ? _caster.GetHitDirection(hit)
            : (hit.Position - origin).normalized;

        // 대상 표면에서 공격이 온 쪽의 점. 피격 이펙트가 여기서 터진다.
        // 출발점이 콜라이더 안이면(밀착 사격) ClosestPoint가 출발점을 그대로 준다 — 그래도 대상 위다.
        Vector2 point = hit.Collider != null ? hit.Collider.ClosestPoint(origin) : hit.Position;

        DamageData damageData = BuildDamageData(owner, direction).WithHitPoint(point);
        damageData.damage *= _damageMultiplier * hit.DamageMultiplier;
        damageData.hitPart = hit.HitBox != null ? hit.HitBox.Part : EquipSlotType.Core;

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
