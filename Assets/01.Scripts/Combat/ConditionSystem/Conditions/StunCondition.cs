/// <summary>
/// 별도 효과 없이 존재 여부만으로 행동을 막는다.
/// 이동/공격 쪽에서 HasCondition(ConditionType.Stun)으로 확인할 것.
/// 레벨은 지속시간 가산에만 쓴다.
/// </summary>
public class StunCondition : ConditionBase
{
    public override ConditionType Type => ConditionType.Stun;

    private const float ExtraDurationPerLevel = 0.2f;

    public override void Initialize(Agent owner, object origin, int level, float duration)
    {
        base.Initialize(owner, origin, level, duration + ExtraDurationPerLevel * (level - 1));
    }
}
