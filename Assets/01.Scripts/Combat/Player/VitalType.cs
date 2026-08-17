/// <summary>
/// 시간에 따라 닳는 생존 수치. 체력은 여기 들어가지 않는다 —
/// 체력은 전투 판정(방어력·저항)을 타고 Health가 따로 관리한다.
/// </summary>
public enum VitalType
{
    /// <summary>질주 같은 행동에 쓰이고 쉬면 찬다.</summary>
    Stamina = 0,

    /// <summary>식사. 시간이 지나면 줄어든다.</summary>
    Hunger,

    /// <summary>수분. 허기보다 빨리 줄어든다.</summary>
    Thirst,
}
