using TMPro;
using UnityEngine;

/// <summary>
/// 캐릭터 장착 칸. 어느 슬롯인지 이름이 붙고, 비어 있으면 + 표시가 뜬다.
/// 드래그·선택 동작은 전부 CellInventorySlot 것을 그대로 쓴다.
/// </summary>
public class CellEquipmentSlot : CellInventorySlot
{
    [Header("Equipment")]
    [SerializeField] private EquipSlotType _slotType = EquipSlotType.None;

    [Tooltip("비워두면 EquipSlotType 이름을 그대로 쓴다.")]
    [SerializeField] private string _displayName;

    [SerializeField] private TextMeshProUGUI _textSlotName;

    [Tooltip("빈 칸일 때 활성화된다.")]
    [SerializeField] private GameObject _plusMark;

    public EquipSlotType SlotType => _slotType;

    protected override void Awake()
    {
        base.Awake();

        if (_textSlotName != null)
            _textSlotName.text = string.IsNullOrEmpty(_displayName) ? _slotType.ToString() : _displayName;
    }

    /// <summary>이 칸이 담당하는 장착 슬롯으로 컨테이너에 연결한다.</summary>
    public void Bind(EquipmentController equipment) => Bind(equipment, (int)_slotType);

    protected override void OnStackChanged()
    {
        // 아이콘 자체는 베이스가 이미 껐다. 여기서는 빈 칸 표시만 얹는다.
        if (_plusMark != null)
            _plusMark.SetActive(IsEmpty);
    }
}
