using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 아이템을 놓으면 버리는 영역.
///
/// 지금은 이벤트만 쏘고 실제로 버리지는 않는다. 월드에 아이템 픽업 오브젝트가 생기면
/// 그때 구독해서 떨어뜨리면 된다. 그 전에 칸에서 빼버리면 아이템이 그냥 사라진다.
/// </summary>
public class ItemDropArea : MonoBehaviour, IDropHandler
{
    /// <summary>(컨테이너, 슬롯키, 개수)</summary>
    public event Action<IItemSlotContainer, int, int> OnDropRequestedEvent;

    [Tooltip("아직 버리기가 구현되지 않았음을 콘솔로 알린다.")]
    [SerializeField] private bool _logWhenUnhandled = true;

    private ItemDragController _drag;

    private void Awake() => _drag = GetComponentInParent<ItemDragController>(true);

    public void OnDrop(PointerEventData eventData)
    {
        if (_drag == null || !_drag.IsDragging) return;

        CellInventorySlot source = _drag.SourceCell;
        int count = _drag.Count;

        _drag.Cancel();

        if (OnDropRequestedEvent != null)
        {
            OnDropRequestedEvent.Invoke(source.Container, source.SlotKey, count);
            return;
        }

        if (_logWhenUnhandled)
            Debug.Log($"[ItemDrop] 버리기 요청을 받았지만 처리할 곳이 없습니다. ({source.Stack.count}개 중 {count}개)", this);
    }
}
