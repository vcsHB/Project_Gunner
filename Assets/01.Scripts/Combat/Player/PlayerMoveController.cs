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
///
/// <b>활공 중에 죽으면 추락한다</b>(<see cref="IDeathCrash"/>). 조종을 잃은 채 그때 속도로 미끄러지듯 날아가며 고도를 잃고,
/// 땅에 닿는 순간 부서진다(폭발·잔해·새 판 카운트다운). 착지 불가 지역 위에서 닿으면 떨어진 것으로 친다(<see cref="OnFellEvent"/>).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireAgentComponent(typeof(AgentStatus))]
[RequireAgentComponent(typeof(AgentConditionEffector), optional: true)]
[RequireAgentComponent(typeof(VitalStatus), optional: true)]
[RequireAgentComponent(typeof(TargetBase), optional: true)]
public class PlayerMoveController : MonoBehaviour, IAgentComponent, IDeathCrash
{
    public event Action OnCrashStartedEvent;
    public event Action OnCrashImpactEvent;

    /// <summary>모드가 바뀌었다. (이전, 다음)</summary>
    public event Action<MechaMoveMode, MechaMoveMode> OnMoveModeChangedEvent;

    /// <summary>빠르게 착륙해 미끄러지기 시작했다. 인자는 착륙 순간 속도 ÷ 착륙 상태 최고 속도(1 초과).</summary>
    public event Action<float> OnSkidStartedEvent;

    /// <summary>착지 불가 지역 위에서 Space로 착륙하려 했다. 막혔다.</summary>
    public event Action OnLandingRejectedEvent;

    /// <summary>착지 불가 지역에 떨어졌다(강제 착륙, 또는 지상에서 미끄러져 들어감). 바로 뒤에 사망 이벤트가 온다.</summary>
    public event Action OnFellEvent;

    /// <summary>지상에서 착지 불가 지역에 미끄러져 들어가, 연료가 있어 자동으로 떴다. 착륙 잠금을 무시한다.</summary>
    public event Action OnAutoGlideEvent;

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

    [Tooltip("활공 고도(월드 유닛). 몸체가 화면 위로 이만큼 뜬다. 판정·총구도 같이 뜨므로 크게 잡으면 그림자(실제 위치)와 멀어진다.")]
    [SerializeField, Min(0f)] private float _glideHeight = 0.6f;

    [Tooltip("떠오르거나 내려앉는 데 걸리는 시간(초).")]
    [SerializeField, Min(0.01f)] private float _liftDuration = 0.3f;

    [Header("미끄러짐 (고속 착륙)")]
    [Tooltip("착륙 순간 속도가 착륙 상태 최고 속도의 이 배수를 넘으면 미끄러진다.")]
    [SerializeField, Min(1f)] private float _skidSpeedRatio = 1.05f;

    [Tooltip("미끄러지는 동안의 감속(유닛/초²). 작을수록 오래 미끄러진다.")]
    [SerializeField, Min(0.1f)] private float _skidDeceleration = 6f;

    [Tooltip("미끄러지는 동안 진행 방향을 꺾을 수 있는 각속도(도/초). 통제가 어렵도록 작게.")]
    [SerializeField, Min(0f)] private float _skidTurnRate = 30f;

    [Tooltip("속도가 착륙 상태 최고 속도의 이 비율까지 떨어지면 미끄러짐이 끝난다.")]
    [SerializeField, Range(0f, 1f)] private float _skidEndRatio = 0.6f;

    [Header("착지 불가 지역")]
    [Tooltip("걸을 때 가장자리에서 이만큼 앞에서 멈춘다(유닛). 몸 반지름 정도. 미끄러짐에는 안 걸린다.")]
    [SerializeField, Min(0f)] private float _edgeMargin = 0.3f;

    [Header("추락 (활공 중 사망)")]
    [Tooltip("고도를 다 잃는 데 걸리는 시간(초). 그동안 미끄러지듯 날아간다.")]
    [SerializeField, Min(0.1f)] private float _crashFallDuration = 1.1f;

    [Tooltip("추락하는 동안의 감속(유닛/초²). 작을수록 멀리 간다.")]
    [SerializeField, Min(0f)] private float _crashDeceleration = 3f;

    [Tooltip("추락하는 동안 진행 방향이 흔들리는 정도(도/초). 통제를 잃은 느낌. 연출이라 시드를 쓰지 않는다.")]
    [SerializeField, Min(0f)] private float _crashWobble = 25f;

    [Header("추진체 파괴")]
    [Tooltip("추진체가 부서지면 이동속도에 더해지는 비율. -0.4면 40% 느려진다. 활공·부스터도 막힌다.")]
    [SerializeField, Range(-0.9f, 0f)] private float _brokenBoosterSpeed = -0.4f;

