using UnityEngine;

[CreateAssetMenu(menuName = "SO/Item/PlayerWeaponData")]
public class PlayerWeaponDataSO : ItemDataSO
{   
    [Header("Player WeaponData Settings")]
    public PlayerWeaponBase playerWeaponPrefab;
    public float defaultDamage = 1; 
    public short maxTargetCount;
    public float attackCooltime;

}