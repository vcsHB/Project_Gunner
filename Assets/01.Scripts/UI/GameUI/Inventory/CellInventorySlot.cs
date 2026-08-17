using System;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 아이템 한 칸. 인벤토리 칸이든 장비 칸이든 핫바 칸이든 같은 프리팹을 쓴다.
/// 무엇을 받을 수 있는지는 이 셀이 아니라 컨테이너가 판단한다.
/// </summary>
public class CellInventorySlot : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TextMeshProUGUI _textSlotIndex;
    [SerializeField] private TextMeshProUGUI _textItemAmount;
    [SerializeField] private Image _imageItemIcon;
    [SerializeField] private GameObject _selectMarker;

    [Tooltip("내구도 고갈처럼 손을 쓰지 않으면 안 풀리는 이유로 쓸 수 없을 때 켜진다. 쿨타임은 여기 포함되지 않는다.")]
    [SerializeField] private GameObject _itemNotUseable;

    [Tooltip("쿨타임 표시. fillAmount로 줄어든다. 없으면 표시하지 않는다.")]
    [SerializeField] private SlicedFilledImage _cooldownFill;

    [Tooltip("내구도 게이지. 내구도가 없는 아이템에서는 꺼진다.")]
    [SerializeField] private Image _durabilityFill;

    /// <summary>왼쪽 클릭됐을 때. 핫바처럼 클릭에 다른 의미를 더할 때 쓴다.</summary>
    public event Action<CellInventorySlot> OnClickedEvent;

    public IItemSlotContainer Container { get; private set; }
    public int SlotKey { get; private set; } = -1;
    public ItemStack Stack { get; private set; }

    public bool IsEmpty => Stack.IsEmpty;

    private int _displayIndex = -1;

    // 내구도 알림을 받기 위해 붙잡아 둔다. 스택이 바뀌면 반드시 떼야 한다.
    private ItemInstance _instance;

    // 캐싱하지 않는다. 씬 로드 순서와 무관하게 항상 현재 것을 쓴다.
    private static ItemDragController Drag => ItemDragController.Instance;
    private static ItemSelectionController Selection => ItemSelectionController.Instance;

    protected virtual void Awake()
    {
        RefreshIndexText();
        RefreshAmountText();
        OnStackChanged();

#if UNITY_EDITOR
        ValidateDropTarget();
#endif
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
        _displayIndex = index;
        RefreshIndexText();
    }

    /// <summary>컨테이너에서 현재 값을 다시 읽어온다.</summary>
    public void Refresh() => SetStack(Container != null ? Container.Peek(SlotKey) : ItemStack.Empty);

    public void SetStack(ItemStack stack)
    {
        // 개체가 바뀌면 이전 개체의 내구도 알림을 계속 받으면 안 된다.
        UnsubscribeInstance();

        Stack = stack;

        if (_imageItemIcon != null)
        {
            ItemDataSO data = stack.Resolve();

            _imageItemIcon.sprite = data != null ? data.IconSprite : null;
            _imageItemIcon.enabled = _imageItemIcon.sprite != null;
        }

        SubscribeInstance();

        RefreshAmountText();
        RefreshUsable();
        RefreshDurability();
        OnStackChanged();
    }

    /// <summary>쿨타임 표시. 핫바가 매 프레임 밀어 넣는다. 0이면 꺼진다.</summary>
    public void SetCooldownRatio(float ratio)
    {
        if (_cooldownFill == null) return;

        bool active = ratio > 0f;

        if (_cooldownFill.enabled != active)
            _cooldownFill.enabled = active;

        if (active)
            _cooldownFill.fillAmount = Mathf.Clamp01(ratio);
    }

    #region 사용 가능 / 내구도

    private void SubscribeInstance()
    {
        _instance = Stack.ResolveInstance();

        if (_instance != null)
            _instance.OnDurabilityChangedEvent += HandleDurabilityChanged;
    }

    private void UnsubscribeInstance()
    {
        if (_instance == null) return;

        _instance.OnDurabilityChangedEvent -= HandleDurabilityChanged;
        _instance = null;
    }

    private void HandleDurabilityChanged(int current, int max)
    {
        RefreshUsable();
        RefreshDurability();
    }

    /// <summary>
    /// 쓸 수 없는 상태 표시. 판정은 ItemUsability 한 곳에서만 한다 —
    /// 칸마다 따로 판단하면 "표시는 멀쩡한데 안 쓰이는" 칸이 생긴다.
    /// </summary>
    private void RefreshUsable()
    {
        if (_itemNotUseable == null) return;

        _itemNotUseable.SetActive(ItemUsability.IsBlocked(Stack));
    }

    private void RefreshDurability()
    {
        if (_durabilityFill == null) return;

        bool show = _instance != null && _instance.HasDurability;

        if (_durabilityFill.enabled != show)
            _durabilityFill.enabled = show;

        if (show)
            _durabilityFill.fillAmount = _instance.DurabilityNormalized;
    }

    #endregion

    public virtual void SetSelected(bool selected)
    {
        if (_selectMarker != null)
            _selectMarker.SetActive(selected);
    }

    public void Clear() => SetStack(ItemStack.Empty);

    /// <summary>
    /// 내용이 바뀐 뒤 호출된다. 파생 칸이 빈 칸 표시 같은 걸 추가할 때 쓴다.
    /// </summary>
    protected virtual void OnStackChanged()
    {
    }

    private void RefreshIndexText()
    {
        if (_textSlotIndex == null) return;

        _textSlotIndex.text = _displayIndex >= 0 ? (_displayIndex + 1).ToString() : string.Empty;
    }

    private void RefreshAmountText()
    {
        if (_textItemAmount == null) return;

        // 1개짜리는 숫자를 지운다. 칸마다 "1"이 붙어 있으면 눈이 피로하고,
        // 겹치지 않는 아이템(무기 등)은 애초에 개수가 의미 없다.
        // 드래그 고스트도 같은 규칙(> 1)을 쓴다.
        bool showAmount = !IsEmpty && Stack.count > 1;

        _textItemAmount.enabled = showAmount;
        _textItemAmount.text = showAmount ? Stack.count.ToString() : string.Empty;
    }

    #region Pointer

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (Stack.IsEmpty) return;

        ItemDragController drag = Drag;
        if (drag == null)
        {
            Debug.LogError("[Cell] 씬에 ItemDragController가 없습니다.", this);
            return;
        }

        // 우클릭 드래그는 반씩 뗀다. 나중에 상세 분할 UI가 생기면 개수만 바꿔 넣으면 된다.
        int count = eventData.button == PointerEventData.InputButton.Right
            ? ItemTransfer.GetHalfCount(Stack)
            : Stack.count;

        drag.Begin(this, count);
    }

    // 고스트는 ItemDragController가 Update에서 따라가므로 여기서 할 일이 없다.
    // 다만 이 핸들러가 없으면 유니티가 드래그 이벤트를 보내지 않는다.
    public void OnDrag(PointerEventData eventData) { }

    public void OnEndDrag(PointerEventData eventData)
    {
        // 대상 칸의 OnDrop이 먼저 불리므로, 여기까지 왔는데 남아있으면 허공에 놓은 것이다.
        ItemDragController drag = Drag;
        if (drag != null)
            drag.End();
    }

    public void OnDrop(PointerEventData eventData)
    {
        ItemDragController drag = Drag;
        if (drag != null)
            drag.DropOn(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.dragging) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;

        ItemSelectionController selection = Selection;
        if (selection != null)
            selection.Select(this);

        OnClickedEvent?.Invoke(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 드래그 중에는 띄우지 않는다. 커서 밑에 고스트가 있는데 설명까지 겹치면 아무것도 안 보인다.
        if (IsEmpty || (Drag != null && Drag.IsDragging)) return;

        PopupItemInformation popup = PopupItemInformation.Instance;
        if (popup != null)
            popup.ShowFor(Stack);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        PopupItemInformation popup = PopupItemInformation.Instance;
        if (popup != null)
            popup.HideFor(Stack);
    }

    #endregion

    private void OnDestroy() => UnsubscribeInstance();

#if UNITY_EDITOR
    /// <summary>
    /// 빈 칸에 드롭이 안 되는 사고가 잦아서 미리 잡는다.
    ///
    /// 아이콘은 빈 칸에서 꺼지기 때문에, 아이콘만 Raycast Target이면
    /// 빈 칸은 레이캐스트에 아예 안 걸리고 OnDrop이 호출되지 않는다.
    /// </summary>
    private void ValidateDropTarget()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);

        for (int i = 0; i < graphics.Length; i++)
        {
            if (!graphics[i].raycastTarget) continue;

            // 아이콘 말고 항상 켜져 있는 그래픽이 하나라도 있으면 된다.
            if (graphics[i] != _imageItemIcon) return;
        }

        Debug.LogWarning(
            $"[Cell] {name}에 항상 켜져 있는 Raycast Target 그래픽이 없습니다. " +
            "빈 칸에는 드롭이 되지 않습니다. 배경 Image를 하나 두고 Raycast Target을 켜세요.", this);
    }
#endif
}
