using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 살아있는 무기의 급탄을 관리한다. <b>탄창도 재장전도 없다.</b>
///
/// 적재함에서 바로 당겨 쓴다 — 적재함 재고가 곧 잔탄이다.
/// 재장전을 없앤 자리는 과열이 메운다(연사 제한은 열이 건다).
/// 탄창을 두면 "적재함에 300발 있는데 30발만 쏠 수 있는" 층이 하나 더 생기고,
/// 그걸 비우는 수단(재장전)이 다시 필요해진다.
///
/// <b>장전 탄종</b>만 WeaponItemInstance가 들고 간다. 무기를 인벤토리에 넣었다 빼도
/// 무슨 탄을 쓰는지는 유지되어야 한다. 잔탄은 적재함이 답이므로 저장할 것이 없다.
/// </summary>
public class WeaponAmmoController
{
    /// <summary>(탄종, 적재함 재고). 탄종이 없으면 첫 번째가 null.</summary>
    public event Action<AmmoDataSO, int> OnAmmoChangedEvent;

    /// <summary>쓸 탄이 적재함에 없을 때. 소리·UI용.</summary>
    public event Action OnOutOfAmmoEvent;

    private readonly PlayerWeaponBase _weapon;
    private readonly List<uint> _candidateBuffer = new();

    private InventoryController _inventoryController;

    // 적재함 조회는 발사마다 돈다. 연사 무기에서 매번 전 칸을 훑지 않도록 마지막 결과를 들고 있는다.
    private uint _cachedAmmoId;
    private int _cachedCount = -1;

    public WeaponAmmoController(PlayerWeaponBase weapon, InventoryController inventoryController)
    {
        _weapon = weapon;
        _inventoryController = inventoryController;
    }

    private WeaponItemInstance Instance => _weapon.Instance;
    private PlayerWeaponDataSO Data => _weapon.Data;

    // Inventory 자체를 잡아두지 않는 이유는 초기화 순서 때문이다.
    // InventoryController.AfterInitialize가 무기보다 늦게 돌면 그 시점의 Inventory는 아직 null이다.
    private Inventory Inventory => _inventoryController != null ? _inventoryController.Inventory : null;

    /// <summary>탄약을 쓰지 않는 무기(근접, 에너지)면 false. 그런 무기는 탄 걱정이 없다.</summary>
    public bool UsesAmmo => Data != null && Data.UsesAmmo;

    /// <summary>지금 쓰는 탄종. 지정이 없으면 적재함에서 맞는 것을 고른다.</summary>
    public AmmoDataSO LoadedAmmo => AmmoDataSO.Find(ResolveAmmoId());

    /// <summary>적재함에 남은 이 탄종의 수. 이게 곧 잔탄이다.</summary>
    public int Loaded
    {
        get
        {
            if (!UsesAmmo) return 0;

            uint id = ResolveAmmoId();
            Inventory inventory = Inventory;

            return id != 0 && inventory != null ? inventory.CountOf(id) : 0;
        }
    }

    /// <summary>한 번 격발에 드는 탄. 탄종이 정하고, 없으면 1이다.</summary>
    public int CostPerShot
    {
        get
        {
            AmmoDataSO ammo = LoadedAmmo;
            return ammo != null ? ammo.CostPerShot : 1;
        }
    }

    public bool CanFire => !UsesAmmo || Loaded >= CostPerShot;

    public void SetInventoryController(InventoryController controller) => _inventoryController = controller;

    /// <summary>
    /// 격발 한 번 분량을 적재함에서 뺀다. 모자라면 아무것도 빼지 않고 false.
    /// </summary>
    public bool TryConsumeForShot()
    {
        if (!UsesAmmo) return true;

        uint id = ResolveAmmoId();
        Inventory inventory = Inventory;

        if (id == 0 || inventory == null)
        {
            OnOutOfAmmoEvent?.Invoke();
            return false;
        }

        int cost = CostPerShot;
        if (inventory.CountOf(id) < cost)
        {
            OnOutOfAmmoEvent?.Invoke();
            return false;
        }

        inventory.Remove(id, cost);
        Invalidate();
        RaiseAmmoChanged();

        return true;
    }

    #region 탄종

    /// <summary>
    /// 무엇을 쓸지 정한다. 지정된 탄종이 적재함에 있으면 그것,
    /// 없으면 적재함에 있는 아무 호환 탄. 하나도 없으면 0.
    ///
    /// 자동으로 넘어가는 이유 — 지정한 탄이 떨어졌는데 다른 탄이 있는데도 못 쏘면,
    /// 플레이어는 "왜 안 나가지"를 UI 없이 알아낼 방법이 없다.
    /// </summary>
    private uint ResolveAmmoId()
    {
        if (!UsesAmmo || Instance == null) return 0;

        uint selected = Instance.LoadedAmmoId;
        if (selected != 0 && HasInInventory(selected)) return selected;

        CollectAvailableAmmo(_candidateBuffer);
        return _candidateBuffer.Count > 0 ? _candidateBuffer[0] : 0u;
    }

    /// <summary>쓸 탄종을 지정한다. 격납고 로드아웃 화면이 쓴다.</summary>
    public bool SelectAmmo(uint ammoItemId)
    {
        if (!UsesAmmo || Instance == null) return false;

        AmmoDataSO ammo = AmmoDataSO.Find(ammoItemId);
        if (ammo == null || !ammo.FitsIn(Data)) return false;

        Instance.SetLoadedAmmo(ammoItemId);
        Invalidate();
        RaiseAmmoChanged();

        return true;
    }

    /// <summary>
    /// 적재함에서 이 무기에 맞는 탄종 Id를 모은다. 칸 순서대로라 순서가 예측 가능하다.
    /// 격납고에서 고를 수 있는 목록이기도 하다.
    /// </summary>
    public void CollectAvailableAmmo(List<uint> results)
    {
        results.Clear();

        Inventory inventory = Inventory;
        if (inventory == null || Data == null) return;

        for (int i = 0; i < inventory.Capacity; i++)
        {
            ItemStack slot = inventory[i];
            if (slot.IsEmpty || results.Contains(slot.itemId)) continue;

            AmmoDataSO ammo = slot.Resolve<AmmoDataSO>();
            if (ammo == null || !ammo.FitsIn(Data)) continue;

            results.Add(slot.itemId);
        }
    }

    #endregion

    private bool HasInInventory(uint ammoItemId)
    {
        if (ammoItemId == 0) return false;

        if (_cachedAmmoId == ammoItemId && _cachedCount >= 0)
            return _cachedCount > 0;

        Inventory inventory = Inventory;
        if (inventory == null) return false;

        AmmoDataSO ammo = AmmoDataSO.Find(ammoItemId);
        if (ammo == null || !ammo.FitsIn(Data)) return false;

        _cachedAmmoId = ammoItemId;
        _cachedCount = inventory.CountOf(ammoItemId);

        return _cachedCount > 0;
    }

    /// <summary>적재함이 바뀌었을 수 있다. 다음 조회에서 다시 센다.</summary>
    public void Invalidate() => _cachedCount = -1;

    private void RaiseAmmoChanged() => OnAmmoChangedEvent?.Invoke(LoadedAmmo, Loaded);
}
