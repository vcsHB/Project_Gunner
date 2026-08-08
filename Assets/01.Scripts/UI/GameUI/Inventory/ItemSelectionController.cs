using System;
using UnityEngine;

/// <summary>
/// 지금 선택된 아이템 칸. 인벤토리든 장비든 파츠든 어느 칸을 눌러도 여기로 모인다.
/// PartItemDetail 같은 상세 표시는 이 이벤트만 구독하면 된다.
/// </summary>
public class ItemSelectionController : MonoBehaviour
{
    /// <summary>(컨테이너, 슬롯키). 선택이 풀리면 컨테이너가 null.</summary>
    public event Action<IItemSlotContainer, int> OnSelectionChangedEvent;

    private CellInventorySlot _selectedCell;

    public IItemSlotContainer Container => _selectedCell != null ? _selectedCell.Container : null;
    public int SlotKey => _selectedCell != null ? _selectedCell.SlotKey : -1;

    public ItemStack SelectedStack
    {
        get
        {
            IItemSlotContainer container = Container;
            return container != null ? container.Peek(SlotKey) : ItemStack.Empty;
        }
    }

    public void Select(CellInventorySlot cell)
    {
        if (_selectedCell == cell) return;

        if (_selectedCell != null)
            _selectedCell.SetSelected(false);

        _selectedCell = cell;

        if (_selectedCell != null)
            _selectedCell.SetSelected(true);

        OnSelectionChangedEvent?.Invoke(Container, SlotKey);
    }

    public void Clear() => Select(null);
}
