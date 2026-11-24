using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>수치를 어떻게 적는가. 비교 차이도 같은 단위로 적는다.</summary>
public enum ItemStatFormat
{
    Number = 0,
    Seconds = 1,
    Percent = 2,
    SignedNumber = 3,
    SignedPercent = 4,
    Multiplier = 5,
    Degrees = 6,
}

/// <summary>
/// 툴팁 한 줄. <see cref="key"/>가 같은 줄끼리 비교한다 — 라벨은 언어마다 달라서 키로 쓸 수 없다.
/// <see cref="text"/>가 있으면 수치가 아니라 글(구경 이름 등)이고 비교하지 않는다.
/// </summary>
public struct ItemStatLine
{
    public string key;
    public string label;
    public float value;
    public string text;
    public ItemStatFormat format;
    public bool higherIsBetter;

    public bool IsText => text != null;
}

/// <summary>
/// 아이템 하나가 화면에 보일 수치 목록을 만든다. <b>툴팁이 데이터 타입을 몰라도 되게</b> 여기 한 곳에서 가른다.
///
/// 무기는 <see cref="WeaponStatusBuilder"/>로 모듈·개량·장전 탄까지 반영한 최종값을 쓴다.
/// 데이터 기본값을 보여주면 개량한 무기가 툴팁에서는 순정으로 보인다.
///
/// 0인 부가 스탯(관통·폭발 등)은 빼서 줄 수를 줄인다. 비교할 때 한쪽에만 있는 줄은 상대를 0으로 본다.
/// </summary>
public static class ItemStatSheet
{
    private const float Epsilon = 0.0001f;

    public static void Collect(ItemStack stack, List<ItemStatLine> lines)
    {
        lines.Clear();

        ItemDataSO data = stack.Resolve();
        if (data == null) return;

        if (data is PlayerWeaponDataSO weapon) CollectWeapon(weapon, stack.ResolveInstance() as WeaponItemInstance, lines);
        else if (data is PartDataSO part) CollectPart(part, lines);
        else if (data is AmmoDataSO ammo) CollectAmmo(ammo, lines);
        else if (data is ModuleDataSO module) AddWeaponDeltas(module.Deltas, lines);
        else if (data is RepairKitDataSO kit) CollectRepairKit(kit, lines);
    }

    #region 수집

    private static void CollectWeapon(PlayerWeaponDataSO data, WeaponItemInstance instance, List<ItemStatLine> lines)
    {
        WeaponStatus status = instance != null ? WeaponStatusBuilder.Build(instance) : null;
        if (status == null) status = new WeaponStatus(data.Stats);

        AddWeapon(status, WeaponStatType.Damage, ItemStatFormat.Number, true, lines, always: true);
        AddWeapon(status, WeaponStatType.AttackCooltime, ItemStatFormat.Seconds, false, lines, always: true);
        AddWeapon(status, WeaponStatType.Range, ItemStatFormat.Number, true, lines, always: true);

        float projectiles = status.Value(WeaponStatType.ProjectileCount);
        if (projectiles > 1f + Epsilon)
            AddWeapon(status, WeaponStatType.ProjectileCount, ItemStatFormat.Number, true, lines);

        AddWeapon(status, WeaponStatType.CriticalRate, ItemStatFormat.Percent, true, lines);
        AddWeapon(status, WeaponStatType.Spread, ItemStatFormat.Degrees, false, lines);
        AddWeapon(status, WeaponStatType.Penetration, ItemStatFormat.Number, true, lines);
        AddWeapon(status, WeaponStatType.ExplosionRadius, ItemStatFormat.Number, true, lines);

        // 열을 쓰는 무기만 과열 줄을 보인다. 용량·냉각은 열이 없으면 의미가 없다.
        if (status.Value(WeaponStatType.HeatPerShot) > Epsilon)
        {
            AddWeapon(status, WeaponStatType.HeatPerShot, ItemStatFormat.Number, false, lines);
            AddWeapon(status, WeaponStatType.HeatCapacity, ItemStatFormat.Number, true, lines);
            AddWeapon(status, WeaponStatType.HeatCooling, ItemStatFormat.Number, true, lines);
        }

        AddPartHealth(data, lines);

        if (data.UsesAmmo)
            AddText("ammo", ItemText.Label("ammo", "사용 탄"), JoinCalibers(data.Calibers), lines);
    }

