using System;
using UnityEngine;

/// <summary>
/// 기체 이동. 두 모드가 있다 — <b>착륙</b>(기본)과 <b>활공</b>(Space로 전환).
///
/// 활공은 빠르지만 연료를 계속 먹고, 그동안 <see cref="TargetType.Air"/>로 판정된다.
/// Shift 부스터는 두 모드 모두에서 따로 얹힌다.
///
/// 빠른 채로 착륙하면 <b>미끄러진다</b> — 진행 방향으로 천천히 감속하고 조향이 거의 안 먹는다.
/// 착륙 직후 잠시는 다시 뜰 수 없다. 착륙을 공짜 급정거로 쓰지 못하게 하려는 것이다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireAgentComponent(typeof(AgentStatus))]
[RequireAgentComponent(typeof(AgentConditionEffector), optional: true)]
[RequireAgentComponent(typeof(VitalStatus), optional: true)]
[RequireAgentComponent(typeof(TargetBase), optional: true)]
public class PlayerMoveController : MonoBehaviour, IAgentComponent
{
    /// <summary>모드가 바뀌었다. (이전, 다음)</summary>
    public event Action<MechaMoveMode, MechaMoveMode> OnMoveModeChangedEvent;

    /// <summary>빠르게 착륙해 미끄러지기 시작했다. 인자는 착륙 순간 속도 ÷ 착륙 상태 최고 속도(1 초과).</summary>
    public event Action<float> OnSkidStartedEvent;

    [Header("부스터 (Shift)")]
    [Tooltip("부스터 가동 시 이동속도 배율. 0.4면 +40%.")]
    [SerializeField] private float _sprintBonus = 0.4f;

    [Tooltip("부스터가 초당 소모하는 연료.")]
    [SerializeField] private float _boostFuelPerSecond = 15f;

    [Tooltip("입력이 바뀔 때 속도가 따라붙는 속도. 클수록 즉각적이다.")]
    [SerializeField] private float _acceleration = 40f;

    [Header("활공 (Space)")]
    [Tooltip("활공 중 이동속도 배율. 0.5면 +50%. 부스터와 더해진다.")]
    [SerializeField] private float _glideBonus = 0.5f;

    [Tooltip("활공 중 초당 소모하는 연료. 떠 있는 것만으로 먹는다 — 제자리에 있어도.")]
    [SerializeField, Min(0f)] private float _glideFuelPerSecond = 5f;

    [Tooltip("착륙 직후 다시 활공할 수 없는 시간(초).")]
    [SerializeField, Min(0f)] private float _glideLockAfterLanding = 1f;

    [Header("미끄러짐 (고속 착륙)")]
    [Tooltip("착륙 순간 속도가 착륙 상태 최고 속도의 이 배수를 넘으면 미끄러진다.")]
    [SerializeField, Min(1f)] private float _skidSpeedRatio = 1.05f;

    [Tooltip("미끄러지는 동안의 감속(유닛/초²). 작을수록 오래 미끄러진다.")]
    [SerializeField, Min(0.1f)] private float _skidDeceleration = 6f;

    [Tooltip("미끄러지는 동안 진행 방향을 꺾을 수 있는 각속도(도/초). 통제가 어렵도록 작게.")]
    [SerializeField, Min(0f)] private float _skidTurnRate = 30f;

    [Tooltip("속도가 착륙 상태 최고 속도의 이 비율까지 떨어지면 미끄러짐이 끝난다.")]
    [SerializeField, Range(0f, 1f)] private float _skidEndRatio = 0.6f;

    private Rigidbody2D _rigidbody;
    private Player _player;
    private AgentStatus _status;
    private AgentConditionEffector _conditions;
    private VitalStatus _vitals;
    private TargetBase _target;

    private Vector2 _moveInput;

    // 누르고 있는지(_sprintHeld)와 실제로 질주 중인지(_sprintActive)는 다르다.
    // 연료가 바닥나면 누르고 있어도 부스터가 안 켜진다.
    private bool _sprintHeld;
    private bool _sprintActive;

