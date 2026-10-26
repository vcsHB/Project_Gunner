using UnityEngine;

/// <summary>
/// 팔에 달린 것. 입력 하나를 받아 무엇을 할지 <b>여기가</b> 정한다.
///
/// 좌우 무장이 동시에 활성이라 각 팔은 버튼을 하나만 받는다.
/// 좌클릭이 왼팔, 우클릭이 오른팔이고, 그 하나가 무슨 뜻인지는 달린 것마다 다르다.
///
/// | 달린 것 | Use |
/// |---|---|
/// | 총 | 발사 |
/// | 근접 | 휘두르기 |
/// | 도구 | 채굴 |
///
/// 배분은 <see cref="PlayerHandController"/>가 슬롯별로 고정해서 넘긴다.
/// <b>여기에 "내가 나설 차례인가"를 넣지 마세요.</b> 그 판단이 양쪽에 생기면
/// 둘 다 반응하거나 아무도 반응하지 않는 상태가 만들어진다.
/// </summary>
public abstract class HandActionBase : MonoBehaviour
{
    /// <summary>이것을 달고 있는 주체.</summary>
    public Agent Owner { get; private set; }

    /// <summary>어느 팔에 달렸는지. 좌우를 구분해야 하는 표현이 읽어간다.</summary>
    public EquipSlotType Slot { get; private set; }

    /// <summary>달려 있는 아이템. 개체 상태를 볼 때 쓴다.</summary>
    public ItemStack Stack { get; private set; }

    public bool IsEquipped { get; private set; }

    /// <summary>스폰 직후 한 번. 오버라이드할 때 반드시 base를 먼저 호출할 것.</summary>
    public virtual void Initialize(Agent owner, EquipSlotType slot, ItemStack stack)
    {
        Owner = owner;
        Slot = slot;
        Stack = stack;
    }

    /// <summary>팔에 달렸을 때.</summary>
    public virtual void OnEquipped() => IsEquipped = true;

    /// <summary>
    /// 떼어낼 때. 눌린 채로 넘어가는 것을 여기서 되돌린다.
    /// 안 그러면 다음에 달았을 때 눌린 상태로 시작한다.
    /// </summary>
    public virtual void OnUnequipped()
    {
        IsEquipped = false;

        OnUseReleased();
    }

    public virtual void OnUsePressed() { }
    public virtual void OnUseReleased() { }

    /// <summary>재장전 입력. 쓰지 않는 것은 무시한다. (과열로 교체되면 사라진다)</summary>
    public virtual void OnReloadRequested() { }

    /// <summary>재장전 입력을 길게 눌렀을 때. (과열로 교체되면 사라진다)</summary>
    public virtual void OnAmmoCycleRequested(int direction) { }
}
