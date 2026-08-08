using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 살아있는 무기 오브젝트를 개체 상태에 맞춰 따라가게 한다.
///
/// 파츠·개량의 <b>주인은 WeaponItemInstance</b>이고 여기는 반영만 한다.
/// 상태를 여기서 들고 있으면 인벤토리에 있는(오브젝트가 없는) 무기를 모딩할 수 없다.
///
/// 스탯 적용은 파츠에 프리팹이 있든 없든 전부 여기서만 한다.
/// WeaponPartBehaviour 쪽에서 따로 만지게 두면 경로가 둘로 갈려서 반드시 어긋난다.
/// </summary>
public class WeaponModController
{
    private readonly PlayerWeaponBase _weapon;
    private readonly WeaponStatus _status;

    private readonly Dictionary<WeaponPartSlotType, WeaponPartDataSO> _appliedParts = new();
    private readonly Dictionary<WeaponPartSlotType, WeaponPartBehaviour> _behaviours = new();

    private WeaponItemInstance _instance;

    public WeaponModController(PlayerWeaponBase weapon, WeaponStatus status)
    {
        _weapon = weapon;
        _status = status;
    }

    public WeaponItemInstance Instance => _instance;

    public WeaponPartDataSO Get(WeaponPartSlotType slot)
        => _instance != null ? _instance.GetPart(slot) : null;

    public WeaponPartBehaviour GetBehaviour(WeaponPartSlotType slot)
        => _behaviours.TryGetValue(slot, out WeaponPartBehaviour behaviour) ? behaviour : null;

    public void Bind(WeaponItemInstance instance)
    {
        if (_instance == instance) return;

        Unbind();
        _instance = instance;

        if (_instance == null) return;

        _instance.OnPartChangedEvent += HandlePartChanged;
        _instance.OnUpgradeChangedEvent += HandleUpgradeChanged;

        ApplyAll();
    }

    /// <summary>
    /// 무기 오브젝트가 사라질 때 호출한다.
    /// 개체 상태는 건드리지 않는다. 여기서 파츠를 떼면 저장된 모딩이 통째로 날아간다.
    /// </summary>
    public void Unbind()
    {
        if (_instance != null)
        {
            _instance.OnPartChangedEvent -= HandlePartChanged;
            _instance.OnUpgradeChangedEvent -= HandleUpgradeChanged;
            _instance = null;
        }

        foreach (WeaponPartSlotType slot in new List<WeaponPartSlotType>(_behaviours.Keys))
            DespawnBehaviour(slot);

        foreach (WeaponPartDataSO part in _appliedParts.Values)
            WeaponStatusBuilder.Remove(_status, part);

        _appliedParts.Clear();
    }

    /// <summary>무기 기본 모드 + 파츠가 추가한 모드. results를 비우고 채운다.</summary>
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

        foreach (WeaponPartDataSO part in _instance.Parts.Values)
        {
            IReadOnlyList<WeaponFireMode> added = part.AddFireModes;
            for (int i = 0; i < added.Count; i++)
            {
                if (added[i] != WeaponFireMode.None && !results.Contains(added[i]))
                    results.Add(added[i]);
            }
        }
    }

    private void ApplyAll()
    {
        foreach (KeyValuePair<WeaponUpgradeSO, int> pair in _instance.Upgrades)
            WeaponStatusBuilder.ApplyUpgrade(_status, pair.Key, pair.Value);

        foreach (KeyValuePair<WeaponPartSlotType, WeaponPartDataSO> pair in _instance.Parts)
            ApplyPart(pair.Key, pair.Value);

        _weapon.ValidateFireMode();
    }

    private void HandlePartChanged(WeaponPartSlotType slot, WeaponPartDataSO part)
    {
        if (_appliedParts.TryGetValue(slot, out WeaponPartDataSO old))
        {
            WeaponStatusBuilder.Remove(_status, old);
            _appliedParts.Remove(slot);
        }

        DespawnBehaviour(slot);

        if (part != null)
            ApplyPart(slot, part);

        _weapon.ValidateFireMode();
    }

    private void HandleUpgradeChanged(WeaponUpgradeSO upgrade, int level)
    {
        // 단계는 누적이 아니라 교체다. 이전 단계를 먼저 전부 걷어낸다.
        WeaponStatusBuilder.Remove(_status, upgrade);
        WeaponStatusBuilder.ApplyUpgrade(_status, upgrade, level);
    }

    private void ApplyPart(WeaponPartSlotType slot, WeaponPartDataSO part)
    {
        if (part == null) return;

        _appliedParts[slot] = part;
        WeaponStatusBuilder.ApplyPart(_status, part);

        SpawnBehaviour(slot, part);
    }

    private void SpawnBehaviour(WeaponPartSlotType slot, WeaponPartDataSO part)
    {
        if (part.BehaviourPrefab == null) return;

        WeaponPartBehaviour behaviour =
            Object.Instantiate(part.BehaviourPrefab, _weapon.GetMountPoint(slot));

        behaviour.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

        _behaviours[slot] = behaviour;
        behaviour.OnAttached(_weapon, part);
    }

    private void DespawnBehaviour(WeaponPartSlotType slot)
    {
        if (!_behaviours.Remove(slot, out WeaponPartBehaviour behaviour)) return;
        if (behaviour == null) return;

        behaviour.OnDetached();
        Object.Destroy(behaviour.gameObject);
    }
}
