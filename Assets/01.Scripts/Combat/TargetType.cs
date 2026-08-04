using System;

/// <summary>
/// 타게팅 분류. 유닛은 보통 하나만 갖고, 캐스터는 여러 개를 조합해서 "무엇을 때릴 수 있는가"를 정한다.
/// 각 값은 TargetLayerTableSO를 통해 물리 레이어와 1:1로 대응한다.
/// </summary>
[Flags]
public enum TargetType
{
    None = 0,
    Ground = 1 << 0,
    Air = 1 << 1,
}
