using System.Collections.Generic;

/// <summary>
/// 개체 상태(파츠·개량)를 WeaponStatus에 반영하는 규칙.
///
/// 살아있는 무기(WeaponModController)와 UI 미리보기가 같은 결과를 내야 하므로
/// 계산을 여기 한 곳에 모은다.
/// </summary>
public static class WeaponStatusBuilder
{
    /// <summary>
    /// 장착하지 않은 무기의 최종 스탯을 계산한다. 상세 정보 표시용.
    /// </summary>
    public static WeaponStatus Build(WeaponItemInstance instance)
    {
        PlayerWeaponDataSO data = instance != null ? instance.WeaponData : null;
        if (data == null) return null;

        WeaponStatus status = new(data.Stats);
        ApplyAll(status, instance);

        return status;
    }

    public static void ApplyAll(WeaponStatus status, WeaponItemInstance instance)
    {
        if (status == null || instance == null) return;

        foreach (KeyValuePair<WeaponUpgradeSO, int> pair in instance.Upgrades)
            ApplyUpgrade(status, pair.Key, pair.Value);

        foreach (KeyValuePair<WeaponPartSlotType, WeaponPartDataSO> pair in instance.Parts)
            ApplyPart(status, pair.Value);
    }

    public static void ApplyPart(WeaponStatus status, WeaponPartDataSO part)
    {
        if (status == null || part == null) return;

        IReadOnlyList<WeaponStatDelta> deltas = part.Deltas;
        for (int i = 0; i < deltas.Count; i++)
            ApplyDelta(status, deltas[i], part);
    }

    /// <summary>
    /// 개량 단계는 누적이 아니라 교체다. 부르기 전에 이전 단계를 걷어내야 한다.
    /// </summary>
    public static void ApplyUpgrade(WeaponStatus status, WeaponUpgradeSO upgrade, int level)
    {
        if (status == null || upgrade == null || level <= 0) return;

        WeaponUpgradeLevel data = upgrade.GetLevel(level);
        if (data == null) return;

        for (int i = 0; i < data.deltas.Count; i++)
            ApplyDelta(status, data.deltas[i], upgrade);
    }

    /// <summary>origin이 건 modifier를 전부 걷어낸다.</summary>
    public static void Remove(WeaponStatus status, object origin)
    {
        if (status == null || origin == null) return;

        status.RemoveModifiers(origin);
    }

    private static void ApplyDelta(WeaponStatus status, WeaponStatDelta delta, object origin)
    {
        if (!delta.IsValid) return;

        Status<float> stat = status.Get(delta.type);
        if (stat == null) return;

        stat.AddModifier(origin, delta.value, delta.mode);
    }
}
