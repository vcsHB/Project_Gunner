/// <summary>
/// 조준·카메라·조준점이 LateUpdate에서 도는 순서.
///
/// 순서가 어긋나면 <b>조준점이 커서에서 밀린다.</b>
/// 카메라가 옮겨간 뒤에 조준점을 계산해야 커서 밑에 정확히 놓이는데,
/// 유니티는 같은 단계 안의 호출 순서를 보장하지 않아서 값으로 못박아 둔다.
///
/// 카메라 이동 → 조준 계산 → 조준점 배치 순이다.
/// </summary>
public static class AimExecutionOrder
{
    /// <summary>카메라가 먼저 움직인다. 지난 프레임의 조준점을 향해 부드럽게 따라간다.</summary>
    public const int CameraFollow = 100;

    /// <summary>옮겨간 카메라를 기준으로 커서를 월드 좌표로 바꾼다.</summary>
    public const int Aim = 200;

    /// <summary>계산이 끝난 조준점 위치에 마커를 놓는다.</summary>
    public const int AimPoint = 300;
}
