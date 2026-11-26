using UnityEngine;

/// <summary>
/// 인벤토리 화면 전체(Tab으로 여닫는 Panel). 플레이어 인벤토리 / 루팅 컨테이너 두 구역(Section)을 묶는다.
///
/// <b>죽어 있는 동안은 열리지 않는다.</b> 열려 있었으면 닫는다 — 기체를 잃기 전에 안전 슬롯으로 옮겨 담는 걸 막는다(<see cref="RunController"/>).
///
/// <b>루팅</b> — 잔해에서 E를 누르면(<see cref="LootContainer.OnOpenRequestedEvent"/>) 이 패널이 열리며 오른쪽에 잔해 구역이 붙는다.
/// Tab으로 열면 내 것만. 패널을 닫으면 잔해 구역도 닫힌다. Shift+클릭의 "반대편"도 여기서 정한다(<see cref="ItemQuickMove"/>).
/// </summary>
public class PanelInventory : UIPanelBase
{
    [SerializeField] private SectionPlayerInventory _playerInventory;
    [SerializeField] private SectionLootingInventory _lootingInventory;

    private PlayerInput _input;
    private Health _health;
    private Player _player;
    private Inventory _playerCargo;

    // Awake가 아니라 Start에서 붙는다. Agent의 컴포넌트 초기화가 Awake에서 끝나기 때문이다.
    private void Start()
    {
        Player player = PlayerPilot.LocalMecha;
        _player = player;
        _input = PlayerPilot.Local != null ? PlayerPilot.Local.Input : null; // 입력은 조종권의 것 — 갈아타도 그대로다
        _health = player != null ? player.GetCompo<Health>() : null;

        InventoryController cargo = player != null ? player.GetCompo<InventoryController>() : null;
        _playerCargo = cargo != null ? cargo.Inventory : null;

        if (_input != null)
            _input.OnInventoryToggleEvent += HandleToggle;

        if (_health != null)
            _health.OnDeadEvent += HandleDead;

        LootContainer.OnOpenRequestedEvent += HandleLootRequested;
        ItemQuickMove.Resolver = ResolveQuickMoveTarget;
        ItemQuickMove.OnFailedEvent += HandleQuickMoveFailed;
    }

    private void OnDestroy()
    {
        if (_input != null)
            _input.OnInventoryToggleEvent -= HandleToggle;

        if (_health != null)
            _health.OnDeadEvent -= HandleDead;

        LootContainer.OnOpenRequestedEvent -= HandleLootRequested;
        ItemQuickMove.OnFailedEvent -= HandleQuickMoveFailed;
        if (ItemQuickMove.Resolver == ResolveQuickMoveTarget) ItemQuickMove.Resolver = null;
    }

    #region 루팅

    private void HandleLootRequested(LootContainer container, Agent who)
    {
        // 내 화면은 내가 연 것만 띄운다(멀티에서 남이 연 잔해가 내 화면에 뜨지 않게).
        if (who == null || who != _player) return;
        if (_health != null && _health.IsDead) return;
        if (PanelGamePause.IsOpen || _lootingInventory == null) return;

        _lootingInventory.Open(container, who);
        Show();
    }

    /// <summary>Shift+클릭의 반대편. 루팅 중일 때만 있다 — 내 적재함 ↔ 잔해 적재함.</summary>
    private Inventory ResolveQuickMoveTarget(IItemSlotContainer from)
    {
        if (!IsShown || _lootingInventory == null || !_lootingInventory.IsOpen) return null;

        Inventory loot = _lootingInventory.Inventory;
        if (ReferenceEquals(from, loot)) return _playerCargo;
        if (ReferenceEquals(from, _playerCargo)) return loot;

        return null;
    }

    private void HandleQuickMoveFailed(Inventory target, string reason)
    {
        SectionInventoryGrid grid = null;

        if (_lootingInventory != null && ReferenceEquals(target, _lootingInventory.Inventory))
            grid = _lootingInventory.Grid;
        else if (_playerInventory != null)
            grid = _playerInventory.GetComponentInChildren<SectionInventoryGrid>();

        if (grid != null) grid.FlashFull();
    }

    #endregion

    private void HandleToggle()
    {
        if (_health != null && _health.IsDead && !IsShown) return;

        // 일시정지 밑에서 열리면 ESC 한 번에 둘 중 무엇이 닫힐지 헷갈린다.
        if (PanelGamePause.IsOpen) return;

        Toggle();
    }

    // 칸에서 띄운 작은 창들은 이 패널 안에 있다. 패널이 투명해져도 저들은 "떠 있는" 채로 남아 ESC 한 번을 먹는다.
    protected override void OnHide()
    {
        PopupItemContext.HideIfOpen();
        PopupItemSplit.HideIfOpen();

        if (_lootingInventory != null) _lootingInventory.Close();
    }

    private void HandleDead()
    {
        if (IsShown) Hide();
    }
}
