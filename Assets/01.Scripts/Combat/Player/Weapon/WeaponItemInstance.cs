using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무기 한 정의 개체 상태. 모듈과 개량의 <b>주인</b>이다.
///
/// 인벤토리에 들어있는(오브젝트가 없는) 무기도 모딩할 수 있어야 해서, 상태를 여기 둔다.
/// 살아있는 PlayerWeaponBase는 이 상태를 따라가기만 한다. (ModController가 그 역할)
///
/// 모듈 칸도 IItemSlotContainer라서 인벤토리에서 모듈을 끌어다 놓는 게 그대로 동작한다.
/// </summary>
public class WeaponItemInstance : ItemInstance, IItemSlotContainer
{
    /// <summary>(슬롯, 새 모듈). 떼면 두 번째가 null.</summary>
    public event Action<ModuleSlotType, ModuleDataSO> OnModuleChangedEvent;

    /// <summary>(개량 경로, 새 단계)</summary>
    public event Action<UpgradeSO, int> OnUpgradeChangedEvent;

    /// <summary>(장전된 탄종, 남은 발수). 탄종이 없으면 첫 번째가 null.</summary>
    public event Action<AmmoDataSO, int> OnAmmoChangedEvent;

    private readonly Dictionary<ModuleSlotType, ModuleDataSO> _modules = new();
    private readonly Dictionary<UpgradeSO, int> _upgrades = new();

    private uint _loadedAmmoId;
    private int _ammoInMagazine;

    public PlayerWeaponDataSO WeaponData => Data as PlayerWeaponDataSO;

    public IReadOnlyDictionary<ModuleSlotType, ModuleDataSO> Modules => _modules;
    public IReadOnlyDictionary<UpgradeSO, int> Upgrades => _upgrades;

    /// <summary>
    /// 레지스트리에 등록하지 않는 임시 개체. 테스트용으로 직접 스폰한 무기 등에 쓴다.
    /// 이게 없으면 그런 무기는 모딩이 아예 불가능해진다.
    /// </summary>
    public static WeaponItemInstance CreateDetached(PlayerWeaponDataSO data)
    {
        WeaponItemInstance instance = new();
        if (data != null)
            instance.ItemId = data.Id;

        return instance;
    }

    #region Modules

    public ModuleDataSO GetModule(ModuleSlotType slot)
        => _modules.TryGetValue(slot, out ModuleDataSO module) ? module : null;

    public bool CanAttach(ModuleDataSO module) => string.IsNullOrEmpty(GetAttachBlockReason(module));

    /// <summary>못 끼우는 이유. 끼울 수 있으면 빈 문자열. UI 툴팁에 그대로 쓴다.</summary>
    public string GetAttachBlockReason(ModuleDataSO module)
    {
        if (module == null) return "모듈이 없습니다.";

        string reason = module.GetIncompatibleReason(WeaponData);
        if (!string.IsNullOrEmpty(reason)) return reason;

        // modifier를 모듈 SO로 식별하므로 같은 모듈이 두 칸에 들어가면 해제가 꼬인다.
        foreach (KeyValuePair<ModuleSlotType, ModuleDataSO> pair in _modules)
        {
            if (pair.Value == module && pair.Key != module.Slot)
                return "같은 모듈이 이미 장착되어 있습니다.";
        }

        return string.Empty;
    }

    public bool AttachModule(ModuleDataSO module)
    {
        string reason = GetAttachBlockReason(module);
        if (!string.IsNullOrEmpty(reason))
        {
            Debug.LogWarning($"[WeaponInstance] {module?.DisplayName} 장착 불가 - {reason}");
            return false;
        }

        if (GetModule(module.Slot) == module) return true;

        _modules[module.Slot] = module;
        OnModuleChangedEvent?.Invoke(module.Slot, module);
        return true;
    }

    public bool DetachModule(ModuleSlotType slot)
    {
        if (!_modules.Remove(slot)) return false;

        OnModuleChangedEvent?.Invoke(slot, null);
        return true;
    }

    #endregion

    #region Upgrades

    /// <summary>걸려있지 않으면 0.</summary>
    public int GetUpgradeLevel(UpgradeSO upgrade)
        => upgrade != null && _upgrades.TryGetValue(upgrade, out int level) ? level : 0;

    public bool IsUpgradeAllowed(UpgradeSO upgrade)
        => upgrade != null && WeaponData != null && WeaponData.CanUpgrade(upgrade);

    public bool CanLevelUp(UpgradeSO upgrade)
        => IsUpgradeAllowed(upgrade) && GetUpgradeLevel(upgrade) < upgrade.MaxLevel;

