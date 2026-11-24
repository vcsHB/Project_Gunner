using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 칸을 우클릭하면 커서 옆에 뜨는 작은 메뉴. 무엇을 띄울지는 <see cref="ItemContextActions"/>가 정한다.
///
/// 루트는 화면 전체를 덮는 투명 판이다 — 메뉴 밖 어디를 눌러도 닫힌다. ESC로도 닫힌다(<c>_closesOnCancel</c>).
/// 줄은 템플릿 버튼을 복제해 만들고, 다음에 열 때 재사용한다.
/// </summary>
public class PopupItemContext : UIPanelBase, IPointerClickHandler
{
    public static PopupItemContext Instance { get; private set; }

    /// <summary>메뉴가 떠 있다. 그동안 칸 툴팁을 띄우지 않는다.</summary>
    public static bool IsOpen => Instance != null && Instance.IsShown;

    [Tooltip("줄들을 담는 상자. 커서 옆으로 옮겨진다. 앵커는 부모 중앙, 피벗은 왼쪽 위.")]
    [SerializeField] private RectTransform _menu;

    [Tooltip("한 줄. 꺼둔 채로 두고 복제해서 쓴다. 자식에 TextMeshProUGUI가 있어야 한다.")]
    [SerializeField] private UnityEngine.UI.Button _template;

    [SerializeField] private Color _disabledTextColor = new(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] private Vector2 _offset = new(6f, -6f);

    private readonly List<ItemContextAction> _actions = new();
    private readonly List<UnityEngine.UI.Button> _rows = new();
    private Agent _owner;
    private Color _textColor = Color.white;

    protected override void Awake()
    {
        base.Awake();
        Instance = this;

        if (_template != null)
        {
            TextMeshProUGUI text = _template.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null) _textColor = text.color;
            _template.gameObject.SetActive(false);
        }
    }

    private void Start() => _owner = FindAnyObjectByType<Player>();

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Open(CellInventorySlot cell)
    {
        ItemContextActions.Collect(cell, _owner, _actions);
        if (_actions.Count == 0 || _template == null)
        {
            Hide();
            return;
        }

        for (int i = 0; i < _actions.Count; i++)
            SetupRow(GetRow(i), _actions[i]);

        for (int i = _actions.Count; i < _rows.Count; i++)
            _rows[i].gameObject.SetActive(false);

        Show();
        PlaceAtPointer();
    }

    /// <summary>메뉴 밖을 눌렀다. 줄(버튼)은 자기가 클릭을 먹으므로 여기 오지 않는다.</summary>
    public void OnPointerClick(PointerEventData eventData) => Hide();

    private UnityEngine.UI.Button GetRow(int index)
    {
        while (_rows.Count <= index)
        {
            UnityEngine.UI.Button row = Instantiate(_template, _template.transform.parent);
            row.name = $"Row{_rows.Count}";
            _rows.Add(row);
        }

        return _rows[index];
    }

    private void SetupRow(UnityEngine.UI.Button row, ItemContextAction action)
    {
        row.gameObject.SetActive(true);
        row.interactable = action.enabled;

        TextMeshProUGUI text = row.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
        {
            bool withReason = !action.enabled && !string.IsNullOrEmpty(action.reason);
            text.text = withReason ? $"{action.label} <size=75%>({action.reason})</size>" : action.label;
            text.color = action.enabled ? _textColor : _disabledTextColor;
        }

        row.onClick.RemoveAllListeners();
        System.Action run = action.run;
        row.onClick.AddListener(() =>
        {
            // 먼저 닫는다 — 작업이 다른 팝업(분할)을 열 수 있다.
            Hide();
            run?.Invoke();
        });
    }

    private void PlaceAtPointer()
    {
        if (_menu == null || Pointer.current == null) return;

        RectTransform parent = _menu.parent as RectTransform;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (parent == null || canvas == null) return;

        Canvas root = canvas.rootCanvas;
        Camera camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, Pointer.current.position.ReadValue(), camera, out Vector2 local))
            return;

        // 줄 수가 바뀌었으니 크기를 먼저 맞춘 뒤에 화면 안으로 민다.
        LayoutRebuilder.ForceRebuildLayoutImmediate(_menu);
        _menu.anchoredPosition = UIClamp.InsideParent(local + _offset, parent.rect, _menu);
    }

    /// <summary>인벤토리가 닫힐 때. 투명해진 채 떠 있으면 ESC 한 번을 먹는다.</summary>
    public static void HideIfOpen()
    {
        if (IsOpen) Instance.Hide();
    }
}
