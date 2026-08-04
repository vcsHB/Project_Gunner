using UnityEngine;

/// <summary>
/// ConditionType -> 실제 구현 클래스. 새 상태이상을 추가하면 여기에 분기를 넣는다.
/// </summary>
public static class ConditionFactory
{
    public static ConditionBase Create(ConditionType type)
    {
        switch (type)
        {
            case ConditionType.Burn: return new BurnCondition();
            case ConditionType.Poison: return new PoisonCondition();
            case ConditionType.Weakness: return new WeaknessCondition();
            case ConditionType.Stun: return new StunCondition();
        }

        Debug.LogError($"[ConditionFactory] {type}에 대응하는 Condition 구현이 없습니다.");
        return null;
    }
}
