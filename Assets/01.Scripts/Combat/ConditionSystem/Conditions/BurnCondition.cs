public class BurnCondition : DamageOverTimeCondition
{
    public override ConditionType Type => ConditionType.Burn;

    protected override float DamagePerLevel => 3f;
    protected override float TickInterval => 0.5f;
}
