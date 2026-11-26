using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 아이템을 놓으면 바닥에 떨어뜨리는 영역.
///
/// 구독자가 있으면 그쪽에 넘기고, 없으면 플레이어 발밑에 떨군다.
/// </summary>
public class ItemDropArea : MonoBehaviour, IDropHandler
{
    /// <summary>(컨테이너, 슬롯키, 개수). 구독하면 기본 떨구기 대신 이쪽이 처리한다.</summary>
    public event Action<IItemSlotContainer, int, int> OnDropRequestedEvent;

    private ItemDragController _drag;
    // 갈아탈 수 있으므로 매번 본다.
    private static Agent Dropper => PlayerPilot.LocalMecha;

    private void Awake() => _drag = GetComponentInParent<ItemDragController>(true);

    public void OnDrop(PointerEventData eventData)
    {
        if (_drag == null || !_drag.IsDragging) return;

        CellInventorySlot source = _drag.SourceCell;
        IItemSlotContainer container = source.Container;
        int slotKey = source.SlotKey;
        int count = _drag.Count;

        _drag.Cancel();

        if (OnDropRequestedEvent != null)
        {
            OnDropRequestedEvent.Invoke(container, slotKey, count);
            return;
        }

        DropToWorld(container, slotKey, count);
    }

    private void DropToWorld(IItemSlotContainer container, int slotKey, int count)
    {
        if (Dropper == null)
        {
            Debug.LogWarning("[ItemDrop] 떨어뜨릴 주인이 없어 취소합니다.", this);
            return;
        }

        ItemDiscard.Discard(container, slotKey, count, Dropper);
    }
}
