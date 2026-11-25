using TMPro;
using UnityEngine;

/// <summary>
/// 인벤토리 화면 오른쪽, <b>열어 본 적재함</b>(잔해) 구역. 상호작용(E)으로 열리고, 멀어지거나 패널을 닫으면 닫힌다.
/// <b>다 비워도 닫지 않는다</b> — 마지막 것을 꺼내는 순간 창이 사라지면 헷갈린다. 빈 적재함은 닫은 뒤 잠시 있다가 사라진다(<see cref="LootContainer"/>).
///
/// 격자는 <see cref="SectionInventoryGrid"/>를 그대로 쓴다(플레이어에 스스로 붙지 않게 꺼 둔 것). 드래그·Shift+클릭은 플레이어 쪽과 같은 길이다.
///
/// <b>탐색</b> — 열려 있는 동안 <see cref="LootContainer.TickSearch"/>를 돌리고, 가려진 칸과 진행을 격자에 입힌다.
/// 탐색 상태는 적재함에 있다(보는 사람마다). 여기는 보여주기만 한다.
/// </summary>
public class SectionLootingInventory : MonoBehaviour
{
    [SerializeField] private SectionInventoryGrid _grid;
    [SerializeField] private TextMeshProUGUI _textTitle;

    private LootContainer _container;
    private Agent _viewer;
    private PlayerInteractor _interactor;

    public bool IsOpen => _container != null;
    public LootContainer Container => _container;
    public Inventory Inventory => _container != null ? _container.Inventory : null;
    public SectionInventoryGrid Grid => _grid;

    public void Open(LootContainer container, Agent viewer)
    {
        Close();
        if (container == null || viewer == null) return;

        _container = container;
        _viewer = viewer;
        _interactor = viewer.GetCompo<PlayerInteractor>();

        _container.BeginSearch(viewer);
        _container.SetViewing(viewer, true);

        gameObject.SetActive(true);

        if (_textTitle != null)
            _textTitle.text = Localization.Get("ui.loot.wreck", "잔해");

        if (_grid != null)
        {
            _grid.Bind(_container.Inventory);
            ApplySearch();
        }
    }

    public void Close()
    {
        // 열려 있던 적이 없으면 격자는 아직 깨어나지도 않았다(꺼진 채 시작).
        if (!ReferenceEquals(_container, null))
        {
            if (_container != null)
                _container.SetViewing(_viewer, false);

            if (_grid != null)
            {
                _grid.ClearConcealment();
                _grid.Unbind();
            }
        }

        _container = null;
        _viewer = null;
        _interactor = null;

        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    private void Update()
    {
        if (_container == null)
        {
            // 파괴된 적재함(새 판 등). 유니티의 가짜 null도 여기서 걸린다.
            if (!ReferenceEquals(_container, null)) Close();
            return;
        }

        if (_interactor != null && !_interactor.IsInReach(_container))
        {
            Close();
            return;
        }

        _container.TickSearch(_viewer, Time.deltaTime);
        ApplySearch();
    }

    private void ApplySearch()
    {
        if (_grid == null || _container == null) return;

        int searching = _container.GetSearching(_viewer, out float progress);
        _grid.ApplyConcealment(slot => _container.IsConcealed(_viewer, slot), searching, progress);
    }

    private void OnDisable()
    {
        // 패널이 꺼지는 게 아니라 이 구역만 꺼질 때도 탐색 표시가 남지 않게.
        if (_container != null) Close();
    }
}
