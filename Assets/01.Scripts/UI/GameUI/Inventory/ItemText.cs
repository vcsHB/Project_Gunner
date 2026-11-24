/// <summary>
/// 아이템 UI에 나가는 고정 이름들(장착 칸, 스탯, 구경). 전부 <see cref="Localization.Get"/>를 거친다.
///
/// enum 이름(<c>ArmLeft</c>)을 화면에 그대로 내지 않기 위해 둔다. 키는 <c>ui.{분류}.{enum 이름}</c>이고
/// 한국어 원문이 폴백이다 — 시트가 없어도 한글로 뜬다.
///
/// enum에 값이 추가되면 여기도 한 줄 넣을 것. 빠지면 enum 이름이 그대로 보여서 바로 눈에 띈다.
/// </summary>
public static class ItemText
{
    public static string SlotName(EquipSlotType slot)
    {
        string fallback;
        switch (slot)
        {
            case EquipSlotType.ArmLeft: fallback = "좌무기"; break;
            case EquipSlotType.ArmRight: fallback = "우무기"; break;
            case EquipSlotType.Core: fallback = "코어"; break;
            case EquipSlotType.Booster: fallback = "추진체"; break;
            default: fallback = slot.ToString(); break;
        }

        return Localization.Get("ui.slot." + slot, fallback);
    }

    public static string StatName(StatusType type)
    {
        string fallback;
        switch (type)
        {
            case StatusType.Damage: fallback = "공격력"; break;
            case StatusType.Health: fallback = "코어 체력"; break;
            case StatusType.Defense: fallback = "장갑"; break;
            case StatusType.CriticalRate: fallback = "치명타율"; break;
            case StatusType.DamageResistance: fallback = "피해 감소"; break;
            case StatusType.IsResist: fallback = "상태이상 면역"; break;
            case StatusType.MoveSpeed: fallback = "이동 속도"; break;
            case StatusType.InventorySize: fallback = "적재 칸"; break;
            case StatusType.MaxPower: fallback = "출력"; break;
            case StatusType.MaxFuel: fallback = "연료"; break;
            case StatusType.MaxShield: fallback = "실드"; break;
            case StatusType.ShieldRegenDelay: fallback = "실드 재충전 대기"; break;
            case StatusType.ShieldRegenRate: fallback = "실드 재충전 속도"; break;
            default: fallback = type.ToString(); break;
        }

        return Localization.Get("ui.stat.agent." + type, fallback);
    }

    public static string StatName(WeaponStatType type)
    {
        string fallback;
        switch (type)
        {
            case WeaponStatType.Damage: fallback = "피해량"; break;
            case WeaponStatType.AttackCooltime: fallback = "공격 간격"; break;
            case WeaponStatType.Range: fallback = "사거리"; break;
            case WeaponStatType.MaxTargetCount: fallback = "최대 대상 수"; break;
            case WeaponStatType.CriticalRate: fallback = "치명타율"; break;
            case WeaponStatType.Recoil: fallback = "반동"; break;
            case WeaponStatType.Spread: fallback = "탄 퍼짐"; break;
            case WeaponStatType.BurstCount: fallback = "점사 수"; break;
            case WeaponStatType.ProjectileCount: fallback = "발사체 수"; break;
            case WeaponStatType.ProjectileSpeed: fallback = "탄속"; break;
            case WeaponStatType.Penetration: fallback = "관통"; break;
            case WeaponStatType.ChargeTime: fallback = "충전 시간"; break;
            case WeaponStatType.ExplosionRadius: fallback = "폭발 반경"; break;
            case WeaponStatType.Knockback: fallback = "넉백"; break;
            case WeaponStatType.BurstInterval: fallback = "점사 간격"; break;
            case WeaponStatType.AimRange: fallback = "조준 거리"; break;
            case WeaponStatType.HeatPerShot: fallback = "발당 열"; break;
            case WeaponStatType.HeatCapacity: fallback = "열 용량"; break;
            case WeaponStatType.HeatCooling: fallback = "냉각"; break;
            case WeaponStatType.OverheatLockTime: fallback = "과열 잠김"; break;
            default: fallback = type.ToString(); break;
        }

        return Localization.Get("ui.stat.weapon." + type, fallback);
    }

    public static string CaliberName(AmmoCaliberType caliber)
    {
        string fallback;
        switch (caliber)
        {
            case AmmoCaliberType.Small: fallback = "소구경탄"; break;
            case AmmoCaliberType.Middle: fallback = "경소총탄"; break;
            case AmmoCaliberType.Heavy: fallback = "중소총탄"; break;
            case AmmoCaliberType.Buckshot: fallback = "산탄"; break;
            case AmmoCaliberType.Grenade: fallback = "유탄"; break;
            case AmmoCaliberType.Rocket: fallback = "로켓"; break;
            case AmmoCaliberType.EnergyCell: fallback = "에너지 셀"; break;
            default: fallback = caliber.ToString(); break;
        }

        return Localization.Get("ui.caliber." + caliber, fallback);
    }

    /// <summary>스탯 이름이 아닌 고정 문구. 키가 곧 분류라 호출하는 쪽에서 원문을 같이 준다.</summary>
    public static string Label(string key, string fallback) => Localization.Get("ui.item." + key, fallback);
}
