using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 아이템 드래그 진행 상태와 커서를 따라다니는 고스트를 관리한다. 캔버스 루트에 하나 둔다.
///
/// 커서가 하나라 드래그도 하나뿐이므로 싱글턴으로 둔다.
/// (부모에서 찾게 했더니 컨트롤러를 셀의 조상이 아닌 곳에 두면 조용히 실패했다)
///
/// 드래그를 시작해도 아이템을 원래 칸에서 빼지 않는다. 드롭 시점에 ItemTransfer가 한 번에 옮긴다.
/// 미리 빼두면 드래그 도중 창이 닫히거나 예외가 나는 순간 아이템이 증발한다.
///
/// <b>패널 밖에 놓으면 버린다</b>(2026-11-25). "패널"은 <see cref="_keepAreas"/>(각 Section의 상자)다 — 화면 전체를 덮는
/// 인벤토리 바탕이 아니다. 커서가 그 밖으로 나가는 순간 고스트에 "버리기"가 붙고 패널 밖이 붉어진다.
/// 놓기 전에 결과가 보여야 실수로 버리지 않는다.
/// </summary>
public class ItemDragController : Singleton<ItemDragController>
{
    [SerializeField] private Canvas _canvas;
    [SerializeField] private RectTransform _ghost;
    [SerializeField] private Image _ghostIcon;
    [SerializeField] private TextMeshProUGUI _ghostAmount;

    [Header("버리기")]
    [Tooltip("이 영역 안에 놓으면 버리지 않는다. 각 Section의 상자. 꺼진 것은 무시한다. 비우면 버리기를 쓰지 않는다.")]
    [SerializeField] private RectTransform[] _keepAreas;

    [Tooltip("커서가 패널 밖에 있을 때 켜진다. 패널 뒤 화면 전체를 붉게.")]
    [SerializeField] private GameObject _discardHint;

    [Tooltip("커서가 패널 밖에 있을 때 고스트에 붙는 \"버리기\" 표시.")]
    [SerializeField] private GameObject _ghostDiscardLabel;

    private Agent _dropper;

    private CellInventorySlot _sourceCell;
    private int _count;

    public bool IsDragging => _sourceCell != null;
    public CellInventorySlot SourceCell => _sourceCell;
    public int Count => _count;

    /// <summary>지금 놓으면 버려지는가.</summary>
    public bool IsOverDiscard { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        if (_canvas == null)
            _canvas = GetComponentInParent<Canvas>();

        MakeGhostClickThrough();
        SetGhostVisible(false);
        SetDiscardVisual(false);
    }

    private void Start()
    {
        _dropper = FindAnyObjectByType<Player>();
    }

    /// <summary>
    /// 고스트는 커서 바로 아래에 있어서, 레이캐스트를 막으면 드롭 대상 칸 대신 고스트가 잡힌다.
    /// 그러면 OnDrop이 아예 호출되지 않아 "드래그는 되는데 놓아지지 않는" 상태가 된다.
    /// 프리팹 설정을 깜빡해도 동작하도록 코드에서 강제한다.
    /// </summary>
    private void MakeGhostClickThrough()
    {
        if (_ghost == null) return;

        if (!_ghost.TryGetComponent(out CanvasGroup group))
            group = _ghost.gameObject.AddComponent<CanvasGroup>();

        group.blocksRaycasts = false;
        group.interactable = false;
    }

    private void Update()
    {
        if (!IsDragging) return;

        FollowPointer();
        RefreshDiscard();
    }

    public void Begin(CellInventorySlot cell, int count)
    {
        if (cell == null || cell.Stack.IsEmpty || count <= 0) return;

        _sourceCell = cell;
        _count = Mathf.Clamp(count, 1, cell.Stack.count);

        ItemDataSO data = cell.Stack.Resolve();

        if (_ghostIcon != null)
        {
            _ghostIcon.sprite = data != null ? data.IconSprite : null;
            _ghostIcon.enabled = _ghostIcon.sprite != null;
        }

        if (_ghostAmount != null)
            _ghostAmount.text = _count > 1 ? _count.ToString() : string.Empty;

        SetGhostVisible(true);
        FollowPointer();
        RefreshDiscard();
    }

