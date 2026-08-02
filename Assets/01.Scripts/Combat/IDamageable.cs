using UnityEngine;


public interface IDamageable
{
    public bool IsDead { get; }

    /// <summary>
    /// 데미지를 적용하고 실제로 적중했는지/반사 데미지가 있는지를 돌려준다.
    /// </summary>
    public DamageResponse ApplyDamage(DamageData damageData);

}
