using UnityEngine;

/// <summary>
/// 인벤토리 화면 전체. 플레이어 인벤토리 / 상세 / 컨테이너 세 부분을 묶는다.
/// </summary>
public class PartInventory : UIPanelBase
{
    [SerializeField] private PartPlayerInventory _playerInventory;
    [SerializeField] private PartItemDetail _itemDetail;
    [SerializeField] private PartLootingInventory _lootingInventory;

    private PlayerInput _input;

    // Awake가 아니라 Start에서 붙는다. Agent의 컴포넌트 초기화가 Awake에서 끝나기 때문이다.
    private void Start()
    {
        Player player = FindAnyObjectByType<Player>();
        _input = player != null ? player.Input : null;

        if (_input != null)
            _input.OnInventoryToggleEvent += Toggle;
    }

    private void OnDestroy()
    {
        if (_input != null)
            _input.OnInventoryToggleEvent -= Toggle;
    }
}