    private Rigidbody2D _rigidbody;
    private Player _player;
    private AgentStatus _status;
    private AgentConditionEffector _conditions;
    private VitalStatus _vitals;
    private TargetBase _target;
    private MechaPartHealth _parts;

    private Vector2 _moveInput;

    // 누르고 있는지(_sprintHeld)와 실제로 질주 중인지(_sprintActive)는 다르다.
    // 연료가 바닥나면 누르고 있어도 부스터가 안 켜진다.
    private bool _sprintHeld;
    private bool _sprintActive;

    // modifier origin은 참조 동일성으로 비교된다. 부스터(this)와 활공을 따로 걷어내야 해서 분리한다.
    private readonly object _glideOrigin = new();

    // 추진체 파괴 감속. 부스터·활공과 따로 걷어내야 한다.
    private readonly object _brokenBoosterOrigin = new();

    private MechaMoveMode _mode = MechaMoveMode.Landed;
    private bool _skidding;
    private bool _crashing;
    private Action _onCrashImpact;
    private float _glideLockTimer;
    private float _liftProgress;

    public bool IsSprinting => _sprintActive;
    public MechaMoveMode Mode => _mode;
    public bool IsGliding => _mode == MechaMoveMode.Gliding;
    public bool IsSkidding => _skidding;
    public bool IsCrashing => _crashing;

    /// <summary>지금 Space를 누르면 뜰 수 있는지. UI가 잠금을 보여줄 때 쓴다.</summary>
    public bool CanStartGlide => !IsGliding && _glideLockTimer <= 0f && CanMove && HasFuel && !IsBoosterBroken;

    /// <summary>착륙 직후 다시 뜰 수 없는 남은 시간(초). HUD가 잠금을 보여줄 때 쓴다.</summary>
    public float GlideLockRemaining => Mathf.Max(0f, _glideLockTimer);

    /// <summary>잠금 전체 길이. 남은 시간을 비율로 그릴 때 쓴다.</summary>
    public float GlideLockDuration => _glideLockAfterLanding;

    /// <summary>연료가 바닥났는가. 활공·부스터가 막힌다.</summary>
    public bool IsOutOfFuel => !HasFuel;

    /// <summary>지금 실제 위치(그림자)가 착지 불가 지역 위인가. 활공 중에만 의미가 있다.</summary>
    public bool IsOverNoLandingZone => NoLandingZone.Contains(transform.position);

    /// <summary>추진체가 부서졌다. 활공·부스터가 막히고 느려진다.</summary>
    public bool IsBoosterBroken => _parts != null && _parts.IsDestroyed(EquipSlotType.Booster);

    public Vector2 Velocity => _rigidbody.linearVelocity;

    /// <summary>
    /// 지금 고도(월드 유닛). 활공하면 0 → <see cref="_glideHeight"/>로 부드럽게 오르고, 착륙하면 내려온다.
    ///
    /// <b>로직이 가진 값이다.</b> 몸체(비주얼·히트박스·총구·실드 원)가 이만큼 화면 위로 뜨고,
    /// 조준 원점도 같이 뜬다. 연출(<see cref="GlideFeedback"/>)과 조준이 <b>같은 값</b>을 읽어야 보이는 총구와 탄이 어긋나지 않는다.
    /// 실제 위치(이동·벽·줍기)는 그림자 쪽, 즉 이 오브젝트의 위치 그대로다.
    /// </summary>
    public float Altitude => Mathf.SmoothStep(0f, 1f, _liftProgress) * _glideHeight;

    /// <summary>0(착륙) ~ 1(완전히 뜸). 연출의 크기·그림자·카메라가 쓴다.</summary>
    public float Altitude01 => Mathf.SmoothStep(0f, 1f, _liftProgress);
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
        _parts = _player.GetCompo<MechaPartHealth>();

        if (_parts != null)
        {
            _parts.OnPartDestroyedEvent += HandlePartDestroyed;
            _parts.OnPartRestoredEvent += HandlePartRestored;
        }

