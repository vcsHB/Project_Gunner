using UnityEngine;

/// <summary>
/// 캐스터가 찾아낸 대상 하나. 같은 TargetBase는 이미 하나로 접힌 상태로 전달된다.
/// </summary>
public readonly struct CastHit
{
    public readonly TargetBase Target;

    /// <summary>히트박스를 나누지 않은 대상이면 null.</summary>
    public readonly HitBox HitBox;

    public readonly Collider2D Collider;

    public CastHit(TargetBase target, HitBox hitBox, Collider2D collider)
    {
        Target = target;
        HitBox = hitBox;
        Collider = collider;
    }

    public float DamageMultiplier => HitBox != null ? HitBox.DamageMultiplier : 1f;

    /// <summary>실제로 맞은 지점에 가까운 좌표. 이펙트/넉백 방향 계산용.</summary>
    public Vector2 Position => Collider != null ? Collider.bounds.center : Target.AimPosition;
}
