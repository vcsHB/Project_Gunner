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
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireAgentComponent(typeof(AgentStatus))]
[RequireAgentComponent(typeof(AgentConditionEffector), optional: true)]
public class EnemyMoveController : MonoBehaviour, IAgentComponent
{
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

    private Rigidbody2D _rigidbody;
    private Agent _owner;
    private AgentStatus _status;
    private AgentConditionEffector _conditions;

    private Vector2 _moveDirection;

    // 멈춰 있을 때 바라볼 쪽. 공격 자세에서 등을 돌리지 않게 한다.
    private Vector2 _lookDirection;

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

    /// <summary>이 방향으로 간다. 길이는 무시하고 방향만 본다.</summary>
    public void Move(Vector2 direction)
    {
        _moveDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.zero;
    }

    /// <summary>선다. 공격 중에도 몸은 계속 대상을 바라본다.</summary>
    public void Stop() => _moveDirection = Vector2.zero;

    /// <summary>
    /// 움직이지 않으면서 이쪽을 보게 한다. 공격 자세에서 쓴다 —
    /// 멈췄다고 등을 돌린 채 때리면 어디서 맞는지 읽을 수 없다.
    /// </summary>
    public void LookAt(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f) return;

        _lookDirection = direction.normalized;
    }

    private void FixedUpdate()
    {
        float deltaTime = Time.fixedDeltaTime;

        bool moves = CanMove && _locomotion != EnemyLocomotion.Fixed;
        Vector2 target = moves ? _moveDirection * CurrentSpeed : Vector2.zero;

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

    /// <summary>
    /// 바라보는 방향은 <b>가는 쪽</b>이 기본이고, 멈춰 있으면 마지막으로 지정된 쪽을 본다.
    /// 속도로만 판단하면 공격하려고 멈추는 순간 방향을 잃는다.
    /// </summary>
    private void UpdateFacing(float deltaTime)
    {
        if (_visualRoot == null) return;

        Vector2 direction = _moveDirection.sqrMagnitude > 0.0001f ? _moveDirection : _lookDirection;
        if (direction.sqrMagnitude <= 0.0001f) return;

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        FacingAngle = Mathf.MoveTowardsAngle(FacingAngle, targetAngle, _turnSpeed * deltaTime);

        // 리소스 방향 보정은 여기서만 들어간다. 파츠처럼 두 번 걸리면 180° 틀어진다.
        _visualRoot.rotation = _spriteFacing.ToRotation(FacingAngle);
    }
}
