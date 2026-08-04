using UnityEngine;

/// <summary>
/// 상태이상/버프의 베이스. AgentConditionEffector가 수명을 관리한다.
/// 강도는 Type별 상수 * Level로 정한다. (레벨은 1부터)
/// </summary>
public abstract class ConditionBase
{
    public abstract ConditionType Type { get; }

    public Agent Owner { get; private set; }

    /// <summary>이 상태를 건 주체. Status의 modifier 제거 키로도 쓴다.</summary>
    public object Origin { get; private set; }

    public int Level { get; private set; } = 1;

    public float Duration { get; private set; }
    public float RemainTime { get; private set; }

    /// <summary>Duration이 0 이하면 직접 제거할 때까지 유지되는 영구 상태다.</summary>
    public bool IsPermanent => Duration <= 0f;
    public bool IsFinished => !IsPermanent && RemainTime <= 0f;

    public virtual void Initialize(Agent owner, object origin, int level, float duration)
    {
        Owner = owner;
        Origin = origin;
        Level = Mathf.Max(1, level);
        Duration = duration;
        RemainTime = duration;
    }

    /// <summary>
    /// 같은 상태가 다시 걸렸을 때. 지속시간은 항상 갱신하고,
    /// 더 높은 레벨로 들어오면 기존 효과를 걷어내고 새 레벨로 다시 건다.
    /// </summary>
    public virtual void Refresh(int level, float duration)
    {
        Duration = duration;
        RemainTime = duration;

        level = Mathf.Max(1, level);
        if (level <= Level) return;

        OnEnd();
        Level = level;
        OnStart();
    }

    public virtual void OnStart() { }

    public virtual void OnUpdate(float deltaTime)
    {
        if (!IsPermanent)
            RemainTime -= deltaTime;
    }

    public virtual void OnEnd() { }
}
