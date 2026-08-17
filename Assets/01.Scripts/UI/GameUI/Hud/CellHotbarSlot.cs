using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// 핫바 한 칸. 인벤토리 앞쪽 칸을 그대로 가리키므로 드래그·드롭이 그대로 동작한다.
///
/// 지금 든 칸 표시는 베이스의 SelectMark를 그대로 쓴다.
/// 다만 상세 정보 선택도 같은 마커를 쓰므로, 둘 중 하나라도 켜져 있으면 표시한다.
/// (안 그러면 인벤토리에서 다른 아이템을 고르는 순간 든 칸 표시가 꺼진다)
/// </summary>
public class CellHotbarSlot : CellInventorySlot
{
    private bool _isActive;
    private bool _isSelected;
    public bool IsActive => _isActive;

    /// <summary>지금 손에 든 칸인지.</summary>
    public void SetActive(bool active)
    {
        _isActive = active;
        UpdateMarker();
    }

    public override void SetSelected(bool selected)
    {
        _isSelected = selected;
        UpdateMarker();
    }

    private void UpdateMarker() => base.SetSelected(_isActive || _isSelected);
}
