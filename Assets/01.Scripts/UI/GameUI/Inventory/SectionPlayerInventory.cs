using UnityEngine;

public class SectionPlayerInventory : MonoBehaviour
{
    [Header("Main Slots")]
    [SerializeField] private CellEquipmentSlot[] _mainEquipmentSlots; // 장비, 갑옷 등등.

    [SerializeField] private SectionInventoryGrid _inventory;

    [Tooltip("안전 슬롯. 죽어도 남는 한 칸(InventoryController.SafeSlot).")]
    [SerializeField] private CellInventorySlot _safeSlot;

    private Inventory _safeInventory;

    private EquipmentController _equipment;

    // Awake가 아니라 Start에서 붙는다. Agent의 컴포넌트 초기화가 Awake에서 끝나기 때문이다.
    // UIInventory는 스스로 붙으므로 여기서는 장착 칸만 챙긴다.
    private void Start()
    {
        Player player = PlayerPilot.LocalMecha;
        if (player == null) return;

        Bind(player.GetCompo<EquipmentController>());
        BindSafeSlot(player.GetCompo<InventoryController>());
    }

    private void BindSafeSlot(InventoryController controller)
    {
        if (_safeInventory != null)
            _safeInventory.OnSlotChangedEvent -= HandleSafeSlotChanged;

        _safeInventory = controller != null ? controller.SafeSlot : null;

        if (_safeSlot == null) return;

        _safeSlot.Bind(_safeInventory, 0);

        if (_safeInventory != null)
            _safeInventory.OnSlotChangedEvent += HandleSafeSlotChanged;
    }

    private void HandleSafeSlotChanged(int slot)
    {
        if (_safeSlot != null) _safeSlot.Refresh();
    }

    public void Bind(EquipmentController equipment)
    {
        Unbind();

        _equipment = equipment;
        if (_equipment == null) return;

        _equipment.OnEquipChangedEvent += HandleEquipChanged;

        for (int i = 0; i < _mainEquipmentSlots.Length; i++)
        {
            if (_mainEquipmentSlots[i] == null) continue;

            _mainEquipmentSlots[i].Bind(_equipment);
        }
    }

    public void Unbind()
    {
        if (_equipment == null) return;

        _equipment.OnEquipChangedEvent -= HandleEquipChanged;
        _equipment = null;
    }

    private void OnDestroy()
    {
        Unbind();
        BindSafeSlot(null);
    }

    private void HandleEquipChanged(EquipSlotType slot, ItemStack stack)
    {
        for (int i = 0; i < _mainEquipmentSlots.Length; i++)
        {
            if (_mainEquipmentSlots[i] == null || _mainEquipmentSlots[i].SlotType != slot) continue;

            _mainEquipmentSlots[i].Refresh();
        }
    }
}
