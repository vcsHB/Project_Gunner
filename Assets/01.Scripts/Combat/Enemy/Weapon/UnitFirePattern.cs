/// <summary>
/// 공격 방식의 진행 상태. <see cref="UnitWeaponSO.CreatePattern"/>이 기체마다 하나 만든다.
///
/// AI는 매 틱 "지금 대상 쪽을 보고 있는가"만 넘긴다(<see cref="Tick"/>). 언제 당기고 언제 쉬는지는 여기가 정한다 —
/// AI가 재면 같은 무기가 기체마다 다른 박자로 나간다.
/// 사정권을 벗어나면 <see cref="Stop"/>(<c>cancel = false</c>), 죽으면 <c>cancel = true</c>가 온다.
///
/// 쉬는 시간은 시각(<see cref="UnityEngine.Time.time"/>)으로 잰다 — 사정권 밖이라 틱이 오지 않는 동안에도 흘러야
/// 경계를 들락거리며 쉼 없이 쏘는 일이 없다.
/// </summary>
public abstract class UnitFirePattern
{
    protected UnitWeapon Weapon { get; private set; }

    /// <summary>true면 기체가 돌지 않는다. 정밀 사격이 조준을 고정한 동안 — 이 틈이 피할 구간이다.</summary>
    public virtual bool IsAimLocked => false;

    public void Bind(UnitWeapon weapon) => Weapon = weapon;

    /// <param name="aimed">기체가 대상 쪽을 보고 있고, 쏘면 맞을 수 있는 대상인가.</param>
    public abstract void Tick(bool aimed, float deltaTime);

    /// <summary>
    /// 공격을 그만둔다. 진행 중이던 예고·연발을 끊는다. 쉬는 시간은 그대로 흐른다.
    /// <paramref name="cancel"/>이 true면(죽음) 아무것도 내보내지 않고 즉시 멈춘다.
    /// </summary>
    public abstract void Stop(bool cancel);

    protected static float Now => UnityEngine.Time.time;
}
