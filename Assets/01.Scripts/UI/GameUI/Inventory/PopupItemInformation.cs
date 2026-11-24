using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 슬롯에 마우스를 올리면 뜨는 아이템 설명. 커서를 따라다닌다.
///
/// 이름·설명 아래에 <b>수치</b>(<see cref="ItemStatSheet"/>)를 적는다. 적재함에 있는 장비 아이템이면
/// 같은 칸에 지금 달린 것과 비교해 ▲▼를 붙인다 — 바꿔 낄지 말지를 툴팁 하나로 정하게 한다.
/// 장비 칸 자체에 올렸을 때는 비교하지 않고 "장착 중"만 붙인다.
///
/// 비교 대상은 그 아이템이 들어갈 수 있는 칸 중 <b>먼저 차 있는 칸</b>이다. 양팔에 다 무기가 있으면 좌무기와 비교한다.
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

    [Tooltip("수치 목록. 리치 텍스트로 줄마다 이름 / 값 / 비교를 적는다. 비우면 수치를 보이지 않는다.")]
    [SerializeField] private TextMeshProUGUI _textStats;

    [Tooltip("값이 시작하는 가로 위치(툴팁 폭 대비 %). 이름 길이와 상관없이 값 열을 맞춘다.")]
    [SerializeField, Range(20f, 80f)] private float _valueColumn = 58f;

    [SerializeField] private Color _betterColor = new(0.45f, 1f, 0.45f, 1f);
    [SerializeField] private Color _worseColor = new(1f, 0.42f, 0.38f, 1f);
    [SerializeField] private Color _noteColor = new(0.65f, 0.75f, 0.85f, 1f);

    [Tooltip("커서에서 얼마나 떨어뜨릴지. 커서에 딱 붙으면 아이콘을 가린다.")]
    [SerializeField] private Vector2 _offset = new(16f, -16f);

    private ItemStack _current;
    private EquipmentController _equipment;

    private readonly List<ItemStatLine> _lines = new();
    private readonly List<ItemStatLine> _compareLines = new();
    private readonly StringBuilder _sb = new();

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

    // 비교 기준이 되는 장비. HUD·인벤토리와 같이 Start에서 씬의 플레이어를 찾는다.
    private void Start()
    {
        Player player = FindAnyObjectByType<Player>();
        _equipment = player != null ? player.GetCompo<EquipmentController>() : null;
    }

    private void MakeClickThrough()
    {
        if (_root == null) return;

        if (!_root.TryGetComponent(out CanvasGroup group))
            group = _root.gameObject.AddComponent<CanvasGroup>();

        group.blocksRaycasts = false;
        group.interactable = false;
    }

    /// <param name="source">그 아이템이 들어 있는 컨테이너. 장비 칸이면 비교하지 않는다.</param>
    public void ShowFor(ItemStack stack, IItemSlotContainer source = null)
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
        RefreshStats(stack, source);

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

    #region 수치

    private void RefreshStats(ItemStack stack, IItemSlotContainer source)
    {
        if (_textStats == null) return;

        ItemStatSheet.Collect(stack, _lines);

        bool equipped = _equipment != null && ReferenceEquals(source, _equipment);
        EquipSlotType compareSlot = EquipSlotType.None;
        ItemStack compareWith = equipped ? ItemStack.Empty : FindCompareTarget(stack, out compareSlot);
        bool comparing = !compareWith.IsEmpty;

        if (comparing) ItemStatSheet.Collect(compareWith, _compareLines);
        else _compareLines.Clear();

        _sb.Clear();

        string note = equipped
            ? ItemText.Label("equipped", "장착 중")
            : comparing
                ? Localization.Format("ui.item.compare", "{0} 장착품과 비교", ItemText.SlotName(compareSlot))
                : null;

        if (note != null)
            _sb.Append("<size=80%><color=#").Append(ColorUtility.ToHtmlStringRGB(_noteColor)).Append('>')
               .Append(note).Append("</color></size>\n");

        for (int i = 0; i < _lines.Count; i++)
            AppendLine(_lines[i], comparing);

        // 장착품에만 있는 스탯(산탄의 발사체 수 등). 빼면 "바꾸면 잃는 것"이 안 보인다. 이쪽 값은 0으로 적는다.
        if (comparing)
        {
            for (int i = 0; i < _compareLines.Count; i++)
            {
                ItemStatLine missing = _compareLines[i];
                if (missing.IsText || ItemStatSheet.TryFindValue(_lines, missing.key, out _)) continue;

                missing.value = 0f;
                AppendLine(missing, true);
            }
        }

        bool any = _sb.Length > 0;
        _textStats.gameObject.SetActive(any);

        if (any)
            _textStats.text = _sb.ToString().TrimEnd('\n');
    }

    private void AppendLine(ItemStatLine line, bool comparing)
    {
        _sb.Append(line.label).Append("<pos=").Append(_valueColumn.ToString("0.#")).Append("%>");

        if (line.IsText)
        {
            _sb.Append(line.text).Append('\n');
            return;
        }

        _sb.Append(ItemStatSheet.Format(line.value, line.format));

        if (comparing)
        {
            ItemStatSheet.TryFindValue(_compareLines, line.key, out float other);
            float difference = line.value - other;

            if (ItemStatSheet.IsMeaningful(difference))
            {
                bool better = difference > 0f == line.higherIsBetter;

                _sb.Append("  <color=#").Append(ColorUtility.ToHtmlStringRGB(better ? _betterColor : _worseColor)).Append('>')
                   .Append(difference > 0f ? '▲' : '▼')
                   .Append(ItemStatSheet.FormatDifference(Mathf.Abs(difference), line.format))
                   .Append("</color>");
            }
        }

        _sb.Append('\n');
    }

    /// <summary>들어갈 수 있는 칸 중 먼저 차 있는 칸의 아이템. 장비가 아니거나 다 비었으면 Empty.</summary>
    private ItemStack FindCompareTarget(ItemStack stack, out EquipSlotType slot)
    {
        slot = EquipSlotType.None;
        if (_equipment == null) return ItemStack.Empty;

        EquipableItemDataSO data = stack.Resolve<EquipableItemDataSO>();
        if (data == null) return ItemStack.Empty;

        IReadOnlyList<EquipSlotType> allowed = data.AllowedSlots;
        for (int i = 0; i < allowed.Count; i++)
        {
            ItemStack equipped = _equipment.GetStack(allowed[i]);
            if (equipped.IsEmpty) continue;

            slot = allowed[i];
            return equipped;
        }

        return ItemStack.Empty;
    }

    #endregion

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
