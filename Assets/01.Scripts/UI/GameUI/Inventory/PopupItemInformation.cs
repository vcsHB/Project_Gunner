using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 슬롯에 마우스를 올리면 뜨는 아이템 설명. 커서를 따라다닌다.
///
/// 커서가 하나라 툴팁도 하나뿐이므로 싱글턴으로 둔다. ItemDragController와 같은 판단이다.
/// 칸마다 툴팁을 들고 있으면 칸 수만큼 오브젝트가 늘어난다.
///
/// <b>레이캐스트를 막으면 안 된다.</b> 커서 바로 아래에 뜨기 때문에,
/// 막으면 칸의 OnPointerExit이 즉시 불려 떴다 사라졌다를 반복한다.
/// </summary>
public class PopupItemInformation : Singleton<PopupItemInformation>
{
    [Tooltip("실제로 켜고 끌 대상. 비우면 자기 자신.")]
    [SerializeField] private RectTransform _root;

    [SerializeField] private Canvas _canvas;

    [SerializeField] private TextMeshProUGUI _textItemName;
    [SerializeField] private TextMeshProUGUI _textItemDescription;
    [SerializeField] private SimpleGauge _durabilityGauge;

    [Tooltip("커서에서 얼마나 떨어뜨릴지. 커서에 딱 붙으면 아이콘을 가린다.")]
    [SerializeField] private Vector2 _offset = new(16f, -16f);

    private ItemStack _current;

    public bool IsShown => !_current.IsEmpty;

    protected override void Awake()
    {
        base.Awake();

        if (_root == null)
            _root = transform as RectTransform;

        if (_canvas == null)
            _canvas = GetComponentInParent<Canvas>();

        MakeClickThrough();
        Hide();
    }

    private void MakeClickThrough()
    {
        if (_root == null) return;

        if (!_root.TryGetComponent(out CanvasGroup group))
            group = _root.gameObject.AddComponent<CanvasGroup>();

        group.blocksRaycasts = false;
        group.interactable = false;
    }

    public void ShowFor(ItemStack stack)
    {
        if (stack.IsEmpty)
        {
            Hide();
            return;
        }

        ItemDataSO data = stack.Resolve();
        if (data == null)
        {
            Hide();
            return;
        }

        _current = stack;

        if (_textItemName != null)
            _textItemName.text = data.DisplayName;

        if (_textItemDescription != null)
            _textItemDescription.text = data.Description;

        RefreshDurability(stack);

        SetVisible(true);
        FollowPointer();
    }

    /// <summary>
    /// 그 칸에서 벗어났을 때. 지금 다른 칸을 보여주고 있으면 무시한다.
    ///
    /// 칸 사이를 빠르게 지나가면 새 칸의 Enter가 옛 칸의 Exit보다 먼저 오는 경우가 있다.
    /// 그때 무조건 끄면 툴팁이 사라진 채로 남는다.
    /// </summary>
    public void HideFor(ItemStack stack)
    {
        if (_current.itemId != stack.itemId || _current.instanceId != stack.instanceId) return;

        Hide();
    }

    public void Hide()
    {
        _current = ItemStack.Empty;
        SetVisible(false);
    }

    private void RefreshDurability(ItemStack stack)
    {
        if (_durabilityGauge == null) return;

        ItemInstance instance = stack.ResolveInstance();
        bool show = instance != null && instance.HasDurability;

        _durabilityGauge.gameObject.SetActive(show);

        if (show)
            _durabilityGauge.UpdateProgress(instance.Durability, instance.MaxDurability);
    }

    private void SetVisible(bool visible)
    {
        if (_root != null && _root.gameObject.activeSelf != visible)
            _root.gameObject.SetActive(visible);
    }

    // 레이아웃이 잡힌 뒤에 옮겨야 화면 밖으로 나가는지 제대로 잰다.
    private void LateUpdate()
    {
        if (!IsShown) return;

        FollowPointer();
    }

    private void FollowPointer()
    {
        if (_root == null || _canvas == null || Pointer.current == null) return;

        RectTransform canvasRect = _canvas.transform as RectTransform;
        if (canvasRect == null) return;

        // Overlay 캔버스는 카메라가 null이어야 좌표 변환이 맞는다.
        Camera camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        Vector2 screen = Pointer.current.position.ReadValue();

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, camera, out Vector2 local))
            return;

        _root.anchoredPosition = Clamp(local + _offset, canvasRect.rect, _root.rect);
    }

    /// <summary>
    /// 화면 가장자리에서 툴팁이 잘리지 않게 안으로 밀어 넣는다.
    /// 오른쪽 아래 칸에 올렸을 때 설명이 반쯤 잘려 보이는 게 가장 흔한 사고다.
    /// </summary>
    private static Vector2 Clamp(Vector2 position, Rect canvas, Rect popup)
    {
        float minX = canvas.xMin - popup.xMin;
        float maxX = canvas.xMax - popup.xMax;
        float minY = canvas.yMin - popup.yMin;
        float maxY = canvas.yMax - popup.yMax;

        return new Vector2(
            Mathf.Clamp(position.x, Mathf.Min(minX, maxX), Mathf.Max(minX, maxX)),
            Mathf.Clamp(position.y, Mathf.Min(minY, maxY), Mathf.Max(minY, maxY)));
    }
}
