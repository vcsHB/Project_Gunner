using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CellHotbarSlot : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _textSlotIndex;
    [SerializeField] private TextMeshProUGUI _textItemAmount;
    [SerializeField] private Image _imageItemIcon;
    [SerializeField] private GameObject _selectMarker;

    public EquipSlotType SlotType { get; private set; } = EquipSlotType.None;

    public void SetSlotType(EquipSlotType slotType, int displayIndex)
    {
        SlotType = slotType;

        if (_textSlotIndex != null)
            _textSlotIndex.text = (displayIndex + 1).ToString();
    }

    public void SetStack(ItemStack stack)
    {
        ItemDataSO data = stack.Resolve();

        if (_imageItemIcon != null)
        {
            _imageItemIcon.sprite = data != null ? data.IconSprite : null;
            _imageItemIcon.enabled = _imageItemIcon.sprite != null;
        }

        if (_textItemAmount != null)
        {
            bool showAmount = !stack.IsEmpty && stack.count > 1;
            _textItemAmount.text = showAmount ? stack.count.ToString() : string.Empty;
        }
    }

    public void SetSelected(bool selected)
    {
        if (_selectMarker != null)
            _selectMarker.SetActive(selected);
    }

    public void Clear() => SetStack(ItemStack.Empty);
}
