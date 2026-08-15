using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무기 한 정의 개체 상태. 파츠와 개량의 <b>주인</b>이다.
///
/// 인벤토리에 들어있는(오브젝트가 없는) 무기도 모딩할 수 있어야 해서, 상태를 여기 둔다.
/// 살아있는 PlayerWeaponBase는 이 상태를 따라가기만 한다. (WeaponModController가 그 역할)
///
/// 파츠 칸도 IItemSlotContainer라서 인벤토리에서 파츠를 끌어다 놓는 게 그대로 동작한다.
/// </summary>
public class WeaponItemInstance : ItemInstance, IItemSlotContainer
{
    /// <summary>(슬롯, 새 파츠). 떼면 두 번째가 null.</summary>
    public event Action<WeaponPartSlotType, WeaponPartDataSO> OnPartChangedEvent;

    /// <summary>(개량 경로, 새 단계)</summary>
    public event Action<WeaponUpgradeSO, int> OnUpgradeChangedEvent;

    /// <summary>(장전된 탄종, 남은 발수). 탄종이 없으면 첫 번째가 null.</summary>
    public event Action<AmmoDataSO, int> OnAmmoChangedEvent;

    private readonly Dictionary<WeaponPartSlotType, WeaponPartDataSO> _parts = new();
    private readonly Dictionary<WeaponUpgradeSO, int> _upgrades = new();

    private uint _loadedAmmoId;
    private int _ammoInMagazine;

    public PlayerWeaponDataSO WeaponData => Data as PlayerWeaponDataSO;

    public IReadOnlyDictionary<WeaponPartSlotType, WeaponPartDataSO> Parts => _parts;
    public IReadOnlyDictionary<WeaponUpgradeSO, int> Upgrades => _upgrades;

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

    #region Parts

    public WeaponPartDataSO GetPart(WeaponPartSlotType slot)
        => _parts.TryGetValue(slot, out WeaponPartDataSO part) ? part : null;

    public bool CanAttach(WeaponPartDataSO part) => string.IsNullOrEmpty(GetAttachBlockReason(part));

    /// <summary>못 끼우는 이유. 끼울 수 있으면 빈 문자열. UI 툴팁에 그대로 쓴다.</summary>
    public string GetAttachBlockReason(WeaponPartDataSO part)
    {
        if (part == null) return "파츠가 없습니다.";

        string reason = part.GetIncompatibleReason(WeaponData);
        if (!string.IsNullOrEmpty(reason)) return reason;

        // modifier를 파츠 SO로 식별하므로 같은 파츠가 두 칸에 들어가면 해제가 꼬인다.
        foreach (KeyValuePair<WeaponPartSlotType, WeaponPartDataSO> pair in _parts)
        {
            if (pair.Value == part && pair.Key != part.Slot)
                return "같은 파츠가 이미 장착되어 있습니다.";
        }

        return string.Empty;
    }

    public bool AttachPart(WeaponPartDataSO part)
    {
        string reason = GetAttachBlockReason(part);
        if (!string.IsNullOrEmpty(reason))
        {
            Debug.LogWarning($"[WeaponInstance] {part?.DisplayName} 장착 불가 - {reason}");
            return false;
        }

        if (GetPart(part.Slot) == part) return true;

        _parts[part.Slot] = part;
        OnPartChangedEvent?.Invoke(part.Slot, part);
        return true;
    }

    public bool DetachPart(WeaponPartSlotType slot)
    {
        if (!_parts.Remove(slot)) return false;

        OnPartChangedEvent?.Invoke(slot, null);
        return true;
    }

    #endregion

    #region Upgrades

    /// <summary>걸려있지 않으면 0.</summary>
    public int GetUpgradeLevel(WeaponUpgradeSO upgrade)
        => upgrade != null && _upgrades.TryGetValue(upgrade, out int level) ? level : 0;

    public bool IsUpgradeAllowed(WeaponUpgradeSO upgrade)
        => upgrade != null && WeaponData != null && WeaponData.CanUpgrade(upgrade);

    public bool CanLevelUp(WeaponUpgradeSO upgrade)
        => IsUpgradeAllowed(upgrade) && GetUpgradeLevel(upgrade) < upgrade.MaxLevel;

    public bool TryLevelUp(WeaponUpgradeSO upgrade)
        => CanLevelUp(upgrade) && SetUpgradeLevel(upgrade, GetUpgradeLevel(upgrade) + 1);

    /// <summary>단계를 직접 지정한다. 0이면 해제. 세이브 복원에도 이걸 쓴다.</summary>
    public bool SetUpgradeLevel(WeaponUpgradeSO upgrade, int level)
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

    // slotKey는 (int)WeaponPartSlotType 이다.

    public ItemStack Peek(int slotKey)
    {
        WeaponPartDataSO part = GetPart((WeaponPartSlotType)slotKey);

        return part != null ? new ItemStack(part.Id, 1) : ItemStack.Empty;
    }

    public bool CanAccept(int slotKey, ItemStack stack, out string reason)
    {
        reason = string.Empty;

        WeaponPartSlotType slot = (WeaponPartSlotType)slotKey;
        if (slot == WeaponPartSlotType.None)
        {
            reason = "없는 슬롯입니다.";
            return false;
        }

        WeaponPartDataSO part = stack.Resolve<WeaponPartDataSO>();
        if (part == null)
        {
            reason = "파츠가 아닙니다.";
            return false;
        }

        if (part.Slot != slot)
        {
            reason = $"{slot} 칸에 들어가는 파츠가 아닙니다.";
            return false;
        }

        reason = GetAttachBlockReason(part);
        return string.IsNullOrEmpty(reason);
    }

    public ItemStack Take(int slotKey, int count)
    {
        WeaponPartSlotType slot = (WeaponPartSlotType)slotKey;

        WeaponPartDataSO part = GetPart(slot);
        if (part == null) return ItemStack.Empty;

        return DetachPart(slot) ? new ItemStack(part.Id, 1) : ItemStack.Empty;
    }

    public ItemStack Place(int slotKey, ItemStack stack)
    {
        if (!CanAccept(slotKey, stack, out _)) return stack;

        // 이미 차 있으면 여기서 밀어내지 않는다. 교환은 ItemTransfer가 순서를 맞춰서 처리한다.
        if (GetPart((WeaponPartSlotType)slotKey) != null) return stack;

        if (!AttachPart(stack.Resolve<WeaponPartDataSO>())) return stack;

        // 파츠 칸은 한 개만 받는다. 여러 개를 들고 왔으면 나머지는 돌려준다.
        stack.count -= 1;
        return stack.count <= 0 ? ItemStack.Empty : stack;
    }

    #endregion
}
