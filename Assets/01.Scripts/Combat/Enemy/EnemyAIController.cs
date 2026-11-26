using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 적의 머리. <b>상태 전환만 하고, 움직임과 판정은 남에게 맡긴다.</b>
///
/// | 하는 일 | 맡기는 곳 |
/// |---|---|
/// | 대상 고르기 | 여기 |
/// | 상태 전환 | 여기 + <see cref="EnemyState"/> |
/// | 실제 이동 | <see cref="EnemyMoveController"/> |
/// | 공격 판정 | <see cref="CasterBase"/> |
///
/// 거리값(탐지·사정권)은 데이터(<see cref="UnitDataSO"/>)가 아니라 여기, 즉 <b>프리팹</b>에 있다.
/// 몸집과 무기 길이에 따라 정해지는 값이라 비주얼과 같이 움직여야 하기 때문이다.
/// 체력·공격력처럼 밸런스로 조정되는 값은 데이터에 있다.
/// </summary>
[RequireAgentComponent(typeof(EnemyMoveController))]
public class EnemyAIController : MonoBehaviour, IAgentComponent, IAimDirectionProvider, IAimTargetProvider
{
    [Header("거리")]
    [Tooltip("이 안에 들어오면 쫓기 시작한다.")]
    [SerializeField, Min(0f)] private float _detectRange = 12f;

    [Tooltip("탐지 거리에 더해지는 여유. 쫓다가 이만큼 더 벌어져야 놓아준다. " +
             "0으로 두면 경계에서 쫓았다 멈췄다 하며 떨린다.")]
    [SerializeField, Min(0f)] private float _loseMargin = 4f;

    [Tooltip("무장이 없을 때 쓰는 근접 사정권. 이 안에 들어오면 멈춰서 때린다.")]
    [SerializeField, Min(0.1f)] private float _attackRange = 1.6f;

    [Tooltip("팔에 무기를 달고 있을 때 멈춰서 쏘기 시작하는 거리. " +
             "근접 사정권과 따로 두는 이유는 총을 든 기체가 코앞까지 붙을 이유가 없기 때문이다.")]
    [SerializeField, Min(0.5f)] private float _fireRange = 8f;

    [Tooltip("사정권에 더해지는 여유. 이만큼 벌어져야 다시 쫓는다.")]
    [SerializeField, Min(0f)] private float _attackLeaveMargin = 0.6f;

    [Header("조준")]
    [Tooltip("켜면 대상이 아니라 몸(포탑)이 보는 쪽으로 쏜다. 포탑이 느리게 돌면 그 사이가 피할 틈이 된다. " +
             "끄면 몸이 덜 돌았어도 대상 쪽으로 나간다(메카).")]
    [SerializeField] private bool _aimFollowsFacing;

    [Tooltip("보는 쪽과 대상 사이가 이 각도 안일 때만 방아쇠를 당긴다(도). 조준이 몸을 따를 때만 쓴다.")]
    [SerializeField, Range(0f, 180f)] private float _fireAngleTolerance = 8f;

    [Header("공격 중 이동")]
    [Tooltip("서서 쏜다 / 대상 주위를 돌며 쏜다 / 자리 잡고 노리다 쏘고 나면 옮긴다(사격 드론).")]
    [FormerlySerializedAs("_strafe")]
    [SerializeField] private EnemyAttackMove _attackMove = EnemyAttackMove.Stand;

    [Tooltip("대상과 유지하는 거리 = 사정권 × 이 비율. 돌기·옮기기에 쓴다.")]
    [SerializeField, Range(0.3f, 1f)] private float _strafeDistance = 0.75f;

    [Tooltip("도는 방향을 바꾸는 간격(초, 최소~최대). 한 방향으로만 돌면 읽히기 쉽다.")]
    [SerializeField] private Vector2 _strafeFlipInterval = new(2.5f, 5f);

