using System;
using UnityEngine;

/// <summary>
/// 핫바에서 지금 고른 칸. 인벤토리 앞 7칸이 곧 핫바다.
/// 무기든 도구든 블럭이든 "지금 든 것"은 여기서 정해진다.
/// </summary>
public class PlayerHotbar : MonoBehaviour, IAgentComponent
{
    /// <summary>선택 칸이 바뀌었거나, 그 칸의 내용물이 바뀌었을 때.</summary>
    public event Action<int> OnSelectedChangedEvent;

    private Player _player;
    private InventoryController _inventoryController;

    public int Size => InventoryLayout.HotbarSize;
    public int SelectedIndex { get; private set; }

    public Inventory Inventory => _inventoryController != null ? _inventoryController.Inventory : null;

    public ItemStack SelectedStack => Inventory != null ? Inventory[SelectedIndex] : ItemStack.Empty;

    public void Initialize(Agent owner)
    {
        _player = owner as Player;
    }

    public void AfterInitialize()
    {
        _inventoryController = _player.GetCompo<InventoryController>();

        if (Inventory != null)
            Inventory.OnSlotChangedEvent += HandleSlotChanged;

        if (_player.Input != null)
            _player.Input.OnSlotCycleEvent += Cycle;

        OnSelectedChangedEvent?.Invoke(SelectedIndex);
    }

    public void Dispose()
    {
        if (Inventory != null)
            Inventory.OnSlotChangedEvent -= HandleSlotChanged;

        if (_player != null && _player.Input != null)
            _player.Input.OnSlotCycleEvent -= Cycle;
    }

    public void Select(int index)
    {
        index = Mathf.Clamp(index, 0, Size - 1);
        if (SelectedIndex == index) return;

        SelectedIndex = index;
        OnSelectedChangedEvent?.Invoke(SelectedIndex);
    }

    /// <summary>앞뒤로 순환한다. 빈 칸도 건너뛰지 않는다. (맨손이 유효한 상태다)</summary>
    public void Cycle(int direction)
    {
        if (direction == 0) return;

        int next = (SelectedIndex + direction) % Size;
        if (next < 0) next += Size;

        Select(next);
    }

    // 든 칸의 내용이 바뀌면 들고 있는 것도 바뀌어야 한다.
    private void HandleSlotChanged(int slotIndex)
    {
        if (slotIndex != SelectedIndex) return;

        OnSelectedChangedEvent?.Invoke(SelectedIndex);
    }
}
