using UnityEngine;

public class PartPlayerInventory : MonoBehaviour
{
    [Header("Main Slots")]
    [SerializeField] private CellEquipmentSlot[] _mainEquipmentSlots; // 장비, 갑옷 등등.

    [SerializeField] private UIInventory _inventory;

    private EquipmentController _equipment;

    // Awake가 아니라 Start에서 붙는다. Agent의 컴포넌트 초기화가 Awake에서 끝나기 때문이다.
    // UIInventory는 스스로 붙으므로 여기서는 장착 칸만 챙긴다.
    private void Start()
    {
        Player player = FindAnyObjectByType<Player>();
        if (player == null) return;

        Bind(player.GetCompo<EquipmentController>());
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

    private void OnDestroy() => Unbind();

    private void HandleEquipChanged(EquipSlotType slot, ItemStack stack)
    {
        for (int i = 0; i < _mainEquipmentSlots.Length; i++)
        {
            if (_mainEquipmentSlots[i] == null || _mainEquipmentSlots[i].SlotType != slot) continue;

            _mainEquipmentSlots[i].Refresh();
        }
    }
}
