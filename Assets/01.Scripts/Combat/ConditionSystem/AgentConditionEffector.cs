using System;
using System.Collections.Generic;
using UnityEngine;

public class AgentConditionEffector : MonoBehaviour, IAgentComponent
{
    public event Action<ConditionBase> OnConditionAddedEvent;
    public event Action<ConditionBase> OnConditionRemovedEvent;

    private readonly List<ConditionBase> _conditions = new();
    private Agent _owner;
    private AgentStatus _status;

    public IReadOnlyList<ConditionBase> Conditions => _conditions;

    public void Initialize(Agent owner)
    {
        _owner = owner;
    }

    public void AfterInitialize()
    {
        _status = _owner.GetCompo<AgentStatus>();
    }

    public void Dispose()
    {
        ClearConditions();
    }

    /// <summary>
    /// 이미 같은 타입이 걸려있으면 새로 쌓지 않고 지속시간/레벨만 갱신한다.
    /// duration이 0 이하면 직접 제거할 때까지 유지된다.
    /// </summary>
    public void AddCondition(ConditionType type, int level, float duration, object origin = null)
    {
        if (type == ConditionType.None) return;

        // 상태이상 면역
        if (_status != null && _status.IsResist.TotalValue) return;

        ConditionBase exist = GetCondition(type);
        if (exist != null)
        {
            exist.Refresh(level, duration);
            return;
        }

        ConditionBase condition = ConditionFactory.Create(type);
        if (condition == null) return;

        condition.Initialize(_owner, origin, level, duration);
        _conditions.Add(condition);
        condition.OnStart();

        OnConditionAddedEvent?.Invoke(condition);
    }

    public ConditionBase GetCondition(ConditionType type)
    {
        for (int i = 0; i < _conditions.Count; i++)
        {
            if (_conditions[i].Type == type)
                return _conditions[i];
        }

        return null;
    }

    public bool HasCondition(ConditionType type) => GetCondition(type) != null;

    /// <summary>걸려있지 않으면 0.</summary>
    public int GetConditionLevel(ConditionType type) => GetCondition(type)?.Level ?? 0;

    public void RemoveCondition(ConditionType type)
    {
        ConditionBase condition = GetCondition(type);
        if (condition == null) return;

        _conditions.Remove(condition);
        EndCondition(condition);
    }

    public void ClearConditions()
    {
        if (_conditions.Count == 0) return;

        // OnEnd에서 리스트를 건드릴 수 있으니 먼저 비우고 정리한다.
        ConditionBase[] snapshot = _conditions.ToArray();
        _conditions.Clear();

        for (int i = 0; i < snapshot.Length; i++)
            EndCondition(snapshot[i]);
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        for (int i = _conditions.Count - 1; i >= 0; i--)
        {
            if (i >= _conditions.Count) continue;

            ConditionBase condition = _conditions[i];
            condition.OnUpdate(deltaTime);

            if (!condition.IsFinished) continue;

            _conditions.Remove(condition);
            EndCondition(condition);
        }
    }

    private void EndCondition(ConditionBase condition)
    {
        condition.OnEnd();
        OnConditionRemovedEvent?.Invoke(condition);
    }
}
