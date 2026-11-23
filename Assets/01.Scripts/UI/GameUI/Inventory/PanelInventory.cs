using UnityEngine;

/// <summary>
/// 인벤토리 화면 전체. 플레이어 인벤토리 / 상세 / 컨테이너 세 부분을 묶는다.
///
/// <b>죽어 있는 동안은 열리지 않는다.</b> 열려 있었으면 닫는다 — 기체를 잃기 전에 안전 슬롯으로 옮겨 담는 걸 막는다(<see cref="RunController"/>).
/// </summary>
public class PartInventory : UIPanelBase
{
    [SerializeField] private PartPlayerInventory _playerInventory;
    [SerializeField] private PartItemDetail _itemDetail;
    [SerializeField] private PartLootingInventory _lootingInventory;

    private PlayerInput _input;
    private Health _health;

    // Awake가 아니라 Start에서 붙는다. Agent의 컴포넌트 초기화가 Awake에서 끝나기 때문이다.
    private void Start()
    {
        Player player = FindAnyObjectByType<Player>();
        _input = player != null ? player.Input : null;
        _health = player != null ? player.GetCompo<Health>() : null;

        if (_input != null)
            _input.OnInventoryToggleEvent += HandleToggle;

        if (_health != null)
            _health.OnDeadEvent += HandleDead;
    }

    private void OnDestroy()
    {
        if (_input != null)
            _input.OnInventoryToggleEvent -= HandleToggle;

        if (_health != null)
            _health.OnDeadEvent -= HandleDead;
    }

    private void HandleToggle()
    {
        if (_health != null && _health.IsDead && !IsShown) return;

        Toggle();
    }

    private void HandleDead()
    {
        if (IsShown) Hide();
    }
}
