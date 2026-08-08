using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 아이템 한 칸. 인벤토리 칸이든 장비 칸이든 같은 프리팹을 쓴다.
/// 무엇을 받을 수 있는지는 이 셀이 아니라 컨테이너가 판단한다.
/// </summary>
public class CellInventorySlot : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler
{
    [SerializeField] private TextMeshProUGUI _textSlotIndex;
    [SerializeField] private TextMeshProUGUI _textItemAmount;
    [SerializeField] private Image _imageItemIcon;
    [SerializeField] private GameObject _selectMarker;

    public IItemSlotContainer Container { get; private set; }
    public int SlotKey { get; private set; } = -1;
    public ItemStack Stack { get; private set; }

    public bool IsEmpty => Stack.IsEmpty;

    private ItemDragController _drag;
    private ItemSelectionController _selection;

    private void Awake()
    {
        _drag = GetComponentInParent<ItemDragController>(true);
        _selection = GetComponentInParent<ItemSelectionController>(true);
    }

    /// <summary>어느 컨테이너의 몇 번 칸인지 알려준다. 표시는 Refresh가 한다.</summary>
    public void Bind(IItemSlotContainer container, int slotKey)
    {
        Container = container;
        SlotKey = slotKey;
        Refresh();
    }

    public void SetDisplayIndex(int index)
    {
        if (_textSlotIndex != null)
            _textSlotIndex.text = (index + 1).ToString();
    }

    /// <summary>컨테이너에서 현재 값을 다시 읽어온다.</summary>
    public void Refresh() => SetStack(Container != null ? Container.Peek(SlotKey) : ItemStack.Empty);

    public void SetStack(ItemStack stack)
    {
        Stack = stack;

        ItemDataSO data = stack.Resolve();

        if (_imageItemIcon != null)
        {
            _imageItemIcon.sprite = data != null ? data.IconSprite : null;
            _imageItemIcon.enabled = _imageItemIcon.sprite != null;
        }

        if (_textItemAmount != null)
        {
            // 1개짜리는 숫자를 띄우지 않는다.
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

    #region Pointer

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_drag == null || Stack.IsEmpty) return;

        // 우클릭 드래그는 반씩 뗀다. 나중에 상세 분할 UI가 생기면 개수만 바꿔 넣으면 된다.
        int count = eventData.button == PointerEventData.InputButton.Right
            ? ItemTransfer.GetHalfCount(Stack)
            : Stack.count;

        _drag.Begin(this, count);
    }

    // 고스트는 ItemDragController가 Update에서 따라가므로 여기서 할 일이 없다.
    // 다만 이 핸들러가 없으면 유니티가 드래그 이벤트를 보내지 않는다.
    public void OnDrag(PointerEventData eventData) { }

    public void OnEndDrag(PointerEventData eventData)
    {
        // 대상 칸의 OnDrop이 먼저 불리므로, 여기까지 왔는데 남아있으면 허공에 놓은 것이다.
        if (_drag != null)
            _drag.End();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (_drag != null)
            _drag.DropOn(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.dragging) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;

        if (_selection != null)
            _selection.Select(this);
    }

    #endregion
}
