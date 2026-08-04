public class PoisonCondition : DamageOverTimeCondition
{
    public override ConditionType Type => ConditionType.Poison;

    protected override float DamagePerLevel => 1f;
    protected override float TickInterval => 1f;
}
