using TMPro;
using UnityEngine;

/// <summary>
/// Inventory의 한 구간을 격자로 보여준다.
///
/// 칸은 인벤토리 용량에 맞춰 자동으로 만들어진다.
/// 상자·제작대도 이 클래스를 그대로 재사용한다.
///
/// 몇 칸을 쓰고 있는지("18 / 30")를 <see cref="_textUsage"/>에 적는다. 루팅할 때 "더 들어가나"를 칸을 세지 않고 보게 한다.
/// </summary>
public class SectionInventoryGrid : MonoBehaviour
{
    [Header("Cells")]
    [Tooltip("모자란 칸을 찍어낼 프리팹.")]
    [SerializeField] private CellInventorySlot _cellPrefab;

    [Tooltip("칸이 생성될 부모. 비우면 자기 자신. 보통 GridLayoutGroup이 붙어 있다.")]
    [SerializeField] private RectTransform _cellRoot;

    [Tooltip("이미 만들어 둔 칸이 있으면 여기 등록한다. 비워둬도 루트 자식에서 찾는다.")]
    [SerializeField] private CellInventorySlot[] _presetSlots;

    [Header("Range")]
    [Tooltip("첫 번째 칸이 인벤토리의 몇 번을 가리킬지. 보관 칸 뷰는 핫바 다음부터 시작한다.")]
    [SerializeField, Min(0)] private int _startSlotIndex;

    [Tooltip("0이면 인벤토리 용량에 맞춰 자동. 값을 넣으면 그 개수까지만 보여준다.")]
    [SerializeField, Min(0)] private int _maxVisibleCount;

    [Header("Usage")]
    [Tooltip("사용 칸 / 전체 칸. 비우면 표시하지 않는다.")]
    [SerializeField] private TextMeshProUGUI _textUsage;

    [Tooltip("남은 칸이 이 수 이하이면 경고 색.")]
    [SerializeField, Min(0)] private int _warnFreeSlots = 3;

    [SerializeField] private Color _usageColor = new(0.85f, 0.9f, 0.95f, 1f);
    [SerializeField] private Color _usageWarnColor = new(1f, 0.6f, 0.3f, 1f);

    [Header("Binding")]
    [Tooltip("켜면 씬에서 플레이어를 찾아 스스로 붙는다. 상자 뷰라면 끄고 Bind를 직접 부를 것.")]
    [SerializeField] private bool _bindPlayerInventory = true;

    private Inventory _inventory;
    private CellSlotBuilder _builder;

    public Inventory Inventory => _inventory;

    private void Awake()
    {
        if (_cellRoot == null)
            _cellRoot = transform as RectTransform;

        _builder = new CellSlotBuilder(_cellPrefab, _cellRoot, _presetSlots);
    }

    // Awake가 아니라 Start에서 붙인다. Agent의 컴포넌트 초기화가 Awake에서 끝나기 때문이다.
    private void Start()
    {
        if (!_bindPlayerInventory || _inventory != null) return;

        Player player = PlayerPilot.LocalMecha;
        InventoryController controller = player != null ? player.GetCompo<InventoryController>() : null;

        if (controller != null)
            Bind(controller.Inventory);
    }

    public void Bind(Inventory inventory)
    {
        if (_inventory == inventory) return;

        Unbind();
        _inventory = inventory;

        if (_inventory == null) return;

        _inventory.OnSlotChangedEvent += HandleSlotChanged;
        _inventory.OnCapacityChangedEvent += RefreshAll;
        RefreshAll();
    }

    public void Unbind()
    {
        if (_inventory == null) return;

        _inventory.OnSlotChangedEvent -= HandleSlotChanged;
        _inventory.OnCapacityChangedEvent -= RefreshAll;
        _inventory = null;
    }

    private void OnDestroy() => Unbind();

    public void RefreshAll()
    {
        int needed = GetVisibleCount();

        if (!_builder.Ensure(needed))
        {
            Debug.LogError(
                $"[SectionInventoryGrid] 칸이 {needed}개 필요한데 {_builder.Count}개뿐입니다. " +
                "Cell Prefab을 지정하면 모자란 만큼 자동으로 만듭니다.", this);
        }

        for (int i = 0; i < _builder.Count; i++)
        {
            CellInventorySlot cell = _builder[i];
            if (cell == null) continue;

            int slotIndex = _startSlotIndex + i;

            cell.SetDisplayIndex(slotIndex);
            cell.Bind(i < needed ? _inventory : null, slotIndex);
        }

        RefreshUsage();
    }

    private void RefreshUsage()
    {
        if (_textUsage == null) return;

        int total = GetVisibleCount();
        int used = 0;

        for (int i = 0; i < total; i++)
        {
            if (!_inventory[_startSlotIndex + i].IsEmpty) used++;
        }

        _textUsage.text = Localization.Format("ui.inventory.usage", "{0} / {1}", used, total);
        _textUsage.color = total - used <= _warnFreeSlots ? _usageWarnColor : _usageColor;
    }

    /// <summary>실제로 보여줄 칸 수.</summary>
    private int GetVisibleCount()
    {
        if (_inventory == null) return 0;

        int available = Mathf.Max(0, _inventory.Capacity - _startSlotIndex);

        return _maxVisibleCount > 0 ? Mathf.Min(available, _maxVisibleCount) : available;
    }

    #region 탐색 (잔해)

    /// <summary>
    /// 가려진 칸과 지금 뒤지는 칸의 진행을 칸에 입힌다. 루팅 구역이 매 프레임 부른다.
    /// 진행은 쿨타임 막대(<see cref="CellInventorySlot.SetCooldownRatio"/>)를 빌려 쓴다 — 줄어드는 막대가 "곧 보인다".
    /// </summary>
    public void ApplyConcealment(System.Func<int, bool> isConcealed, int searchingSlot, float progress01)
    {
        for (int i = 0; i < _builder.Count; i++)
        {
            CellInventorySlot cell = _builder[i];
            if (cell == null || !cell.gameObject.activeSelf) continue;

            int slot = _startSlotIndex + i;
            cell.SetConcealed(isConcealed != null && isConcealed(slot));
            cell.SetCooldownRatio(slot == searchingSlot ? 1f - progress01 : 0f);
        }
    }

    public void ClearConcealment() => ApplyConcealment(null, -1, 0f);

    #endregion

    #region 공간 부족 표시

    private float _flashUntil;

    /// <summary>빠른 이동이 꽉 차서 실패했다. 사용 칸 표시를 잠깐 깜빡인다.</summary>
    public void FlashFull(float duration = 0.6f) => _flashUntil = Time.unscaledTime + duration;

    private void Update()
    {
        if (_textUsage == null || _flashUntil <= 0f) return;

        if (Time.unscaledTime >= _flashUntil)
        {
            _flashUntil = 0f;
            RefreshUsage();
            return;
        }

        bool on = Mathf.Repeat(Time.unscaledTime * 8f, 1f) < 0.5f;
        _textUsage.color = on ? _usageWarnColor : _usageColor;
    }

    #endregion

    private void HandleSlotChanged(int slotIndex)
    {
        int cellIndex = slotIndex - _startSlotIndex;

        RefreshUsage();

        CellInventorySlot cell = _builder[cellIndex];
        if (cell == null) return;

        cell.Refresh();
    }
}
