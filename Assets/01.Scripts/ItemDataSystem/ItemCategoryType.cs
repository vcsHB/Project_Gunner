using System;

[Flags] // 플래그로 하는게 나은지, enum배열로 하는게 나을지 확장 방향에 따라 고민이 필요
public enum ItemCategoryType
{
    None = 0,
    Resources,
    UseableItem,
    PlayerWeapon,
    PlayerWeaponPart,
    Unit,

}