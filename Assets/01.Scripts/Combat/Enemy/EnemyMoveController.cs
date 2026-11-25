using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적의 이동. <b>어디로 갈지는 정하지 않는다</b> — <see cref="EnemyAIController"/>가 방향만 넘긴다.
///
/// 플레이어 이동과 같은 방식이다(Rigidbody2D + 가속). 적만 다른 물리를 쓰면
/// 같은 지형·넉백·상태이상에 서로 다르게 반응해서, 한쪽에서만 나는 버그가 생긴다.
///
/// 속도는 <see cref="AgentStatus.MoveSpeed"/>를 읽는다. 둔화·가속이 modifier로 걸리면
/// 여기를 고치지 않고도 그대로 반영된다.
///
/// <b>다리(<see cref="EnemyLocomotion"/>)는 여기서 갈아 끼운다</b> — 걷기 / 비행 / 제자리.
/// 제자리여도 바라보기는 돈다. 터렛의 포탑이 그것이다: <see cref="_visualRoot"/>를 포탑 머리로 두고
/// <see cref="_turnSpeed"/>를 느리게 잡으면 "천천히 돌아서 피할 틈이 있는" 포탑이 된다.
///
/// <b>떠 있는 몸</b>(<see cref="_altitude"/> &gt; 0 — 드론·부표)은 고도를 가진다. 몸을 띄워 그리는 건 <see cref="UnitHoverFeedback"/>.
/// 떠 있다 죽으면 <b>추락한다</b>(<see cref="IDeathCrash"/>) — 그때 속도로 미끄러지며 고도를 잃고 땅에 닿을 때 부서진다. 플레이어 활공 추락과 같은 길.
///
/// <b>간격 유지</b>(<see cref="_separationRadius"/>) — 떼로 다니는 적(드론)이 한 점에 뭉치지 않게 서로 민다. 길찾기가 아니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireAgentComponent(typeof(AgentStatus))]
[RequireAgentComponent(typeof(AgentConditionEffector), optional: true)]
public class EnemyMoveController : MonoBehaviour, IAgentComponent, IDeathCrash
{
    public event Action OnCrashStartedEvent;
    public event Action OnCrashImpactEvent;

    private static readonly List<EnemyMoveController> s_all = new();

    [Tooltip("방향이 바뀔 때 속도가 따라붙는 속도. 클수록 즉각적이다.")]
    [SerializeField, Min(1f)] private float _acceleration = 25f;

    [Tooltip("다리. 걷기는 착지 불가 지역 가장자리에서 멈추고, 비행은 넘고, 제자리는 몸이 안 움직인다(바라보기는 돈다).")]
    [SerializeField] private EnemyLocomotion _locomotion = EnemyLocomotion.Walk;

    [Tooltip("걸을 때 착지 불가 지역 가장자리에서 이만큼 앞에서 멈춘다(유닛).")]
    [SerializeField, Min(0f)] private float _edgeMargin = 0.3f;

    [Header("바라보기")]
    [Tooltip("진행 방향으로 돌릴 오브젝트. 비우면 돌리지 않는다.")]
    [SerializeField] private Transform _visualRoot;

    [Tooltip("리소스가 그려진 정면. 보정은 여기 한 곳에서만 들어간다.")]
    [SerializeField] private FacingType _spriteFacing = FacingType.Up;

    [Tooltip("초당 회전 각도. 포탑이면 느리게 — 이 속도가 곧 피할 틈이다.")]
    [SerializeField, Min(1f)] private float _turnSpeed = 360f;

    [Header("고도 (드론·부표)")]
    [Tooltip("몸이 그림자 위로 떠 있는 높이(월드 유닛). 0이면 땅에 있다. 판정 분류(공중)는 TargetBase가 따로 정한다.")]
    [SerializeField, Min(0f)] private float _altitude;

    [Tooltip("떠 있다 죽으면 추락하는가. 끄면 그 자리에서 바로 부서진다(부표 — 맞으면 바로 터진다).")]
    [SerializeField] private bool _crashOnDeath = true;

    [SerializeField, Min(0.1f)] private float _crashFallDuration = 0.9f;
    [SerializeField, Min(0f)] private float _crashDeceleration = 4f;

