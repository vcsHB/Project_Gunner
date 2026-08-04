/// <summary>
/// 상태이상 종류. 새로 추가하면 ConditionFactory.Create에도 분기를 넣어줄 것.
/// </summary>
public enum ConditionType
{
    None = 0,
    Burn,       // 화상 - 짧고 강한 도트
    Poison,     // 중독 - 길고 약한 도트
    Weakness,   // 약화 - 방어력 감소
    Stun,       // 기절 - 행동 불가 플래그
}
