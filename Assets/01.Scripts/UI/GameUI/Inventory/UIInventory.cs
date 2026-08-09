using UnityEngine;

/// <summary>
/// Inventory의 한 구간을 격자로 보여준다.
///
/// 앞 7칸은 핫바가 따로 표시하므로, 보관 칸 뷰는 _startSlotIndex를 7로 두어 겹치지 않게 한다.
/// 상자·제작대도 이 클래스를 그대로 재사용한다.
/// </summary>
public class UIInventory : MonoBehaviour
{
    [SerializeField] private CellInventorySlot[] _inventorySlots;

    [Tooltip("첫 번째 칸이 인벤토리의 몇 번을 가리킬지. 보관 칸 뷰는 핫바 다음부터 시작한다.")]
    [SerializeField, Min(0)] private int _startSlotIndex;

    [Tooltip("켜면 씬에서 플레이어를 찾아 스스로 붙는다. 상자 뷰라면 끄고 Bind를 직접 부를 것.")]
    [SerializeField] private bool _bindPlayerInventory = true;

    private Inventory _inventory;

    public Inventory Inventory => _inventory;

    private void Awake()
    {
        for (int i = 0; i < _inventorySlots.Length; i++)
        {
            if (_inventorySlots[i] == null) continue;

            _inventorySlots[i].SetDisplayIndex(_startSlotIndex + i);
            _inventorySlots[i].Clear();
            _inventorySlots[i].SetSelected(false);
        }
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

        int shown = _inventory.Capacity - _startSlotIndex;

        if (shown <= 0)
        {
            // 이 상태에서는 모든 칸이 컨테이너 없이 남아서 클릭은 되는데 드롭만 안 되는 것처럼 보인다.
            Debug.LogError(
                $"[UIInventory] 보여줄 칸이 없습니다. 인벤토리 용량 {_inventory.Capacity} / 시작 인덱스 {_startSlotIndex}. " +
                "AgentStatus의 InventorySize를 늘리거나 _startSlotIndex를 줄이세요.", this);
        }
        else if (shown > _inventorySlots.Length)
        {
            Debug.LogWarning(
                $"[UIInventory] 칸이 모자랍니다. 보여줄 칸 {shown} / UI {_inventorySlots.Length}. " +
                "뒤쪽 아이템은 보이지 않습니다.", this);
        }

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
        for (int i = 0; i < _inventorySlots.Length; i++)
        {
            if (_inventorySlots[i] == null) continue;

            int slotIndex = _startSlotIndex + i;

            // 인벤토리 용량을 넘는 칸은 컨테이너 없이 비워둔다. 드래그도 걸리지 않는다.
            bool inRange = _inventory != null && slotIndex < _inventory.Capacity;
            _inventorySlots[i].Bind(inRange ? _inventory : null, slotIndex);
        }
    }

    private void HandleSlotChanged(int slotIndex)
    {
        int cellIndex = slotIndex - _startSlotIndex;
        if (cellIndex < 0 || cellIndex >= _inventorySlots.Length) return;
        if (_inventorySlots[cellIndex] == null) return;

        _inventorySlots[cellIndex].Refresh();
    }
}