        _player.OnInputChangedEvent += HandleInputChanged;
        HandleInputChanged(null, _player.Input);
    }

    public void Dispose()
    {
        if (_player != null)
        {
            _player.OnInputChangedEvent -= HandleInputChanged;
            HandleInputChanged(_player.Input, null);
        }

        if (_parts != null)
        {
            _parts.OnPartDestroyedEvent -= HandlePartDestroyed;
            _parts.OnPartRestoredEvent -= HandlePartRestored;
        }

        // 부스터·활공·파괴 감속 modifier를 걸어둔 채로 사라지면 안 된다.
        if (_status != null)
        {
            _status.MoveSpeed.RemoveModifier(this);
            _status.MoveSpeed.RemoveModifier(_glideOrigin);
            _status.MoveSpeed.RemoveModifier(_brokenBoosterOrigin);
        }
    }

    /// <summary>추진체가 부서지면 내려앉고 느려진다. 빠른 채였으면 그대로 미끄러진다.</summary>
    private void HandlePartDestroyed(EquipSlotType slot)
    {
        if (slot != EquipSlotType.Booster) return;

        if (IsGliding && !_crashing) ForceLand();
        SetSprintActive(false);

        if (_status != null)
        {
            _status.MoveSpeed.RemoveModifier(_brokenBoosterOrigin);
            _status.MoveSpeed.AddModifier(_brokenBoosterOrigin, _brokenBoosterSpeed, ModifierMode.Percent);
        }
    }

    private void HandlePartRestored(EquipSlotType slot)
    {
        if (slot != EquipSlotType.Booster) return;

        _status?.MoveSpeed.RemoveModifier(_brokenBoosterOrigin);
    }

    // 고도는 보이는 값이라 렌더 프레임마다 움직인다. FixedUpdate에서 올리면 몸체가 계단처럼 뜬다.
    private void Update()
    {
        if (_crashing)
        {
            // 떠 있던 높이에서 천천히 잃는다. 다 잃으면 땅에 닿은 것.
            _liftProgress = Mathf.MoveTowards(_liftProgress, 0f, Time.deltaTime / _crashFallDuration);
            if (_liftProgress <= 0f) CrashImpact();
            return;
        }

        float target = IsGliding ? 1f : 0f;
        _liftProgress = Mathf.MoveTowards(_liftProgress, target, Time.deltaTime / _liftDuration);
    }

    private void FixedUpdate()
    {
        float deltaTime = Time.fixedDeltaTime;

        if (_glideLockTimer > 0f)
            _glideLockTimer -= deltaTime;

        if (_crashing)
        {
            TickCrash(deltaTime);
            return;
        }

        UpdateGlideFuel(deltaTime);
        UpdateSprint(deltaTime);
        CheckGroundInNoLandingZone();

        if (_skidding)
        {
            TickSkid(deltaTime);
            return;
        }

        Vector2 target = CanMove ? _moveInput.normalized * CurrentSpeed : Vector2.zero;

        Vector2 velocity = Vector2.MoveTowards(_rigidbody.linearVelocity, target, _acceleration * deltaTime);

        // 걸어서는 착지 불가 지역에 못 들어간다. 감속 중인 속도까지 막아야 가장자리에서 멈춘다.
        if (!IsGliding)
            velocity = NoLandingZone.ClampWalk(_rigidbody.position, velocity, deltaTime, _edgeMargin);

        _rigidbody.linearVelocity = velocity;
    }

    /// <summary>
    /// 지상 상태로 착지 불가 지역 안에 있다 — 미끄러져 들어왔거나 밀려 들어왔다.
    /// 뜰 수 있으면(연료·추진체·행동 가능) <b>착륙 잠금을 무시하고</b> 자동으로 뜬다. 못 뜨면 추락한다.
    /// </summary>
    private void CheckGroundInNoLandingZone()
    {
        if (IsGliding || _player.IsDead) return;
        if (!NoLandingZone.Contains(_rigidbody.position)) return;

        if (CanMove && HasFuel && !IsBoosterBroken)
        {
            StartGlide();
            OnAutoGlideEvent?.Invoke();
            return;
        }

        Fall();
    }

    private float CurrentSpeed => _status != null ? _status.MoveSpeed.TotalValue : 0f;

    /// <summary>탔다·내렸다. 내린 기체는 쥐고 있던 입력을 놓는다 — 안 그러면 마지막 방향으로 계속 간다.</summary>
    private void HandleInputChanged(PlayerInput previous, PlayerInput current)
    {
        if (previous != null)
        {
            previous.OnMoveEvent -= HandleMove;
            previous.OnSprintEvent -= HandleSprint;
            previous.OnGlideToggleEvent -= HandleGlideToggle;
        }

        _moveInput = Vector2.zero;
        _sprintHeld = false;

        if (current == null) return;

        current.OnMoveEvent += HandleMove;
        current.OnSprintEvent += HandleSprint;
        current.OnGlideToggleEvent += HandleGlideToggle;
    }

    private void HandleMove(Vector2 input) => _moveInput = input;

    // 입력은 눌림 여부만 기록한다. 실제로 부스터가 켜지는지는 연료를 보고 FixedUpdate에서 정한다.
    private void HandleSprint(bool value) => _sprintHeld = value;

    private void HandleGlideToggle()
    {
        // 죽은 뒤(추락 중 포함)에는 내려앉지도 뜨지도 않는다.
        if (_player.IsDead) return;

        if (IsGliding)
        {
            // 착지 불가 지역 위에서는 내려앉지 않는다. 눌렀다는 사실만 알린다(UI·경고음).
            if (IsOverNoLandingZone)
            {
                OnLandingRejectedEvent?.Invoke();
                return;
            }

            Land();
        }
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
        bool wanted = _sprintHeld && CanMove && !_skidding && !IsBoosterBroken && _moveInput.sqrMagnitude > 0.01f;

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

    /// <summary>
    /// 원해서가 아니라 <b>어쩔 수 없이</b> 내려앉는다(연료 고갈·추진체 파괴). 착지 불가 지역 위였으면 <b>추락해 죽는다.</b>
    /// </summary>
    private void ForceLand()
    {
        bool overNoLanding = IsOverNoLandingZone;

        Land();

        if (overNoLanding) Fall();
    }

    #region 추락

    /// <summary>
    /// 죽는 순간 <see cref="Health"/>가 묻는다. 떠 있으면(완전히 내려앉기 전이면) 추락을 시작한다.
    /// 막 이륙하던 중이어도 몸이 떠 있으면 추락이다 — 고도가 있는 만큼만 떨어진다.
    /// </summary>
    public bool TryBeginCrash(Action onImpact)
    {
        if (_crashing) return true;
        if (!IsGliding && _liftProgress <= 0f) return false;

        _crashing = true;
        _onCrashImpact = onImpact;
        _skidding = false;
        SetSprintActive(false);

        OnCrashStartedEvent?.Invoke();
        return true;
    }

    /// <summary>조종 없이 진행 방향으로 천천히 감속하며, 방향이 조금씩 흔들린다.</summary>
    private void TickCrash(float deltaTime)
    {
        Vector2 velocity = _rigidbody.linearVelocity;
        float speed = Mathf.Max(0f, velocity.magnitude - _crashDeceleration * deltaTime);

        if (speed <= 0.01f)
        {
            _rigidbody.linearVelocity = Vector2.zero;
            return;
        }

        float wobble = (Mathf.PerlinNoise(Time.time * 2f, 0.37f) - 0.5f) * 2f * _crashWobble * deltaTime;
        _rigidbody.linearVelocity = (Vector2)(Quaternion.Euler(0f, 0f, wobble) * velocity.normalized) * speed;
    }

    /// <summary>땅에 닿았다. 멈추고 착륙 상태로, 착지 불가 지역이면 떨어진 것으로 알린 뒤 부서진다.</summary>
    private void CrashImpact()
    {
        _crashing = false;
        _rigidbody.linearVelocity = Vector2.zero;

        if (_status != null)
            _status.MoveSpeed.RemoveModifier(_glideOrigin);

        SetMode(MechaMoveMode.Landed);

        if (IsOverNoLandingZone)
            OnFellEvent?.Invoke();

        OnCrashImpactEvent?.Invoke();

        Action impact = _onCrashImpact;
        _onCrashImpact = null;
        impact?.Invoke();
    }

    #endregion

    private void Fall()
    {
        if (_player.IsDead) return;

        _skidding = false;
        OnFellEvent?.Invoke();
        _player.AgentHealth?.Kill();
    }

    /// <summary>
    /// 새 판·리스폰. 착륙 상태로, 멈춘 채로, 고도 0으로. 활공·부스터·파괴 감속 modifier를 걷고 판정 분류도 되돌린다.
    /// 미끄러짐·착지 이벤트는 쏘지 않는다 — 되살아난 것이지 내려앉은 게 아니다.
    /// </summary>
    public void ResetMotion()
    {
        // 추락 중에 새 판이 오면 끊는다. 부서짐은 부르지 않는다 — 이미 되살아났다.
        _crashing = false;
        _onCrashImpact = null;
        _skidding = false;
        _glideLockTimer = 0f;
        _liftProgress = 0f;
        _rigidbody.linearVelocity = Vector2.zero;

        SetSprintActive(false);

        if (_status != null)
        {
            _status.MoveSpeed.RemoveModifier(_glideOrigin);
            _status.MoveSpeed.RemoveModifier(_brokenBoosterOrigin);
        }

        SetMode(MechaMoveMode.Landed);
        _target?.ResetTargetType();
    }

    /// <summary>연료가 바닥나면 강제로 내려앉는다. 빠르면 그대로 미끄러진다.</summary>
    private void UpdateGlideFuel(float deltaTime)
    {
        if (!IsGliding || _crashing) return;

        bool keep = !_player.IsDead && !IsBoosterBroken;

        if (keep && _vitals != null)
            keep = _vitals.TryConsume(VitalType.Fuel, _glideFuelPerSecond * deltaTime);

        if (!keep)
            ForceLand();
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
