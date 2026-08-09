using UnityEngine;

/// <summary>
/// 인벤토리 앞 7칸을 핫바로 보여준다. 칸을 고르면 그게 손에 든 것이 된다.
/// </summary>
public class PartHudHotbar : MonoBehaviour
{
    [SerializeField] private CellHotbarSlot[] _slots;

    private Inventory _inventory;
    private PlayerHotbar _hotbar;

    private void Awake()
    {
        if (_slots.Length != InventoryLayout.HotbarSize)
        {
            Debug.LogError(
                $"[Hotbar] 칸이 {_slots.Length}개인데 핫바 크기는 {InventoryLayout.HotbarSize}입니다.", this);
        }

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == null) continue;

            _slots[i].SetDisplayIndex(i);
            _slots[i].Clear();
            _slots[i].SetActive(false);

            // 숫자키가 아직 없어서 클릭이 유일한 직접 선택 수단이다.
            _slots[i].OnClickedEvent += HandleCellClicked;
        }
    }

    private void HandleCellClicked(CellInventorySlot cell)
    {
        if (_hotbar == null) return;

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] != cell) continue;

            _hotbar.Select(i);
            return;
        }
    }

    public void Bind(Player player)
    {
        Unbind();

        if (player == null) return;

        InventoryController controller = player.GetCompo<InventoryController>();
        _inventory = controller != null ? controller.Inventory : null;
        _hotbar = player.GetCompo<PlayerHotbar>();

        if (_inventory != null)
        {
            _inventory.OnSlotChangedEvent += HandleSlotChanged;
            _inventory.OnCapacityChangedEvent += RefreshAll;
        }

        if (_hotbar != null)
            _hotbar.OnSelectedChangedEvent += HandleSelectedChanged;

        RefreshAll();
        HandleSelectedChanged(_hotbar != null ? _hotbar.SelectedIndex : 0);
    }

    public void Unbind()
    {
        if (_inventory != null)
        {
            _inventory.OnSlotChangedEvent -= HandleSlotChanged;
            _inventory.OnCapacityChangedEvent -= RefreshAll;
            _inventory = null;
        }

        if (_hotbar != null)
        {
            _hotbar.OnSelectedChangedEvent -= HandleSelectedChanged;
            _hotbar = null;
        }
    }

    private void OnDestroy() => Unbind();

    public void RefreshAll()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == null) continue;

            _slots[i].Bind(_inventory, i);
        }
    }

    private void HandleSlotChanged(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Length) return;
        if (_slots[slotIndex] == null) return;

        _slots[slotIndex].Refresh();
    }

    private void HandleSelectedChanged(int selectedIndex)
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == null) continue;

            _slots[i].SetActive(i == selectedIndex);
        }
    }
}