    /// <summary>대상 칸 위에서 놓았을 때. 셀의 OnDrop에서 호출한다.</summary>
    public void DropOn(CellInventorySlot target)
    {
        if (!IsDragging || target == null)
        {
            Cancel();
            return;
        }

        bool moved = ItemTransfer.Move(_sourceCell.Container, _sourceCell.SlotKey,
            target.Container, target.SlotKey, _count);

#if UNITY_EDITOR
        if (!moved)
            LogDropFailure(target);
#endif

        Cancel();
    }

    /// <summary>
    /// 칸 위가 아닌 곳에서 드래그가 끝났을 때. 셀의 OnEndDrag에서 호출한다.
    /// 패널 밖이면 버리고, 패널 안의 빈 곳이면 되돌린다.
    /// </summary>
    public void End()
    {
        if (!IsDragging) return;

        RefreshDiscard();

        if (IsOverDiscard && _dropper != null)
            ItemDiscard.Discard(_sourceCell.Container, _sourceCell.SlotKey, _count, _dropper);

        Cancel();
    }

    public void Cancel()
    {
        _sourceCell = null;
        _count = 0;
        SetGhostVisible(false);
        SetDiscardVisual(false);
    }

    private void RefreshDiscard()
    {
        bool over = IsDragging && _keepAreas != null && _keepAreas.Length > 0 && !IsPointerInKeepArea();
        if (over == IsOverDiscard) return;

        SetDiscardVisual(over);
    }

    private bool IsPointerInKeepArea()
    {
        if (Pointer.current == null) return true;

        Vector2 screen = Pointer.current.position.ReadValue();
        Camera camera = _canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

        for (int i = 0; i < _keepAreas.Length; i++)
        {
            RectTransform area = _keepAreas[i];
            if (area == null || !area.gameObject.activeInHierarchy) continue;

            if (RectTransformUtility.RectangleContainsScreenPoint(area, screen, camera)) return true;
        }

        return false;
    }

    private void SetDiscardVisual(bool over)
    {
        IsOverDiscard = over;

        if (_discardHint != null) _discardHint.SetActive(over);
        if (_ghostDiscardLabel != null) _ghostDiscardLabel.SetActive(over);
    }

#if UNITY_EDITOR
    /// <summary>
    /// 드롭이 조용히 무시되면 원인을 찾기가 매우 어렵다. 왜 실패했는지 남긴다.
    /// </summary>
    private void LogDropFailure(CellInventorySlot target)
    {
        if (target.Container == null)
        {
            Debug.LogWarning(
                $"[Drag] {target.name}에 컨테이너가 연결되어 있지 않습니다. " +
                "UIInventory의 _startSlotIndex가 인벤토리 용량을 넘었거나, Bind가 되지 않았습니다.", target);
            return;
        }

        ItemTransfer.CanMove(_sourceCell.Container, _sourceCell.SlotKey,
            target.Container, target.SlotKey, _count, out string reason);

        Debug.LogWarning($"[Drag] 옮기지 못했습니다. {(string.IsNullOrEmpty(reason) ? "(같은 칸)" : reason)}", target);
    }
#endif

    private void SetGhostVisible(bool visible)
    {
        if (_ghost != null)
            _ghost.gameObject.SetActive(visible);
    }

    private void FollowPointer()
    {
        if (_ghost == null || _canvas == null || Pointer.current == null) return;

        Vector2 screen = Pointer.current.position.ReadValue();
        RectTransform canvasRect = _canvas.transform as RectTransform;

        // Overlay 캔버스는 카메라가 null이어야 좌표 변환이 맞는다.
        Camera camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, camera, out Vector2 local))
            _ghost.anchoredPosition = local;
    }
}
