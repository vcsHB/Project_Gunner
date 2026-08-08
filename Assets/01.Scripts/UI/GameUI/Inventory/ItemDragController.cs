using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 아이템 드래그 진행 상태와 커서를 따라다니는 고스트를 관리한다. 캔버스 루트에 하나 둔다.
///
/// 싱글턴을 쓰지 않고 셀이 부모에서 찾게 한 이유는, 나중에 화면 분할이 생겼을 때
/// 캔버스마다 독립적으로 동작해야 하기 때문이다.
///
/// 드래그를 시작해도 아이템을 원래 칸에서 빼지 않는다. 드롭 시점에 ItemTransfer가 한 번에 옮긴다.
/// 미리 빼두면 드래그 도중 창이 닫히거나 예외가 나는 순간 아이템이 증발한다.
/// </summary>
public class ItemDragController : MonoBehaviour
{
    [SerializeField] private Canvas _canvas;
    [SerializeField] private RectTransform _ghost;
    [SerializeField] private Image _ghostIcon;
    [SerializeField] private TextMeshProUGUI _ghostAmount;

    private CellInventorySlot _sourceCell;
    private int _count;

    public bool IsDragging => _sourceCell != null;
    public CellInventorySlot SourceCell => _sourceCell;
    public int Count => _count;

    private void Awake()
    {
        if (_canvas == null)
            _canvas = GetComponentInParent<Canvas>();

        SetGhostVisible(false);
    }

    private void Update()
    {
        if (!IsDragging) return;

        FollowPointer();
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
    }

    /// <summary>대상 칸 위에서 놓았을 때. 셀의 OnDrop에서 호출한다.</summary>
    public void DropOn(CellInventorySlot target)
    {
        if (!IsDragging || target == null)
        {
            Cancel();
            return;
        }

        ItemTransfer.Move(_sourceCell.Container, _sourceCell.SlotKey,
            target.Container, target.SlotKey, _count);

        Cancel();
    }

    /// <summary>드롭 없이 드래그가 끝났을 때. 셀의 OnEndDrag에서 호출한다.</summary>
    public void End()
    {
        if (IsDragging)
            Cancel();
    }

    public void Cancel()
    {
        _sourceCell = null;
        _count = 0;
        SetGhostVisible(false);
    }

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
