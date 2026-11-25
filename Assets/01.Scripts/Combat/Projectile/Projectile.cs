using System;
using UnityEngine;

/// <summary>
/// 날아가는 투사체. 풀에서 나오고 풀로 돌아간다.
///
/// 판정은 직접 하지 않고 <see cref="ProjectileCaster"/>에게 맡긴다.
/// 여기는 "얼마나 갔고 언제 죽는가"만 안다. 무엇을 때리는지는 캐스터와 이펙터의 몫이다.
///
/// 프리팹 구조 — 폭발 캐스터는 직격 캐스터의 <b>형제</b>여야 한다.
/// 자식으로 두면 CasterBase가 GetComponentsInChildren으로 상대의 이펙터까지 집어간다.
///
///   Projectile
///    ├ HitCaster  (ProjectileCaster + DamageCastEffector)
///    ├ Explosion  (CircleCaster + DamageCastEffector [+ ConditionCastEffector])
///    └ Body       (Collider2D + HitBox) — 미사일만. 루트에 TargetBase + Health
///
/// <b>유도</b>(<see cref="_turnRate"/> &gt; 0) — 발사자가 넘긴 대상(<see cref="ProjectileLaunchData.homingTarget"/>) 쪽으로
/// 초당 이 각도까지 꺾는다. 꺾는 힘이 한정이라 옆으로 빠지면 피한다.
///
/// <b>맞는 몸</b>(<see cref="_body"/>) — 미사일은 총으로 떨어뜨릴 수 있다. Agent 없이 맞는 대상(<see cref="TargetBase"/> + <see cref="Health"/>)이다.
/// 떨어지면 그 자리에서 터지고, 그 폭발은 <b>편을 가리지 않는다</b>(<see cref="_shotDownFactions"/>) — 적 미사일을 적 근처에서 떨어뜨리는 플레이.
/// </summary>
public class Projectile : PoolableMono
{
    /// <summary>터졌다(폭발 캐스터가 나간 자리). 폭발 연출이 듣는다 — 판정은 이걸 보지 않는다.</summary>
    public event Action<Vector2> OnExplodedEvent;

    [SerializeField] private ProjectileCaster _caster;

    [Tooltip("폭발하는 탄만 채운다. 비어있으면 explosionRadius가 와도 무시된다.")]
    [SerializeField] private CircleCaster _explosionCaster;

    [Tooltip("사거리로도 안 죽는 경우의 안전장치. 화면 밖으로 나간 탄이 영원히 남지 않게 한다.")]
    [SerializeField] private float _maxLifeTime = 5f;

    [Header("유도")]
    [Tooltip("초당 꺾는 각도(도). 0이면 직진.")]
    [SerializeField, Min(0f)] private float _turnRate;

    [Tooltip("발사 후 이 시간(초) 동안은 곧게 날아간다. 포신에서 바로 꺾이면 발사 방향이 읽히지 않는다.")]
    [SerializeField, Min(0f)] private float _homingDelay = 0.15f;

    [Header("맞는 몸 (미사일)")]
    [Tooltip("총에 맞아 떨어지는 몸. 루트의 Health. 비우면 맞지 않는다.")]
    [SerializeField] private Health _body;

    [Tooltip("떨어져서 터질 때 맞히는 편. 기본은 전부 — 적 근처에서 떨어뜨리면 적이 맞는다.")]
    [SerializeField] private FactionType _shotDownFactions = FactionType.All;

    private TargetBase _bodyTarget;
    private TrailRenderer[] _trails;

    private ProjectileLaunchData _data;
    private float _travelled;
    private float _aliveTime;
    private int _pierceLeft;
    private bool _launched;

    private void Awake()
    {
        _trails = GetComponentsInChildren<TrailRenderer>(true);

        if (_body == null) return;

        _bodyTarget = _body.GetComponent<TargetBase>();
        _body.OnDeadEvent += HandleShotDown;

        // 첫 프레임의 제자리 질의가 제 몸을 집는다. 편이 "전부"인 발사에서도 자기 몸은 빼야 한다.
        if (_caster != null) _caster.IgnoredTarget = _bodyTarget;
    }

    private void OnDestroy()
    {
        if (_body != null) _body.OnDeadEvent -= HandleShotDown;
    }

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

        ApplyRotation();
        transform.position = position;

