using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전력과 연료. 최대값은 AgentStatus가, 현재값은 여기가 들고 간다.
/// Health와 같은 구조다 — 스탯은 "얼마까지 찰 수 있나"고, 이 컴포넌트는 "지금 얼마인가"다.
///
/// <b>무엇이 쓰는지는 여기가 정하지 않는다.</b> <see cref="TryConsume"/>를 부르는 쪽이 정한다.
/// 여기서 부스터를 알게 되면 발사·실드가 생길 때마다 이 파일을 고쳐야 한다.
///
/// 둘의 성격이 다르다 — <b>전력은 스스로 차고, 연료는 안 찬다.</b>
/// 연료가 시간만 지나면 차면 보급이라는 선택이 사라지고, 전력이 안 차면 무장이 일회용이 된다.
/// </summary>
[RequireAgentComponent(typeof(AgentStatus))]
[RequireAgentComponent(typeof(Health))]
public class VitalStatus : MonoBehaviour, IAgentComponent
{
    /// <summary>(종류, 현재, 최대)</summary>
    public event Action<VitalType, float, float> OnVitalChangedEvent;

    [Header("전력")]
    [Tooltip("제너레이터가 초당 채우는 양.")]
    [SerializeField] private float _powerRegenPerSecond = 20f;

    [Tooltip("마지막으로 전력을 쓴 뒤 이 시간이 지나야 다시 찬다.")]
    [SerializeField] private float _powerRegenDelay = 1f;

    [Tooltip("바닥나면 이 비율만큼 찰 때까지 다시 쓸 수 없다. 0.2면 20%.")]
    [SerializeField, Range(0f, 1f)] private float _powerRecoverThreshold = 0.2f;

    [Header("연료 고갈")]
    [Tooltip("연료가 0일 때 초당 들어오는 손상. 방어력을 무시한다.")]
    [SerializeField] private float _emptyFuelDamagePerSecond = 1f;

    [Tooltip("연료가 0일 때 이동속도 감소율. 0.5면 -50%.")]
    [SerializeField, Range(0f, 1f)] private float _emptyFuelMoveSpeedPenalty = 0.5f;

    [Tooltip("연료가 0일 때 전력 회복 배율. 제너레이터도 연료를 먹는다.")]
    [SerializeField, Range(0f, 1f)] private float _emptyFuelPowerRegenScale = 0.4f;

    // 값 타입을 origin으로 쓰면 참조 동일성 비교라 제거가 안 된다.
    private readonly object _fuelPenaltyOrigin = new();

    private readonly float[] _current = new float[Enum.GetValues(typeof(VitalType)).Length];

    private Agent _owner;
    private AgentStatus _status;
    private Health _health;

    private float _powerIdleTime;
    private bool _powerLocked;

    /// <summary>전력이 바닥나 잠긴 상태인지. 임계치까지 차야 풀린다.</summary>
    public bool IsPowerEmpty => _powerLocked;

    /// <summary>연료가 바닥났는지. 이동 페널티와 손상이 걸린다.</summary>
    public bool IsFuelEmpty => Get(VitalType.Fuel) <= 0f;

    public float Get(VitalType type) => _current[(int)type];

    public float GetMax(VitalType type)
    {
        Status<float> max = _status != null ? _status.GetMaxVital(type) : null;

        return max != null ? Mathf.Max(1f, max.TotalValue) : 100f;
    }

    public float GetNormalized(VitalType type) => Get(type) / GetMax(type);

    public void Initialize(Agent owner)
    {
        _owner = owner;
    }

    public void AfterInitialize()
    {
        _status = _owner.GetCompo<AgentStatus>();
        _health = _owner.GetCompo<Health>();

        ResetAll();
    }

    public void Dispose()
    {
        // modifier를 걸어둔 채로 사라지면 안 된다.
        if (_status != null)
            _status.MoveSpeed.RemoveModifier(_fuelPenaltyOrigin);
    }

    private void OnDestroy() => Dispose();

    /// <summary>새 판·출격. 전부 가득 채운다.</summary>
    public void ResetAll()
    {
        for (int i = 0; i < _current.Length; i++)
            Set((VitalType)i, GetMax((VitalType)i));

        _powerLocked = false;
        _powerIdleTime = _powerRegenDelay;
    }

    private void Update()
    {
        if (_owner != null && _owner.IsDead) return;

        float deltaTime = Time.deltaTime;

        TickPowerRegen(deltaTime);
        TickEmptyFuel(deltaTime);
    }

