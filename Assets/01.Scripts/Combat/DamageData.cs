using UnityEngine;

public struct DamageData
{
    public float damage;
    public bool isCritical;
    public bool isSuperCritical;

    public Agent origin;
    public Vector2 direction;

    /// <summary>
    /// 계산이 끝난 데미지에 발사자/방향 정보를 붙인다.
    /// </summary>
    public DamageData WithSource(Agent origin, Vector2 direction)
    {
        this.origin = origin;
        this.direction = direction;
        return this;
    }
}
