using System;
using UnityEngine;

/// <summary>
/// 단일 기체의 무기 하나 — <b>1기체 1무기</b>. 총구 자리(포탑 머리의 소켓)에 붙인다.
///
/// 플레이어 무기(<see cref="PlayerRangedWeapon"/>)와 같은 것은 <b>탄</b>뿐이다(<see cref="Projectile"/>, <see cref="ProjectileLaunchData"/>).
/// 개량·과열·탄종·내구도는 없다. 그 대신 공격 방식(<see cref="UnitFirePattern"/>)이 박자를 정하고,
/// 그 박자를 <see cref="OnPhaseChangedEvent"/>로 내보내 연출이 예고를 그리게 한다.
///
/// AI(<see cref="EnemyAIController"/>)는 "대상 쪽을 보고 있나"만 넘긴다. 어느 쪽으로 쏘는지는
/// <see cref="IAimDirectionProvider"/>(포탑이 보는 쪽)에게 묻는다 — 포탑이 덜 돌았으면 덜 돈 쪽으로 나간다.
/// </summary>
public class UnitWeapon : MonoBehaviour, IAgentComponent
{
    [SerializeField] private UnitWeaponSO _data;

    [Tooltip("탄이 나가는 자리. 비우면 이 오브젝트.")]
    [SerializeField] private Transform _muzzle;

    /// <summary>공격 단계가 바뀌었다. (단계, 그 단계가 이어질 시간). 예고·조준선 연출이 듣는다.</summary>
    public event Action<UnitWeaponPhase, float> OnPhaseChangedEvent;

    /// <summary>한 번 쐈다(산탄이면 여러 발이 한 번에). (총구 위치, 방향).</summary>
    public event Action<Vector2, Vector2> OnFiredEvent;

    private Agent _owner;
    private IAimDirectionProvider _aim;
    private UnitFirePattern _pattern;

    public UnitWeaponSO Data => _data;
    public Agent Owner => _owner;
    public Transform Muzzle => _muzzle != null ? _muzzle : transform;

    public float Range => _data != null ? _data.Range : 0f;
    public TargetType HitTargets => _data != null ? _data.HitTargets : TargetType.None;

    public UnitWeaponPhase Phase { get; private set; } = UnitWeaponPhase.Idle;

    /// <summary>true면 기체가 돌지 않는다(정밀 사격의 고정). AI가 이 동안 사정권 판정도 미룬다.</summary>
    public bool IsAimLocked => _pattern != null && _pattern.IsAimLocked;

    public Vector2 AimDirection => _aim != null ? _aim.AimDirection : (Vector2)Muzzle.right;

    public void Initialize(Agent owner) => _owner = owner;

    public void AfterInitialize()
    {
        _aim = _owner.GetCompo<IAimDirectionProvider>();

        if (_data == null)
        {
            Debug.LogError($"[UnitWeapon] {_owner.name}에 무기 데이터가 없습니다. 쏘지 않습니다.", this);
            return;
        }

        _pattern = _data.CreatePattern();
        _pattern.Bind(this);
    }

    public void Dispose()
    {
        _pattern?.Stop(true);
        _pattern = null;
    }

    /// <summary>이 대상을 맞힐 수 있는가. 대공포는 활공 중인 기체만, 지상포는 걷는 기체만.</summary>
    public bool CanHit(TargetBase target)
        => target != null && (target.CurrentTargetType & HitTargets) != 0;

    /// <summary>AI가 공격 상태에서 매 틱 부른다.</summary>
    public void Tick(bool aimed, float deltaTime) => _pattern?.Tick(aimed, deltaTime);

    /// <summary>공격을 그만둔다. <paramref name="cancel"/>이면(죽음) 예고 중이던 한 발도 내보내지 않는다.</summary>
    public void Stop(bool cancel) => _pattern?.Stop(cancel);

    /// <summary>공격 방식이 부른다. 연출만을 위한 신호다 — 판정은 이걸 보지 않는다.</summary>
    public void SetPhase(UnitWeaponPhase phase, float duration)
    {
        if (Phase == phase && phase == UnitWeaponPhase.Idle) return;

        Phase = phase;
        OnPhaseChangedEvent?.Invoke(phase, duration);
    }

    /// <summary>
    /// 한 번 쏜다. 공격 방식이 부른다.
    /// </summary>
    /// <param name="pellets">한 번에 나가는 탄 수.</param>
    /// <param name="fan">탄 여러 개가 고르게 놓이는 부채꼴 전체 각도(도).</param>
    /// <param name="spread">발마다 무작위로 흩어지는 전체 각도(도). 시드 난수 — 판정이다.</param>
    public void Fire(int pellets, float fan, float spread)
    {
        if (_data == null) return;

        if (_data.Projectile == PoolType.None)
        {
            Debug.LogError($"[UnitWeapon] {_data.name}에 투사체가 지정되지 않았습니다.", this);
            return;
        }

        Vector2 origin = Muzzle.position;
        Vector2 aim = AimDirection;

        ProjectileLaunchData launch = new()
        {
            owner = _owner,
            speed = _data.ProjectileSpeed,
            range = _data.Range,
            power = new CastPower(_data.Damage, _data.CriticalRate),
            penetration = _data.Penetration,
            explosionRadius = _data.ExplosionRadius,
            targetTypes = _data.HitTargets,
            hitFactions = _data.HitFactions,
        };

        int count = Mathf.Max(1, pellets);
        float jitterHalf = Mathf.Max(0f, spread) * 0.5f;

        for (int i = 0; i < count; i++)
        {
            // 부채꼴 안에 고르게. 한 발이면 가운데.
            float angle = count > 1 ? Mathf.Lerp(-fan * 0.5f, fan * 0.5f, i / (float)(count - 1)) : 0f;

            if (jitterHalf > 0f)
                angle += GameRandom.Combat.Range(-jitterHalf, jitterHalf);

            launch.direction = Quaternion.Euler(0f, 0f, angle) * aim;

            Projectile projectile = ObjectPool.Get(_data.Projectile) as Projectile;
            if (projectile == null) return;

            projectile.Launch(origin, launch);
        }

        OnFiredEvent?.Invoke(origin, aim);
    }
}
