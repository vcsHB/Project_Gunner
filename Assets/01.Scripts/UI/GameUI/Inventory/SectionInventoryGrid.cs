using UnityEngine;

/// <summary>
/// Inventory의 한 구간을 격자로 보여준다.
///
/// 칸은 인벤토리 용량에 맞춰 자동으로 만들어진다.
/// 앞 7칸은 핫바가 따로 표시하므로, 보관 칸 뷰는 _startSlotIndex를 7로 둔다.
/// 상자·제작대도 이 클래스를 그대로 재사용한다.
/// </summary>
public class UIInventory : MonoBehaviour
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

        Player player = FindAnyObjectByType<Player>();
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
                $"[UIInventory] 칸이 {needed}개 필요한데 {_builder.Count}개뿐입니다. " +
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
    }

    /// <summary>실제로 보여줄 칸 수.</summary>
    private int GetVisibleCount()
    {
        if (_inventory == null) return 0;

        int available = Mathf.Max(0, _inventory.Capacity - _startSlotIndex);

        return _maxVisibleCount > 0 ? Mathf.Min(available, _maxVisibleCount) : available;
    }

    private void HandleSlotChanged(int slotIndex)
    {
        int cellIndex = slotIndex - _startSlotIndex;

        CellInventorySlot cell = _builder[cellIndex];
        if (cell == null) return;

        cell.Refresh();
    }
}
