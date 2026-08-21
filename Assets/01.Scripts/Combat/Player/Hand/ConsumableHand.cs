using UnityEngine;

/// <summary>
/// 손에 든 소모품. <b>보조(우클릭)로 쓴다.</b> 주 동작은 없다.
///
/// 사용 시간·쿨타임·효과 적용은 <see cref="PlayerItemUseController"/>가 들고 간다.
/// 쿨타임은 손을 바꿔도 남아야 하는데, 이 오브젝트는 핫바를 넘길 때마다 파괴되기 때문이다.
/// 여기는 "우클릭이 사용을 뜻한다"만 안다.
/// </summary>
public class ConsumableHand : HandActionBase
{
    protected PlayerItemUseController Use { get; private set; }

    public ConsumableDataSO Data { get; private set; }

    public override void Initialize(Agent owner, ItemStack stack)
    {
        base.Initialize(owner, stack);

        Data = stack.Resolve<ConsumableDataSO>();

        if (owner != null)
            Use = owner.GetCompo<PlayerItemUseController>();
    }

    public override void OnSecondaryPressed()
    {
        if (Use != null)
            Use.TryBeginUse(Stack);
    }

    public override void OnSecondaryReleased()
    {
        // 쓰다 만 것은 취소된다. 붕대를 감다 말면 안 감긴 것이다.
        if (Use != null)
            Use.CancelUse();
    }

    public override void OnUnequipped()
    {
        base.OnUnequipped();

        if (Use != null)
            Use.CancelUse();
    }
}
