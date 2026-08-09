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
    private Transform _dropOrigin;

    private void Awake() => _drag = GetComponentInParent<ItemDragController>(true);

    private void Start()
    {
        Player player = FindAnyObjectByType<Player>();
        if (player != null)
            _dropOrigin = player.transform;
    }

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
        if (container == null) return;

        if (_dropOrigin == null)
        {
            Debug.LogWarning("[ItemDrop] 떨어뜨릴 기준 위치가 없어 취소합니다.", this);
            return;
        }

        // 꺼낸 다음에 스폰이 실패하면 아이템이 사라지므로, 실패 시 되돌린다.
        ItemStack taken = container.Take(slotKey, count);
        if (taken.IsEmpty) return;

        if (WorldItemSpawner.Spawn(taken, _dropOrigin.position) == null)
            container.Place(slotKey, taken);
    }
}
