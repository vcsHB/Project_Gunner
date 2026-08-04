/// <summary>
/// 레벨에 비례해 방어력을 깎는다. Status modifier로 걸었다가 종료 시 되돌린다.
/// </summary>
public class WeaknessCondition : ConditionBase
{
    public override ConditionType Type => ConditionType.Weakness;

    private const float DefenseDownPerLevel = 5f;

    private AgentStatus _status;

    public override void Initialize(Agent owner, object origin, int level, float duration)
    {
        base.Initialize(owner, origin, level, duration);
        _status = owner.GetCompo<AgentStatus>();
    }

    public override void OnStart()
    {
        _status?.Defense.AddModifier(this, -DefenseDownPerLevel * Level);
    }

    public override void OnEnd()
    {
        _status?.Defense.RemoveModifier(this);
    }
}