    [Tooltip("옮길 때 대상 둘레로 도는 각도(도, 최소~최대). 방향은 매번 굴린다.")]
    [SerializeField] private Vector2 _repositionTurn = new(35f, 80f);

    [Tooltip("편대 동료와 공격 자리 사이에 두는 최소 거리(유닛). 같은 자리로 몰려 겹치지 않게 한다.")]
    [SerializeField, Min(0f)] private float _spotSpacing = 2.2f;

    [Header("편대 (무리로 나올 때)")]
    [Tooltip("0보다 크면 무리로 스폰될 때 편대를 짠다 — 대장이 순찰하고 나머지가 V자 대열을 따른다. 대열 간격(유닛). " +
             "간격 유지(EnemyMoveController) 반경보다 넓어야 자리에서 밀려나지 않는다.")]
    [SerializeField, Min(0f)] private float _formationSpacing;

    [Tooltip("산개할 때 동료끼리 대상 둘레에서 벌리는 각도(도).")]
    [SerializeField, Min(0f)] private float _scatterStep = 40f;

    [Tooltip("대장이 순찰하는 반경(집 기준, 유닛).")]
    [SerializeField, Min(0f)] private float _patrolRadius = 6f;

    [Tooltip("순찰 속도 배율. 따라오는 쪽이 따라잡을 여유가 있어야 대열이 선다.")]
    [SerializeField, Range(0.1f, 1f)] private float _patrolSpeed = 0.5f;

    [Tooltip("순찰 지점에서 쉬는 시간(초, 최소~최대).")]
    [SerializeField] private Vector2 _patrolPause = new(1f, 2.5f);

    [Header("공격")]
    [Tooltip("판정이 나가는 자리. 보통 몸 앞쪽에 둔 CircleCaster다.")]
    [SerializeField] private CasterBase _attackCaster;

    [Tooltip("휘두르기 시작해서 판정이 나갈 때까지. 플레이어가 빠져나갈 구간이다. " +
             "0으로 두면 붙는 순간 무조건 맞는 적이 된다.")]
    [SerializeField, Min(0f)] private float _attackWindup = 0.35f;

    [Tooltip("때린 뒤 다음 공격까지.")]
    [SerializeField, Min(0.05f)] private float _attackCooltime = 1.2f;

    /// <summary>휘두르기 시작했다. 선딜 연출이 여기에 붙는다. (M3)</summary>
    public event Action OnWindupEvent;

    /// <summary>판정이 실제로 나갔다.</summary>
    public event Action OnAttackEvent;

    private Agent _owner;
    private TargetBase _target;

    private readonly Dictionary<EnemyStateType, EnemyState> _states = new();
    private EnemyState _current;

    // 메카 적은 팔에 무기를 달고 쏜다. 비메카(짐승·드론)는 없으므로 null일 수 있다.
    private EnemyHandController _hands;

    // 단일 기체(터렛·드론)는 팔 대신 무기 하나를 몸에 단다. 둘 다 있으면 단일 기체 무기가 우선한다.
    private UnitWeapon _weapon;

    public EnemyMoveController Move { get; private set; }

    private float _strafeSign = 1f;
    private float _strafeFlipAt;

    /// <summary>사정권 안에서 어떻게 움직이나.</summary>
    public EnemyAttackMove AttackMove => _attackMove;

    /// <summary>쏘고 나서 옮겨 갈 때 대상 둘레로 도는 각도(도). 방향까지 굴린다 — 판정에 영향을 주므로 Combat 소스.</summary>
    public float RollRepositionTurn()
    {
        float sign = GameRandom.Combat.Value < 0.5f ? -1f : 1f;
        return sign * GameRandom.Combat.Range(_repositionTurn.x, Mathf.Max(_repositionTurn.x, _repositionTurn.y));
    }

    public float SpotSpacing => _spotSpacing;

    /// <summary>대상과 유지하는 거리.</summary>
    public float HoldDistance => AttackRange * _strafeDistance;

