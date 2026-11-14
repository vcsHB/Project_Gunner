using System;

/// <summary>
/// 누구 편인가. <see cref="TargetType"/>(지상/공중)과는 축이 다르다 —
/// 그쪽은 "어떻게 생겼나"고 이쪽은 "누구를 때려도 되나"다.
///
/// 둘을 한 enum에 섞으면 "공중에 뜬 플레이어"를 표현할 수 없게 된다.
///
/// 유닛은 하나만 갖고, 캐스터는 여러 개를 조합해서 때릴 수 있는 편을 정한다.
/// <b>같은 편이라고 자동으로 빠지지 않는다.</b> 이 게임은 PVPVE라 플레이어끼리도 싸운다 —
/// 자기 자신만 제외되고(<see cref="CasterBase.Owner"/>), 나머지는 캐스터가 명시한다.
/// </summary>
[Flags]
public enum FactionType
{
    None = 0,

    Player = 1 << 0,
    Enemy = 1 << 1,

    /// <summary>어느 편도 아닌 것. 폭발통, 파괴 가능한 엄폐물.</summary>
    Neutral = 1 << 2,

    All = Player | Enemy | Neutral,
}