    // modifier origin은 참조 동일성으로 비교된다. 부스터(this)와 활공을 따로 걷어내야 해서 분리한다.
    private readonly object _glideOrigin = new();

    private MechaMoveMode _mode = MechaMoveMode.Landed;
    private bool _skidding;
    private float _glideLockTimer;

    public bool IsSprinting => _sprintActive;
    public MechaMoveMode Mode => _mode;
    public bool IsGliding => _mode == MechaMoveMode.Gliding;
    public bool IsSkidding => _skidding;

    /// <summary>지금 Space를 누르면 뜰 수 있는지. UI가 잠금을 보여줄 때 쓴다.</summary>
    public bool CanStartGlide => !IsGliding && _glideLockTimer <= 0f && CanMove && HasFuel;

    public Vector2 Velocity => _rigidbody.linearVelocity;
    public bool CanMove => !_player.IsDead && !IsStunned;

    private bool IsStunned => _conditions != null && _conditions.HasCondition(ConditionType.Stun);
    private bool HasFuel => _vitals == null || !_vitals.IsFuelEmpty;

    public void Initialize(Agent owner)
    {
        _player = owner as Player;
        _rigidbody = GetComponent<Rigidbody2D>();

        // 탑다운이라 중력을 쓰지 않는다.
        _rigidbody.gravityScale = 0f;
        _rigidbody.freezeRotation = true;
    }

    public void AfterInitialize()
    {
        _status = _player.GetCompo<AgentStatus>();
        _conditions = _player.GetCompo<AgentConditionEffector>();
        _vitals = _player.GetCompo<VitalStatus>();
        _target = _player.GetCompo<TargetBase>();

        if (_player.Input == null) return;

        _player.Input.OnMoveEvent += HandleMove;
        _player.Input.OnSprintEvent += HandleSprint;
        _player.Input.OnGlideToggleEvent += HandleGlideToggle;
    }

    public void Dispose()
    {
        if (_player != null && _player.Input != null)
        {
            _player.Input.OnMoveEvent -= HandleMove;
            _player.Input.OnSprintEvent -= HandleSprint;
            _player.Input.OnGlideToggleEvent -= HandleGlideToggle;
        }

        // 부스터·활공 modifier를 걸어둔 채로 사라지면 안 된다.
        if (_status != null)
        {
            _status.MoveSpeed.RemoveModifier(this);
            _status.MoveSpeed.RemoveModifier(_glideOrigin);
        }
    }

    private void FixedUpdate()
    {
        float deltaTime = Time.fixedDeltaTime;

        if (_glideLockTimer > 0f)
            _glideLockTimer -= deltaTime;

        UpdateGlideFuel(deltaTime);
        UpdateSprint(deltaTime);

        if (_skidding)
        {
            TickSkid(deltaTime);
            return;
        }

        Vector2 target = CanMove ? _moveInput.normalized * CurrentSpeed : Vector2.zero;

        _rigidbody.linearVelocity = Vector2.MoveTowards(
            _rigidbody.linearVelocity, target, _acceleration * deltaTime);
    }

    private float CurrentSpeed => _status != null ? _status.MoveSpeed.TotalValue : 0f;

    private void HandleMove(Vector2 input) => _moveInput = input;

    // 입력은 눌림 여부만 기록한다. 실제로 부스터가 켜지는지는 연료를 보고 FixedUpdate에서 정한다.
    private void HandleSprint(bool value) => _sprintHeld = value;

    private void HandleGlideToggle()
    {
        if (IsGliding)
            Land();
        else if (CanStartGlide)
            StartGlide();
    }

    #region 부스터

    /// <summary>
    /// 제자리에서는 연료를 쓰지 않는다. 가만히 서서 Shift를 누르고 있다고 연료가 닳으면 이상하다.
    /// 미끄러지는 동안도 마찬가지 — 속도가 입력을 안 따르므로 부스터가 의미 없다.
    /// </summary>
    private void UpdateSprint(float deltaTime)
    {
        bool wanted = _sprintHeld && CanMove && !_skidding && _moveInput.sqrMagnitude > 0.01f;

        if (wanted && _vitals != null)
            wanted = _vitals.TryConsume(VitalType.Fuel, _boostFuelPerSecond * deltaTime);

        SetSprintActive(wanted);
    }

