/// <summary>
/// 같은 아이템이라도 개체마다 달라지는 상태.
/// ItemStack은 (Id, 개수)뿐이라 모딩된 무기 두 정을 구분하지 못한다. 그 구분을 여기가 담당한다.
///
/// 겹치는 아이템(자원 등)은 인스턴스를 만들지 않는다. 수천 개에 객체를 붙일 이유가 없다.
/// </summary>
public abstract class ItemInstance
{
    public uint InstanceId { get; internal set; }
    public uint ItemId { get; internal set; }

    public ItemDataSO Data
        => ItemDatabaseSO.Instance != null && ItemDatabaseSO.Instance.TryGet(ItemId, out ItemDataSO data)
            ? data
            : null;
}
