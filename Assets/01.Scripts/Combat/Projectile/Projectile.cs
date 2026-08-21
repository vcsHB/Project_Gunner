using UnityEngine;

/// <summary>
/// 직선으로 날아가는 투사체. 풀에서 나오고 풀로 돌아간다.
///
/// 판정은 직접 하지 않고 <see cref="ProjectileCaster"/>에게 맡긴다.
/// 여기는 "얼마나 갔고 언제 죽는가"만 안다. 무엇을 때리는지는 캐스터와 이펙터의 몫이다.
///
/// 프리팹 구조 — 폭발 캐스터는 직격 캐스터의 <b>형제</b>여야 한다.
/// 자식으로 두면 CasterBase가 GetComponentsInChildren으로 상대의 이펙터까지 집어간다.
///
///   Projectile
///    ├ HitCaster  (ProjectileCaster + DamageCastEffector)
///    └ Explosion  (CircleCaster + DamageCastEffector [+ ConditionCastEffector])
/// </summary>
public class Projectile : PoolableMono
{
    [SerializeField] private ProjectileCaster _caster;

    [Tooltip("폭발하는 탄만 채운다. 비어있으면 explosionRadius가 와도 무시된다.")]
    [SerializeField] private CircleCaster _explosionCaster;

    [Tooltip("사거리로도 안 죽는 경우의 안전장치. 화면 밖으로 나간 탄이 영원히 남지 않게 한다.")]
    [SerializeField] private float _maxLifeTime = 5f;

    private ProjectileLaunchData _data;
    private float _travelled;
    private float _aliveTime;
    private int _pierceLeft;
    private bool _launched;

    /// <summary>
    /// 위치와 방향을 함께 받는 이유는 순서 때문이다.
    /// 캐스터의 경로 추적을 자리 잡기 전에 초기화하면 첫 프레임에 원점부터 훑어버린다.
    /// </summary>
    public void Launch(Vector2 position, in ProjectileLaunchData data)
    {
        _data = data;
        _data.direction = data.direction.sqrMagnitude > Mathf.Epsilon
            ? data.direction.normalized
            : Vector2.right;

        _travelled = 0f;
        _aliveTime = 0f;
        _pierceLeft = Mathf.Max(0, data.penetration);

        float angle = Mathf.Atan2(_data.direction.y, _data.direction.x) * Mathf.Rad2Deg;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angle));

        if (_caster != null)
        {
            _caster.Owner = data.owner;
            _caster.Power = data.power;
            _caster.ResetTrail();
        }

        _launched = true;
    }

    private void Update()
    {
        if (!_launched) return;

        float step = _data.speed * Time.deltaTime;
        transform.position += (Vector3)(_data.direction * step);

        _travelled += step;
        _aliveTime += Time.deltaTime;

        // 이동한 뒤에 훑어야 지나온 구간이 판정에 들어간다.
        if (_caster != null)
        {
            _caster.Cast();

            int hitCount = _caster.LastHitCount;
            if (hitCount > 0)
            {
                _pierceLeft -= hitCount;
                if (_pierceLeft < 0)
                {
                    Die();
                    return;
                }
            }
        }

        if (_travelled >= _data.range || _aliveTime >= _maxLifeTime)
            Die();
    }

    private void Die()
    {
        _launched = false;

        Explode();
        Release();
    }

    private void Explode()
    {
        if (_explosionCaster == null) return;

        // 스탯에서 반경이 오지 않으면 프리팹에 찍힌 값을 쓴다.
        // 수류탄처럼 위력과 반경이 무기 스탯이 아니라 프리팹에 붙어 있는 경우다.
        if (_data.explosionRadius > 0f)
            _explosionCaster.Radius = _data.explosionRadius;
        else if (_explosionCaster.Radius <= 0f)
            return;

        _explosionCaster.Owner = _data.owner;
        _explosionCaster.Power = _data.power;
        _explosionCaster.Cast();
    }

    /// <summary>
    /// 수명을 덮어쓴다. 신관이 타고 있는 수류탄처럼 남은 시간이 정해져 있을 때 쓴다.
    /// Launch 직후에 부를 것.
    /// </summary>
    public void OverrideLifeTime(float seconds)
    {
        _maxLifeTime = Mathf.Max(0.01f, seconds);
    }

    /// <summary>
    /// 풀에서 나올 때는 아직 Launch 전이라 위치가 정해지지 않았다.
    /// 여기서 경로를 초기화하면 안 되고, 움직이지 않도록 잠가두기만 한다.
    /// </summary>
    public override void OnSpawn()
    {
        _launched = false;
    }

    public override void OnDespawn()
    {
        _launched = false;
        _travelled = 0f;
        _aliveTime = 0f;
        _pierceLeft = 0;
        _data = default;

        // 다음 발사자에게 이전 판의 위력이 새어나가지 않게 한다.
        if (_caster != null)
        {
            _caster.Owner = null;
            _caster.Power = null;
        }

        if (_explosionCaster != null)
        {
            _explosionCaster.Owner = null;
            _explosionCaster.Power = null;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_caster == null)
            _caster = GetComponentInChildren<ProjectileCaster>(true);
    }
#endif
}
