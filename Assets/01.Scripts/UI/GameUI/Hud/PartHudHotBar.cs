using UnityEngine;

/// <summary>
/// 장착 슬롯을 나열하고 지금 들고 있는 무기를 표시한다.
/// </summary>
public class PartHudHotbar : MonoBehaviour
{
    [SerializeField] private CellHotbarSlot[] _slots;

    [Tooltip("각 셀이 담당할 장착 슬롯. 셀과 개수가 같아야 한다.")]
    [SerializeField]
    private EquipSlotType[] _slotTypes =
    {
        EquipSlotType.PrimaryWeapon,
        EquipSlotType.SecondaryWeapon,
        EquipSlotType.Tool,
    };

    private EquipmentController _equipment;
    private PlayerWeaponController _weapons;

    private void Awake()
    {
        if (_slots.Length != _slotTypes.Length)
        {
            Debug.LogError(
                $"[Hotbar] 셀 {_slots.Length}개 / 슬롯 타입 {_slotTypes.Length}개로 개수가 다릅니다.", this);
        }

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == null) continue;

            _slots[i].SetSlotType(i < _slotTypes.Length ? _slotTypes[i] : EquipSlotType.None, i);
            _slots[i].Clear();
            _slots[i].SetSelected(false);
        }
    }

    public void Bind(EquipmentController equipment, PlayerWeaponController weapons)
    {
        Unbind();

        _equipment = equipment;
        _weapons = weapons;

        if (_equipment != null)
            _equipment.OnEquipChangedEvent += HandleEquipChanged;

        if (_weapons != null)
            _weapons.OnWeaponChangedEvent += HandleWeaponChanged;

        RefreshAll();
        RefreshSelection();
    }

    public void Unbind()
    {
        if (_equipment != null)
        {
            _equipment.OnEquipChangedEvent -= HandleEquipChanged;
            _equipment = null;
        }

        if (_weapons != null)
        {
            _weapons.OnWeaponChangedEvent -= HandleWeaponChanged;
            _weapons = null;
        }
    }

    private void OnDestroy() => Unbind();

    public void RefreshAll()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == null) continue;

            _slots[i].SetStack(_equipment != null ? _equipment.GetStack(_slots[i].SlotType) : ItemStack.Empty);
        }
    }

    private void HandleEquipChanged(EquipSlotType slot, ItemStack stack)
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == null || _slots[i].SlotType != slot) continue;

            _slots[i].SetStack(stack);
        }
    }

    private void HandleWeaponChanged(PlayerWeaponBase weapon) => RefreshSelection();

    private void RefreshSelection()
    {
        EquipSlotType current = _weapons != null ? _weapons.CurrentSlot : EquipSlotType.None;

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == null) continue;

            _slots[i].SetSelected(_slots[i].SlotType == current && current != EquipSlotType.None);
        }
    }
}
