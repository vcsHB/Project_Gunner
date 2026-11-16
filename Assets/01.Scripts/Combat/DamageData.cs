using UnityEngine;

public struct DamageData
{
    public float damage;
    public bool isCritical;
    public bool isSuperCritical;

    public Agent origin;

    /// <summary>공격이 나아가던 방향(공격자 → 대상). 투사체면 비행 방향이다.</summary>
    public Vector2 direction;

    /// <summary>맞은 지점(대상 콜라이더 표면). <see cref="hasHitPoint"/>가 false면 의미 없다 — 지속 피해처럼 "어디"가 없는 피해.</summary>
    public Vector2 hitPoint;
    public bool hasHitPoint;

    /// <summary>
    /// 맞은 부위. <see cref="EquipSlotType.None"/>/<see cref="EquipSlotType.Core"/>는 본체다 — 본체 체력(<see cref="Health"/>)이 받는다.
    /// 팔·추진체면 <see cref="MechaPartHealth"/>가 먼저 받는다. 히트박스의 위치(어느 소켓 아래인가)로 정해진다.
    /// </summary>
    public EquipSlotType hitPart;

    /// <summary>실드가 이 타격을 받아냈다. <see cref="Health"/>가 피격 이벤트 직전에 채운다. 피격 연출이 재질을 고르는 데 쓴다.</summary>
    public bool blockedByShield;

    /// <summary>
    /// 계산이 끝난 데미지에 발사자/방향 정보를 붙인다.
    /// </summary>
    public DamageData WithSource(Agent origin, Vector2 direction)
    {
        this.origin = origin;
        this.direction = direction;
        return this;
    }

    public DamageData WithHitPoint(Vector2 point)
    {
        hitPoint = point;
        hasHitPoint = true;
        return this;
    }
}
