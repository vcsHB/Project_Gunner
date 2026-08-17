using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 살아있는 무기의 탄창을 관리한다. 소모, 재장전, 탄종 전환이 여기 모여 있다.
///
/// 탄창 <b>상태</b>는 WeaponItemInstance가 갖는다. 여기는 재장전 진행처럼
/// 오브젝트가 살아있는 동안만 의미 있는 것만 들고 있다.
/// 상태를 여기 두면 무기를 인벤토리에 넣었다 뺄 때마다 탄창이 리셋된다.
/// </summary>
public class WeaponAmmoController
{
    /// <summary>(탄종, 잔탄). 탄종이 없으면 첫 번째가 null.</summary>
    public event Action<AmmoDataSO, int> OnAmmoChangedEvent;

    /// <summary>재장전 시작(true) / 끝나거나 취소됨(false).</summary>
    public event Action<bool> OnReloadStateChangedEvent;

    /// <summary>쓸 수 있는 탄약이 인벤토리에 없어서 재장전이 안 됐을 때. 소리·UI용.</summary>
    public event Action OnReloadFailedEvent;

    private readonly PlayerWeaponBase _weapon;
    private readonly List<uint> _candidateBuffer = new();

    private InventoryController _inventoryController;

    // 재장전이 끝났을 때 실제로 채울 탄종. 탄종 전환은 "다음 재장전부터" 반영된다.
    private uint _targetAmmoId;
    private float _reloadTimer;
    private float _reloadDuration;

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

    public bool IsReloading { get; private set; }

    /// <summary>탄약을 쓰지 않는 무기(근접 등)면 false. 그런 무기는 언제나 쏠 수 있다.</summary>
    public bool UsesAmmo => Data != null && Data.UsesAmmo;

    public AmmoDataSO LoadedAmmo => Instance != null ? Instance.LoadedAmmo : null;
    public int Loaded => Instance != null ? Instance.AmmoInMagazine : 0;

    public int MagazineSize
        => Mathf.Max(1, Mathf.RoundToInt(_weapon.Status.Value(WeaponStatType.MagazineSize)));

    /// <summary>0~1. UI 게이지가 그대로 쓴다.</summary>
    public float ReloadProgress01
        => IsReloading && _reloadDuration > 0f
            ? Mathf.Clamp01(1f - _reloadTimer / _reloadDuration)
            : 0f;

    /// <summary>재장전이 끝나기까지 남은 초. 재장전 중이 아니면 0.</summary>
    public float ReloadRemainTime => IsReloading ? Mathf.Max(0f, _reloadTimer) : 0f;

    /// <summary>한 번 격발에 드는 탄. 탄종이 정하고, 없으면 1이다.</summary>
    public int CostPerShot
    {
        get
        {
            AmmoDataSO ammo = LoadedAmmo;
            return ammo != null ? ammo.CostPerShot : 1;
        }
    }

    public bool CanFire => !UsesAmmo || (!IsReloading && Loaded >= CostPerShot);

    public void SetInventoryController(InventoryController controller) => _inventoryController = controller;

    /// <summary>무기를 내려놓을 때. 진행 중이던 재장전은 없던 일이 된다.</summary>
    public void CancelReload()
    {
        if (!IsReloading) return;

        IsReloading = false;
        _reloadTimer = 0f;

        OnReloadStateChangedEvent?.Invoke(false);
    }

    public void Tick(float deltaTime)
    {
        if (!IsReloading) return;

        _reloadTimer -= deltaTime;
        if (_reloadTimer > 0f) return;

        IsReloading = false;
        CompleteReload();

        OnReloadStateChangedEvent?.Invoke(false);
    }

    /// <summary>격발 한 번 분량을 뺀다. 모자라면 아무것도 빼지 않고 false.</summary>
    public bool TryConsumeForShot()
    {
        if (!UsesAmmo) return true;
        if (IsReloading || Instance == null) return false;

        if (!Instance.TryConsumeAmmo(CostPerShot)) return false;

        RaiseAmmoChanged();
        return true;
    }

    #region Reload

    public void BeginReload()
    {
        if (IsReloading || !UsesAmmo || Instance == null) return;

        uint target = ResolveReloadTarget();
        if (target == 0)
        {
            OnReloadFailedEvent?.Invoke();
            return;
        }

        // 같은 탄으로 가득 차 있으면 할 일이 없다.
        if (target == Instance.LoadedAmmoId && Loaded >= MagazineSize) return;

        _targetAmmoId = target;
        _reloadDuration = Mathf.Max(0.01f, _weapon.Status.Value(WeaponStatType.ReloadTime));
        _reloadTimer = _reloadDuration;
        IsReloading = true;

        OnReloadStateChangedEvent?.Invoke(true);
    }