    #region 편대

    /// <summary>속한 무리. 혼자면 null.</summary>
    public EnemySquad Squad { get; private set; }

    /// <summary>무리로 스폰될 때 편대를 짜는가.</summary>
    public bool UsesFormation => _formationSpacing > 0f;

    public float FormationSpacing => _formationSpacing;
    public float ScatterStep => _scatterStep;
    public float PatrolRadius => _patrolRadius;
    public float PatrolSpeed => _patrolSpeed;

    public float RollPatrolPause() => GameRandom.Combat.Range(_patrolPause.x, Mathf.Max(_patrolPause.x, _patrolPause.y));

    /// <summary>무리가 묶을 때 부른다(<see cref="EnemySquad"/> 생성자).</summary>
    public void JoinSquad(EnemySquad squad) => Squad = squad;

    #endregion

    /// <summary>죽었거나 사라졌다. 무리가 대장·자리를 셀 때 본다.</summary>
    public bool IsDead => _owner == null || _owner.IsDead;

    /// <summary>노리는 대상. 유도탄이 쫓는다.</summary>
    public TargetBase AimTarget => HasTarget ? _target : null;

    /// <summary>무기가 지금 무엇을 하는가. 없으면 Idle.</summary>
    public UnitWeaponPhase WeaponPhase => _weapon != null ? _weapon.Phase : UnitWeaponPhase.Idle;

    /// <summary>
    /// 대상 주위를 도는 방향. 접선 + 거리 보정. 방향은 가끔 뒤집는다(시드 난수 — 판정에 영향을 주므로 Combat 소스).
    /// </summary>
    public Vector2 StrafeDirection
    {
        get
        {
            if (Time.time >= _strafeFlipAt)
            {
                _strafeSign = GameRandom.Combat.Value < 0.5f ? -1f : 1f;
                _strafeFlipAt = Time.time + GameRandom.Combat.Range(_strafeFlipInterval.x, Mathf.Max(_strafeFlipInterval.x, _strafeFlipInterval.y));
            }

            Vector2 to = DirectionToTarget;
            Vector2 tangent = new Vector2(-to.y, to.x) * _strafeSign;

            float desired = AttackRange * _strafeDistance;
            float error = DistanceToTarget - desired;
            Vector2 radial = to * Mathf.Clamp(error / Mathf.Max(0.5f, desired * 0.3f), -1f, 1f);

            return (tangent + radial).normalized;
        }
    }

    public EnemyStateType CurrentState { get; private set; } = EnemyStateType.Idle;

    /// <summary>사정권보다 좁으면 쏠 수 있는 거리에서 못 알아챈다. 무기 사거리가 데이터에서 오므로 여기서 맞춘다.</summary>
    public float DetectRange => Mathf.Max(_detectRange, AttackRange);
    public float LoseRange => DetectRange + _loseMargin;

    /// <summary>
    /// 멈춰서 공격하기 시작하는 거리. <b>무장을 달았으면 사격 거리, 없으면 근접 사정권이다.</b>
    /// 총을 든 기체가 주먹 거리까지 붙을 이유가 없다.
    /// 단일 기체 무기는 무기의 사거리를 그대로 쓴다 — 화면에 그린 범위와 실제로 쏘는 거리가 달라지면 안 된다.
    /// </summary>
    public float AttackRange => _weapon != null ? _weapon.Range : IsArmed ? _fireRange : _attackRange;

    public float AttackLeaveRange => AttackRange + _attackLeaveMargin;
    public float AttackWindup => _attackWindup;
    public float AttackCooltime => _attackCooltime;

    /// <summary>팔에 무기가 달려 있는가. 달려 있으면 때리지 않고 쏜다.</summary>
    public bool IsArmed => _weapon != null || (_hands != null && _hands.HasAnyHand);

    /// <summary>단일 기체 무기. 없으면 null.</summary>
    public UnitWeapon Weapon => _weapon;