    #region 소모와 회복

    /// <summary>
    /// 연료는 시간만으로 줄지 않는다. 부스터·이동이 <see cref="TryConsume"/>로 빼간다.
    /// 시간당 자동 감소를 두면 가만히 서 있어도 손해라 "아껴 쓴다"는 선택이 사라진다.
    /// </summary>
    private void TickPowerRegen(float deltaTime)
    {
        _powerIdleTime += deltaTime;
        if (_powerIdleTime < _powerRegenDelay) return;

        float scale = IsFuelEmpty ? _emptyFuelPowerRegenScale : 1f;
        if (scale <= 0f) return;

        Add(VitalType.Power, _powerRegenPerSecond * scale * deltaTime);

        // 바닥난 뒤에는 어느 정도 차야 다시 쓸 수 있다. 안 그러면 한 방울씩 차며 딸꾹질한다.
        if (_powerLocked && GetNormalized(VitalType.Power) >= _powerRecoverThreshold)
            _powerLocked = false;
    }

    /// <summary>
    /// 자원을 쓴다. 모자라거나 잠겨 있으면 아무것도 쓰지 않고 false.
    /// 부스터처럼 매 프레임 조금씩 쓰는 쪽이 그대로 호출한다.
    ///
    /// 전력만 잠금이 걸린다. 연료는 바닥나도 "못 쓰는 상태"가 아니라 "없는 상태"라서,
    /// 보급하면 바로 다시 쓸 수 있어야 한다.
    /// </summary>
    public bool TryConsume(VitalType type, float amount)
    {
        if (amount <= 0f) return true;

        bool isPower = type == VitalType.Power;

        if (isPower && _powerLocked) return false;

        if (Get(type) < amount)
        {
            Set(type, 0f);

            if (isPower)
            {
                // 바닥났다. 조금씩 찰 때마다 다시 쓰이는 것을 막는다.
                _powerLocked = true;
                _powerIdleTime = 0f;
            }

            return false;
        }

        Add(type, -amount);

        if (isPower)
            _powerIdleTime = 0f;

        return true;
    }

    /// <summary>보급 아이템이 쓴다.</summary>
    public void Restore(VitalType type, float amount)
    {
        if (amount <= 0f) return;

        Add(type, amount);
    }

    private void Add(VitalType type, float delta)
    {
        if (Mathf.Approximately(delta, 0f)) return;

        Set(type, _current[(int)type] + delta);
    }

    private void Set(VitalType type, float value)
    {
        float max = GetMax(type);
        float clamped = Mathf.Clamp(value, 0f, max);

        if (Mathf.Approximately(_current[(int)type], clamped)) return;

        _current[(int)type] = clamped;
        OnVitalChangedEvent?.Invoke(type, clamped, max);
    }

    #endregion

    #region 연료 고갈

    /// <summary>
    /// 연료가 바닥나면 페널티와 손상을 <b>둘 다</b> 준다.
    /// 페널티는 modifier라 켜고 끄는 순간에만 손대고, 손상은 매 프레임 들어간다.
    /// 손상이 방어력을 무시하는 이유 — 장갑을 둘렀다고 연료가 덜 떨어지지는 않는다.
    /// </summary>
    private void TickEmptyFuel(float deltaTime)
    {
        ApplyFuelPenalty(IsFuelEmpty);

        if (!IsFuelEmpty || _health == null) return;

        _health.ApplyDirectDamage(_emptyFuelDamagePerSecond * deltaTime);
    }

    private void ApplyFuelPenalty(bool active)
    {
        if (_status == null) return;

        // 이미 걸려 있는지 확인하지 않고 매번 걸면 modifier가 무한히 쌓인다.
        if (IsPenaltyApplied() == active) return;

        if (active)
            _status.MoveSpeed.AddModifier(_fuelPenaltyOrigin, -_emptyFuelMoveSpeedPenalty, ModifierMode.Percent);
        else
            _status.MoveSpeed.RemoveModifier(_fuelPenaltyOrigin);
    }

    private bool IsPenaltyApplied()
    {
        IReadOnlyList<ModifierData<float>> modifiers = _status.MoveSpeed.Modifiers;

        for (int i = 0; i < modifiers.Count; i++)
        {
            if (ReferenceEquals(modifiers[i].origin, _fuelPenaltyOrigin)) return true;
        }

        return false;
    }

    #endregion
}
