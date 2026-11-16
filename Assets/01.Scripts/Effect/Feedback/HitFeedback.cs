using UnityEngine;

/// <summary>
/// 맞았을 때의 연출. <b>맞는 쪽(유닛 루트)에 붙인다</b> — 무엇에 맞았는지(실드인지 몸체인지)는 맞는 쪽만 안다.
/// 근접·폭발·투사체 모두 <see cref="Health.OnDamagedEvent"/> 하나로 온다.
///
/// <list type="bullet">
/// <item><b>몸체</b> — 맞은 지점(<see cref="DamageData.hitPoint"/>)에서, 공격이 온 쪽으로 튄다(공격 방향의 반대).</item>
/// <item><b>실드</b> — 맞은 지점에서 공격이 온 쪽으로 되짚어 <b>실드 외곽선과 만나는 점</b>에서, 바깥(법선)으로 튄다.
/// 판정은 몸체 콜라이더 그대로다 — 위치만 외곽으로 옮긴다.</item>
/// </list>
///
/// 맞은 지점이 없는 피해(지속 피해 등)는 연출하지 않는다. 틱마다 불꽃이 튀면 안 된다.
/// </summary>
public class HitFeedback : MonoBehaviour
{
    [Header("Body")]
    [SerializeField] private PoolType _hitEffect = PoolType.None;
    [SerializeField] private SoundDataSO _hitSound;

    [Header("Shield")]
    [SerializeField] private PoolType _shieldHitEffect = PoolType.None;
    [SerializeField] private SoundDataSO _shieldHitSound;

    [Tooltip("실드 중심. 비우면 이 오브젝트.")]
    [SerializeField] private Transform _shieldCenter;
    [Tooltip("실드 외곽 반지름(월드). 0이면 실드 중심 아래 SpriteRenderer 크기로 잰다.")]
    [SerializeField, Min(0f)] private float _shieldRadius;

    private Health _health;

    private void Awake()
    {
        _health = GetComponent<Health>();

        if (_shieldCenter == null)
            _shieldCenter = transform;

        if (_shieldRadius <= 0f)
        {
            SpriteRenderer sprite = _shieldCenter.GetComponentInChildren<SpriteRenderer>(true);
            if (sprite != null && _shieldCenter != transform)
                _shieldRadius = sprite.bounds.extents.x;
        }
    }

    private void OnEnable()
    {
        if (_health != null)
            _health.OnDamagedEvent += HandleDamaged;
    }

    private void OnDisable()
    {
        if (_health != null)
            _health.OnDamagedEvent -= HandleDamaged;
    }

    private void HandleDamaged(DamageData data)
    {
        if (!data.hasHitPoint) return;

        Vector2 incoming = data.direction.sqrMagnitude > Mathf.Epsilon ? data.direction.normalized : Vector2.down;

        if (data.blockedByShield)
        {
            // 외곽 반지름을 모르면(실드 그림이 없는 유닛) 맞은 지점에서 그대로 터뜨린다.
            Vector2 point = data.hitPoint;
            Vector2 outward = -incoming;

            if (_shieldRadius > 0f)
            {
                Vector2 center = _shieldCenter.position;
                point = ShieldEntryPoint(center, _shieldRadius, data.hitPoint, incoming);

                Vector2 normal = point - center;
                if (normal.sqrMagnitude > Mathf.Epsilon) outward = normal;
            }

            EffectPlayer.Play(_shieldHitEffect, point, outward);
            SoundManager.Play(_shieldHitSound, point);
            return;
        }

        EffectPlayer.Play(_hitEffect, data.hitPoint, -incoming);
        SoundManager.Play(_hitSound, data.hitPoint);
    }

    /// <summary>
    /// 맞은 지점에서 공격이 온 쪽(-incoming)으로 되짚어 원과 만나는 점. 탄이 실드에 들어온 자리다.
    /// 맞은 지점이 원 밖이면(실드보다 큰 몸체) 그 지점에서 가장 가까운 외곽점으로 대신한다.
    /// </summary>
    private static Vector2 ShieldEntryPoint(Vector2 center, float radius, Vector2 hitPoint, Vector2 incoming)
    {
        Vector2 back = -incoming;
        Vector2 offset = hitPoint - center;

        // |offset + t·back|² = r²  →  t² + 2(offset·back)t + (|offset|² − r²) = 0
        float b = Vector2.Dot(offset, back);
        float c = offset.sqrMagnitude - radius * radius;

        if (c > 0f)
            return center + offset.normalized * radius;

        float t = -b + Mathf.Sqrt(Mathf.Max(0f, b * b - c));
        return hitPoint + back * t;
    }
}