    private void SetSprintActive(bool value)
    {
        if (_sprintActive == value) return;

        _sprintActive = value;

        if (_status == null) return;

        // 부스터는 다른 버프와 곱해지지 않고 더해지도록 비율 modifier로 건다.
        if (value)
            _status.MoveSpeed.AddModifier(this, _sprintBonus, ModifierMode.Percent);
        else
            _status.MoveSpeed.RemoveModifier(this);
    }

    #endregion

    #region 활공 / 착륙

    private void StartGlide()
    {
        // 미끄러지던 중에 다시 떴다. 잠금이 풀렸으면 허용한다.
        _skidding = false;

        if (_status != null)
            _status.MoveSpeed.AddModifier(_glideOrigin, _glideBonus, ModifierMode.Percent);

        SetMode(MechaMoveMode.Gliding);
    }

    /// <summary>
    /// 내려앉는다. 착륙 상태의 최고 속도보다 빠르면 미끄러진다.
    /// 최고 속도는 활공 배율을 걷어낸 뒤에 잰다 — 부스터를 켠 채 착륙하면 그만큼 기준도 올라간다.
    /// </summary>
    private void Land()
    {
        if (_status != null)
            _status.MoveSpeed.RemoveModifier(_glideOrigin);

        _glideLockTimer = _glideLockAfterLanding;

        SetMode(MechaMoveMode.Landed);

        float landedMax = Mathf.Max(0.01f, CurrentSpeed);
        float ratio = _rigidbody.linearVelocity.magnitude / landedMax;

        if (ratio > _skidSpeedRatio)
        {
            _skidding = true;
            OnSkidStartedEvent?.Invoke(ratio);
        }
    }

    /// <summary>연료가 바닥나면 강제로 내려앉는다. 빠르면 그대로 미끄러진다.</summary>
    private void UpdateGlideFuel(float deltaTime)
    {
        if (!IsGliding) return;

        bool keep = !_player.IsDead;

        if (keep && _vitals != null)
            keep = _vitals.TryConsume(VitalType.Fuel, _glideFuelPerSecond * deltaTime);

        if (!keep)
            Land();
    }

    /// <summary>
    /// 진행 방향으로 천천히 감속한다. 입력은 방향을 조금 꺾는 데만 쓰인다.
    /// 끝나는 기준은 착륙 최고 속도에 대한 비율이다 — 절대값이면 추진체마다 미끄러지는 길이가 달라진다.
    /// </summary>
    private void TickSkid(float deltaTime)
    {
        Vector2 velocity = _rigidbody.linearVelocity;
        float speed = Mathf.Max(0f, velocity.magnitude - _skidDeceleration * deltaTime);

        if (speed <= CurrentSpeed * _skidEndRatio || speed <= 0.01f)
        {
            _skidding = false;
            _rigidbody.linearVelocity = velocity.normalized * speed;
            return;
        }

        Vector2 direction = velocity.normalized;

        if (CanMove && _moveInput.sqrMagnitude > 0.01f)
        {
            direction = Vector3.RotateTowards(
                direction, _moveInput.normalized, _skidTurnRate * Mathf.Deg2Rad * deltaTime, 0f);
        }

        _rigidbody.linearVelocity = direction * speed;
    }

    /// <summary>
    /// 판정 분류를 같이 바꾼다. 죽어서 <see cref="TargetType.None"/>이 된 뒤에 덮어쓰면 시체가 다시 맞는다.
    /// </summary>
    private void SetMode(MechaMoveMode next)
    {
        if (_mode == next) return;

        MechaMoveMode prev = _mode;
        _mode = next;

        if (_target != null && !_player.IsDead)
        {
            if (next == MechaMoveMode.Gliding)
                _target.SetTargetType(TargetType.Air);
            else
                _target.ResetTargetType();
        }

        OnMoveModeChangedEvent?.Invoke(prev, next);
    }

    #endregion
}
