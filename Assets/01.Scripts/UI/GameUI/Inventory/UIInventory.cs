using UnityEngine;

public class UIInventory : MonoBehaviour
{
    [SerializeField] private CellInventorySlot[] _inventorySlots;

    private Inventory _inventory;
    private int _selectedIndex = -1;

    public Inventory Inventory => _inventory;

    private void Awake()
    {
        for (int i = 0; i < _inventorySlots.Length; i++)
        {
            if (_inventorySlots[i] == null) continue;

            _inventorySlots[i].SetDisplayIndex(i);
            _inventorySlots[i].Clear();
            _inventorySlots[i].SetSelected(false);
        }
    }

    // Awake가 아니라 Start에서 붙인다. Agent의 컴포넌트 초기화가 Awake에서 끝나기 때문이다.
    private void Start()
    {
        if (_inventory != null) return;

        Player player = FindAnyObjectByType<Player>();
        InventoryController controller = player != null ? player.GetCompo<InventoryController>() : null;

        if (controller != null)
            Bind(controller.Inventory);
    }

    public void Bind(Inventory inventory)
    {
        if (_inventory == inventory) return;

        Unbind();
        _inventory = inventory;

        if (_inventory == null) return;

        if (_inventory.Capacity > _inventorySlots.Length)
        {
            Debug.LogWarning(
                $"[UIInventory] 칸이 모자랍니다. 인벤토리 {_inventory.Capacity}칸 / UI {_inventorySlots.Length}칸. " +
                "뒤쪽 아이템은 보이지 않습니다.", this);
        }

        _inventory.OnSlotChangedEvent += HandleSlotChanged;
        RefreshAll();
    }

    public void Unbind()
    {
        if (_inventory == null) return;

        _inventory.OnSlotChangedEvent -= HandleSlotChanged;
        _inventory = null;
    }

    private void OnDestroy() => Unbind();

    public void SetSelected(int index)
    {
        if (_selectedIndex == index) return;

        if (IsValidSlot(_selectedIndex))
            _inventorySlots[_selectedIndex].SetSelected(false);

        _selectedIndex = index;

        if (IsValidSlot(_selectedIndex))
            _inventorySlots[_selectedIndex].SetSelected(true);
    }

    public void RefreshAll()
    {
        for (int i = 0; i < _inventorySlots.Length; i++)
        {
            if (_inventorySlots[i] == null) continue;

            // 인벤토리 용량을 넘는 칸은 컨테이너 없이 비워둔다. 드래그도 걸리지 않는다.
            bool inRange = _inventory != null && i < _inventory.Capacity;
            _inventorySlots[i].Bind(inRange ? _inventory : null, i);
        }
    }

    private void HandleSlotChanged(int index)
    {
        if (!IsValidSlot(index)) return;

        _inventorySlots[index].Refresh();
    }

    private bool IsValidSlot(int index)
        => index >= 0 && index < _inventorySlots.Length && _inventorySlots[index] != null;
}
