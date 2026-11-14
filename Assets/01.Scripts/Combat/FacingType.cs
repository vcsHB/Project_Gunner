using UnityEngine;

/// <summary>
/// 리소스가 어느 쪽을 보고 그려졌는가.
///
/// 코드는 전부 <b>"+X가 앞"</b> 하나만 씁니다. 조준 방향, 발사 방향, 총구 위치,
/// 모듈 장착점이 전부 그 규칙 위에 서 있습니다. 각도로 바꾸는 곳도 `Atan2(y, x)`라
/// 0°가 +X입니다 — 수학 관례와 같아서 머릿속에서 보정할 게 없습니다.
///
/// <b>리소스가 다른 쪽을 보고 그려진 차이는 여기서 한 번만 흡수합니다.</b>
/// 이 프로젝트의 유닛 리소스는 <see cref="Up"/>(위가 정면)로 그립니다.
///
/// <b>회전 보정은 계층에서 딱 한 노드만 적용합니다.</b> 기체 루트가 -90°를 넣는데
/// 그 아래 무기도 -90°를 넣으면 두 번 걸려서 180° 틀어집니다. 기체에 달리는 것들은
/// 루트가 이미 돌려놨으므로 각자 보정하지 않습니다.
///
/// 미러링(<see cref="Left"/>)은 다릅니다 — 부모가 돌든 말든 독립이라 겹쳐도 됩니다.
///
/// 발사 방향이나 각도 계산에서 부호를 뒤집는 식으로 맞추지 마세요 —
/// 규칙이 두 개가 되는 순간 스프라이트·총구·장착점이 따로 놉니다.
/// ([INCIDENTS] 총은 왼쪽을 보는데 총알은 오른쪽으로 나간다)
/// </summary>
public enum FacingType
{
    /// <summary>오른쪽(+X)을 향하게 그려진 리소스. 보정하지 않는다.</summary>
    Right = 0,

    /// <summary>왼쪽(-X)을 향하게 그려진 리소스. 통째로 X 미러링된다.</summary>
    Left = 1,

    /// <summary>위(+Y)를 향하게 그려진 리소스. 탑다운 유닛이 보통 이쪽이다.</summary>
    Up = 2,

    /// <summary>아래(-Y)를 향하게 그려진 리소스.</summary>
    Down = 3,
}

public static class FacingTypeExtensions
{
    /// <summary>
    /// 리소스 방향을 논리 정면(+X)에 맞추기 위해 더할 Z 회전(도).
    ///
    /// 좌우는 회전이 아니라 미러링으로 처리한다(<see cref="IsMirrored"/>) —
    /// 180° 돌리면 비대칭 리소스가 위아래로 뒤집힌다.
    /// </summary>
    public static float ToAngleOffset(this FacingType facing)
    {
        switch (facing)
        {
            case FacingType.Up: return -90f;
            case FacingType.Down: return 90f;
            default: return 0f;
        }
    }

    public static bool IsMirrored(this FacingType facing) => facing == FacingType.Left;

    /// <summary>리소스 방향을 흡수한 최종 회전. 논리 각도(+X 기준)를 받는다.</summary>
    public static Quaternion ToRotation(this FacingType facing, float logicalAngle)
        => Quaternion.Euler(0f, 0f, logicalAngle + facing.ToAngleOffset());
}
