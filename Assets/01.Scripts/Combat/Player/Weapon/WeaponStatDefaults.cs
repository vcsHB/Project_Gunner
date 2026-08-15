using System;
using UnityEngine;

/// <summary>
/// SO에 들어가는 무기 기본 스탯. 인스펙터에서 읽기 좋게 이름 있는 필드로 둔다.
/// 스탯을 추가하려면 WeaponStatType에 값을 넣고, 여기에 필드와 Get 분기를 함께 추가할 것.
/// </summary>
[Serializable]
public class WeaponStatDefaults
{
    [Header("공통")]
    public float damage = 10f;
    public float attackCooltime = 0.2f;
    public float range = 10f;
    public float maxTargetCount = 1f;
    public float criticalRate = 0f;

    [Header("탄약")]
    public float magazineSize = 30f;
    public float reloadTime = 1.5f;

    [Header("조작감")]
    public float recoil = 1f;

    [Tooltip("탄이 퍼지는 각도(도). 전체 폭이고 좌우로 절반씩 갈라진다.")]
    public float spread = 0f;

    public float burstCount = 3f;

    [Tooltip("점사 안에서 발과 발 사이 간격(초). attackCooltime은 묶음이 끝난 뒤의 쿨이다.")]
    public float burstInterval = 0.06f;

    [Header("투사체")]
    public float projectileCount = 1f;
    public float projectileSpeed = 20f;
    public float penetration = 0f;
    public float chargeTime = 0f;

    [Header("타격")]
    public float explosionRadius = 0f;
    public float knockback = 0f;

    public float Get(WeaponStatType type)
    {
        switch (type)
        {
            case WeaponStatType.Damage: return damage;
            case WeaponStatType.AttackCooltime: return attackCooltime;
            case WeaponStatType.Range: return range;
            case WeaponStatType.MaxTargetCount: return maxTargetCount;
            case WeaponStatType.CriticalRate: return criticalRate;

            case WeaponStatType.MagazineSize: return magazineSize;
            case WeaponStatType.ReloadTime: return reloadTime;

            case WeaponStatType.Recoil: return recoil;
            case WeaponStatType.Spread: return spread;
            case WeaponStatType.BurstCount: return burstCount;

            case WeaponStatType.ProjectileCount: return projectileCount;
            case WeaponStatType.ProjectileSpeed: return projectileSpeed;
            case WeaponStatType.Penetration: return penetration;
            case WeaponStatType.ChargeTime: return chargeTime;

            case WeaponStatType.ExplosionRadius: return explosionRadius;
            case WeaponStatType.Knockback: return knockback;

            case WeaponStatType.BurstInterval: return burstInterval;
        }

        return 0f;
    }
}
