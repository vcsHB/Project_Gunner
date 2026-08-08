using UnityEngine;

public class PartPlayerInventory : MonoBehaviour
{
    [Header("Main Slots")]
    [SerializeField] private CellInventorySlot[] _mainEquipmentSlots; // 장비, 갑옷 등등.

    [SerializeField] private UIInventory _inventory;

}