    [Header("간격 유지")]
    [Tooltip("이 안의 다른 적을 밀어낸다(유닛). 0이면 끈다. 떼로 다니는 적만.")]
    [SerializeField, Min(0f)] private float _separationRadius;

    [Tooltip("미는 힘. 1이면 이동 입력과 같은 크기까지.")]
    [SerializeField, Min(0f)] private float _separationStrength = 1.2f;

    private Rigidbody2D _rigidbody;
    private Agent _owner;
    private AgentStatus _status;
    private AgentConditionEffector _conditions;

    private Vector2 _moveDirection;

    // 멈춰 있을 때 바라볼 쪽. 공격 자세에서 등을 돌리지 않게 한다.
    private Vector2 _lookDirection;

    // 켜져 있으면 움직이는 중에도 _lookDirection을 본다. 대상 주위를 돌며 쏘는 드론.
    private bool _lookWhileMoving;

    private bool _crashing;
    private float _altitude01 = 1f;
    private Action _onCrashImpact;

    /// <summary>지금 향하고 있는 논리 각도(도). 0° = +X.</summary>
    public float FacingAngle { get; private set; }

    /// <summary>지금 향하고 있는 방향(단위 벡터). 포탑이 보는 쪽으로 쏘는 적이 쓴다.</summary>
    public Vector2 FacingDirection
    {
        get
        {
            float r = FacingAngle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }
    }

    public EnemyLocomotion Locomotion => _locomotion;

    /// <summary>바라보기가 돌 수 있는가. 돌릴 오브젝트가 없으면 <see cref="FacingAngle"/>이 영영 0이다.</summary>
    public bool CanTurn => _visualRoot != null;

    public Vector2 Velocity => _rigidbody.linearVelocity;

    /// <summary>지금 고도(월드 유닛). 추락하면 0으로 내려간다. 연출(<see cref="UnitHoverFeedback"/>)이 읽는다.</summary>
    public float Altitude => _altitude * _altitude01;

    public bool IsCrashing => _crashing;

    private bool IsStunned => _conditions != null && _conditions.HasCondition(ConditionType.Stun);

    /// <summary>죽었거나 기절했으면 못 움직인다. AI가 아니라 여기서 막는다 — 조건이 흩어지면 새는 경로가 생긴다.</summary>
    public bool CanMove => _owner != null && !_owner.IsDead && !IsStunned;

    public void Initialize(Agent owner)
    {
        _owner = owner;
        _rigidbody = GetComponent<Rigidbody2D>();

        // 탑다운이라 중력을 쓰지 않는다.
        _rigidbody.gravityScale = 0f;
        _rigidbody.freezeRotation = true;
    }

    public void AfterInitialize()
    {
        _status = _owner.GetCompo<AgentStatus>();
        _conditions = _owner.GetCompo<AgentConditionEffector>();

        // 처음 바라보는 쪽은 놓인 그대로. 0°에서 시작하면 스폰 직후 포탑이 한 바퀴 돌아 제자리를 찾는다.
        if (_visualRoot != null)
            FacingAngle = _visualRoot.eulerAngles.z - _spriteFacing.ToAngleOffset();
    }

    public void Dispose() { }

    private void OnEnable() => s_all.Add(this);
    private void OnDisable() => s_all.Remove(this);

    /// <summary>이 방향으로 간다. 길이는 무시하고 방향만 본다. 가는 쪽을 바라본다.</summary>
    public void Move(Vector2 direction)
    {
        _moveDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.zero;
        _lookWhileMoving = false;
    }

    /// <summary>선다. 공격 중에도 몸은 계속 대상을 바라본다.</summary>
    public void Stop() => _moveDirection = Vector2.zero;

    /// <summary>
    /// 이쪽을 보게 한다. 공격 자세에서 쓴다 — 멈췄다고 등을 돌린 채 때리면 어디서 맞는지 읽을 수 없다.
    /// <paramref name="whileMoving"/>를 켜면 움직이는 중에도 이쪽을 본다(대상 주위를 돌며 쏘기). <see cref="Move"/>가 끈다.
    /// </summary>
    public void LookAt(Vector2 direction, bool whileMoving = false)
    {
        if (direction.sqrMagnitude <= 0.0001f) return;

        _lookDirection = direction.normalized;
        _lookWhileMoving = whileMoving;
    }

