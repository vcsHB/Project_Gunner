/// <summary>
/// 아이템 칸을 가진 무언가. 드래그 시스템이 말을 거는 유일한 창구다.
///
/// 인벤토리(칸 인덱스), 장비(EquipSlotType), 파츠(WeaponPartSlotType)가 모두 이걸 구현하면
/// 드래그 쪽은 셋을 구분하지 않아도 된다.
///
/// slotKey의 의미는 구현체가 정한다. 인벤토리는 인덱스, 장비는 (int)EquipSlotType 식이다.
/// </summary>
public interface IItemSlotContainer
{
    ItemStack Peek(int slotKey);

    /// <summary>
    /// 넣을 수 있는지. 못 넣으면 reason에 이유가 담긴다. (툴팁에 그대로 쓸 수 있게)
    /// 이미 다른 아이템이 차 있는 경우는 여기서 보지 않는다. 그건 ItemTransfer가 스왑으로 처리한다.
    /// </summary>
    bool CanAccept(int slotKey, ItemStack stack, out string reason);

    /// <summary>실제로 빼낸 것을 반환한다. 못 빼면 Empty.</summary>
    ItemStack Take(int slotKey, int count);

    /// <summary>넣지 <b>못하고 남은</b> 것을 반환한다. Empty면 전부 들어간 것이다.</summary>
    ItemStack Place(int slotKey, ItemStack stack);
}
