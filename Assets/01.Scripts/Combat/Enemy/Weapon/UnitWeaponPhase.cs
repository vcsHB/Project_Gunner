/// <summary>
/// 단일 기체 무기가 지금 무엇을 하고 있는가. 연출(<see cref="UnitWeaponFeedback"/>)이 이걸 보고 예고를 그린다.
/// 값은 저장되지 않지만 이벤트로 나가므로 순서를 바꾸지 말 것.
/// </summary>
public enum UnitWeaponPhase
{
    /// <summary>쏘지 않는다. 사정권 밖이거나 대상이 없다.</summary>
    Idle = 0,

    /// <summary>노리는 중. 기체는 계속 돈다(정밀 사격의 조준선).</summary>
    Charging = 1,

    /// <summary>조준 고정. 기체가 돌지 않는다 — 곧 나간다.</summary>
    Locked = 2,

    /// <summary>연발이 나가는 중.</summary>
    Firing = 3,

    /// <summary>다음 공격까지 쉰다. 파고들 틈이다.</summary>
    Resting = 4,
}
