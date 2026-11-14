using System;
using UnityEngine;

/// <summary>
/// 실드. 피해를 체력보다 먼저 받는다. 최대값·회복 대기·회복 속도는 <see cref="AgentStatus"/>가(코어가 Deltas로 준다),
/// 현재값은 여기가 들고 간다 — <see cref="VitalStatus"/>와 같은 구조다.
///
/// <b>실드가 조금이라도 남아 있으면 그 타격은 실드에서 끝난다.</b> 넘친 피해가 체력으로 넘어가지 않는다.
/// 실드는 방어력·저항을 타지 않는다 — 장갑은 기체 껍데기의 수치다.
///
/// 맞을 때마다(실드가 0이어도) 회복 대기 타이머가 처음부터 다시 돈다.
/// 대기가 끝나면 <b>전력을 쓰며</b> 찬다. 전력이 모자라거나 잠기면 회복만 멈추고 타이머는 그대로 둔다 —
/// 맞은 게 아니라 힘이 없는 것이라서, 전력이 돌아오면 바로 이어서 차야 한다.
///
/// 이 컴포넌트가 없거나 최대값이 0이면 실드가 없는 유닛이다(메카가 아닌 적).
/// </summary>
[RequireAgentComponent(typeof(AgentStatus))]
[RequireAgentComponent(typeof(VitalStatus), optional: true)]
public class ShieldStatus : MonoBehaviour, IAgentComponent
{
    /// <summary>(현재, 최대)</summary>
    public event Action<float, float> OnShieldChangedEvent;

    /// <summary>실드가 0이 됐다. 이 순간부터 체력이 깎인다.</summary>
    public event Action OnShieldBrokenEvent;

    [Tooltip("실드 1을 채우는 데 드는 전력.")]
    [SerializeField, Min(0f)] private float _powerPerShield = 0.5f;

    private Agent _owner;
    private AgentStatus _status;
    private VitalStatus _vitals;

    private float _current;

    // 비율 유지에 이전 최대값이 필요하다. 스탯은 바뀐 뒤의 값만 알려준다.
    private float _max;
    private float _sinceLastHit;

    public float Current => _current;
    public float Max => _max;
    public float Normalized => _max <= 0f ? 0f : _current / _max;
    public bool HasShield => _current > 0f;

    /// <summary>회복 대기 중 남은 시간. 0이면 회복 중이거나 가득 찬 상태.</summary>
    public float RegenDelayRemaining
        => _status != null ? Mathf.Max(0f, _status.ShieldRegenDelay.TotalValue - _sinceLastHit) : 0f;

    public void Initialize(Agent owner)
    {
        _owner = owner;
    }

    public void AfterInitialize()
    {
        _status = _owner.GetCompo<AgentStatus>();
        _vitals = _owner.GetCompo<VitalStatus>();

        if (_status != null)
        {
            _status.MaxShield.OnChangedEvent += HandleMaxChanged;
            _max = Mathf.Max(0f, _status.MaxShield.TotalValue);
        }

        ResetShield();
    }

    public void Dispose()
    {
        if (_status != null)
            _status.MaxShield.OnChangedEvent -= HandleMaxChanged;
    }

    private void OnDestroy() => Dispose();

    /// <summary>새 판·출격·풀 재사용. 가득 채우고 대기 타이머를 끝낸 상태로 둔다.</summary>
    public void ResetShield()
    {
        _sinceLastHit = float.MaxValue;
        Set(Max);
    }

    /// <summary>
    /// 피격 통보. <see cref="Health"/>가 방어력 계산 전에 부른다.
    /// 실드가 남아 있었으면 그 타격을 전부 받아내고 true — 체력은 건드리지 않는다.
    /// 실드가 없었으면 타이머만 초기화하고 false.
    /// </summary>
    public bool TryAbsorb(float damage)
    {
        _sinceLastHit = 0f;

        if (_current <= 0f) return false;

        Set(_current - damage);

        if (_current <= 0f)
            OnShieldBrokenEvent?.Invoke();

        return true;
    }

    private void Update()
    {
        if (_owner != null && _owner.IsDead) return;

        TickRegen(Time.deltaTime);
    }

    private void TickRegen(float deltaTime)
    {
        if (_status == null) return;

        // 오버플로 방지. 가득 찬 채로 오래 있으면 계속 더해지기만 한다.
        if (_sinceLastHit < float.MaxValue)
            _sinceLastHit += deltaTime;

        float max = _max;
        if (_current >= max) return;
        if (_sinceLastHit < _status.ShieldRegenDelay.TotalValue) return;

        float amount = Mathf.Min(_status.ShieldRegenRate.TotalValue * deltaTime, max - _current);
        if (amount <= 0f) return;

        if (_vitals != null && !_vitals.TryConsume(VitalType.Power, amount * _powerPerShield)) return;

        Set(_current + amount);
    }

    private void Set(float value)
    {
        float clamped = Mathf.Clamp(value, 0f, _max);

        if (Mathf.Approximately(_current, clamped)) return;

        _current = clamped;
        OnShieldChangedEvent?.Invoke(_current, _max);
    }

    /// <summary>
    /// 코어가 바뀌어 최대값이 변하면 비율을 유지한다 (<see cref="Health"/>와 같은 규칙).
    /// 실드가 없던 기체(최대 0)에 코어가 붙은 경우는 가득 채운다 — 출격 직후 0에서 시작하면 20초 동안 맨몸이다.
    /// </summary>
    private void HandleMaxChanged(float newMax)
    {
        newMax = Mathf.Max(0f, newMax);

        float next = _max <= 0f ? newMax : newMax * (_current / _max);

        _max = newMax;
        _current = Mathf.Clamp(next, 0f, _max);
        OnShieldChangedEvent?.Invoke(_current, _max);
    }
}
