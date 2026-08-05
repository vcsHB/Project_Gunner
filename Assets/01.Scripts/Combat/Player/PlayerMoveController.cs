using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMoveController : MonoBehaviour, IAgentComponent
{
    [Tooltip("질주 시 이동속도 배율. 0.4면 +40%.")]
    [SerializeField] private float _sprintBonus = 0.4f;

    [Tooltip("입력이 바뀔 때 속도가 따라붙는 속도. 클수록 즉각적이다.")]
    [SerializeField] private float _acceleration = 40f;

    private Rigidbody2D _rigidbody;
    private Player _player;
    private AgentStatus _status;
    private AgentConditionEffector _conditions;

    private Vector2 _moveInput;
    private bool _isSprinting;

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

        if (_player.Input == null) return;

        _player.Input.OnMoveEvent += HandleMove;
        _player.Input.OnSprintEvent += HandleSprint;
    }

    public void Dispose()
    {
        if (_player == null || _player.Input == null) return;

        _player.Input.OnMoveEvent -= HandleMove;
        _player.Input.OnSprintEvent -= HandleSprint;

        // 질주 modifier를 걸어둔 채로 사라지면 안 된다.
        if (_status != null)
            _status.MoveSpeed.RemoveModifier(this);
    }

    private void FixedUpdate()
    {
        Vector2 target = CanMove ? _moveInput.normalized * CurrentSpeed : Vector2.zero;

        _rigidbody.linearVelocity = Vector2.MoveTowards(
            _rigidbody.linearVelocity, target, _acceleration * Time.fixedDeltaTime);
    }

    private float CurrentSpeed => _status != null ? _status.MoveSpeed.TotalValue : 0f;

    private void HandleMove(Vector2 input) => _moveInput = input;

    private void HandleSprint(bool value)
    {
        if (_isSprinting == value) return;

        _isSprinting = value;

        if (_status == null) return;

        // 질주는 다른 버프와 곱해지지 않고 더해지도록 비율 modifier로 건다.
        if (value)
            _status.MoveSpeed.AddModifier(this, _sprintBonus, ModifierMode.Percent);
        else
            _status.MoveSpeed.RemoveModifier(this);
    }
}
