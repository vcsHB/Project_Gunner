using UnityEngine;

public struct DamageResponse
{
    public bool isHit;
    public float reflactionDamage;

    /// <summary>무적/이미 사망 등으로 데미지가 들어가지 않은 경우.</summary>
    public static DamageResponse Miss => new DamageResponse { isHit = false, reflactionDamage = 0f };

    public static DamageResponse Hit(float reflactionDamage = 0f)
        => new DamageResponse { isHit = true, reflactionDamage = reflactionDamage };
}
