using System.Collections.Generic;
using UnityEngine;

public enum StatusType
{
    Damage,
    Health,
    Defense,
    CriticalRate,
    DamageResistance,
    IsResist
}

public class AgentStatus : MonoBehaviour, IAgentComponent
{
    [SerializeField] private Status<float> damage = new(10f);
    [SerializeField] private Status<float> health = new(100f);
    [SerializeField] private Status<float> defense = new(0f);
    [SerializeField] private Status<float> criticalRate = new(0f); // 0 ~ 1
    [SerializeField] private Status<float> damageResistance = new(0f); // 받는 피해 감소율 0 ~ 1
    [SerializeField] private Status<bool> isResist = new(false); // 상태이상 면역

    public Status<float> Damage => damage;
    public Status<float> Health => health;
    public Status<float> Defense => defense;
    public Status<float> CriticalRate => criticalRate;
    public Status<float> DamageResistance => damageResistance;
    public Status<bool> IsResist => isResist;

    private Dictionary<StatusType, StatusBase> _statuses;

    protected Agent Owner { get; private set; }

    public void Initialize(Agent owner)
    {
        Owner = owner;

        _statuses = new Dictionary<StatusType, StatusBase>
        {
            { StatusType.Damage, damage },
            { StatusType.Health, health },
            { StatusType.Defense, defense },
            { StatusType.CriticalRate, criticalRate },
            { StatusType.DamageResistance, damageResistance },
            { StatusType.IsResist, isResist },
        };
    }

    public void AfterInitialize()
    {
    }

    public void Dispose()
    {
        foreach (StatusBase status in _statuses.Values)
            status.ClearModifiers();
    }

    /// <summary>
    /// 타입을 알고 있을 때 사용. 타입이 맞지 않으면 null을 반환한다.
    /// </summary>
    public Status<T> GetStatus<T>(StatusType statusType)
        => GetStatus(statusType) as Status<T>;

    public StatusBase GetStatus(StatusType statusType)
    {
        if (_statuses.TryGetValue(statusType, out StatusBase status))
            return status;

        Debug.LogError($"[AgentStatus] 정의되지 않은 StatusType: {statusType}", this);
        return null;
    }

    /// <summary>
    /// 버프/디버프 해제용. origin을 기준으로 모든 스탯에서 modifier를 제거한다.
    /// </summary>
    public void RemoveModifiers(object origin)
    {
        foreach (StatusBase status in _statuses.Values)
            status.RemoveModifier(origin);
    }
}
