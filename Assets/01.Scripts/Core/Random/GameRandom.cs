using System;

/// <summary>
/// 판정용 난수의 단일 창구. 전투 코드는 UnityEngine.Random을 직접 부르지 않는다.
///
/// 용도별로 소스를 나눠 둔 이유는 <b>호출 순서</b> 때문이다.
/// 맵 생성이 전투와 같은 소스를 쓰면 전투 한 번이 이후 지형을 통째로 바꿔버린다.
///
/// 지금은 실행할 때마다 다른 시드로 시작한다. 서버 권한 구조가 생기면
/// 서버가 정한 시드로 <see cref="Reseed"/>를 부르면 된다.
/// </summary>
public static class GameRandom
{
    /// <summary>치명타, 상태이상 확률, 산탄 각도처럼 결과가 게임 상태를 바꾸는 판정.</summary>
    public static IRandomSource Combat { get; private set; } = new SeededRandomSource(Environment.TickCount);

    /// <summary>맵 생성, 전리품 테이블처럼 월드를 만드는 판정.</summary>
    public static IRandomSource World { get; private set; } = new SeededRandomSource(Environment.TickCount + 1);

    /// <summary>새 게임/월드 로드에서 호출한다. 시드 하나로 두 소스를 갈라 만든다.</summary>
    public static void Reseed(int seed)
    {
        Combat = new SeededRandomSource(seed);
        World = new SeededRandomSource(seed + 1);
    }
}