    private void Update()
    {
        if (!_crashing) return;

        _altitude01 = Mathf.MoveTowards(_altitude01, 0f, Time.deltaTime / _crashFallDuration);
        if (_altitude01 <= 0f) CrashImpact();
    }

    private void FixedUpdate()
    {
        float deltaTime = Time.fixedDeltaTime;

        if (_crashing)
        {
            Vector2 v = _rigidbody.linearVelocity;
            float speed = Mathf.Max(0f, v.magnitude - _crashDeceleration * deltaTime);
            _rigidbody.linearVelocity = speed > 0.01f ? v.normalized * speed : Vector2.zero;
            return;
        }

        bool moves = CanMove && _locomotion != EnemyLocomotion.Fixed;

        Vector2 desired = moves ? _moveDirection + Separation() * _separationStrength : Vector2.zero;
        if (desired.sqrMagnitude > 1f) desired.Normalize();

        Vector2 target = desired * CurrentSpeed;

        Vector2 velocity = _locomotion == EnemyLocomotion.Fixed
            ? Vector2.zero
            : Vector2.MoveTowards(_rigidbody.linearVelocity, target, _acceleration * deltaTime);

        // 길찾기가 없어서, 쫓다가 구렁 가장자리에 서서 기다린다.
        if (_locomotion == EnemyLocomotion.Walk)
            velocity = NoLandingZone.ClampWalk(_rigidbody.position, velocity, deltaTime, _edgeMargin);

        _rigidbody.linearVelocity = velocity;

        UpdateFacing(deltaTime);
    }

    private float CurrentSpeed => _status != null ? _status.MoveSpeed.TotalValue : 0f;

    /// <summary>가까운 동료에게서 멀어지는 방향(가까울수록 세다). 같은 다리끼리만 — 걷는 것과 나는 것은 서로 안 막는다.</summary>
    private Vector2 Separation()
    {
        if (_separationRadius <= 0f) return Vector2.zero;

        Vector2 me = _rigidbody.position;
        Vector2 push = Vector2.zero;

        for (int i = 0; i < s_all.Count; i++)
        {
            EnemyMoveController other = s_all[i];
            if (other == this || other._locomotion != _locomotion || other._owner == null || other._owner.IsDead) continue;

            Vector2 delta = me - other._rigidbody.position;
            float distance = delta.magnitude;
            if (distance >= _separationRadius) continue;

            // 겹쳐 있으면 방향이 없다. 순서로 갈라 준다(연출이 아닌 판정이라 Random을 쓰지 않는다).
            Vector2 away = distance > 0.001f ? delta / distance : (GetInstanceID() < other.GetInstanceID() ? Vector2.right : Vector2.left);
            push += away * (1f - distance / _separationRadius);
        }

        return push;
    }

    /// <summary>
    /// 바라보는 방향은 <b>가는 쪽</b>이 기본이고, 멈춰 있으면 마지막으로 지정된 쪽을 본다.
    /// 속도로만 판단하면 공격하려고 멈추는 순간 방향을 잃는다.
    /// </summary>
    private void UpdateFacing(float deltaTime)
    {
        if (_visualRoot == null) return;

        bool useLook = _lookWhileMoving || _moveDirection.sqrMagnitude <= 0.0001f;
        Vector2 direction = useLook ? _lookDirection : _moveDirection;
        if (direction.sqrMagnitude <= 0.0001f) return;

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        FacingAngle = Mathf.MoveTowardsAngle(FacingAngle, targetAngle, _turnSpeed * deltaTime);

        // 리소스 방향 보정은 여기서만 들어간다. 파츠처럼 두 번 걸리면 180° 틀어진다.
        _visualRoot.rotation = _spriteFacing.ToRotation(FacingAngle);
    }

    #region 추락

    public bool TryBeginCrash(Action onImpact)
    {
        if (_crashing) return true;
        if (!_crashOnDeath || Altitude <= 0f) return false;

        _crashing = true;
        _onCrashImpact = onImpact;

        OnCrashStartedEvent?.Invoke();
        return true;
    }

    private void CrashImpact()
    {
        _crashing = false;
        _rigidbody.linearVelocity = Vector2.zero;

        OnCrashImpactEvent?.Invoke();

        Action impact = _onCrashImpact;
        _onCrashImpact = null;
        impact?.Invoke();
    }

    #endregion

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => s_all.Clear();
}
