using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 살아있는 무기 오브젝트를 개체 상태에 맞춰 따라가게 한다.
///
/// 모듈·개량의 <b>주인은 WeaponItemInstance</b>이고 여기는 반영만 한다.
/// 상태를 여기서 들고 있으면 인벤토리에 있는(오브젝트가 없는) 무기를 모딩할 수 없다.
///
/// 스탯 적용은 모듈에 프리팹이 있든 없든 전부 여기서만 한다.
/// ModuleBehaviour 쪽에서 따로 만지게 두면 경로가 둘로 갈려서 반드시 어긋난다.
/// </summary>
public class ModdingController
{
    private readonly PlayerWeaponBase _weapon;
    private readonly WeaponStatus _status;

    private readonly Dictionary<ModuleSlotType, ModuleDataSO> _appliedModules = new();
    private readonly Dictionary<ModuleSlotType, ModuleBehaviour> _behaviours = new();

    private WeaponItemInstance _instance;

    // 장전된 탄약도 스탯을 바꾼다. 모듈과 같은 자리에서 관리해야 경로가 갈리지 않는다.
    private AmmoDataSO _appliedAmmo;

    public ModdingController(PlayerWeaponBase weapon, WeaponStatus status)
    {
        _weapon = weapon;
        _status = status;
    }

    public WeaponItemInstance Instance => _instance;

    public ModuleDataSO Get(ModuleSlotType slot)
        => _instance != null ? _instance.GetModule(slot) : null;

    public ModuleBehaviour GetBehaviour(ModuleSlotType slot)
        => _behaviours.TryGetValue(slot, out ModuleBehaviour behaviour) ? behaviour : null;

    public void Bind(WeaponItemInstance instance)
    {
        if (_instance == instance) return;

        Unbind();
        _instance = instance;

        if (_instance == null) return;

        _instance.OnModuleChangedEvent += HandleModuleChanged;
        _instance.OnUpgradeChangedEvent += HandleUpgradeChanged;
        _instance.OnAmmoChangedEvent += HandleAmmoChanged;

        ApplyAll();
    }

    /// <summary>
    /// 무기 오브젝트가 사라질 때 호출한다.
    /// 개체 상태는 건드리지 않는다. 여기서 모듈을 떼면 저장된 모딩이 통째로 날아간다.
    /// </summary>
    public void Unbind()
    {
        if (_instance != null)
        {
            _instance.OnModuleChangedEvent -= HandleModuleChanged;
            _instance.OnUpgradeChangedEvent -= HandleUpgradeChanged;
            _instance.OnAmmoChangedEvent -= HandleAmmoChanged;
            _instance = null;
        }

        foreach (ModuleSlotType slot in new List<ModuleSlotType>(_behaviours.Keys))
            DespawnBehaviour(slot);

        foreach (ModuleDataSO module in _appliedModules.Values)
            WeaponStatusBuilder.Remove(_status, module);

        _appliedModules.Clear();

        if (_appliedAmmo != null)
        {
            WeaponStatusBuilder.Remove(_status, _appliedAmmo);
            _appliedAmmo = null;
        }
    }

    /// <summary>무기 기본 모드 + 모듈이 추가한 모드. results를 비우고 채운다.</summary>
    public void GetAvailableFireModes(List<WeaponFireMode> results)
    {
        results.Clear();

        IReadOnlyList<WeaponFireMode> baseModes = _weapon.Data.FireModes;
        for (int i = 0; i < baseModes.Count; i++)
        {
            if (baseModes[i] != WeaponFireMode.None && !results.Contains(baseModes[i]))
                results.Add(baseModes[i]);
        }

        if (_instance == null) return;

        foreach (ModuleDataSO module in _instance.Modules.Values)
        {
            IReadOnlyList<WeaponFireMode> added = module.AddFireModes;
            for (int i = 0; i < added.Count; i++)
            {
                if (added[i] != WeaponFireMode.None && !results.Contains(added[i]))
                    results.Add(added[i]);
            }
        }
    }

    private void ApplyAll()
    {
        foreach (KeyValuePair<UpgradeSO, int> pair in _instance.Upgrades)
            WeaponStatusBuilder.ApplyUpgrade(_status, pair.Key, pair.Value);

        foreach (KeyValuePair<ModuleSlotType, ModuleDataSO> pair in _instance.Modules)
            ApplyModule(pair.Key, pair.Value);

        HandleAmmoChanged(_instance.LoadedAmmo, _instance.AmmoInMagazine);

        _weapon.ValidateFireMode();
    }

    private void HandleModuleChanged(ModuleSlotType slot, ModuleDataSO module)
    {
        if (_appliedModules.TryGetValue(slot, out ModuleDataSO old))
        {
            WeaponStatusBuilder.Remove(_status, old);
            _appliedModules.Remove(slot);
        }

        DespawnBehaviour(slot);

        if (module != null)
            ApplyModule(slot, module);

        _weapon.ValidateFireMode();
    }

    private void HandleUpgradeChanged(UpgradeSO upgrade, int level)
    {
        // 단계는 누적이 아니라 교체다. 이전 단계를 먼저 전부 걷어낸다.
        WeaponStatusBuilder.Remove(_status, upgrade);
        WeaponStatusBuilder.ApplyUpgrade(_status, upgrade, level);
    }

    /// <summary>
    /// 잔탄만 줄어든 경우는 스탯이 그대로다. 탄종이 실제로 바뀌었을 때만 modifier를 갈아끼운다.
    /// </summary>
    private void HandleAmmoChanged(AmmoDataSO ammo, int count)
    {
        if (_appliedAmmo == ammo) return;

        if (_appliedAmmo != null)
            WeaponStatusBuilder.Remove(_status, _appliedAmmo);

        _appliedAmmo = ammo;

        if (_appliedAmmo != null)
            WeaponStatusBuilder.ApplyAmmo(_status, _appliedAmmo);
    }

    private void ApplyModule(ModuleSlotType slot, ModuleDataSO module)
    {
        if (module == null) return;

        _appliedModules[slot] = module;
        WeaponStatusBuilder.ApplyModule(_status, module);

        SpawnBehaviour(slot, module);
    }

    private void SpawnBehaviour(ModuleSlotType slot, ModuleDataSO module)
    {
        if (module.BehaviourPrefab == null) return;

        ModuleBehaviour behaviour =
            Object.Instantiate(module.BehaviourPrefab, _weapon.GetMountPoint(slot));

        behaviour.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

        _behaviours[slot] = behaviour;
        behaviour.OnAttached(_weapon, module);
    }

    private void DespawnBehaviour(ModuleSlotType slot)
    {
        if (!_behaviours.Remove(slot, out ModuleBehaviour behaviour)) return;
        if (behaviour == null) return;

        behaviour.OnDetached();
        Object.Destroy(behaviour.gameObject);
    }
}
