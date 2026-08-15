/// <summary>
/// 발사자가 계산해서 캐스터에 실어 보내는 위력.
///
/// 이게 없으면 투사체는 발사자의 AgentStatus.Damage만 쓰게 되고, 무기 스탯과 탄종 차이가
/// 전혀 반영되지 않는다. 총알이 날아가는 동안 무기를 바꿔도 위력이 변하면 안 되므로
/// 발사 시점에 값으로 고정해서 넘긴다.
/// </summary>
public struct CastPower
{
    public float damage;

    /// <summary>1을 넘으면 치명타 확정 + 초과분이 극대화 확률. DamageHandler 규칙과 같다.</summary>
    public float criticalRate;

    public CastPower(float damage, float criticalRate)
    {
        this.damage = damage;
        this.criticalRate = criticalRate;
    }
}
