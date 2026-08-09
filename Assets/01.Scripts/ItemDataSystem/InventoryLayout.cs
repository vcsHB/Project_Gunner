/// <summary>
/// 플레이어 인벤토리의 칸 배치 규칙.
///
/// 앞쪽 HotbarSize 칸이 핫바다. 핫바에 넣는 것이 곧 "손에 드는 것"이라
/// 별도 컨테이너를 두지 않고 인벤토리 앞부분을 그대로 쓴다.
/// 덕분에 인벤토리 안에서 위로 끌어올리는 것만으로 장착이 된다.
/// </summary>
public static class InventoryLayout
{
    public const int HotbarSize = 7;

    public static bool IsHotbarSlot(int slotIndex) => slotIndex >= 0 && slotIndex < HotbarSize;
}
