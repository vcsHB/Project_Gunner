using System;
using UnityEngine;

/// <summary>
/// 스테미너·허기·갈증. 최대값은 AgentStatus가, 현재값은 여기가 들고 간다.
/// Health와 같은 구조다 — 스탯은 "얼마까지 찰 수 있나"고, 이 컴포넌트는 "지금 얼마인가"다.
///
/// 스테미너를 무엇이 쓰는지는 여기가 정하지 않는다. <see cref="TryConsumeStamina"/>를
/// 쓰는 쪽(지금은 질주)이 정한다. 여기서 질주를 알게 되면 구르기·근접공격이 생길 때마다 고쳐야 한다.
/// </summary>
public class VitalStatus : MonoBehaviour, IAgentComponent
{
    /// <summary>(종류, 현재, 최대)</summary>
    public event Action<VitalType, float, float> OnVitalChangedEvent;

    [Header("소모")]
    [Tooltip("허기가 초당 줄어드는 양.")]
    [SerializeField] private float _hungerDrainPerSecond = 0.15f;

    [Tooltip("갈증이 초당 줄어드는 양. 보통 허기보다 빠르다.")]
    [SerializeField] private float _thirstDrainPerSecond = 0.25f;

    [Header("스테미너")]
    [SerializeField] private float _staminaRegenPerSecond = 20f;

    [Tooltip("마지막으로 스테미너를 쓴 뒤 이 시간이 지나야 다시 찬다.")]
    [SerializeField] private float _staminaRegenDelay = 1f;

    [Tooltip("바닥나면 이 비율만큼 찰 때까지 다시 쓸 수 없다. 0.2면 20%.")]
    [SerializeField, Range(0f, 1f)] private float _staminaRecoverThreshold = 0.2f;

    [Header("고갈 페널티")]
    [Tooltip("허기나 갈증이 0일 때 초당 들어오는 피해. 방어력을 무시한다.")]
    [SerializeField] private float _starvingDamagePerSecond = 1f;

    [Tooltip("허기나 갈증이 0일 때 이동속도 감소율. 0.3이면 -30%. 둘 다 0이면 두 번 걸린다.")]
    [SerializeField, Range(0f, 1f)] private float _starvingMoveSpeedPenalty = 0.3f;

    [Tooltip("허기나 갈증이 0일 때 스테미너 회복 배율.")]
    [SerializeField, Range(0f, 1f)] private float _starvingStaminaRegenScale = 0.4f;

    // 값 타입을 origin으로 쓰면 참조 동일성 비교라 제거가 안 된다. 굶주림·탈수를 따로 떼기 위해 각각 키를 둔다.
    private readonly object _hungerPenaltyOrigin = new();
    private readonly object _thirstPenaltyOrigin = new();

    private readonly float[] _current = new float[3];

    private Agent _owner;
    private AgentStatus _status;
    private Health _health;

    private float _staminaIdleTime;
    private bool _staminaLocked;

    public bool IsStaminaEmpty => _staminaLocked;

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
            _status.MoveSpeed.RemoveModifier(_hungerPenaltyOrigin);

        if (_status != null)
            _status.MoveSpeed.RemoveModifier(_thirstPenaltyOrigin);
    }

    private void OnDestroy() => Dispose();

    /// <summary>새 게임·리스폰. 전부 가득 채운다.</summary>
    public void ResetAll()
    {
        for (int i = 0; i < _current.Length; i++)
            Set((VitalType)i, GetMax((VitalType)i));

        _staminaLocked = false;
        _staminaIdleTime = _staminaRegenDelay;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        if (_owner != null && _owner.IsDead) return;

        TickDrain(deltaTime);
        TickStamina(deltaTime);
        TickStarving(deltaTime);
    }

    #region 소모와 회복

    private void TickDrain(float deltaTime)
    {
        Add(VitalType.Hunger, -_hungerDrainPerSecond * deltaTime);
        Add(VitalType.Thirst, -_thirstDrainPerSecond * deltaTime);
    }

    private void TickStamina(float deltaTime)
    {
        _staminaIdleTime += deltaTime;
        if (_staminaIdleTime < _staminaRegenDelay) return;

        float scale = IsStarving ? _starvingStaminaRegenScale : 1f;
        if (scale <= 0f) return;

        Add(VitalType.Stamina, _staminaRegenPerSecond * scale * deltaTime);

        // 바닥난 뒤에는 어느 정도 차야 다시 쓸 수 있다. 안 그러면 한 방울씩 차며 딸꾹질한다.
        if (_staminaLocked && GetNormalized(VitalType.Stamina) >= _staminaRecoverThreshold)
            _staminaLocked = false;
    }

    /// <summary>
    /// 스테미너를 쓴다. 모자라거나 잠겨 있으면 아무것도 쓰지 않고 false.
    /// 질주처럼 매 프레임 조금씩 쓰는 쪽이 그대로 호출한다.
    /// </summary>
    public bool TryConsumeStamina(float amount)
    {
        if (amount <= 0f) return true;
        if (_staminaLocked) return false;

        if (Get(VitalType.Stamina) < amount)
        {
            // 바닥났다. 조금씩 찰 때마다 다시 쓰이는 것을 막는다.
            Set(VitalType.Stamina, 0f);
            _staminaLocked = true;
            _staminaIdleTime = 0f;
            return false;
        }

        Add(VitalType.Stamina, -amount);
        _staminaIdleTime = 0f;
        return true;
    }

    /// <summary>음식·물·회복 아이템이 쓴다.</summary>
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

    #region 고갈 페널티

    private bool IsHungry => Get(VitalType.Hunger) <= 0f;
    private bool IsThirsty => Get(VitalType.Thirst) <= 0f;
    private bool IsStarving => IsHungry || IsThirsty;

    /// <summary>
    /// 굶주림·탈수는 페널티와 피해를 <b>둘 다</b> 준다.
    /// 페널티는 modifier라 켜고 끄는 순간에만 손대고, 피해는 매 프레임 들어간다.
    /// </summary>
    private void TickStarving(float deltaTime)
    {
        ApplyPenalty(_hungerPenaltyOrigin, IsHungry);
        ApplyPenalty(_thirstPenaltyOrigin, IsThirsty);

        if (!IsStarving || _health == null) return;

        _health.ApplyDirectDamage(_starvingDamagePerSecond * deltaTime);
    }

    private void ApplyPenalty(object origin, bool active)
    {
        if (_status == null) return;

        // 이미 걸려 있는지 확인하지 않고 매번 걸면 modifier가 무한히 쌓인다.
        bool applied = IsPenaltyApplied(origin);
        if (applied == active) return;

        if (active)
            _status.MoveSpeed.AddModifier(origin, -_starvingMoveSpeedPenalty, ModifierMode.Percent);
        else
            _status.MoveSpeed.RemoveModifier(origin);
    }

    private bool IsPenaltyApplied(object origin)
    {
        System.Collections.Generic.IReadOnlyList<ModifierData<float>> modifiers = _status.MoveSpeed.Modifiers;

        for (int i = 0; i < modifiers.Count; i++)
        {
            if (ReferenceEquals(modifiers[i].origin, origin)) return true;
        }

        return false;
    }

    #endregion
}
