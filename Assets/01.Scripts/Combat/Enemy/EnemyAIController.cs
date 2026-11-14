using System;
using System.Collections.Generic;
using UnityEngine;

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
public class EnemyAIController : MonoBehaviour, IAgentComponent, IAimDirectionProvider
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

    public EnemyMoveController Move { get; private set; }

    public EnemyStateType CurrentState { get; private set; } = EnemyStateType.Idle;

    public float DetectRange => _detectRange;
    public float LoseRange => _detectRange + _loseMargin;

    /// <summary>
    /// 멈춰서 공격하기 시작하는 거리. <b>무장을 달았으면 사격 거리, 없으면 근접 사정권이다.</b>
    /// 총을 든 기체가 주먹 거리까지 붙을 이유가 없다.
    /// </summary>
    public float AttackRange => IsArmed ? _fireRange : _attackRange;

    public float AttackLeaveRange => AttackRange + _attackLeaveMargin;
    public float AttackWindup => _attackWindup;
    public float AttackCooltime => _attackCooltime;

    /// <summary>팔에 무기가 달려 있는가. 달려 있으면 때리지 않고 쏜다.</summary>
    public bool IsArmed => _hands != null && _hands.HasAnyHand;

    /// <summary>
    /// 무기가 탄을 보낼 방향. 기체가 아직 다 돌지 않았어도 겨눈 곳으로 나간다 —
    /// 표현(회전)이 판정을 끌고 가면 "보이는 곳과 맞는 곳이 다른" 문제가 생긴다.
    /// </summary>
    public Vector2 AimDirection
    {
        get
        {
            Vector2 direction = DirectionToTarget;

            return direction.sqrMagnitude > 0.0001f ? direction : (Vector2)transform.right;
        }
    }

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

        if (_attackCaster == null)
            _attackCaster = GetComponentInChildren<CasterBase>(true);

        // 무장도 캐스터도 없으면 영영 못 때린다. 둘 중 하나는 있어야 한다.
        if (_attackCaster == null && _hands == null)
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
            StopFiring();
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

        Player player = FindAnyObjectByType<Player>();

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
        if (_hands == null) return;

        _hands.Tick(firing, deltaTime);
    }

    /// <summary>즉시 놓는다. 죽거나 상태를 벗어날 때.</summary>
    public void StopFiring() => _hands?.StopFiring();

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