    /// <summary>정밀 사격이 조준을 고정했다. 이 동안은 돌지 않고, 상태도 벗어나지 않는다 — 예고한 한 발은 나가야 한다.</summary>
    public bool IsAimLocked => _weapon != null && _weapon.IsAimLocked;

    /// <summary>지금 대상을 맞힐 수 있는 무기인가. 대공포는 걷는 기체를, 지상포는 활공 중인 기체를 쏘지 않는다.</summary>
    public bool CanHitTarget => _weapon == null || _weapon.CanHit(_target);

    /// <summary>
    /// 무기가 탄을 보낼 방향. 기체가 아직 다 돌지 않았어도 겨눈 곳으로 나간다 —
    /// 표현(회전)이 판정을 끌고 가면 "보이는 곳과 맞는 곳이 다른" 문제가 생긴다.
    /// </summary>
    public Vector2 AimDirection
    {
        get
        {
            if (AimsWithFacing) return Move.FacingDirection;

            Vector2 direction = DirectionToTarget;

            return direction.sqrMagnitude > 0.0001f ? direction : (Vector2)transform.right;
        }
    }

    /// <summary>몸이 보는 쪽으로 쏘는가. 돌릴 몸이 없으면 대상 쪽으로 쏜다 — 안 그러면 영영 0°로 쏜다.</summary>
    private bool AimsWithFacing => _aimFollowsFacing && Move != null && Move.CanTurn;

    /// <summary>
    /// 지금 쏴도 대상 쪽인가. 몸을 따라 쏘는 적은 포탑이 대상 쪽으로 다 돌기 전에는 당기지 않는다 —
    /// 엉뚱한 쪽으로 계속 쏘면 탄막이 "피할 틈"이 아니라 그냥 소음이 된다.
    /// </summary>
    public bool IsAimedAtTarget
        => !AimsWithFacing || Vector2.Angle(Move.FacingDirection, DirectionToTarget) <= _fireAngleTolerance;

    /// <summary>노릴 대상이 있는가. 죽었거나 무적이면 false가 된다.</summary>
    public bool HasTarget => _target != null && _target.IsTargetable;

    public Vector2 TargetPosition => _target != null ? _target.AimPosition : (Vector2)transform.position;

    public Vector2 DirectionToTarget
    {
        get
        {
            Vector2 delta = TargetPosition - (Vector2)transform.position;

            return delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.zero;
        }
    }

    public float DistanceToTarget => Vector2.Distance(transform.position, TargetPosition);

    public void Initialize(Agent owner)
    {
        _owner = owner;
    }

    public void AfterInitialize()
    {
        Move = _owner.GetCompo<EnemyMoveController>();

        if (Move == null)
        {
            Debug.LogError("[EnemyAI] EnemyMoveController가 없습니다. 움직이지 않습니다.", this);
            enabled = false;
            return;
        }

        _hands = _owner.GetCompo<EnemyHandController>();
        _weapon = _owner.GetCompo<UnitWeapon>();

        if (_attackCaster == null)
            _attackCaster = GetComponentInChildren<CasterBase>(true);

        // 자폭 드론의 폭발 캐스터를 근접 공격으로 집으면 붙을 때마다 터진다. 폭발은 UnitDetonator만 부른다.
        UnitDetonator detonator = _owner.GetCompo<UnitDetonator>();
        if (detonator != null && _attackCaster == detonator.Explosion)
            _attackCaster = null;

        // 무장도 캐스터도 없으면 영영 못 때린다. 둘 중 하나는 있어야 한다. 자폭 드론은 몸이 무기다.
        if (_attackCaster == null && _hands == null && _weapon == null && detonator == null)
            Debug.LogWarning($"[EnemyAI] {name}에 무장도 공격 캐스터도 없습니다. 쫓아오기만 합니다.", this);

        _states[EnemyStateType.Idle] = new EnemyIdleState(this);
        _states[EnemyStateType.Chase] = new EnemyChaseState(this);
        _states[EnemyStateType.Attack] = new EnemyAttackState(this);

        AcquireTarget();

        _current = _states[EnemyStateType.Idle];
        _current.Enter();
    }

