using UnityEngine;

/// <summary>
/// 인벤토리 앞 7칸을 핫바로 보여준다. 칸을 고르면 그게 손에 든 것이 된다.
/// 칸은 InventoryLayout.HotbarSize에 맞춰 자동으로 만들어진다.
/// </summary>
public class PartHudHotbar : MonoBehaviour
{
    [Tooltip("모자란 칸을 찍어낼 프리팹. CellHotbarSlot이어야 한다.")]
    [SerializeField] private CellHotbarSlot _cellPrefab;

    [Tooltip("칸이 생성될 부모. 비우면 자기 자신.")]
    [SerializeField] private RectTransform _cellRoot;

    [Tooltip("이미 만들어 둔 칸이 있으면 여기 등록한다. 비워둬도 루트 자식에서 찾는다.")]
    [SerializeField] private CellHotbarSlot[] _presetSlots;

    private CellSlotBuilder _builder;
    private Inventory _inventory;
    private PlayerHotbar _hotbar;

    private void Awake()
    {
        if (_cellRoot == null)
            _cellRoot = transform as RectTransform;

        _builder = new CellSlotBuilder(_cellPrefab, _cellRoot, _presetSlots);

        if (!_builder.Ensure(InventoryLayout.HotbarSize))
        {
            Debug.LogError(
                $"[Hotbar] 칸이 {InventoryLayout.HotbarSize}개 필요한데 {_builder.Count}개뿐입니다. " +
                "Cell Prefab을 지정하면 모자란 만큼 자동으로 만듭니다.", this);
        }

        for (int i = 0; i < _builder.Count; i++)
        {
            CellInventorySlot cell = _builder[i];
            if (cell == null) continue;

            cell.SetDisplayIndex(i);
            cell.Clear();

            // 숫자키가 아직 없어서 클릭이 유일한 직접 선택 수단이다.
            cell.OnClickedEvent += HandleCellClicked;

            SetCellActive(i, false);
        }
    }

    private void SetCellActive(int index, bool active)
    {
        if (_builder[index] is CellHotbarSlot hotbarCell)
            hotbarCell.SetActive(active);
    }

    public void Bind(Player player)
    {
        Unbind();

        if (player == null) return;

        InventoryController controller = player.GetCompo<InventoryController>();
        _inventory = controller != null ? controller.Inventory : null;
        _hotbar = player.GetCompo<PlayerHotbar>();

        if (_inventory != null)
        {
            _inventory.OnSlotChangedEvent += HandleSlotChanged;
            _inventory.OnCapacityChangedEvent += RefreshAll;
        }

        if (_hotbar != null)
            _hotbar.OnSelectedChangedEvent += HandleSelectedChanged;

        RefreshAll();
        HandleSelectedChanged(_hotbar != null ? _hotbar.SelectedIndex : 0);
    }

    public void Unbind()
    {
        if (_inventory != null)
        {
            _inventory.OnSlotChangedEvent -= HandleSlotChanged;
            _inventory.OnCapacityChangedEvent -= RefreshAll;
            _inventory = null;
        }

        if (_hotbar != null)
        {
            _hotbar.OnSelectedChangedEvent -= HandleSelectedChanged;
            _hotbar = null;
        }
    }

    private void OnDestroy() => Unbind();

    public void RefreshAll()
    {
        for (int i = 0; i < InventoryLayout.HotbarSize; i++)
        {
            CellInventorySlot cell = _builder[i];
            if (cell == null) continue;

            cell.Bind(_inventory, i);
        }
    }

    private void HandleCellClicked(CellInventorySlot cell)
    {
        if (_hotbar == null) return;

        for (int i = 0; i < InventoryLayout.HotbarSize; i++)
        {
            if (_builder[i] != cell) continue;

            _hotbar.Select(i);
            return;
        }
    }

    private void HandleSlotChanged(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= InventoryLayout.HotbarSize) return;

        CellInventorySlot cell = _builder[slotIndex];
        if (cell == null) return;

        cell.Refresh();
    }

    private void HandleSelectedChanged(int selectedIndex)
    {
        for (int i = 0; i < InventoryLayout.HotbarSize; i++)
            SetCellActive(i, i == selectedIndex);
    }
}
