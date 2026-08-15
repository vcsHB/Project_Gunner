using UnityEngine;

/// <summary>
/// 발사 시점에 확정되는 투사체 한 발의 정보.
///
/// 무기 스탯을 참조로 들고 가지 않고 값으로 복사하는 이유는,
/// 총알이 날아가는 동안 무기를 바꾸거나 파츠를 갈아도 이미 나간 탄의 위력이 변하면 안 되기 때문이다.
/// </summary>
public struct ProjectileLaunchData
{
    /// <summary>쏜 주체. 자기 자신을 맞히지 않기 위해 캐스터에 전달된다.</summary>
    public Agent owner;

    /// <summary>정규화된 진행 방향.</summary>
    public Vector2 direction;

    public float speed;

    /// <summary>이 거리를 날아가면 사라진다. 무기의 Range 스탯이다.</summary>
    public float range;

    public CastPower power;

    /// <summary>추가로 뚫을 수 있는 대상 수. 0이면 하나 맞히고 사라진다.</summary>
    public int penetration;

    /// <summary>0보다 크면 사라질 때 이 반경으로 터진다. 투사체 프리팹에 폭발 캐스터가 있어야 한다.</summary>
    public float explosionRadius;
}