    public bool TryLevelUp(UpgradeSO upgrade)
        => CanLevelUp(upgrade) && SetUpgradeLevel(upgrade, GetUpgradeLevel(upgrade) + 1);

    /// <summary>단계를 직접 지정한다. 0이면 해제. 세이브 복원에도 이걸 쓴다.</summary>
    public bool SetUpgradeLevel(UpgradeSO upgrade, int level)
    {
        if (!IsUpgradeAllowed(upgrade))
        {
            Debug.LogWarning($"[WeaponInstance] {WeaponData?.DisplayName}은 {upgrade?.UpgradeName} 개량을 할 수 없습니다.");
            return false;
        }

        level = Mathf.Clamp(level, 0, upgrade.MaxLevel);
        if (GetUpgradeLevel(upgrade) == level) return true;

        if (level == 0)
            _upgrades.Remove(upgrade);
        else
            _upgrades[upgrade] = level;

        OnUpgradeChangedEvent?.Invoke(upgrade, level);
        return true;
    }

    #endregion

    #region Ammo

    // 탄창은 무기 오브젝트가 아니라 여기 있다.
    // 인벤토리에 넣었다 다시 들어도 반쯤 남은 탄창이 유지되어야 하고, 그대로 저장되어야 한다.

    /// <summary>장전된 탄종의 아이템 Id. 0이면 비어있다.</summary>
    public uint LoadedAmmoId => _loadedAmmoId;

    /// <summary>탄창에 남은 발수.</summary>
    public int AmmoInMagazine => _ammoInMagazine;

    public AmmoDataSO LoadedAmmo => AmmoDataSO.Find(_loadedAmmoId);

    /// <summary>
    /// 탄창 내용을 통째로 바꾼다. 재장전과 세이브 복원이 같은 길을 쓴다.
    /// count가 0이면 탄종도 함께 비운다 — 빈 탄창에 탄종만 남아있으면 UI가 거짓말을 한다.
    /// </summary>
    public void SetMagazine(uint ammoItemId, int count)
    {
        if (count <= 0)
        {
            ammoItemId = 0;
            count = 0;
        }

        if (_loadedAmmoId == ammoItemId && _ammoInMagazine == count) return;

        _loadedAmmoId = ammoItemId;
        _ammoInMagazine = count;

        OnAmmoChangedEvent?.Invoke(LoadedAmmo, _ammoInMagazine);
    }

    /// <summary>격발로 탄을 뺀다. 모자라면 아무것도 빼지 않고 false.</summary>
    public bool TryConsumeAmmo(int count)
    {
        if (count <= 0) return true;
        if (_ammoInMagazine < count) return false;

        SetMagazine(_loadedAmmoId, _ammoInMagazine - count);
        return true;
    }

    #endregion

    #region IItemSlotContainer

    // slotKey는 (int)ModuleSlotType 이다.

    public ItemStack Peek(int slotKey)
    {
        ModuleDataSO module = GetModule((ModuleSlotType)slotKey);

        return module != null ? new ItemStack(module.Id, 1) : ItemStack.Empty;
    }

    public bool CanAccept(int slotKey, ItemStack stack, out string reason)
    {
        reason = string.Empty;

        ModuleSlotType slot = (ModuleSlotType)slotKey;
        if (slot == ModuleSlotType.None)
        {
            reason = "없는 슬롯입니다.";
            return false;
        }

        ModuleDataSO module = stack.Resolve<ModuleDataSO>();
        if (module == null)
        {
            reason = "모듈이 아닙니다.";
            return false;
        }

        if (module.Slot != slot)
        {
            reason = $"{slot} 칸에 들어가는 모듈이 아닙니다.";
            return false;
        }

        reason = GetAttachBlockReason(module);
        return string.IsNullOrEmpty(reason);
    }

    public ItemStack Take(int slotKey, int count)
    {
        ModuleSlotType slot = (ModuleSlotType)slotKey;

        ModuleDataSO module = GetModule(slot);
        if (module == null) return ItemStack.Empty;

        return DetachModule(slot) ? new ItemStack(module.Id, 1) : ItemStack.Empty;
    }

    public ItemStack Place(int slotKey, ItemStack stack)
    {
        if (!CanAccept(slotKey, stack, out _)) return stack;

        // 이미 차 있으면 여기서 밀어내지 않는다. 교환은 ItemTransfer가 순서를 맞춰서 처리한다.
        if (GetModule((ModuleSlotType)slotKey) != null) return stack;

        if (!AttachModule(stack.Resolve<ModuleDataSO>())) return stack;

        // 모듈 칸은 한 개만 받는다. 여러 개를 들고 왔으면 나머지는 돌려준다.
        stack.count -= 1;
        return stack.count <= 0 ? ItemStack.Empty : stack;
    }

    #endregion
}
