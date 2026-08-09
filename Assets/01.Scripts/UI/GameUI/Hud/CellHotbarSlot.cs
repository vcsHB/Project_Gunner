using UnityEngine;

/// <summary>
/// 핫바 한 칸. 인벤토리 앞쪽 칸을 그대로 가리키므로 드래그·드롭이 그대로 동작한다.
///
/// 베이스의 _selectMarker는 "상세 정보로 고른 칸" 표시이고,
/// 여기서 쓰는 _activeMarker는 "지금 손에 든 칸" 표시다. 서로 다른 개념이라 나눠 둔다.
/// </summary>
public class CellHotbarSlot : CellInventorySlot
{
    [Header("Hotbar")]
    [Tooltip("지금 들고 있는 칸일 때 활성화된다.")]
    [SerializeField] private GameObject _activeMarker;

    public bool IsActive { get; private set; }

    public void SetActive(bool active)
    {
        IsActive = active;

        if (_activeMarker != null)
            _activeMarker.SetActive(active);
    }
}
