/// <summary>
/// 기체가 땅에 붙어 있는지, 떠서 활공 중인지. Space로 전환한다.
/// 활공 중에는 <see cref="TargetType.Air"/>로 판정된다 — 지상만 노리는 공격이 안 닿는다.
/// </summary>
public enum MechaMoveMode
{
    Landed = 0,
    Gliding,
}