        // 꼬리는 지난 발의 자리에서부터 이어 그려진다. 자리 잡은 뒤에 지운다.
        for (int i = 0; i < _trails.Length; i++)
            if (_trails[i] != null) _trails[i].Clear();

        if (_caster != null)
        {
            _caster.Owner = data.owner;

            // 경로를 먼저 지우고 위력을 싣는다. ResetTrail이 위력도 비우므로 순서가 바뀌면
            // 무기가 실어 보낸 위력이 사라지고 발사자 스탯(또는 고정값)으로 맞는다.
            _caster.ResetTrail();
            _caster.Power = data.power;

            ApplyTargetTypes(_caster, data.targetTypes);
            ApplyFactions(_caster, data.hitFactions);
        }

        if (_explosionCaster != null)
        {
            ApplyTargetTypes(_explosionCaster, data.targetTypes);
            ApplyFactions(_explosionCaster, data.hitFactions);
        }

        if (_body != null)
        {
            _body.ResetHealth();
            _bodyTarget?.ResetTargetType();
        }

        _launched = true;
    }

    private void Update()
    {
        if (!_launched) return;

        float deltaTime = Time.deltaTime;
        _aliveTime += deltaTime;

        Steer(deltaTime);

        float step = _data.speed * deltaTime;
        transform.position += (Vector3)(_data.direction * step);

        _travelled += step;

        // 이동한 뒤에 훑어야 지나온 구간이 판정에 들어간다.
        if (_caster != null)
        {
            _caster.Cast();

            // 캐스트 안에서 떨어졌을 수 있다(맞은 대상이 터지며 이 탄의 몸을 맞힌 경우).
            if (!_launched) return;

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

    /// <summary>유도. 대상이 사라졌거나 노릴 수 없게 되면(무적·사망) 그 방향 그대로 간다.</summary>
    private void Steer(float deltaTime)
    {
        if (_turnRate <= 0f || _aliveTime < _homingDelay) return;

        TargetBase target = _data.homingTarget;
        if (target == null || !target.IsTargetable) return;

        Vector2 to = target.AimPosition - (Vector2)transform.position;
        if (to.sqrMagnitude <= 0.0001f) return;

        float current = Mathf.Atan2(_data.direction.y, _data.direction.x) * Mathf.Rad2Deg;
        float desired = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
        float next = Mathf.MoveTowardsAngle(current, desired, _turnRate * deltaTime) * Mathf.Deg2Rad;

        _data.direction = new Vector2(Mathf.Cos(next), Mathf.Sin(next));
        ApplyRotation();
    }

    private void ApplyRotation()
    {
        float angle = Mathf.Atan2(_data.direction.y, _data.direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    /// <summary>총에 맞아 떨어졌다. 그 자리에서, 편을 가리지 않고 터진다.</summary>
    private void HandleShotDown()
    {
        if (!_launched) return;

        if (_explosionCaster != null)
            _explosionCaster.OverrideHitFactions(_shotDownFactions);

        Die();
    }

    private void Die()
    {
        _launched = false;

        Explode();
        _bodyTarget?.NotifyLost();
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

        // 떨어진 미사일의 폭발은 쏜 쪽도 맞을 수 있어야 한다 — 주인을 비워 "자기 편 제외"가 걸리지 않게.
        _explosionCaster.Owner = _body != null && _body.IsDead ? null : _data.owner;
        _explosionCaster.Power = _data.power;
        _explosionCaster.Cast();

        OnExplodedEvent?.Invoke(_explosionCaster.transform.position);
    }

    // 지정이 없어도 되돌린다. 풀에서 나온 탄이 이전 무기(대공포)의 설정을 들고 있으면 안 된다.
    private static void ApplyTargetTypes(CasterBase caster, TargetType? types)
    {
        if (types.HasValue) caster.OverrideTargetTypes(types.Value);
        else caster.ResetTargetTypes();
    }

    // 풀에서 다시 나온 탄이 이전 발사자의 편 설정을 들고 있으면 안 된다 — 없으면 프리팹 값으로.
    private static void ApplyFactions(CasterBase caster, FactionType? factions)
    {
        if (factions.HasValue) caster.OverrideHitFactions(factions.Value);
        else caster.ResetHitFactions();
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
            _explosionCaster.ResetHitFactions();
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
