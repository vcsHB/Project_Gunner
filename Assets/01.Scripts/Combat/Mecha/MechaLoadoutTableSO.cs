using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 구성을 <b>굴려서</b> 만든다. 적 기체가 매번 다른 조합으로 나오게 하는 데 쓴다.
///
/// 손으로 만든 로드아웃 여러 개를 두고 하나를 고르는 방식도 있지만, 그러면 파츠가
/// 늘어날 때마다 조합을 사람이 적어줘야 한다. 슬롯별 후보만 두면 조합은 자동으로 늘어난다.
///
/// <b><see cref="MechaLoadoutSO"/>를 런타임에 만들지 않는다.</b> ScriptableObject는 참조가
/// 끊겨도 바로 사라지지 않아서, 적이 스폰될 때마다 하나씩 만들면 조용히 쌓인다.
/// 굴린 결과는 재사용 리스트에 담아 넘긴다.
/// </summary>
[CreateAssetMenu(menuName = "SO/Mecha/Loadout Table")]
public class MechaLoadoutTableSO : ScriptableObject
{
    /// <summary>슬롯 하나의 후보들.</summary>
    [System.Serializable]
    public struct SlotPool
    {
        public EquipSlotType slot;

        [Tooltip("이 슬롯이 채워질 확률. 1이면 반드시 채워진다. " +
                 "추진체처럼 없어도 되는 슬롯은 낮춰 둔다.")]
        [Range(0f, 1f)] public float chance;

        [Tooltip("이 중 하나가 뽑힌다. 같은 것을 여러 번 넣으면 그만큼 자주 나온다.")]
        public List<EquipableItemDataSO> candidates;

        public bool IsValid => slot != EquipSlotType.None && candidates != null && candidates.Count > 0;
    }

    [Tooltip("슬롯별 후보. 코어는 chance 1로 두는 것이 좋다 — 코어가 없으면 몸도 없다.")]
    [SerializeField] private List<SlotPool> _pools = new();

    [Tooltip("적재함에 싣는 것. 탄약이 없으면 무기를 달아도 한 발도 못 쏜다.")]
    [SerializeField] private List<MechaLoadoutSO.Cargo> _cargo = new();

    [Tooltip("좌우 팔에 같은 무기를 달지. 끄면 양팔이 따로 굴려져 비대칭 기체가 나온다.")]
    [SerializeField] private bool _symmetricArms;

    public IReadOnlyList<MechaLoadoutSO.Cargo> CargoItems => _cargo;

    /// <summary>
    /// 한 벌 굴린다. <b>results는 호출 전에 비워야 한다</b> — 재사용 리스트를 넘기는 쪽이
    /// 매번 새 리스트를 만들지 않도록 여기서 Clear하지 않는다.
    ///
    /// <see cref="GameRandom.World"/>를 쓴다. 적 구성은 월드를 만드는 판정이라
    /// 전투 판정(치명타·산탄)과 소스를 섞으면 호출 순서가 깨져 재현이 안 된다.
    /// </summary>
    public void Roll(List<MechaLoadoutSO.Entry> results)
    {
        if (results == null) return;

        EquipableItemDataSO armPick = null;

        for (int i = 0; i < _pools.Count; i++)
        {
            SlotPool pool = _pools[i];
            if (!pool.IsValid) continue;

            // 확률은 항상 굴린다. 1이라고 건너뛰면 호출 횟수가 달라져 이후 굴림이 전부 어긋난다.
            bool filled = GameRandom.World.Value < pool.chance;

            EquipableItemDataSO picked = Pick(pool);
            if (!filled || picked == null) continue;

            // 좌우를 맞출 때는 먼저 뽑힌 팔 것을 그대로 쓴다.
            if (_symmetricArms && IsArm(pool.slot))
            {
                if (armPick == null) armPick = picked;
                picked = armPick;
            }

            if (!picked.CanEquipTo(pool.slot))
            {
                Debug.LogWarning(
                    $"[LoadoutTable] {picked.DisplayName}은 {pool.slot} 슬롯에 들어가지 않습니다.", this);
                continue;
            }

            results.Add(new MechaLoadoutSO.Entry { slot = pool.slot, item = picked });
        }
    }

    /// <summary>후보 중 하나. 비어 있는 칸이 섞여 있어도 터지지 않게 한 번 더 본다.</summary>
    private static EquipableItemDataSO Pick(SlotPool pool)
    {
        for (int attempt = 0; attempt < pool.candidates.Count; attempt++)
        {
            EquipableItemDataSO picked = pool.candidates[GameRandom.World.Range(0, pool.candidates.Count)];
            if (picked != null) return picked;
        }

        return null;
    }

    private static bool IsArm(EquipSlotType slot)
        => slot == EquipSlotType.ArmLeft || slot == EquipSlotType.ArmRight;

    /// <summary>
    /// 이 표의 적재품으로 <b>먹일 수 있는 무기인가.</b>
    ///
    /// 구경이 안 맞는 무기를 달면 <b>조용히 한 발도 못 쏜다</b> — 조립도 되고 에러도 안 나고
    /// 적이 가만히 서서 방아쇠만 당긴다. 찾기 가장 어려운 부류라 표를 짤 때 걸러낸다.
    /// </summary>
    public bool HasAmmoFor(PlayerWeaponDataSO weapon)
    {
        if (weapon == null) return false;

        // 탄을 안 쓰는 무기(근접·에너지)는 적재품과 무관하다.
        if (!weapon.UsesAmmo) return true;

        for (int i = 0; i < _cargo.Count; i++)
        {
            if (_cargo[i].item is not AmmoDataSO ammo) continue;

            if (weapon.AcceptsCaliber(ammo.Caliber)) return true;
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        for (int i = 0; i < _pools.Count; i++)
        {
            if (_pools[i].slot == EquipSlotType.Core && _pools[i].chance <= 0f)
            {
                Debug.LogWarning(
                    $"[LoadoutTable] {name}: 코어 확률이 0입니다. 몸이 없는 기체가 나옵니다.", this);
            }

            WarnUnfeedable(_pools[i]);
        }
    }

    private void WarnUnfeedable(SlotPool pool)
    {
        if (pool.candidates == null) return;

        for (int i = 0; i < pool.candidates.Count; i++)
        {
            if (pool.candidates[i] is not PlayerWeaponDataSO weapon) continue;
            if (HasAmmoFor(weapon)) continue;

            Debug.LogWarning(
                $"[LoadoutTable] {name}: {weapon.DisplayName}에 맞는 탄이 적재품에 없습니다. " +
                "달려도 한 발도 못 쏩니다.", this);
        }
    }
#endif
}
