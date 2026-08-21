using UnityEngine;

/// <summary>
/// 손에 든 것. 주/보조 입력이 무엇을 뜻하는지 <b>여기가</b> 정한다.
///
/// 입력은 두 개(좌클릭=Primary, 우클릭=Secondary)뿐이고 의미는 든 것마다 다르다.
///
/// | 든 것 | Primary | Secondary |
/// |---|---|---|
/// | 총 | 발사 | 정조준 |
/// | 투척(신관) | 던지기 | 안전핀 제거 + 조준 |
/// | 소모품 | — | 사용 |
///
/// 입력을 구독하는 쪽이 각자 "내가 나설 차례인가"를 판단하면 조건이 여러 곳에 흩어지고,
/// 결국 둘 다 반응하거나 아무도 반응하지 않는 상태가 생긴다.
/// <see cref="PlayerHandController"/>가 지금 든 것 하나에게만 입력을 넘긴다.
/// </summary>
public abstract class HandActionBase : MonoBehaviour
{
    /// <summary>이것을 들고 있는 주체.</summary>
    public Agent Owner { get; private set; }

    /// <summary>손에 든 아이템. 개수를 줄이거나 개체 상태를 볼 때 쓴다.</summary>
    public ItemStack Stack { get; private set; }

    public bool IsEquipped { get; private set; }

    /// <summary>스폰 직후 한 번. 오버라이드할 때 반드시 base를 먼저 호출할 것.</summary>
    public virtual void Initialize(Agent owner, ItemStack stack)
    {
        Owner = owner;
        Stack = stack;
    }

    /// <summary>손에 들렸을 때.</summary>
    public virtual void OnEquipped() => IsEquipped = true;

    /// <summary>
    /// 내려놓을 때. 눌린 채로 넘어가는 것을 여기서 되돌린다.
    /// 안 그러면 다음에 들었을 때 눌린 상태로 시작한다.
    /// </summary>
    public virtual void OnUnequipped()
    {
        IsEquipped = false;

        OnPrimaryReleased();
        OnSecondaryReleased();
    }

    public virtual void OnPrimaryPressed() { }
    public virtual void OnPrimaryReleased() { }

    public virtual void OnSecondaryPressed() { }
    public virtual void OnSecondaryReleased() { }

    /// <summary>재장전 입력(짧게 누름). 쓰지 않는 손은 무시한다.</summary>
    public virtual void OnReloadRequested() { }

    /// <summary>재장전 입력을 길게 눌렀을 때.</summary>
    public virtual void OnAmmoCycleRequested(int direction) { }
}