    public void Dispose()
    {
        _current?.Exit();
        _current = null;
        _target = null;
    }

    private void Update()
    {
        // 죽은 뒤에도 Update가 한두 프레임 더 돈다. 시체가 마지막 한 대를 때리면 안 된다.
        if (_owner == null || _owner.IsDead)
        {
            // 연사 무기는 트리거를 들고 있는 동안 스스로 발사를 이어간다.
            // 놓아주지 않으면 파괴되기까지의 한두 프레임 동안 계속 쏜다.
            CancelFiring();
            return;
        }

        if (!HasTarget)
            AcquireTarget();

        _current?.Tick(Time.deltaTime);
    }

    public void ChangeState(EnemyStateType next)
    {
        if (CurrentState == next || !_states.TryGetValue(next, out EnemyState state)) return;

        _current?.Exit();

        CurrentState = next;
        _current = state;

        _current.Enter();
    }

    /// <summary>
    /// 노릴 대상을 고른다. 지금은 플레이어 하나뿐이다.
    ///
    /// 씬에서 직접 찾는 것은 <b>임시</b>다. 스폰 흐름이 생기면(M2) 스포너가 넣어주는 쪽으로
    /// 바꾼다 — 멀티에서는 "플레이어 하나"라는 전제가 깨진다.
    /// </summary>
    private void AcquireTarget()
    {
        if (_target != null && _target.IsTargetable) return;

        Player player = PlayerPilot.LocalMecha;

        _target = player != null ? player.GetCompo<TargetBase>() : null;
    }

    /// <summary>대상을 밖에서 지정한다. 스포너·어그로가 쓴다.</summary>
    public void SetTarget(TargetBase target) => _target = target;

    public void NotifyWindup() => OnWindupEvent?.Invoke();

    /// <summary>
    /// 양팔의 방아쇠. <b>발사 속도는 AI가 재지 않는다</b> — 무기의 쿨타임과 과열이 정한다.
    /// AI가 따로 재면 같은 무기가 플레이어 손에서와 적 손에서 다른 속도로 나간다.
    /// </summary>
    public void SetFiring(bool firing, float deltaTime)
    {
        if (_weapon != null)
        {
            _weapon.Tick(firing && CanHitTarget, deltaTime);
            return;
        }

        if (_hands == null) return;

        _hands.Tick(firing, deltaTime);
    }

    /// <summary>즉시 놓는다. 상태를 벗어날 때. 정밀 사격이 고정까지 갔으면 그 한 발은 나간다.</summary>
    public void StopFiring()
    {
        _weapon?.Stop(false);
        _hands?.StopFiring();
    }

    /// <summary>죽었을 때. 예고 중이던 것도 내보내지 않는다.</summary>
    private void CancelFiring()
    {
        _weapon?.Stop(true);
        _hands?.StopFiring();
    }

    /// <summary>
    /// 판정을 내보낸다. 위력은 캐스터가 <see cref="Agent.AgentStatus"/>에서 읽으므로
    /// 여기서 데미지를 계산하지 않는다 — 계산이 두 곳에 생기면 반드시 갈라진다.
    /// </summary>
    public void PerformAttack()
    {
        if (_attackCaster == null) return;

        _attackCaster.Cast();

        OnAttackEvent?.Invoke();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, _detectRange);

        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, _attackRange);
    }

    private void OnValidate()
    {
        // 사정권이 탐지 거리보다 멀면 영영 쫓지 않고 그 자리에서 때리기만 한다.
        float longest = Mathf.Max(_attackRange, _fireRange);

        if (longest > _detectRange)
            _detectRange = longest;
    }
#endif
}
