using System;
using System.Collections.Generic;
using UnityEngine;

public class AgentConditionEffector : MonoBehaviour, IAgentComponent
{
    private readonly List<ConditionBase> _conditions = new();
    private Agent _owner;

    public IReadOnlyList<ConditionBase> Conditions => _conditions;

    public void Initialize(Agent owner)
    {
        _owner = owner;
    }

    public void AfterInitialize()
    {
    }

    public void Dispose()
    {
        ClearConditions();
    }

    /// <summary>
    /// 같은 타입이 이미 걸려있으면 새로 쌓지 않고 지속시간만 갱신한다.
    /// </summary>
    public void AddCondition(ConditionBase condition, object origin, float duration)
    {
        ConditionBase exist = GetCondition(condition.GetType());
        if (exist != null)
        {
            exist.Refresh();
            return;
        }

        condition.Initialize(_owner, origin, duration);
        _conditions.Add(condition);
        condition.OnStart();
    }

    public T GetCondition<T>() where T : ConditionBase => GetCondition(typeof(T)) as T;

    public bool HasCondition<T>() where T : ConditionBase => GetCondition(typeof(T)) != null;

    public void RemoveCondition<T>() where T : ConditionBase
    {
        ConditionBase condition = GetCondition(typeof(T));
        if (condition == null) return;

        _conditions.Remove(condition);
        condition.OnEnd();
    }

    public void ClearConditions()
    {
        for (int i = _conditions.Count - 1; i >= 0; i--)
            _conditions[i].OnEnd();

        _conditions.Clear();
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        // OnEnd에서 리스트가 바뀔 수 있으니 뒤에서부터 순회한다.
        for (int i = _conditions.Count - 1; i >= 0; i--)
        {
            ConditionBase condition = _conditions[i];
            condition.OnUpdate(deltaTime);

            if (!condition.IsFinished) continue;

            _conditions.RemoveAt(i);
            condition.OnEnd();
        }
    }

    private ConditionBase GetCondition(Type type)
    {
        for (int i = 0; i < _conditions.Count; i++)
        {
            if (_conditions[i].GetType() == type)
                return _conditions[i];
        }

        return null;
    }
}
