using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무기 인스턴스 하나의 파츠 장착 현황.
///
/// 스탯 적용/해제는 파츠에 프리팹이 있든 없든 전부 여기서만 한다.
/// 프리팹 쪽에서 따로 만지게 두면 경로가 둘로 갈려서 반드시 어긋난다.
/// modifier의 origin은 파츠 SO 자신이다.
/// </summary>
public class WeaponModController
{
    public event Action<WeaponPartSlotType, WeaponPartDataSO> OnPartChangedEvent;

    private readonly PlayerWeaponBase _weapon;
    private readonly WeaponStatus _status;

    private readonly Dictionary<WeaponPartSlotType, WeaponPartDataSO> _parts = new();
    private readonly Dictionary<WeaponPartSlotType, WeaponPartBehaviour> _behaviours = new();

    public WeaponModController(PlayerWeaponBase weapon, WeaponStatus status)
    {
        _weapon = weapon;
        _status = status;
    }

    public IReadOnlyDictionary<WeaponPartSlotType, WeaponPartDataSO> Parts => _parts;

    public WeaponPartDataSO Get(WeaponPartSlotType slot)
        => _parts.TryGetValue(slot, out WeaponPartDataSO part) ? part : null;

    public WeaponPartBehaviour GetBehaviour(WeaponPartSlotType slot)
        => _behaviours.TryGetValue(slot, out WeaponPartBehaviour behaviour) ? behaviour : null;

    public bool CanAttach(WeaponPartDataSO part) => string.IsNullOrEmpty(GetBlockReason(part));

    /// <summary>못 끼우는 이유. 끼울 수 있으면 빈 문자열. UI에서 그대로 보여주면 된다.</summary>
    public string GetBlockReason(WeaponPartDataSO part)
    {
        if (part == null) return "파츠가 없습니다.";

        string reason = part.GetIncompatibleReason(_weapon.Data);
        if (!string.IsNullOrEmpty(reason)) return reason;

        // modifier를 파츠 SO로 식별하므로 같은 파츠가 두 슬롯에 들어가면 해제가 꼬인다.
        foreach (KeyValuePair<WeaponPartSlotType, WeaponPartDataSO> pair in _parts)
        {
            if (pair.Value == part && pair.Key != part.Slot)
                return "같은 파츠가 이미 장착되어 있습니다.";
        }

        return string.Empty;
    }

    public bool Attach(WeaponPartDataSO part)
    {
        string reason = GetBlockReason(part);
        if (!string.IsNullOrEmpty(reason))
        {
            Debug.LogWarning($"[WeaponMod] {part?.DisplayName} 장착 불가 - {reason}");
            return false;
        }

        // 같은 슬롯에 이미 있으면 갈아끼운다.
        Detach(part.Slot);

        _parts[part.Slot] = part;
        ApplyDeltas(part);
        SpawnBehaviour(part);

        OnPartChangedEvent?.Invoke(part.Slot, part);
        return true;
    }

    public bool Detach(WeaponPartSlotType slot)
    {
        if (!_parts.TryGetValue(slot, out WeaponPartDataSO part)) return false;

        DespawnBehaviour(slot);
        _status.RemoveModifiers(part);
        _parts.Remove(slot);

        OnPartChangedEvent?.Invoke(slot, null);
        return true;
    }

    public void DetachAll()
    {
        // 순회 중에 컬렉션이 바뀌므로 슬롯 목록을 먼저 뜬다.
        WeaponPartSlotType[] slots = new WeaponPartSlotType[_parts.Count];
        _parts.Keys.CopyTo(slots, 0);

        for (int i = 0; i < slots.Length; i++)
            Detach(slots[i]);
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

        foreach (WeaponPartDataSO part in _parts.Values)
        {
            IReadOnlyList<WeaponFireMode> added = part.AddFireModes;
            for (int i = 0; i < added.Count; i++)
            {
                if (added[i] != WeaponFireMode.None && !results.Contains(added[i]))
                    results.Add(added[i]);
            }
        }
    }

    private void ApplyDeltas(WeaponPartDataSO part)
    {
        IReadOnlyList<WeaponStatDelta> deltas = part.Deltas;

        for (int i = 0; i < deltas.Count; i++)
        {
            WeaponStatDelta delta = deltas[i];
            if (!delta.IsValid) continue;

            _status.Get(delta.type)?.AddModifier(part, delta.value, delta.mode);
        }
    }

    private void SpawnBehaviour(WeaponPartDataSO part)
    {
        if (part.BehaviourPrefab == null) return;

        WeaponPartBehaviour behaviour =
            UnityEngine.Object.Instantiate(part.BehaviourPrefab, _weapon.GetMountPoint(part.Slot));

        behaviour.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

        _behaviours[part.Slot] = behaviour;
        behaviour.OnAttached(_weapon, part);
    }

    private void DespawnBehaviour(WeaponPartSlotType slot)
    {
        if (!_behaviours.TryGetValue(slot, out WeaponPartBehaviour behaviour)) return;

        _behaviours.Remove(slot);

        if (behaviour == null) return;

        behaviour.OnDetached();
        UnityEngine.Object.Destroy(behaviour.gameObject);
    }
}
