using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireAgentComponent(typeof(AgentStatus))]
[RequireAgentComponent(typeof(AgentConditionEffector), optional: true)]
[RequireAgentComponent(typeof(VitalStatus), optional: true)]
public class PlayerMoveController : MonoBehaviour, IAgentComponent
{
    [Tooltip("부스터 가동 시 이동속도 배율. 0.4면 +40%.")]
    [SerializeField] private float _sprintBonus = 0.4f;

    [Tooltip("부스터가 초당 소모하는 연료.")]
    [SerializeField] private float _boostFuelPerSecond = 15f;

    [Tooltip("입력이 바뀔 때 속도가 따라붙는 속도. 클수록 즉각적이다.")]
    [SerializeField] private float _acceleration = 40f;

    private Rigidbody2D _rigidbody;
    private Player _player;
    private AgentStatus _status;
    private AgentConditionEffector _conditions;
    private VitalStatus _vitals;

    private Vector2 _moveInput;

    // 누르고 있는지(_sprintHeld)와 실제로 질주 중인지(_sprintActive)는 다르다.
    // 연료가 바닥나면 누르고 있어도 부스터가 안 켜진다.
    private bool _sprintHeld;
    private bool _sprintActive;

    public bool IsSprinting => _sprintActive;

    public Vector2 Velocity => _rigidbody.linearVelocity;
    public bool CanMove => !_player.IsDead && !IsStunned;

    private bool IsStunned => _conditions != null && _conditions.HasCondition(ConditionType.Stun);

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

        if (_player.Input == null) return;

        _player.Input.OnMoveEvent += HandleMove;
        _player.Input.OnSprintEvent += HandleSprint;
    }

    public void Dispose()
    {
        if (_player == null || _player.Input == null) return;

        _player.Input.OnMoveEvent -= HandleMove;
        _player.Input.OnSprintEvent -= HandleSprint;

        // 부스터 modifier를 걸어둔 채로 사라지면 안 된다.
        if (_status != null)
            _status.MoveSpeed.RemoveModifier(this);
    }

    private void FixedUpdate()
    {
        UpdateSprint(Time.fixedDeltaTime);

        Vector2 target = CanMove ? _moveInput.normalized * CurrentSpeed : Vector2.zero;

        _rigidbody.linearVelocity = Vector2.MoveTowards(
            _rigidbody.linearVelocity, target, _acceleration * Time.fixedDeltaTime);
    }

    private float CurrentSpeed => _status != null ? _status.MoveSpeed.TotalValue : 0f;

    private void HandleMove(Vector2 input) => _moveInput = input;

    // 입력은 눌림 여부만 기록한다. 실제로 부스터가 켜지는지는 연료를 보고 FixedUpdate에서 정한다.
    private void HandleSprint(bool value) => _sprintHeld = value;

    /// <summary>
    /// 제자리에서는 연료를 쓰지 않는다. 가만히 서서 Shift를 누르고 있다고 연료가 닳으면 이상하다.
    /// </summary>
    private void UpdateSprint(float deltaTime)
    {
        bool wanted = _sprintHeld && CanMove && _moveInput.sqrMagnitude > 0.01f;

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
}