    private static void CollectPart(PartDataSO part, List<ItemStatLine> lines)
    {
        AddPartHealth(part, lines);

        IReadOnlyList<AgentStatDelta> deltas = part.Deltas;
        for (int i = 0; i < deltas.Count; i++)
        {
            AgentStatDelta delta = deltas[i];
            if (!delta.IsValid) continue;

            bool percent = delta.mode == ModifierMode.Percent;

            Merge(lines, new ItemStatLine
            {
                key = $"agent.{delta.type}.{delta.mode}",
                label = ItemText.StatName(delta.type),
                value = delta.value,
                format = percent ? ItemStatFormat.SignedPercent : ItemStatFormat.SignedNumber,
                higherIsBetter = delta.type != StatusType.ShieldRegenDelay,
            });
        }

        if (part is CoreDataSO core)
            Add("core.aimRange", ItemText.StatName(WeaponStatType.AimRange), core.AimRange, ItemStatFormat.Number, true, lines);

        if (part is BoosterDataSO booster)
        {
            Add("booster.multiplier", ItemText.Label("boostMultiplier", "부스트 배율"), booster.BoostMultiplier, ItemStatFormat.Multiplier, true, lines);
            Add("booster.fuel", ItemText.Label("boostFuel", "부스트 연료/초"), booster.FuelPerSecond, ItemStatFormat.Number, false, lines);
        }
    }

    private static void CollectAmmo(AmmoDataSO ammo, List<ItemStatLine> lines)
    {
        AddText("caliber", ItemText.Label("caliber", "구경"), ItemText.CaliberName(ammo.Caliber), lines);
        Add("ammo.cost", ItemText.Label("costPerShot", "발당 소모"), ammo.CostPerShot, ItemStatFormat.Number, false, lines);
        AddWeaponDeltas(ammo.Deltas, lines);
    }

    private static void CollectRepairKit(RepairKitDataSO kit, List<ItemStatLine> lines)
    {
        Add("repair.amount", ItemText.Label("repairAmount", "수리량"), kit.Amount, ItemStatFormat.Number, true, lines);
        Add("repair.duration", ItemText.Label("repairDuration", "수리 시간"), kit.Duration, ItemStatFormat.Seconds, false, lines);
    }

    private static void AddWeaponDeltas(IReadOnlyList<WeaponStatDelta> deltas, List<ItemStatLine> lines)
    {
        for (int i = 0; i < deltas.Count; i++)
        {
            WeaponStatDelta delta = deltas[i];
            if (!delta.IsValid || Mathf.Abs(delta.value) < Epsilon) continue;

            bool percent = delta.mode == ModifierMode.Percent;

            Merge(lines, new ItemStatLine
            {
                key = $"weapon.{delta.type}.{delta.mode}",
                label = ItemText.StatName(delta.type),
                value = delta.value,
                format = percent ? ItemStatFormat.SignedPercent : ItemStatFormat.SignedNumber,
                higherIsBetter = IsHigherBetter(delta.type),
            });
        }
    }

    private static void AddPartHealth(EquipableItemDataSO data, List<ItemStatLine> lines)
    {
        if (data.PartHealth > Epsilon)
            Add("part.health", ItemText.Label("partHealth", "부위 체력"), data.PartHealth, ItemStatFormat.Number, true, lines);
    }

    private static void AddWeapon(WeaponStatus status, WeaponStatType type, ItemStatFormat format, bool higherIsBetter,
        List<ItemStatLine> lines, bool always = false)
    {
        float value = status.Value(type);
        if (!always && Mathf.Abs(value) < Epsilon) return;

        Add("weapon." + type, ItemText.StatName(type), value, format, higherIsBetter, lines);
    }

