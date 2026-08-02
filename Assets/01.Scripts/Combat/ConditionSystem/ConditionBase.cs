using UnityEngine;

/// <summary>
/// 상태이상/버프의 베이스. AgentConditionEffector가 수명을 관리한다.
/// </summary>
public abstract class ConditionBase
{
    public Agent Owner { get; private set; }

    /// <summary>이 상태를 건 주체. Status의 modifier 제거 키로도 쓴다.</summary>
    public object Origin { get; private set; }

    public float Duration { get; private set; }
    public float RemainTime { get; private set; }

    /// <summary>Duration이 0 이하면 직접 제거할 때까지 유지되는 영구 상태다.</summary>
    public bool IsPermanent => Duration <= 0f;
    public bool IsFinished => !IsPermanent && RemainTime <= 0f;

    public virtual void Initialize(Agent owner, object origin, float duration)
    {
        Owner = owner;
        Origin = origin;
        Duration = duration;
        RemainTime = duration;
    }

    /// <summary>같은 상태가 다시 걸렸을 때 지속시간을 갱신한다.</summary>
    public virtual void Refresh()
    {
        RemainTime = Duration;
    }

    public virtual void OnStart() { }

    public virtual void OnUpdate(float deltaTime)
    {
        if (!IsPermanent)
            RemainTime -= deltaTime;
    }

    public virtual void OnEnd() { }
}