    /// <summary>
    /// 무엇을 장전할지 정한다. 우선순위는 (1) 사용자가 고른 탄종 (2) 지금 물린 탄종 (3) 인벤토리에 있는 아무 호환 탄.
    /// 인벤토리에 하나도 없으면 0.
    /// </summary>
    private uint ResolveReloadTarget()
    {
        if (HasInInventory(_targetAmmoId)) return _targetAmmoId;
        if (HasInInventory(Instance.LoadedAmmoId)) return Instance.LoadedAmmoId;

        CollectAvailableAmmo(_candidateBuffer);
        return _candidateBuffer.Count > 0 ? _candidateBuffer[0] : 0u;
    }

    private void CompleteReload()
    {
        if (Instance == null || _targetAmmoId == 0) return;

        int magazineSize = MagazineSize;
        int keep = Instance.LoadedAmmoId == _targetAmmoId ? Instance.AmmoInMagazine : 0;

        // 다른 탄종이 물려 있었으면 남은 것을 돌려준다.
        // 그냥 버리면 탄종을 한 번 바꿀 때마다 반 탄창씩 증발한다.
        if (keep == 0 && Instance.AmmoInMagazine > 0)
            ReturnToInventory(Instance.LoadedAmmoId, Instance.AmmoInMagazine);

        int need = magazineSize - keep;
        if (need <= 0)
        {
            RaiseAmmoChanged();
            return;
        }

        Inventory inventory = Inventory;
        int available = inventory != null ? inventory.CountOf(_targetAmmoId) : 0;
        int take = Mathf.Min(need, available);

        if (take <= 0)
        {
            // 남은 탄만 돌려주고 끝난 경우. 탄창이 비었을 수 있다.
            Instance.SetMagazine(keep > 0 ? _targetAmmoId : 0u, keep);
            RaiseAmmoChanged();

            OnReloadFailedEvent?.Invoke();
            return;
        }

        inventory.Remove(_targetAmmoId, take);
        Instance.SetMagazine(_targetAmmoId, keep + take);

        RaiseAmmoChanged();
    }

    /// <summary>탄창에서 빼낸 탄을 인벤토리로. 자리가 없으면 발밑에 떨군다. 조용히 지우지 않는다.</summary>
    private void ReturnToInventory(uint ammoId, int count)
    {
        if (ammoId == 0 || count <= 0) return;

        Inventory inventory = Inventory;
        int left = inventory != null ? inventory.Add(ammoId, count) : count;
        if (left <= 0) return;

        Vector2 position = _weapon.Owner != null ? _weapon.Owner.transform.position : _weapon.transform.position;
        WorldItemSpawner.Spawn(new ItemStack(ammoId, left), position);
    }

    #endregion

    #region Ammo Selection

    /// <summary>
    /// 쓸 수 있는 탄종을 넘긴다. 고른 즉시 재장전이 시작된다 —
    /// 고르기만 하고 반영이 안 되면 "바꿨는데 왜 그대로냐"가 된다.
    /// </summary>
    public void CycleAmmoType(int direction)
    {
        if (!UsesAmmo || direction == 0) return;

        CollectAvailableAmmo(_candidateBuffer);
        if (_candidateBuffer.Count == 0)
        {
            OnReloadFailedEvent?.Invoke();
            return;
        }

        uint current = _targetAmmoId != 0 ? _targetAmmoId : Instance != null ? Instance.LoadedAmmoId : 0u;

        int index = _candidateBuffer.IndexOf(current);
        int next = index < 0
            ? (direction > 0 ? 0 : _candidateBuffer.Count - 1)
            : (index + direction) % _candidateBuffer.Count;

        if (next < 0) next += _candidateBuffer.Count;

        SelectAmmo(_candidateBuffer[next]);
    }

    /// <summary>탄종을 지정한다. 지금 물린 것과 같으면 아무 일도 없다.</summary>
    public bool SelectAmmo(uint ammoItemId)
    {
        if (!UsesAmmo || Instance == null) return false;

        AmmoDataSO ammo = AmmoDataSO.Find(ammoItemId);
        if (ammo == null || !ammo.FitsIn(Data)) return false;

        _targetAmmoId = ammoItemId;

        if (Instance.LoadedAmmoId == ammoItemId && Loaded >= MagazineSize) return true;

        CancelReload();
        BeginReload();
        return true;
    }

    /// <summary>
    /// 인벤토리에서 이 무기에 맞는 탄종 Id를 모은다. 칸 순서대로라 순환 순서가 예측 가능하다.
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
        Inventory inventory = Inventory;
        if (ammoItemId == 0 || inventory == null) return false;

        AmmoDataSO ammo = AmmoDataSO.Find(ammoItemId);
        if (ammo == null || !ammo.FitsIn(Data)) return false;

        return inventory.Has(ammoItemId);
    }

    private void RaiseAmmoChanged() => OnAmmoChangedEvent?.Invoke(LoadedAmmo, Loaded);
}