    private static void Add(string key, string label, float value, ItemStatFormat format, bool higherIsBetter, List<ItemStatLine> lines)
    {
        lines.Add(new ItemStatLine { key = key, label = label, value = value, format = format, higherIsBetter = higherIsBetter });
    }

    private static void AddText(string key, string label, string text, List<ItemStatLine> lines)
    {
        lines.Add(new ItemStatLine { key = key, label = label, text = text ?? string.Empty });
    }

    /// <summary>같은 스탯이 두 번 적혀 있으면 한 줄로 합친다. 데이터에 실수로 중복이 들어가도 줄이 겹치지 않는다.</summary>
    private static void Merge(List<ItemStatLine> lines, ItemStatLine line)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].key != line.key) continue;

            ItemStatLine merged = lines[i];
            merged.value += line.value;
            lines[i] = merged;
            return;
        }

        lines.Add(line);
    }

    private static string JoinCalibers(IReadOnlyList<AmmoCaliberType> calibers)
    {
        StringBuilder sb = new();
        for (int i = 0; i < calibers.Count; i++)
        {
            if (i > 0) sb.Append(" / ");
            sb.Append(ItemText.CaliberName(calibers[i]));
        }

        return sb.ToString();
    }

    /// <summary>낮을수록 좋은 무기 스탯. 비교 화살표 색이 이걸로 갈린다.</summary>
    public static bool IsHigherBetter(WeaponStatType type)
    {
        switch (type)
        {
            case WeaponStatType.AttackCooltime:
            case WeaponStatType.Recoil:
            case WeaponStatType.Spread:
            case WeaponStatType.ChargeTime:
            case WeaponStatType.BurstInterval:
            case WeaponStatType.HeatPerShot:
            case WeaponStatType.OverheatLockTime:
                return false;
            default:
                return true;
        }
    }

    #endregion

    #region 표기

    /// <summary>값 그대로.</summary>
    public static string Format(float value, ItemStatFormat format)
    {
        switch (format)
        {
            case ItemStatFormat.Seconds: return Localization.Format("ui.unit.seconds", "{0:0.##}초", value);
            case ItemStatFormat.Percent: return $"{value * 100f:0.#}%";
            case ItemStatFormat.SignedNumber: return value.ToString("+0.##;-0.##;0");
            case ItemStatFormat.SignedPercent: return (value * 100f).ToString("+0.#;-0.#;0") + "%";
            case ItemStatFormat.Multiplier: return $"×{value:0.##}";
            case ItemStatFormat.Degrees: return $"{value:0.#}°";
            default: return value.ToString("0.##");
        }
    }

    /// <summary>
    /// 차이의 크기. 부호는 화살표가 말하므로 떼고, 단위는 원래 줄을 따른다.
    /// 비율 스탯의 차이는 %p다 — 20%와 30%의 차이는 10%p지 50%가 아니다.
    /// </summary>
    public static string FormatDifference(float magnitude, ItemStatFormat format)
    {
        switch (format)
        {
            case ItemStatFormat.Seconds: return Localization.Format("ui.unit.seconds", "{0:0.##}초", magnitude);
            case ItemStatFormat.Percent:
            case ItemStatFormat.SignedPercent: return $"{magnitude * 100f:0.#}%p";
            case ItemStatFormat.Multiplier: return $"{magnitude:0.##}";
            case ItemStatFormat.Degrees: return $"{magnitude:0.#}°";
            default: return magnitude.ToString("0.##");
        }
    }

    /// <summary>비교 대상에서 같은 줄의 값. 없으면 0 — 상대에게 없는 스탯은 "0에서 생긴 것"으로 본다.</summary>
    public static bool TryFindValue(List<ItemStatLine> lines, string key, out float value)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].key != key || lines[i].IsText) continue;

            value = lines[i].value;
            return true;
        }

        value = 0f;
        return false;
    }

    public static bool IsMeaningful(float difference) => Mathf.Abs(difference) > Epsilon;

    #endregion
}
